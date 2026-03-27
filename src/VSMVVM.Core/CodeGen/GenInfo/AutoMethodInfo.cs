namespace VSMVVM.Core.CodeGen.GenInfo
{
    /// <summary>
    /// Source Generator에서 사용하는 메서드 메타데이터 정보.
    /// </summary>
    internal sealed class AutoMethodInfo
    {
        /// <summary>
        /// 메서드 이름 (예: "Save").
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 반환 타입 (예: "void", "Task").
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// CanExecute 메서드 이름 (null이면 없음).
        /// </summary>
        public string CanExecuteName { get; set; }

        /// <summary>
        /// 비동기 메서드 여부 (Task 반환).
        /// </summary>
        public bool IsAsync { get; set; }

        /// <summary>
        /// 파라미터 타입 (있으면 제네릭 커맨드 사용).
        /// </summary>
        public string ParameterType { get; set; }
    }
}
