using System.Collections.Generic;
using System.Reflection;

namespace VSMVVM.Core.Scheduler.Compilation
{
    /// <summary>
    /// 사용자 작성 C# 코드를 동적으로 컴파일하여 런타임에 노드로 사용 가능한 어셈블리로 emit하는 서비스.
    /// 기본 구현(<c>VSMVVM.Core.Scheduler.Scripting</c> 패키지의 <c>RoslynCompilationService</c>)은
    /// CollectibleAssemblyLoadContext로 어셈블리를 격리하여 <see cref="UnloadAssembly"/>로 메모리 회수가 가능합니다.
    /// </summary>
    public interface ICompilationService
    {
        /// <summary>전체 컴파일 + emit + 어셈블리 로드. 실패 시 Diagnostics에 원인.</summary>
        CompilationResult Compile(string sourceCode, CompilationOptions options);

        /// <summary>emit 없이 진단만 추출. 에디터 인라인 검증용 (빠름).</summary>
        IReadOnlyList<CompilationDiagnostic> Analyze(string sourceCode, CompilationOptions options);

        /// <summary>
        /// <see cref="Compile"/>로 로드한 어셈블리를 언로드합니다 (collectible ALC 회수).
        /// 알 수 없는 어셈블리거나 이미 해제됐으면 false 반환.
        /// </summary>
        bool UnloadAssembly(Assembly assembly);
    }
}
