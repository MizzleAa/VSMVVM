using System;

namespace VSMVVM.Core.Scheduler.Attributes
{
    /// <summary>
    /// 정적 필드에 부착하여 해당 필드를 그래프의 "파라미터 노드"로 노출.
    /// <see cref="MethodNodeAttribute"/> 와 대칭 — 메서드는 함수 노드(<see cref="Nodes.CustomFunctionNode"/>),
    /// 필드는 파라미터 노드(<see cref="Nodes.CustomParameterNode"/>).
    /// <para>
    /// 핀: 출력 1개 (필드 타입). 매 실행마다 필드의 현재 값을 Out 에 흘림.
    /// 필드 자체가 어셈블리에 상주하므로 그래프 전역 공유 — 여러 파라미터 노드 인스턴스가 같은 값을 본다.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ParameterNodeAttribute : Attribute
    {
        /// <summary>노드 타입 식별자 (직렬화 키, 팔레트 키). 어셈블리 전역 고유.</summary>
        public string Id { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }

        public ParameterNodeAttribute(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }
}
