namespace VSMVVM.Core.Scheduler.Compilation
{
    /// <summary>컴파일 진단 심각도. Roslyn의 DiagnosticSeverity와 동일 의미지만 Core가 Roslyn에 의존하지 않도록 별도 enum.</summary>
    public enum CompilationDiagnosticSeverity
    {
        Hidden,
        Info,
        Warning,
        Error,
    }
}
