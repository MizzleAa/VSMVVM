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

            // 글로벌 브레이크포인트를 컨텍스트로 복사 (Breakpoints 는 HashSet 이므로 lock 아래).
            lock (_gateLock)
            {
                foreach (var kv in _globalBreakpoints)
                {
                    context.Breakpoints.Add(kv.Key);
                }
            }

            var entry = graph.GetNode(entryNodeId)
                ?? throw new InvalidOperationException($"Entry node {entryNodeId} not in graph.");

            using var linkedCts = CreateLinkedCancellation(context);
            var effectiveToken = linkedCts.Token;

            var runState = new ParallelRunState();
            var stopwatch = Stopwatch.StartNew();
            var runStartedAt = DateTimeOffset.UtcNow;
            var run = new ExecutionRun(context.RunId, graph.Id, runStartedAt);
            ExecutionStatus status = ExecutionStatus.Completed;
            Exception error = null;
            long memoryBaseline = context.MemoryBudgetBytes.HasValue
                ? GC.GetTotalMemory(forceFullCollection: false)
                : 0;

            WriteLog(context, SchedulerLogLevel.Info, null, null, $"Run started for graph {graph.Id}.", null);

            // Continue() 가 StepMode 를 리셋할 수 있도록 활성 컨텍스트 등록. finally 에서 clear.
            lock (_gateLock) { _activeContext = context; }

            try
            {
                await ExecuteBranchAsync(graph, entry, context, run, runState, memoryBaseline, effectiveToken)
                    .ConfigureAwait(false);
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
                // 진단용: 스택트레이스를 SchedulerLogSink 에 그대로 남겨 UI/파일에서 확인 가능하게.
                WriteLog(context, SchedulerLogLevel.Error, null, null,
                    $"Run FAILED. Type={ex.GetType().Name} Msg={ex.Message}\nStack:\n{ex.StackTrace}", ex);
            }

            int nodesExecuted = runState.NodesExecuted;

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

        /// <summary>
        /// 하나의 exec 브랜치를 직렬 실행한다. <see cref="IParallelForkNode"/> 노드를 만나면 각 발화 브랜치를
        /// <c>Task.Run</c> 으로 재귀 호출해 병렬화한 뒤 <c>Task.WhenAll</c> 로 재합류.
        /// <see cref="IJoinBarrierNode"/> 노드는 도착 카운터를 증가시키고 마지막 도달자만 downstream 을 이어간다.
        /// </summary>
        private async Task ExecuteBranchAsync(
            NodeGraph graph, INode entry, ExecutionContext context, ExecutionRun run,
            ParallelRunState state, long memoryBaseline, CancellationToken effectiveToken)
        {
            // 브랜치별 직렬 스택 — 기존 로직 유지.
            var pending = new Stack<INode>();
            pending.Push(entry);

            while (pending.Count > 0)
            {
                effectiveToken.ThrowIfCancellationRequested();

                if (state.NodesExecuted >= context.MaxNodesExecuted)
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

                // Join 노드는 도착 카운트가 목표에 도달한 경우에만 실제 실행 · downstream 진행.
                // 그 외 도착은 이 브랜치 실행을 여기서 종료 (다른 브랜치가 마지막 도착자로서 이어감).
                if (current is IJoinBarrierNode barrier)
                {
                    int arrivals = state.RecordJoinArrival(current.Id);
                    int expected = ResolveExpectedArrivals(graph, current, barrier);
                    if (arrivals < expected)
                    {
                        // 아직 다른 브랜치가 남았음 — 이 브랜치는 여기서 대기 종료.
                        WriteLog(context, SchedulerLogLevel.Debug, current.Id, current.TypeId,
                            $"Join arrivals {arrivals}/{expected} — awaiting siblings.", null);
                        return;
                    }
                    // 마지막 도착 — 계속 진행. 다음 iteration 에서 이 Join 노드를 정상 실행.
                }

                // 브레이크포인트 검사 — UI 스레드가 SyncActiveContext 로 동시 수정할 수 있으므로 lock.
                bool hasBreakpoint;
                lock (_gateLock) { hasBreakpoint = context.Breakpoints.Contains(current.Id); }
                if (hasBreakpoint)
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
                state.IncrementNodesExecuted();

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
                    throw nodeError; // 상위 RunAsync try/catch 에서 처리하여 ExecutionResult 로 변환
                }

                if (flow.IsHalt)
                {
                    continue;
                }

                // IParallelForkNode 이고 다중 발화면 각 발화 핀의 다음 노드들을 병렬 Task 로 분기.
                bool parallelFork = current is IParallelForkNode && flow.FiredPinIds.Count > 1;
                if (parallelFork)
                {
                    var branchTasks = new List<Task>(flow.FiredPinIds.Count);
                    for (int i = 0; i < flow.FiredPinIds.Count; i++)
                    {
                        var pinNextNodes = ResolveNextNodesForPin(graph, current, flow.FiredPinIds[i]);
                        foreach (var nextNode in pinNextNodes)
                        {
                            var branchEntry = nextNode; // capture
                            branchTasks.Add(Task.Run(
                                () => ExecuteBranchAsync(graph, branchEntry, context, run, state,
                                    memoryBaseline, effectiveToken),
                                effectiveToken));
                        }
                    }
                    await Task.WhenAll(branchTasks).ConfigureAwait(false);
                    // Fork 이후 흐름은 각 브랜치가 각각 이어감 (Join 에서 재합류) — 이 프레임에서는 종료.
                    return;
                }

                // 발화된 exec-out 핀 × 연결 → 다음 노드 push.
                // declaration/발화 순서 보존: pending 이 Stack 이므로 역순 push.
                var nextNodes = ResolveNextNodes(graph, current, flow);
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    pending.Push(nextNodes[i]);
                }
            }
        }

        /// <summary>
        /// Join 노드가 기대해야 할 arrival 수를 결정. 노드가 <see cref="IJoinBarrierNode.ExpectedArrivals"/> 로
        /// 명시한 값이 있으면 그걸 사용하되, 그래프 상 실제 연결된 exec-in 개수와 비교해 더 작은 값을 채택
        /// (사용자가 InputCount 는 4 로 잡아뒀지만 실제 3 개만 연결한 경우도 흘려보내야 함).
        /// </summary>
        private static int ResolveExpectedArrivals(NodeGraph graph, INode joinNode, IJoinBarrierNode barrier)
        {
            int declared = Math.Max(1, barrier.ExpectedArrivals);
            int connected = 0;
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                var c = graph.Connections[i];
                if (c.TargetNodeId == joinNode.Id && c.Kind == PinKind.Exec)
                {
                    connected++;
                }
            }
            if (connected <= 0) return declared;
            return Math.Min(declared, connected);
        }

        private static IReadOnlyList<INode> ResolveNextNodesForPin(NodeGraph graph, INode current, string firedPinId)
        {
            var list = new List<INode>();
            for (int j = 0; j < graph.Connections.Count; j++)
            {
                var c = graph.Connections[j];
                if (c.SourceNodeId == current.Id && c.SourcePinId == firedPinId && c.Kind == PinKind.Exec)
                {
                    var target = graph.GetNode(c.TargetNodeId);
                    if (target != null) list.Add(target);
                }
            }
            return list;
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
            bool nowEnabled;
            if (_globalBreakpoints.ContainsKey(nodeId))
            {
                _globalBreakpoints.TryRemove(nodeId, out _);
                nowEnabled = false;
            }
            else
            {
                _globalBreakpoints.TryAdd(nodeId, 1);
                nowEnabled = true;
            }
            SyncActiveContext(nodeId, nowEnabled);
        }

        public void SetBreakpoint(Guid nodeId, bool enabled)
        {
            if (enabled) _globalBreakpoints.TryAdd(nodeId, 1);
            else _globalBreakpoints.TryRemove(nodeId, out _);
            SyncActiveContext(nodeId, enabled);
        }

        /// <summary>Run 진행 중인 컨텍스트가 있으면 그 컨텍스트의 Breakpoints 도 동기화 —
        /// 그러지 않으면 실행 중에 해제해도 다음 iteration 에서 다시 정지된다.
        /// Breakpoints 는 HashSet 이라 스레드 안전 아님 → _gateLock 아래에서 접근.</summary>
        private void SyncActiveContext(Guid nodeId, bool enabled)
        {
            lock (_gateLock)
            {
                var ctx = _activeContext;
                if (ctx == null) return;
                if (enabled) ctx.Breakpoints.Add(nodeId);
                else ctx.Breakpoints.Remove(nodeId);
            }
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

        /// <summary>
        /// 한 RunAsync 동안 병렬 브랜치 사이에 공유되는 실행 통계 · Join 도착 카운터.
        /// 여러 Task 가 동시 접근하므로 모든 변경은 lock 아래.
        /// </summary>
        private sealed class ParallelRunState
        {
            private readonly object _lock = new object();
            private readonly Dictionary<Guid, int> _joinArrivals = new Dictionary<Guid, int>();
            private int _nodesExecuted;

            public int NodesExecuted
            {
                get { lock (_lock) return _nodesExecuted; }
            }

            public void IncrementNodesExecuted()
            {
                lock (_lock) _nodesExecuted++;
            }

            public int RecordJoinArrival(Guid joinNodeId)
            {
                lock (_lock)
                {
                    _joinArrivals.TryGetValue(joinNodeId, out var c);
                    c++;
                    _joinArrivals[joinNodeId] = c;
                    return c;
                }
            }
        }
    }
}
