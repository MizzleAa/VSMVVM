using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Runtime
{
    /// <summary>
    /// 한 번의 그래프 실행 컨텍스트. SchedulerService가 생성하여 각 노드에 전달합니다.
    /// 노드는 ctx.GetInput&lt;T&gt;(node, "PinId")로 데이터 입력을 풀하고,
    /// ctx.SetOutput&lt;T&gt;(node, "PinId", value)로 결과를 캐시에 둡니다.
    /// <para>
    /// ForkNode 병렬 브랜치가 같은 ExecutionContext 를 공유하므로 pull-eval 캐시는 스레드 안전 컬렉션 사용.
    /// 상류 노드 evaluate 는 노드 id 별 SemaphoreSlim 으로 직렬화 — 같은 상류를 두 브랜치가 동시에 풀해도 1 회만 실행.
    /// </para>
    /// </summary>
    public sealed class ExecutionContext
    {
        private readonly ConcurrentDictionary<(Guid nodeId, string pinId), object> _dataCache
            = new ConcurrentDictionary<(Guid, string), object>();
        // Set 대체 — value 는 사용 안 함 (byte 0). TryAdd 성공 = 최초 방문.
        private readonly ConcurrentDictionary<(Guid nodeId, string pinId), byte> _pullVisitedSet
            = new ConcurrentDictionary<(Guid, string), byte>();
        // 상류 노드 evaluate 직렬화용 per-node lock. Sample Fork/Join 데모에서 두 브랜치가 같은
        // 상류 데이터 노드를 동시 pull 하는 상황 방지.
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _evaluateLocks
            = new ConcurrentDictionary<Guid, SemaphoreSlim>();

        public Guid RunId { get; }
        public NodeGraph Graph { get; }
        public CancellationToken CancellationToken { get; }
        public ILoggerService Logger { get; }
        public IMessenger Messenger { get; }

        /// <summary>완료된 ExecutionRun 보관소(선택). null 이면 보관 안 함.</summary>
        public IExecutionHistoryStore HistoryStore { get; set; }

        /// <summary>I.4 — 구조화된 SchedulerLogEntry 의 sink(선택). null 이면 logging skip.</summary>
        public ISchedulerLogSink LogSink { get; set; }

        /// <summary>I.5 — 메모리 예산(바이트). 초과 감지 시 graceful 중단 + GraphMemoryBudgetExceededException.</summary>
        public long? MemoryBudgetBytes { get; set; }

        /// <summary>그래프 실행 도중 사용자 코드가 자유롭게 사용 가능한 스크래치패드.</summary>
        public IDictionary<string, object> Variables { get; } = new Dictionary<string, object>();

        /// <summary>
        /// I.2c — OutputNode 가 채우는 그래프-출력 키-값. ExecutionResult.Outputs 로 노출되며 외부 호출자가 그래프를 함수처럼 호출하는 진입점.
        /// </summary>
        public IDictionary<string, object> Outputs { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Phase J — 외부 호출자가 RunAsync 전에 채우는 그래프-입력 키-값. <see cref="Nodes.BuiltIn.InputNode"/> 가
        /// Key 로 lookup 하여 Value 출력 핀으로 노출. OutputNode 의 대칭 — 그래프를 함수처럼 호출할 때 입력 전달용.
        /// </summary>
        public IDictionary<string, object> Inputs { get; } = new Dictionary<string, object>();

        /// <summary>그래프 전체 타임아웃(null = 무제한).</summary>
        public TimeSpan? GraphTimeout { get; set; }

        /// <summary>노드 1개당 기본 타임아웃(null = 무제한). [Node(TimeoutMs=...)] 가 우선.</summary>
        public int? PerNodeTimeoutMs { get; set; }

        /// <summary>무한 루프 가드: 한 RunAsync 내 누적 노드 실행 횟수 한도.</summary>
        public int MaxNodesExecuted { get; set; } = 100_000;

        /// <summary>풀 재귀 깊이 가드.</summary>
        public int MaxStackDepth { get; set; } = 1024;

        /// <summary>브레이크포인트가 걸린 노드 id 집합 (스레드 안전 호출은 SchedulerService 측 책임).</summary>
        public ISet<Guid> Breakpoints { get; } = new HashSet<Guid>();

        public DebugStepMode StepMode { get; set; } = DebugStepMode.Run;

        public ExecutionContext(NodeGraph graph,
                                CancellationToken cancellationToken = default,
                                ILoggerService logger = null,
                                IMessenger messenger = null)
        {
            RunId = Guid.NewGuid();
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            CancellationToken = cancellationToken;
            Logger = logger;
            Messenger = messenger;
        }

        /// <summary>
        /// 노드의 데이터 입력 핀을 pull한다. 연결된 출력이 있으면 상류 노드를 평가하여 가져오고,
        /// 없으면 핀의 DefaultValue(또는 PinDescriptor)에서 가져온다.
        /// 같은 (node, pin) 조합은 한 실행 tick 내에서 1번만 평가하여 캐시한다.
        /// </summary>
        public T GetInput<T>(INode node, string pinId)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (pinId == null) throw new ArgumentNullException(nameof(pinId));

            var key = (node.Id, pinId);
            if (_dataCache.TryGetValue(key, out var cached))
            {
                return (T)cached;
            }

            // 순환 의존성 보호 — ConcurrentDictionary.TryAdd 로 원자적 검사·삽입.
            if (!_pullVisitedSet.TryAdd(key, 0))
            {
                throw new CyclicDataDependencyException(node.Id, pinId);
            }
            try
            {
                var value = ResolveInputValue<T>(node, pinId);
                _dataCache[key] = value;
                return value;
            }
            finally
            {
                _pullVisitedSet.TryRemove(key, out _);
            }
        }

        public void SetOutput<T>(INode node, string pinId, T value)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (pinId == null) throw new ArgumentNullException(nameof(pinId));
            _dataCache[(node.Id, pinId)] = value;
        }

        private T ResolveInputValue<T>(INode node, string pinId)
        {
            // 연결 추적: target = (node, pinId), source = upstream
            NodeConnection match = null;
            for (int i = 0; i < Graph.Connections.Count; i++)
            {
                var c = Graph.Connections[i];
                if (c.TargetNodeId == node.Id && c.TargetPinId == pinId && c.Kind == PinKind.Data)
                {
                    match = c;
                    break;
                }
            }

            if (match == null)
            {
                // 미연결: 노드의 사용자 LiteralInputs 가 있으면 우선, 없으면 핀의 DefaultValue.
                if (node is NodeBase nb && nb.LiteralInputs.TryGetValue(pinId, out var lit))
                {
                    if (lit is T tlit) return tlit;
                    if (lit == null) return default;
                    try { return (T)Convert.ChangeType(lit, typeof(T)); }
                    catch { return default; }
                }

                var pin = FindPin(node, pinId);
                if (pin is DataPin dp && dp.DefaultValue is T tv) return tv;
                if (pin is DataPin dp2 && dp2.DefaultValue == null) return default;
                if (pin is DataPin dp3) return (T)Convert.ChangeType(dp3.DefaultValue, typeof(T));
                return default;
            }

            // 연결됨: 상류 노드를 평가하고 그 출력 캐시에서 가져온다.
            var sourceNode = Graph.GetNode(match.SourceNodeId);
            if (sourceNode == null) return default;

            var upstreamKey = (sourceNode.Id, match.SourcePinId);
            if (!_dataCache.TryGetValue(upstreamKey, out var upstream))
            {
                // Fork 병렬 브랜치가 같은 상류 노드를 동시 pull 하면 EvaluateAsync 가 두 번 호출되어
                // SetOutput 이 이중 실행 · 상태 오염. 노드 id 별 SemaphoreSlim 으로 직렬화한 뒤 double-check.
                var sem = _evaluateLocks.GetOrAdd(sourceNode.Id, _ => new SemaphoreSlim(1, 1));
                sem.Wait();
                try
                {
                    if (!_dataCache.TryGetValue(upstreamKey, out upstream))
                    {
                        // EvaluateAsync는 보통 ExecuteAsync를 호출하여 SetOutput을 채운다.
                        // Phase 3a는 동기 wait — 데이터 노드는 보통 비동기성이 약함.
                        sourceNode.EvaluateAsync(this).GetAwaiter().GetResult();
                        _dataCache.TryGetValue(upstreamKey, out upstream);
                    }
                }
                finally
                {
                    sem.Release();
                }
            }

            if (upstream == null) return default;
            if (upstream is T direct) return direct;
            try { return (T)Convert.ChangeType(upstream, typeof(T)); }
            catch { return default; }
        }

        private static IPin FindPin(INode node, string pinId)
        {
            for (int i = 0; i < node.Pins.Count; i++)
            {
                if (node.Pins[i].Id == pinId) return node.Pins[i];
            }
            return null;
        }

        /// <summary>테스트/디버그용 — 캐시 스냅샷.</summary>
        public IReadOnlyDictionary<(Guid, string), object> DataCacheSnapshot => _dataCache;

        /// <summary>루프/반복 노드 (예: RepeatNode) 가 매 반복 시작 시 호출하여 이전 반복의 pull-eval 캐시를 비운다.
        /// 이걸 안 하면 GetVariable 등이 첫 iteration 의 값을 계속 반환하고, 그래프 변수 갱신이 반영되지 않는다.</summary>
        public void InvalidateDataCache()
        {
            _dataCache.Clear();
        }
    }
}
