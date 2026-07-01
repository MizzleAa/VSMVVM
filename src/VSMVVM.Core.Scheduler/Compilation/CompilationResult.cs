using System.Collections.Generic;
using System.Reflection;

namespace VSMVVM.Core.Scheduler.Compilation
{
    /// <summary>
    /// 사용자 코드 컴파일 결과. Success=true면 Assembly가 채워지고,
    /// false면 Diagnostics에 에러가 담깁니다 (Severity Error 1개 이상).
    /// </summary>
    public sealed class CompilationResult
    {
        public bool Success { get; }
        public Assembly Assembly { get; }
        public IReadOnlyList<CompilationDiagnostic> Diagnostics { get; }

        public CompilationResult(bool success, Assembly assembly, IReadOnlyList<CompilationDiagnostic> diagnostics)
        {
            Success = success;
            Assembly = assembly;
            Diagnostics = diagnostics ?? System.Array.Empty<CompilationDiagnostic>();
        }
    }
}
