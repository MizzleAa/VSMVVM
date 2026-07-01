using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// Blueprint 스타일 push exec / pull data 실행 엔진.
    /// 단일 RunAsync 호출은 직렬 실행(병렬은 추후 ForkNode로). 브레이크포인트 게이트는 RunAsync 간 공유.
    /// </summary>
    public sealed class SchedulerService : ISchedulerService
    {
        private readonly ConcurrentDictionary<Guid, byte> _globalBreakpoints
            = new ConcurrentDictionary<Guid, byte>();

        // 현재 일시정지된 실행을 깨우기 위한 게이트. RunAsync별로 TaskCompletionSource 할당.
        private TaskCompletionSource<bool> _continueGate;
        private readonly object _gateLock = new object();
        // Continue() 가 활성 컨텍스트의 StepMode 를 Run 으로 리셋할 수 있도록 RunAsync 동안만 set.
        private ExecutionContext _activeContext;

        public async Task<ExecutionResult> RunAsync(NodeGraph graph, Guid entryNodeId, ExecutionContext context)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (context == null) throw new ArgumentNullException(nameof(context));

            // 글로벌 브레이크포인트를 컨텍스트로 복사
            foreach (var kv in _globalBreakpoints)
            {
                context.Breakpoints.Add(kv.Key);
            }

            var entry = graph.GetNode(entryNodeId)
                ?? throw new InvalidOperationException($"Entry node {entryNodeId} not in graph.");

            using var linkedCts = CreateLinkedCancellation(context);
            var effectiveToken = linkedCts.Token;

            int nodesExecuted = 0;
            var stopwatch = Stopwatch.StartNew();
            var runStartedAt = DateTimeOffset.UtcNow;
            var run = new ExecutionRun(context.RunId, graph.Id, runStartedAt);
            ExecutionStatus status = ExecutionStatus.Completed;
            Exception error = null;
            long memoryBaseline = context.MemoryBudgetBytes.HasValue
                ? GC.GetTotalMemory(forceFullCollection: false)
                : 0;

            WriteLog(context, SchedulerLogLevel.Info, null, null, $"Run started for graph {graph.Id}.", null);

            // 직렬 스택. ExecutionFlow.Continue가 여러 핀을 반환하면 발화 순서대로 LIFO로 push하기 위해
            // 일반 큐가 아닌 명시적 스택 사용. 같은 발화 안에서는 declaration 순서대로 실행되도록 역순 push.
            var pending = new Stack<INode>();
            pending.Push(entry);

            // Continue() 가 StepMode 를 리셋할 수 있도록 활성 컨텍스트 등록. finally 에서 clear.
            lock (_gateLock) { _activeContext = context; }

            try
            {
                while (pending.Count > 0)
                {
                    effectiveToken.ThrowIfCancellationRequested();

                    if (nodesExecuted >= context.MaxNodesExecuted)
                    {
                        var msg = $"MaxNodesExecuted ({context.MaxNodesExecuted}) exceeded.";
                        Emit(context, new GuardTriggeredMessage(context.RunId, graph.Id, "MaxNodes", msg));
                        WriteLog(context, SchedulerLogLevel.Error, null, null, msg, null);
                        throw new SchedulerOverflowException(context.MaxNodesExecuted);
                    }

                    if (context.MemoryBudgetBytes.HasValue)
                    {
                        var observed = GC.GetTotalMemory(forceFullCollection: false) - memoryBaseline;
                        if (observed > context.MemoryBudgetBytes.Value)
                        {
                            var msg = $"MemoryBudget ({context.MemoryBudgetBytes.Value} bytes) exceeded (observed {observed}).";
                            Emit(context, new GuardTriggeredMessage(context.RunId, graph.Id, "Memory", msg));
                            WriteLog(context, SchedulerLogLevel.Error, null, null, msg, null);
                            throw new GraphMemoryBudgetExceededException(context.MemoryBudgetBytes.Value, observed);
                        }
                    }

                    var current = pending.Pop();

                    // 브레이크포인트 검사
                    if (context.Breakpoints.Contains(current.Id))
                    {
                        Emit(context, new BreakpointHitMessage(context.RunId, graph.Id, current.Id));
                        context.StepMode = DebugStepMode.Paused;
                        await WaitForContinueAsync(effectiveToken).ConfigureAwait(false);
                    }
                    else if (context.StepMode == DebugStepMode.Paused)
                    {
                        Emit(context, new BreakpointHitMessage(context.RunId, graph.Id, current.Id));
                        await WaitForContinueAsync(effectiveToken).ConfigureAwait(false);
                    }

                    Emit(context, new NodeEnteringMessage(context.RunId, graph.Id, current.Id, current.TypeId));
                    WriteLog(context, SchedulerLogLevel.Debug, current.Id, current.TypeId,
                        $"Node entering: {current.TypeId}", null);

                    var nodeStartedAt = DateTimeOffset.UtcNow;
                    var nodeStart = Stopwatch.StartNew();
                    ExecutionFlow flow;
                    Exception nodeError = null;
                    try
                    {
                        flow = await ExecuteWithTimeoutAsync(current, context, effectiveToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        nodeError = ex;
                        flow = ExecutionFlow.Halt;
                    }
                    nodeStart.Stop();
                    nodesExecuted++;

                    var (inSnap, outSnap) = CapturePinSnapshots(context, current);
                    run.AddRecord(new NodeExecutionRecord(
                        current.Id, current.TypeId, nodeStartedAt, nodeStart.Elapsed,
                        inSnap, outSnap, nodeError));
                    Emit(context, new NodeExitedMessage(context.RunId, graph.Id, current.Id, current.TypeId,
                        nodeError == null, nodeStart.Elapsed, nodeError, inSnap, outSnap));

                    if (nodeError == null)
                    {
                        WriteLog(context, SchedulerLogLevel.Debug, current.Id, current.TypeId,
                            $"Node exited OK in {nodeStart.Elapsed.TotalMilliseconds:F1} ms.", null);
                    }
                    else
                    {
                        WriteLog(context, SchedulerLogLevel.Error, current.Id, current.TypeId,
                            $"Node failed: {nodeError.Message}", nodeError);
                        throw nodeError; // 상위 try/catch에서 처리하여 ExecutionResult로 변환
                    }

                    if (flow.IsHalt)
                    {
                        continue;
                    }

                    // 발화된 exec-out 핀 × 연결 → 다음 노드 push.
                    // declaration/발화 순서 보존: pending이 Stack이므로 역순 push.
                    var nextNodes = ResolveNextNodes(graph, current, flow);
                    for (int i = nextNodes.Count - 1; i >= 0; i--)
                    {
                        pending.Push(nextNodes[i]);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                status = ExecutionStatus.Cancelled;
            }
            catch (Exception ex)
            {
                status = ExecutionStatus.Failed;
                error = ex;
                context.Logger?.Error($"Graph {graph.Id} run failed: {ex.Message}", ex);
            }

            stopwatch.Stop();
            var outputsSnapshot = new Dictionary<string, object>(context.Outputs);
            var result = new ExecutionResult(context.RunId, status, nodesExecuted, error, stopwatch.Elapsed, outputsSnapshot);
            run.Complete(status, DateTimeOffset.UtcNow, error);
            context.HistoryStore?.Add(run);

            var lvl = status == ExecutionStatus.Completed ? SchedulerLogLevel.Info
                    : status == ExecutionStatus.Cancelled ? SchedulerLogLevel.Warning
                    : SchedulerLogLevel.Error;
            WriteLog(context, lvl, null, null,
                $"Run {status} in {stopwatch.Elapsed.TotalMilliseconds:F1} ms ({nodesExecuted} nodes).", error);

            Emit(context, new GraphCompletedMessage(graph.Id, result));

            // 활성 컨텍스트 등록 해제 — 이후 Continue() 호출이 stale context 를 만지지 않도록.
            lock (_gateLock)
            {
                if (_activeContext == context) _activeContext = null;
            }
            return result;
        }

        private static void WriteLog(ExecutionContext ctx, SchedulerLogLevel level,
                                     Guid? nodeId, string nodeTypeId, string message, Exception ex)
        {
            ctx.LogSink?.Write(new SchedulerLogEntry(
                DateTimeOffset.UtcNow, level, ctx.RunId, nodeId, nodeTypeId, message, ex));
        }

        /// <summary>
        /// 노드 종료 직후 호출 — 데이터 입력/출력 핀의 현재 캐시 값을 immutable 스냅샷으로 캡처.
        /// 미연결 입력은 ExecutionContext가 LiteralInputs/DefaultValue 로 채워두므로 캐시에 있을 가능성이 큼.
        /// 캐시에 없는 핀(노드가 GetInput/SetOutput을 호출하지 않은 경우)은 스냅샷에서 제외 (sparse).
        /// </summary>
        private static (IReadOnlyDictionary<string, object> inputs, IReadOnlyDictionary<string, object> outputs)
            CapturePinSnapshots(ExecutionContext ctx, INode node)
        {
            Dictionary<string, object> ins = null;
            Dictionary<string, object> outs = null;
            var cache = ctx.DataCacheSnapshot;
            for (int i = 0; i < node.Pins.Count; i++)
            {
                var pin = node.Pins[i];
                if (pin.Kind != PinKind.Data) continue;
                if (cache.TryGetValue((node.Id, pin.Id), out var val))
                {
                    if (pin.Direction == PinDirection.Input)
                    {
                        (ins ??= new Dictionary<string, object>())[pin.Id] = val;
                    }
                    else
                    {
                        (outs ??= new Dictionary<string, object>())[pin.Id] = val;
                    }
                }
            }
            return (ins, outs);
        }

        private static IReadOnlyList<INode> ResolveNextNodes(NodeGraph graph, INode current, ExecutionFlow flow)
        {
            // 각 발화 핀별로 연결을 찾아 다음 노드를 모은다.
            // N:M 규칙: 같은 발화 핀에서 여러 노드로 분기 가능 (연결 순서 보존).
            var list = new List<INode>();
            for (int i = 0; i < flow.FiredPinIds.Count; i++)
            {
                var firedPin = flow.FiredPinIds[i];
                for (int j = 0; j < graph.Connections.Count; j++)
                {
                    var c = graph.Connections[j];
                    if (c.SourceNodeId == current.Id && c.SourcePinId == firedPin && c.Kind == PinKind.Exec)
                    {
                        var target = graph.GetNode(c.TargetNodeId);
                        if (target != null) list.Add(target);
                    }
                }
            }
            return list;
        }

        private static async Task<ExecutionFlow> ExecuteWithTimeoutAsync(
            INode node, ExecutionContext ctx, CancellationToken outerToken)
        {
            var meta = NodeMetadataRegistry.GetByClrType(node.GetType());
            var nodeTimeoutMs = meta?.DefaultTimeoutMs ?? 0;
            if (nodeTimeoutMs == 0 && ctx.PerNodeTimeoutMs.HasValue)
            {
                nodeTimeoutMs = ctx.PerNodeTimeoutMs.Value;
            }

            if (nodeTimeoutMs <= 0)
            {
                return await node.ExecuteAsync(ctx).ConfigureAwait(false);
            }

            using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
            nodeCts.CancelAfter(nodeTimeoutMs);

            // 노드의 ExecuteAsync는 ctx.CancellationToken을 직접 보지 않으므로, 게으른 노드 보호는
            // 별도 monitor task로 처리: nodeCts 취소 시 NodeTimeoutException.
            var execTask = node.ExecuteAsync(ctx);
            var completed = await Task.WhenAny(execTask, Task.Delay(Timeout.Infinite, nodeCts.Token))
                .ConfigureAwait(false);
            if (completed == execTask)
            {
                return await execTask.ConfigureAwait(false);
            }
            // Delay가 먼저 끝났음 = 노드가 timeout 안에 못 끝남.
            if (outerToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(outerToken);
            }
            throw new NodeTimeoutException(node.Id, nodeTimeoutMs);
        }

        private static CancellationTokenSource CreateLinkedCancellation(ExecutionContext context)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            if (context.GraphTimeout.HasValue && context.GraphTimeout.Value > TimeSpan.Zero)
            {
                cts.CancelAfter(context.GraphTimeout.Value);
            }
            return cts;
        }

        private static void Emit<TMsg>(ExecutionContext ctx, TMsg msg) where TMsg : MessageBase
        {
            ctx.Messenger?.Send(msg);
        }

        public void ToggleBreakpoint(Guid nodeId)
        {
            if (_globalBreakpoints.ContainsKey(nodeId))
                _globalBreakpoints.TryRemove(nodeId, out _);
            else
                _globalBreakpoints.TryAdd(nodeId, 1);
        }

        /// <summary>
        /// 끝까지 실행. 게이트 풀이 + 활성 컨텍스트의 StepMode 를 Run 으로 리셋 →
        /// 다음 노드의 paused 분기가 일치하지 않아 끝까지 흐름.
        /// </summary>
        public void Continue()
        {
            lock (_gateLock)
            {
                _continueGate?.TrySetResult(true);
                _continueGate = null;
                if (_activeContext != null) _activeContext.StepMode = DebugStepMode.Run;
            }
        }

        /// <summary>
        /// 한 노드만 진행 후 다음 노드에서 다시 정지. 게이트만 풀어 현재 정지된 노드를 진행시키고,
        /// StepMode 는 Paused 그대로 유지 → 다음 iteration 의 paused 분기에서 자연스럽게 다시 정지.
        /// </summary>
        public void StepOver()
        {
            lock (_gateLock)
            {
                _continueGate?.TrySetResult(true);
                _continueGate = null;
            }
        }

        private Task WaitForContinueAsync(CancellationToken token)
        {
            TaskCompletionSource<bool> tcs;
            lock (_gateLock)
            {
                _continueGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcs = _continueGate;
            }
            return WaitWithCancellation(tcs, token);
        }

        private static async Task WaitWithCancellation(TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            using var reg = token.Register(() => tcs.TrySetCanceled(token));
            await tcs.Task.ConfigureAwait(false);
        }
    }
}
