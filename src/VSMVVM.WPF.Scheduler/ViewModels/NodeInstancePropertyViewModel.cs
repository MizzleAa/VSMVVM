using System.Collections.Generic;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// Phase K — 인스펙터의 NODE PROPERTIES 섹션에 1개 속성을 노출.
    /// 후보값(드롭다운) 이 있으면 ComboBox, 없으면 TextBox 사용.
    /// <para>
    /// Phase M — VariableName 종류는 드롭다운 마지막에 <see cref="AddNewVariableMagicValue"/> 를 추가하여
    /// 사용자가 변수가 없는 상태에서도 새 변수를 즉시 추가할 수 있게 한다. 선택 시 호스트의
    /// <see cref="NodeViewModel.AddNewVariableRequested"/> 콜백이 호출됨.
    /// </para>
    /// </summary>
    public sealed class NodeInstancePropertyViewModel : ViewModelBase
    {
        /// <summary>VariableName 드롭다운의 마법 항목 — 선택 시 변수 추가 다이얼로그 트리거.</summary>
        public const string AddNewVariableMagicValue = "+ Add new variable…";

        private readonly NodeInstancePropertyDescriptor _descriptor;
        private readonly NodeViewModel _owner;

        public string Id => _descriptor.Id;
        public string DisplayName => _descriptor.DisplayName;
        public NodeInstancePropertyKind Kind => _descriptor.Kind;

        public bool IsType => Kind == NodeInstancePropertyKind.Type;
        public bool IsVariableName => Kind == NodeInstancePropertyKind.VariableName;
        public bool IsText => Kind == NodeInstancePropertyKind.Text;

        /// <summary>드롭다운 후보값. 없으면 빈 컬렉션.</summary>
        public IReadOnlyList<string> Candidates { get; private set; }

        /// <summary>속성의 현재 값 (string 표현).</summary>
        public string Value
        {
            get => _descriptor.GetValue();
            set
            {
                // Phase M — VariableName 의 마법 항목이 들어오면 변수 추가 콜백 트리거 후 결과를 Value 로 반영.
                if (Kind == NodeInstancePropertyKind.VariableName
                    && value == AddNewVariableMagicValue
                    && _owner.AddNewVariableRequested != null)
                {
                    var newVarName = _owner.AddNewVariableRequested();
                    RefreshCandidates();
                    if (!string.IsNullOrEmpty(newVarName))
                    {
                        _descriptor.SetValue(newVarName);
                        _owner.OnInstancePropertyChanged();
                    }
                    OnPropertyChanged(nameof(Value));
                    return;
                }

                var current = _descriptor.GetValue();
                if (current == value) return;
                _descriptor.SetValue(value);
                _owner.OnInstancePropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        public NodeInstancePropertyViewModel(NodeInstancePropertyDescriptor descriptor, NodeViewModel owner)
        {
            _descriptor = descriptor;
            _owner = owner;
            RefreshCandidates();
        }

        /// <summary>호스트(GraphWorkspaceViewModel) 가 그래프 변수 또는 사용 가능 타입 목록 변경 시 호출.</summary>
        public void RefreshCandidates()
        {
            IReadOnlyList<string> raw;
            if (_descriptor.GetCandidates != null)
            {
                raw = _descriptor.GetCandidates();
            }
            else if (Kind == NodeInstancePropertyKind.VariableName && _owner.VariableNameCandidatesProvider != null)
            {
                raw = _owner.VariableNameCandidatesProvider();
            }
            else if (Kind == NodeInstancePropertyKind.Type && _owner.TypeCandidatesProvider != null)
            {
                raw = _owner.TypeCandidatesProvider();
            }
            else
            {
                raw = System.Array.Empty<string>();
            }

            // Phase M — VariableName 종류는 마법 항목 "+ Add new variable…" 을 마지막에 추가.
            if (Kind == NodeInstancePropertyKind.VariableName && _owner.AddNewVariableRequested != null)
            {
                var list = new List<string>(raw.Count + 1);
                list.AddRange(raw);
                list.Add(AddNewVariableMagicValue);
                Candidates = list;
            }
            else
            {
                Candidates = raw;
            }
            OnPropertyChanged(nameof(Candidates));
        }
    }
}
