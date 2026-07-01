using System;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 한 핀의 View 표현 (Exec 또는 Data, Input 또는 Output).
    /// 미연결 데이터 입력 핀의 경우 사용자가 편집 가능한 리터럴 값을 보유 — 노드의 LiteralInputs와 양방향 동기화.
    /// </summary>
    public partial class PinViewModel : ViewModelBase
    {
        public IPin Model { get; }
        public NodeViewModel Node { get; }

        [Property] private object _literalValue;

        /// <summary>마우스 호버 — 핀 글리프가 강조되어 어디에 있는지 식별 가능하게.</summary>
        [Property] private bool _isHovered;

        /// <summary>클릭-클릭 또는 드래그 연결 모드에서 이 핀이 "출발 핀" 으로 잡혀있음.</summary>
        [Property] private bool _isConnectionSource;

        /// <summary>연결 모드 중, 이 핀이 출발 핀과 연결 가능한 후보임 (반대편 핀들에 표시되는 호환 강조).</summary>
        [Property] private bool _isCompatibleTarget;

        public string Id => Model.Id;
        public string DisplayName => Model.DisplayName;
        public PinDirection Direction => Model.Direction;
        public PinKind Kind => Model.Kind;
        public Type ValueType => Model.ValueType;
        public bool IsExec => Model.Kind == PinKind.Exec;
        public bool IsInput => Model.Direction == PinDirection.Input;
        public bool IsOutput => Model.Direction == PinDirection.Output;

        public PinViewModel(IPin model, NodeViewModel node)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Node = node ?? throw new ArgumentNullException(nameof(node));

            // 노드의 기존 LiteralInputs에서 초기값 가져오기
            if (Model.Owner is NodeBase nb && nb.LiteralInputs.TryGetValue(Id, out var lit))
            {
                _literalValue = lit;
            }
            else if (Model is DataPin dp)
            {
                _literalValue = dp.DefaultValue;
            }
        }

        partial void OnLiteralValueChanged(object value)
        {
            // 사용자 편집을 노드의 LiteralInputs에 반영 (직렬화에 보존되도록).
            if (Model.Owner is NodeBase nb && Kind == PinKind.Data && Direction == PinDirection.Input)
            {
                nb.SetLiteralInput(Id, value);
            }
        }
    }
}
