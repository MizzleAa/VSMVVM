using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// Phase K — 다형성 핀을 가진 노드. <see cref="PinDescriptor.TypeParameterName"/> 가 비어있지 않은
    /// 핀에 대해, 이 dict 가 type parameter 이름 → 실제 CLR 타입 매핑을 제공한다.
    /// <para>
    /// NodeBase.BuildPins 가 이 매핑으로 placeholder ValueType(=object) 을 실제 타입으로 치환하여
    /// 강타입 DataPin&lt;T&gt; 를 생성한다. 누락된 type parameter 는 object 로 폴백.
    /// </para>
    /// <para>
    /// 인스턴스 속성(예: <c>ItemType</c>)을 변경할 때는 setter 에서 <see cref="NodeBase.InvalidatePins"/>
    /// (protected) 를 호출하여 다음 Pins 접근에서 새 핀 컬렉션이 빌드되도록 한다.
    /// </para>
    /// </summary>
    public interface IPolymorphicNode
    {
        IReadOnlyDictionary<string, Type> TypeArguments { get; }
    }
}
