using System;
using System.Collections.Generic;
using System.Reflection;

namespace VSMVVM.Core.Scheduler.Compilation
{
    /// <summary>
    /// 사용자 C# 코드 컴파일 옵션.
    /// 호스트 어셈블리는 기본적으로 자동 수집됩니다 (<see cref="AutoCollectHostReferences"/>).
    /// 추가 참조가 필요하면 <see cref="ExtraReferenceAssemblies"/> 또는 <see cref="ExtraReferencePaths"/>에 더하세요.
    /// </summary>
    public sealed class CompilationOptions
    {
        public string AssemblyName { get; set; } = "VsmvvmUserNode";

        /// <summary>using으로 자동 삽입할 네임스페이스 (선택).</summary>
        public IList<string> ImplicitUsings { get; } = new List<string>();

        /// <summary>호스트 AppDomain에 로드된 어셈블리들을 MetadataReference로 자동 추가.</summary>
        public bool AutoCollectHostReferences { get; set; } = true;

        /// <summary>AutoCollect 시 포함 필터. null이면 동적 어셈블리 제외 외에는 전부 포함.</summary>
        public Predicate<Assembly> HostAssemblyFilter { get; set; }

        /// <summary>추가 참조 어셈블리 (이미 로드된 인스턴스).</summary>
        public IList<Assembly> ExtraReferenceAssemblies { get; } = new List<Assembly>();

        /// <summary>추가 참조 어셈블리 (디스크 경로).</summary>
        public IList<string> ExtraReferencePaths { get; } = new List<string>();

        /// <summary>디버그 정보를 emit하여 사용자 코드 예외 스택 추적 품질 향상.</summary>
        public bool EmitDebugInformation { get; set; } = true;
    }
}
