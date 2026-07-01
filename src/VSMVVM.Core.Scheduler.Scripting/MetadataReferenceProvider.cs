using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using VSMVVM.Core.Scheduler.Compilation;
using CompilationOptions = VSMVVM.Core.Scheduler.Compilation.CompilationOptions;

namespace VSMVVM.Core.Scheduler.Scripting
{
    /// <summary>
    /// CompilationOptions의 정책에 따라 Roslyn MetadataReference 목록을 빌드합니다.
    /// 기본 동작: 호스트 AppDomain에 로드된 모든 어셈블리 중 동적 어셈블리/Location 없는 어셈블리를 제외하고 자동 추가.
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        public static IReadOnlyList<MetadataReference> Build(CompilationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (options.AutoCollectHostReferences)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.IsDynamic) continue;
                    if (string.IsNullOrEmpty(asm.Location)) continue;
                    if (!File.Exists(asm.Location)) continue;
                    if (options.HostAssemblyFilter != null && !options.HostAssemblyFilter(asm)) continue;

                    TryAdd(refs, seen, asm.Location);
                }
            }

            foreach (var asm in options.ExtraReferenceAssemblies)
            {
                if (asm == null || asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                TryAdd(refs, seen, asm.Location);
            }

            foreach (var path in options.ExtraReferencePaths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                TryAdd(refs, seen, path);
            }

            return refs;
        }

        private static void TryAdd(List<MetadataReference> refs, HashSet<string> seen, string path)
        {
            if (!seen.Add(path)) return;
            try
            {
                refs.Add(MetadataReference.CreateFromFile(path));
            }
            catch
            {
                // 깨진 dll/리소스 어셈블리 등은 무시 (사용자 코드 컴파일을 막지 않음).
            }
        }
    }
}
