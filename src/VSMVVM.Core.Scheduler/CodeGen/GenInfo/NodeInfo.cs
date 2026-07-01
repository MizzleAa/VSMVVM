using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.CodeGen.GenInfo
{
    /// <summary>Source Generator가 [Node] 부착 클래스에서 추출한 메타데이터.</summary>
    internal sealed class NodeInfo
    {
        public string Namespace { get; set; }
        public string ClassName { get; set; }
        public string FullClassName { get; set; }      // 네임스페이스 포함 (typeof emit용)
        public string TypeId { get; set; }              // [Node("...")] 인자
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int TimeoutMs { get; set; }
        public List<NodePinInfo> Pins { get; } = new List<NodePinInfo>();
    }

    /// <summary>핀 멤버 1개에서 추출한 정보.</summary>
    internal sealed class NodePinInfo
    {
        /// <summary>핀 id (멤버 이름).</summary>
        public string Id { get; set; }
        public string DisplayName { get; set; }
        /// <summary>Exec / Data.</summary>
        public PinKindGen Kind { get; set; }
        /// <summary>Input / Output.</summary>
        public PinDirectionGen Direction { get; set; }
        /// <summary>Data 핀의 값 타입(typeof emit용). Exec 핀은 "void".</summary>
        public string ValueTypeName { get; set; }
        /// <summary>InputPin.DefaultValue의 C# 리터럴 표현(emit 시 그대로 삽입). 없으면 "null".</summary>
        public string DefaultValueLiteral { get; set; }
    }

    internal enum PinKindGen { Exec, Data }
    internal enum PinDirectionGen { Input, Output }
}
