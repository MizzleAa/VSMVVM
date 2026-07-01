using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// Phase K — 노드가 인스턴스 수준에서 노출하는 편집 가능한 속성 (예: ItemType, VariableName).
    /// 인스펙터 UI 가 이걸 일반화해서 NODE PROPERTIES 섹션에 렌더링.
    /// </summary>
    public interface INodeInstancePropertyHost
    {
        IReadOnlyList<NodeInstancePropertyDescriptor> GetInstanceProperties();
    }

    /// <summary>
    /// 인스펙터에 노출할 단일 속성의 메타데이터 + getter/setter.
    /// </summary>
    public sealed class NodeInstancePropertyDescriptor
    {
        /// <summary>식별자 (예: "ItemType", "VariableName").</summary>
        public string Id { get; }

        /// <summary>UI 표시명 (예: "Item Type", "Variable").</summary>
        public string DisplayName { get; }

        /// <summary>편집기 종류 — 인스펙터가 어떤 컨트롤을 띄울지 결정.</summary>
        public NodeInstancePropertyKind Kind { get; }

        /// <summary>현재 값 (string 표현). Kind 가 Type 이면 stable name, VariableName 이면 변수 이름.</summary>
        public System.Func<string> GetValue { get; }

        /// <summary>값 설정. UI 가 선택 후 호출.</summary>
        public System.Action<string> SetValue { get; }

        /// <summary>드롭다운 후보값 (선택). null 이면 자유 입력.</summary>
        public System.Func<IReadOnlyList<string>> GetCandidates { get; }

        public NodeInstancePropertyDescriptor(
            string id,
            string displayName,
            NodeInstancePropertyKind kind,
            System.Func<string> getValue,
            System.Action<string> setValue,
            System.Func<IReadOnlyList<string>> getCandidates = null)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            GetValue = getValue;
            SetValue = setValue;
            GetCandidates = getCandidates;
        }
    }

    public enum NodeInstancePropertyKind
    {
        /// <summary>CLR 타입 선택 (stable name). 후보값 = 사용 가능한 타입 목록.</summary>
        Type,
        /// <summary>그래프 변수 이름 선택. 후보값 = 현재 그래프의 변수 목록.</summary>
        VariableName,
        /// <summary>자유 텍스트.</summary>
        Text,
    }
}
