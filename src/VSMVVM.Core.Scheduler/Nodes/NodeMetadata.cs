using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 노드 클래스의 정적 메타데이터. Source Generator가 [Node] 부착 클래스에서 추출하여
    /// NodeMetadataRegistry에 등록합니다.
    /// <para>
    /// Phase K — 다형성 노드는 <see cref="TypeParameters"/> 가 비어있지 않다 (예: ["T"]).
    /// 팔레트는 이 목록이 비어있지 않은 entry 에 대해 드롭 시 ItemType 선택 팝업을 띄운다.
    /// 핀 설명자 중 <see cref="PinDescriptor.IsPolymorphic"/> 인 것들이 이 type parameter 를 참조.
    /// </para>
    /// </summary>
    public sealed class NodeMetadata
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public int DefaultTimeoutMs { get; }
        public Type ClrType { get; }
        public Func<INode> Factory { get; }
        public IReadOnlyList<PinDescriptor> Pins { get; }

        /// <summary>
        /// 다형성 type parameter 이름 목록 (순서 보존). 비-제네릭 노드는 빈 리스트.
        /// 예: <c>["T"]</c> (List/Variable), <c>["TKey","TValue"]</c> (Map).
        /// </summary>
        public IReadOnlyList<string> TypeParameters { get; }

        public bool IsPolymorphic => TypeParameters.Count > 0;

        public NodeMetadata(
            string typeId,
            string displayName,
            string category,
            string description,
            int defaultTimeoutMs,
            Type clrType,
            Func<INode> factory,
            IReadOnlyList<PinDescriptor> pins,
            IReadOnlyList<string> typeParameters = null)
        {
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            DisplayName = string.IsNullOrEmpty(displayName) ? typeId : displayName;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            DefaultTimeoutMs = defaultTimeoutMs;
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Pins = pins ?? Array.Empty<PinDescriptor>();
            TypeParameters = typeParameters ?? Array.Empty<string>();
        }
    }
}
