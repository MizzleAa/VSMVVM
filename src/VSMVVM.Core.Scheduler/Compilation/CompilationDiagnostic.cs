namespace VSMVVM.Core.Scheduler.Compilation
{
    /// <summary>
    /// Roslyn 진단을 Roslyn에 의존하지 않는 POCO로 옮긴 형태.
    /// 에디터 인라인 표시(빨간 물결 밑줄 등)에 사용됩니다.
    /// 줄/열 번호는 1-based.
    /// </summary>
    public sealed class CompilationDiagnostic
    {
        public string Id { get; }
        public CompilationDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public int StartLine { get; }
        public int StartColumn { get; }
        public int EndLine { get; }
        public int EndColumn { get; }

        public CompilationDiagnostic(string id,
                                     CompilationDiagnosticSeverity severity,
                                     string message,
                                     int startLine, int startColumn,
                                     int endLine, int endColumn)
        {
            Id = id ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public override string ToString() =>
            $"{Severity} {Id} ({StartLine},{StartColumn})-({EndLine},{EndColumn}): {Message}";
    }
}
