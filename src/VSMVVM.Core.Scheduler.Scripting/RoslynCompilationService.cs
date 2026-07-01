using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using VSMVVM.Core.Scheduler.Compilation;
using CompilationOptions = VSMVVM.Core.Scheduler.Compilation.CompilationOptions;

namespace VSMVVM.Core.Scheduler.Scripting
{
    /// <summary>
    /// Roslyn 기반 <see cref="ICompilationService"/> 구현.
    /// 각 컴파일은 자체 <see cref="CollectibleAssemblyLoadContext"/>에 로드되어 <see cref="UnloadAssembly"/>로 회수 가능합니다.
    /// </summary>
    public sealed class RoslynCompilationService : ICompilationService
    {
        // 로드된 어셈블리 → 소속 ALC. UnloadAssembly에서 ALC.Unload() 호출에 사용.
        private readonly ConcurrentDictionary<Assembly, CollectibleAssemblyLoadContext> _loaded
            = new ConcurrentDictionary<Assembly, CollectibleAssemblyLoadContext>();

        public CompilationResult Compile(string sourceCode, CompilationOptions options)
        {
            if (sourceCode == null) throw new ArgumentNullException(nameof(sourceCode));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var compilation = CreateCompilation(sourceCode, options);

            using var peStream = new MemoryStream();
            using var pdbStream = options.EmitDebugInformation ? new MemoryStream() : null;

            var emitOptions = options.EmitDebugInformation
                ? new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb)
                : null;

            EmitResult emitResult = compilation.Emit(peStream, pdbStream, options: emitOptions);
            var diagnostics = MapDiagnostics(emitResult.Diagnostics);

            if (!emitResult.Success)
            {
                return new CompilationResult(false, null, diagnostics);
            }

            peStream.Position = 0;
            pdbStream?.Seek(0, SeekOrigin.Begin);

            var alc = new CollectibleAssemblyLoadContext($"Vsmvvm_{options.AssemblyName}_{Guid.NewGuid():N}");
            Assembly loaded;
            try
            {
                loaded = pdbStream != null
                    ? alc.LoadFromStream(peStream, pdbStream)
                    : alc.LoadFromStream(peStream);
            }
            catch
            {
                alc.Unload();
                throw;
            }

            _loaded[loaded] = alc;
            return new CompilationResult(true, loaded, diagnostics);
        }

        public IReadOnlyList<CompilationDiagnostic> Analyze(string sourceCode, CompilationOptions options)
        {
            if (sourceCode == null) throw new ArgumentNullException(nameof(sourceCode));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var compilation = CreateCompilation(sourceCode, options);
            return MapDiagnostics(compilation.GetDiagnostics());
        }

        public bool UnloadAssembly(Assembly assembly)
        {
            if (assembly == null) return false;
            if (!_loaded.TryRemove(assembly, out var alc)) return false;
            alc.Unload();
            return true;
        }

        private static CSharpCompilation CreateCompilation(string sourceCode, CompilationOptions options)
        {
            // 묵시적 using 적용 — 사용자가 매번 반복 작성하지 않도록.
            var prefix = "";
            foreach (var ns in options.ImplicitUsings)
            {
                if (!string.IsNullOrWhiteSpace(ns))
                {
                    prefix += $"using {ns};\n";
                }
            }
            var fullSource = prefix + sourceCode;

            var syntaxTree = CSharpSyntaxTree.ParseText(fullSource);
            var references = MetadataReferenceProvider.Build(options);

            return CSharpCompilation.Create(
                assemblyName: options.AssemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug));
        }

        private static IReadOnlyList<CompilationDiagnostic> MapDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics
                .Where(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden ||
                            d.IsWarningAsError)
                .Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    var start = span.StartLinePosition;
                    var end = span.EndLinePosition;
                    return new CompilationDiagnostic(
                        d.Id,
                        MapSeverity(d.Severity),
                        d.GetMessage(),
                        start.Line + 1, start.Character + 1,
                        end.Line + 1, end.Character + 1);
                })
                .ToArray();
        }

        private static CompilationDiagnosticSeverity MapSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity sev) =>
            sev switch
            {
                Microsoft.CodeAnalysis.DiagnosticSeverity.Error => CompilationDiagnosticSeverity.Error,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => CompilationDiagnosticSeverity.Warning,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Info => CompilationDiagnosticSeverity.Info,
                _ => CompilationDiagnosticSeverity.Hidden,
            };
    }
}
