using System;
using System.ComponentModel;
using System.Windows;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Graph;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 한 연결의 View 모델. 양 끝점 (Start, End)은 소스/타겟 노드 위치 + 핀의 노드 내 상대 위치로 결정.
    /// 핀의 상대 위치는 UI에서 측정해 PinPositionInNode를 갱신시키며, 노드 X/Y 가 변할 때 자동 재계산.
    /// </summary>
    public partial class ConnectionViewModel : ViewModelBase
    {
        public NodeConnection Model { get; }
        public NodeViewModel Source { get; }
        public NodeViewModel Target { get; }
        public PinViewModel SourcePin { get; }
        public PinViewModel TargetPin { get; }

        [Property] private Point _start;
        [Property] private Point _end;
        [Property] private Point _sourcePinOffset;   // 소스 핀의 노드 좌상단 기준 상대 좌표
        [Property] private Point _targetPinOffset;

        /// <summary>
        /// 같은 소스 핀에서 나가는 형제 연결들 중 본 연결의 순서(0-based). N:M 시각화 시 베지어 곡률 오프셋에 사용.
        /// NodeGraphViewModel이 그래프 변경 시 갱신.
        /// </summary>
        [Property] private int _siblingIndex;

        /// <summary>같은 소스 핀에서 나가는 형제 연결의 총 개수.</summary>
        [Property] private int _siblingCount = 1;

        public Guid Id => Model.Id;

        /// <summary>
        /// N:M 시각화를 위한 베지어 수직 오프셋. 형제가 1개면 0, 여러 개면 중심선 기준으로 ±픽셀씩 분리.
        /// 예: 형제 3개 → [-12, 0, +12]
        /// </summary>
        public double CurvatureOffset
        {
            get
            {
                if (SiblingCount <= 1) return 0;
                const double Step = 12.0;
                var center = (SiblingCount - 1) / 2.0;
                return (SiblingIndex - center) * Step;
            }
        }

        public ConnectionViewModel(NodeConnection model,
            NodeViewModel source, PinViewModel sourcePin,
            NodeViewModel target, PinViewModel targetPin)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            SourcePin = sourcePin ?? throw new ArgumentNullException(nameof(sourcePin));
            TargetPin = targetPin ?? throw new ArgumentNullException(nameof(targetPin));

            Source.PropertyChanged += OnEndpointChanged;
            Target.PropertyChanged += OnEndpointChanged;
            RecomputeEndpoints();
        }

        partial void OnSourcePinOffsetChanged(Point value) => RecomputeEndpoints();
        partial void OnTargetPinOffsetChanged(Point value) => RecomputeEndpoints();
        partial void OnSiblingIndexChanged(int value) => OnPropertyChanged(nameof(CurvatureOffset));
        partial void OnSiblingCountChanged(int value) => OnPropertyChanged(nameof(CurvatureOffset));

        private void OnEndpointChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeViewModel.X) || e.PropertyName == nameof(NodeViewModel.Y))
            {
                RecomputeEndpoints();
            }
        }

        private void RecomputeEndpoints()
        {
            Start = new Point(Source.X + SourcePinOffset.X, Source.Y + SourcePinOffset.Y);
            End = new Point(Target.X + TargetPinOffset.X, Target.Y + TargetPinOffset.Y);
        }
    }
}
