using System;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 노드 타입의 핀 정의 (메타데이터). 노드 인스턴스가 아닌 클래스 레벨 정보.
    /// Source Generator가 [Node]/[InputPin]/[OutputPin] 등에서 추출하여 emit합니다.
    /// <para>
    /// Phase K — 다형성 핀: <see cref="TypeParameterName"/> 가 null 이 아니면 placeholder.
    /// NodeBase.BuildPins 가 노드 인스턴스(<see cref="IPolymorphicNode"/>) 의 TypeArguments 로
    /// 실제 ValueType 을 치환하여 강타입 DataPin 을 생성한다.
    /// </para>
    /// </summary>
    public sealed class PinDescriptor
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PinDirection Direction { get; }
        public PinKind Kind { get; }
        public Type ValueType { get; }
        public object DefaultValue { get; }

        /// <summary>다형성 type parameter 이름 (예: "T", "TKey"). null/empty 면 정적 핀.</summary>
        public string TypeParameterName { get; }

        /// <summary><see cref="TypeParameterName"/> 가 비어있지 않으면 true.</summary>
        public bool IsPolymorphic => !string.IsNullOrEmpty(TypeParameterName);

        public PinDescriptor(string id, string displayName, PinDirection direction,
                             PinKind kind, Type valueType, object defaultValue,
                             string typeParameterName = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            Direction = direction;
            Kind = kind;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            DefaultValue = defaultValue;

            if (!string.IsNullOrEmpty(typeParameterName))
            {
                if (kind == PinKind.Exec)
                    throw new ArgumentException(
                        "Exec pins cannot be polymorphic — they carry no value.", nameof(typeParameterName));
                if (valueType != typeof(object))
                    throw new ArgumentException(
                        "Polymorphic pin must use typeof(object) as placeholder ValueType — actual type is substituted at BuildPins time.",
                        nameof(valueType));
                TypeParameterName = typeParameterName;
            }
        }
    }
}
