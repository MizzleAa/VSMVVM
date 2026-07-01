using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>한 노드의 View 모델. 위치/선택/실행 상태/브레이크포인트 상태 + 입력/출력 핀.</summary>
    public partial class NodeViewModel : ViewModelBase
    {
        public INode Model { get; }

        [Property] private double _x;
        [Property] private double _y;
        [Property] private bool _isSelected;
        [Property] private bool _isExecuting;
        [Property] private bool _hasBreakpoint;

        /// <summary>
        /// 현재 디버그 정지 위치 — BreakpointHitMessage 를 받은 시점에 한 노드만 true.
        /// 한 번에 한 노드만 paused 상태이므로 NodeGraphViewModel 이 다른 노드는 모두 false 로 유지.
        /// NodeView 가 IDE 디버거의 "현재 명령" 화살표 처럼 노란/주황 보더 + 글로우로 강조.
        /// </summary>
        [Property] private bool _isCurrentBreakpoint;

        /// <summary>현재 Run 안에서 한 번이라도 실행됐는지. Run 종료/새 Run 시작 시 false 로 리셋.
        /// XAML 이 이 값에 따라 노드 보더를 Success(초록)로 유지 → 지나간 흐름이 시각적으로 남는다.</summary>
        [Property] private bool _hasExecutedInCurrentRun;

        /// <summary>현재 Run 안에서 이 노드의 실행이 예외로 끝났는지. XAML 이 Error(빨강) 보더로 강조.
        /// Success/Error 동시 활성 시 Error 우선.</summary>
        [Property] private bool _hasErrorInCurrentRun;

        /// <summary>마지막 노드 실행 소요 시간 (NodeExitedMessage.Elapsed). 미실행이면 Zero.</summary>
        [Property]
        [PropertyChangedFor(nameof(LastElapsedMs))]
        [PropertyChangedFor(nameof(HasElapsedSample))]
        [PropertyChangedFor(nameof(IsSlow))]
        [PropertyChangedFor(nameof(IsVerySlow))]
        private TimeSpan _lastElapsed;

        /// <summary>Slow 임계값 (ms). 기본 100ms. 초과 시 헤더 색상이 Warning(Amber) 으로 변함.</summary>
        [Property]
        [PropertyChangedFor(nameof(IsSlow))]
        [PropertyChangedFor(nameof(IsVerySlow))]
        private int _slowThresholdMs = 100;

        /// <summary>Very-slow 임계값 (ms). 기본 1000ms. 초과 시 헤더 색상이 Error(Red) 으로 변함.</summary>
        [Property]
        [PropertyChangedFor(nameof(IsVerySlow))]
        private int _verySlowThresholdMs = 1000;

        public string TypeId => Model.TypeId;
        public Guid Id => Model.Id;

        /// <summary>마지막 실행 소요 시간을 ms 단위 double로 — XAML 바인딩 편의용.</summary>
        public double LastElapsedMs => LastElapsed.TotalMilliseconds;

        /// <summary>이 노드가 적어도 1번 실행되어 측정값을 가지고 있는가.</summary>
        public bool HasElapsedSample => LastElapsed > TimeSpan.Zero;

        /// <summary>SlowThresholdMs 초과 (그러나 VerySlow 미만).</summary>
        public bool IsSlow => HasElapsedSample
            && LastElapsedMs >= SlowThresholdMs
            && LastElapsedMs < VerySlowThresholdMs;

        /// <summary>VerySlowThresholdMs 초과.</summary>
        public bool IsVerySlow => HasElapsedSample && LastElapsedMs >= VerySlowThresholdMs;

        /// <summary>등록된 메타데이터(있을 경우)에서 DisplayName, 없으면 TypeId 그대로.</summary>
        public string DisplayName =>
            NodeMetadataRegistry.Get(Model.TypeId)?.DisplayName ?? Model.TypeId;

        public ObservableCollection<PinViewModel> InputPins { get; } = new();
        public ObservableCollection<PinViewModel> OutputPins { get; } = new();

        /// <summary>
        /// 마지막 실행 시 데이터 입력 핀별 값 스냅샷 — NodeExitedMessage.Inputs 가 채움.
        /// 인스펙터 패널의 ItemsControl 바인딩 대상.
        /// </summary>
        public ObservableCollection<PinValueSnapshot> LastInputs { get; } = new();

        /// <summary>마지막 실행 시 데이터 출력 핀별 값 스냅샷 — NodeExitedMessage.Outputs 가 채움.</summary>
        public ObservableCollection<PinValueSnapshot> LastOutputs { get; } = new();

        /// <summary>적어도 1번 실행되어 스냅샷이 채워졌는가 (인스펙터의 "(no data)" 표시 결정).</summary>
        public bool HasSnapshot => LastInputs.Count > 0 || LastOutputs.Count > 0;

        /// <summary>
        /// NodeGraphViewModel 이 NodeExitedMessage 수신 시 호출하여 스냅샷을 교체.
        /// 컬렉션 변경 + HasSnapshot 알림을 한 번에 처리하기 위해 ViewModel 내부에서 캡슐화.
        /// </summary>
        internal void UpdateSnapshots(IReadOnlyDictionary<string, object> inputs,
                                      IReadOnlyDictionary<string, object> outputs)
        {
            ReplaceSnapshot(LastInputs, inputs, PinDirection.Input);
            ReplaceSnapshot(LastOutputs, outputs, PinDirection.Output);
            OnPropertyChanged(nameof(HasSnapshot));
        }

        private void ReplaceSnapshot(ObservableCollection<PinValueSnapshot> target,
                                     IReadOnlyDictionary<string, object> src,
                                     PinDirection dir)
        {
            target.Clear();
            if (src == null) return;
            foreach (var kv in src)
            {
                var pinVm = FindPin(kv.Key);
                var displayName = pinVm?.DisplayName ?? kv.Key;
                target.Add(new PinValueSnapshot(kv.Key, displayName, kv.Value, dir));
            }
        }

        public NodeViewModel(INode model, double x, double y)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _x = x;
            _y = y;

            for (int i = 0; i < model.Pins.Count; i++)
            {
                var p = model.Pins[i];
                var pinVm = new PinViewModel(p, this);
                if (p.Direction == PinDirection.Input) InputPins.Add(pinVm);
                else OutputPins.Add(pinVm);
            }

            BuildInstanceProperties();
        }

        // ============= 인스턴스 속성 (INodeInstancePropertyHost) =============

        /// <summary>인스펙터의 NODE PROPERTIES 섹션에 노출할 편집 가능한 속성 목록.</summary>
        public ObservableCollection<NodeInstancePropertyViewModel> InstanceProperties { get; } = new();

        /// <summary>Model 이 <see cref="INodeInstancePropertyHost"/> 면 true.</summary>
        public bool HasInstanceProperties => InstanceProperties.Count > 0;

        /// <summary>호스트(GraphWorkspaceViewModel) 가 그래프 변수 목록 변경 시 호출.</summary>
        public System.Func<IReadOnlyList<string>> VariableNameCandidatesProvider { get; set; }

        /// <summary>
        /// Phase M — 호스트가 "변수 추가" 다이얼로그를 띄우고 새 변수 이름을 반환하는 콜백.
        /// 호출 결과가 null/empty 면 사용자가 취소한 것 — 인스펙터는 기존 Value 유지.
        /// </summary>
        public System.Func<string> AddNewVariableRequested { get; set; }

        /// <summary>호스트가 사용 가능한 ItemType 목록 변경 시 호출.</summary>
        public System.Func<IReadOnlyList<string>> TypeCandidatesProvider { get; set; }

        private void BuildInstanceProperties()
        {
            InstanceProperties.Clear();
            if (Model is INodeInstancePropertyHost host)
            {
                var props = host.GetInstanceProperties();
                if (props != null)
                {
                    foreach (var p in props)
                    {
                        InstanceProperties.Add(new NodeInstancePropertyViewModel(p, this));
                    }
                }
            }
            OnPropertyChanged(nameof(HasInstanceProperties));
        }

        /// <summary>호스트가 후보 provider 변경 후 호출 — 모든 속성에 알림.</summary>
        public void RefreshInstancePropertyCandidates()
        {
            foreach (var p in InstanceProperties) p.RefreshCandidates();
        }

        /// <summary>속성 변경으로 핀 구조가 바뀐 경우 (예: ItemType 변경) PinViewModel 재빌드.</summary>
        internal void OnInstancePropertyChanged()
        {
            RebuildPinViewModels();
        }

        // ============= 가변 핀 (IDynamicPinCountNode) =============

        /// <summary>Model 이 <see cref="IDynamicPinCountNode"/> 면 인스펙터에서 +/- 버튼 노출.</summary>
        public bool IsDynamicPinCount => Model is IDynamicPinCountNode;

        /// <summary>가변 핀 라벨 (예: "Args", "Pin pairs"). 비가변 노드면 빈 문자열.</summary>
        public string DynamicPinCountLabel =>
            Model is IDynamicPinCountNode d ? (d.DynamicPinCountLabel ?? "Pins") : string.Empty;

        /// <summary>현재 가변 핀 수. binding 가능 (TwoWay 가능하지만 보통 명령으로 조작).</summary>
        public int DynamicPinCount
        {
            get => Model is IDynamicPinCountNode d ? d.DynamicPinCount : 0;
            set
            {
                if (Model is not IDynamicPinCountNode d) return;
                if (d.DynamicPinCount == value) return;
                var clamped = Math.Max(d.MinDynamicPinCount, Math.Min(d.MaxDynamicPinCount, value));
                d.DynamicPinCount = clamped;
                RebuildPinViewModels();
                OnPropertyChanged(nameof(DynamicPinCount));
            }
        }

        [RelayCommand]
        private void AddPin()
        {
            if (Model is IDynamicPinCountNode d) DynamicPinCount = d.DynamicPinCount + 1;
        }

        [RelayCommand]
        private void RemovePin()
        {
            if (Model is IDynamicPinCountNode d) DynamicPinCount = d.DynamicPinCount - 1;
        }

        /// <summary>Model 의 Pins 가 재생성된 후 ViewModel 컬렉션 동기화 — 핀 사라진/추가된 항목 반영.</summary>
        private void RebuildPinViewModels()
        {
            InputPins.Clear();
            OutputPins.Clear();
            for (int i = 0; i < Model.Pins.Count; i++)
            {
                var p = Model.Pins[i];
                var pinVm = new PinViewModel(p, this);
                if (p.Direction == PinDirection.Input) InputPins.Add(pinVm);
                else OutputPins.Add(pinVm);
            }
        }

        /// <summary>핀 id로 PinViewModel을 조회 (양방향 모두 검색).</summary>
        public PinViewModel FindPin(string pinId)
        {
            foreach (var p in InputPins)
                if (p.Id == pinId) return p;
            foreach (var p in OutputPins)
                if (p.Id == pinId) return p;
            return null;
        }
    }
}
