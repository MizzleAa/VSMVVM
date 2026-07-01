using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// 메소드에 부착하여 해당 메소드를 그래프 노드로 노출합니다.
    /// 사용자가 동적 컴파일한 어셈블리(또는 정적 어셈블리)의 메소드를 <see cref="Nodes.CustomFunctionNode"/>로 래핑하여
    /// NodeMetadataRegistry에 등록할 때 사용됩니다. <see cref="NodeAttribute"/>는 클래스 레벨, 본 속성은 메소드 레벨.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MethodNodeAttribute : Attribute
    {
        /// <summary>노드 타입 식별자(직렬화 키, 팔레트 키). 어셈블리 전역 고유.</summary>
        public string Id { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int TimeoutMs { get; set; }

        public MethodNodeAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }
}
