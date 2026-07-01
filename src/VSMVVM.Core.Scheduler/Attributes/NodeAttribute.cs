using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// 노드 클래스에 부착하여 그래프 등록 메타데이터를 선언합니다.
    /// Source Generator가 이 속성을 감지하여 핀 디스크립터와 NodeMetadataRegistry 등록 코드를 생성합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NodeAttribute : Attribute
    {
        /// <summary>노드 타입 식별자(직렬화 키, 팔레트 키). 어셈블리 전역 고유.</summary>
        public string Id { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int TimeoutMs { get; set; }

        public NodeAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }
}
