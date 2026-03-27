using System.Collections.Generic;

namespace VSMVVM.Core.CodeGen.GenInfo
{
    /// <summary>
    /// Source Generator에서 사용하는 필드 메타데이터 정보.
    /// </summary>
    internal sealed class AutoFieldInfo
    {
        /// <summary>
        /// 필드 식별자 (예: "_firstName").
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// 필드 타입 이름 (예: "string").
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// PropertyChangedFor 대상 프로퍼티 이름 목록.
        /// </summary>
        public List<string> TargetNames { get; set; } = new List<string>();

        /// <summary>
        /// 생성된 프로퍼티에 전달할 어트리뷰트 텍스트 목록 (DataAnnotation 등).
        /// </summary>
        public List<string> ForwardedAttributes { get; set; } = new List<string>();

        /// <summary>
        /// NotifyCanExecuteChangedFor 대상 Command 이름 목록.
        /// </summary>
        public List<string> CommandNames { get; set; } = new List<string>();
    }
}
