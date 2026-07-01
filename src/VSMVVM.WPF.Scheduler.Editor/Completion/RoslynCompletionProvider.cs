using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;

namespace VSMVVM.WPF.Scheduler.Editor.Completion
{
    /// <summary>
    /// AdhocWorkspace 기반 가벼운 Roslyn 자동완성 백엔드.
    /// CompletionService는 Workspaces/Features 패키지에서 노출되며 문서 단위로 작동한다.
    /// 본 클래스는 한 개의 C# 문서를 갖는 단일 프로젝트를 보관하고,
    /// GetCompletionsAsync 호출 시 최신 텍스트로 SourceText를 갱신한 뒤 결과를 단순 POCO로 매핑.
    /// </summary>
    public sealed class RoslynCompletionProvider : IDisposable
    {
        // MetadataReference 수집은 100여 개 DLL을 OpenRead/MetadataReader 로 여는 비용이 크므로
        // 프로세스 전체에서 1회만 수행 후 캐시. AppDomain 어셈블리 목록은 런타임에 거의 변하지 않음.
        private static readonly Lazy<IReadOnlyList<MetadataReference>> SharedReferences =
            new Lazy<IReadOnlyList<MetadataReference>>(CollectMetadataReferences, isThreadSafe: true);

        // 프로세스 전체에서 1회만 수행되는 Roslyn 워밍업 — 다중 인스턴스/테스트가 안전하게 공유.
        private static Task _sharedWarmupTask;
        private static readonly object _warmupLock = new object();

        private readonly AdhocWorkspace _workspace;
        private DocumentId _documentId;
        private bool _disposed;

        public RoslynCompletionProvider()
        {
            _workspace = new AdhocWorkspace();
            CreateScratchDocument(string.Empty);
            EnsureWarmupStarted();
        }

        /// <summary>워밍업 task — 인수 테스트가 "첫 호출 콜드 스타트 vs 후속 호출" 비교에 사용.</summary>
        internal Task WarmupTask => _sharedWarmupTask ?? Task.CompletedTask;

        private void EnsureWarmupStarted()
        {
            if (_sharedWarmupTask != null) return;
            lock (_warmupLock)
            {
                if (_sharedWarmupTask != null) return;
                _sharedWarmupTask = Task.Run(async () =>
                {
                    try
                    {
                        // WPF 호스트 컴포넌트의 BAML 로드(Application.LoadComponent)가 끝날 때까지 잠시 양보 —
                        // System.IO.Packaging 정적 상태가 비-thread-safe하므로 충돌 회피.
                        await Task.Delay(150).ConfigureAwait(false);
                        // 첫 GetCompletionsAsync 호출은 모든 MetadataReference 메타데이터를 인덱싱하므로 1~3초 걸린다.
                        // 백그라운드에서 미리 1회 수행해두면 사용자 첫 입력 지연이 사라진다.
                        await GetCompletionsAsync("class __VsmvvmWarmup {}", 23, CompletionTriggerKind.Invoke).ConfigureAwait(false);
                    }
                    catch { /* 워밍업 실패는 무시 — 사용자 첫 호출이 직접 처리 */ }
                });
            }
        }

        /// <summary>위치(0-based 문자 오프셋) 기준으로 자동완성 후보를 반환. trigger는 사용자 키 입력 또는 명시 호출.
        /// <paramref name="cancellationToken"/>으로 호출자가 후속 키 입력 시 진행 중인 작업을 취소할 수 있다.</summary>
        public async Task<IReadOnlyList<RoslynCompletionItem>> GetCompletionsAsync(string sourceCode, int position,
            CompletionTriggerKind triggerKind = CompletionTriggerKind.Invoke, char triggerChar = '\0',
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (_disposed) return Array.Empty<RoslynCompletionItem>();
            if (sourceCode == null) sourceCode = string.Empty;
            if (position < 0) position = 0;
            if (position > sourceCode.Length) position = sourceCode.Length;

            // 문서 텍스트 갱신
            var solution = _workspace.CurrentSolution;
            var doc = solution.GetDocument(_documentId);
            if (doc == null) return Array.Empty<RoslynCompletionItem>();

            doc = doc.WithText(SourceText.From(sourceCode));
            if (!_workspace.TryApplyChanges(doc.Project.Solution))
            {
                // workspace apply 실패 시 fresh document로 폴백
                CreateScratchDocument(sourceCode);
                doc = _workspace.CurrentSolution.GetDocument(_documentId);
                if (doc == null) return Array.Empty<RoslynCompletionItem>();
            }
            else
            {
                doc = _workspace.CurrentSolution.GetDocument(_documentId);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var completionService = CompletionService.GetService(doc);
            if (completionService == null) return Array.Empty<RoslynCompletionItem>();

            var trigger = triggerKind switch
            {
                CompletionTriggerKind.Insertion when triggerChar != '\0' =>
                    CompletionTrigger.CreateInsertionTrigger(triggerChar),
                CompletionTriggerKind.Deletion when triggerChar != '\0' =>
                    CompletionTrigger.CreateDeletionTrigger(triggerChar),
                _ => CompletionTrigger.Invoke,
            };

            var completionList = await completionService
                .GetCompletionsAsync(doc, position, trigger, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (completionList == null || completionList.ItemsList == null || completionList.ItemsList.Count == 0)
            {
                return Array.Empty<RoslynCompletionItem>();
            }

            var items = new List<RoslynCompletionItem>(completionList.ItemsList.Count);
            foreach (var ci in completionList.ItemsList)
            {
                var kind = InferKind(ci);
                items.Add(new RoslynCompletionItem(
                    displayText: ci.DisplayText,
                    insertionText: ci.DisplayText,
                    description: ci.InlineDescription ?? string.Empty,
                    sortText: ci.SortText,
                    kind: kind));
            }
            return items;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _workspace.Dispose();
        }

        private void CreateScratchDocument(string source)
        {
            var projectId = ProjectId.CreateNewId(debugName: "VsmvvmEditorScratch");
            var versionStamp = VersionStamp.Create();
            // 공유 Lazy 캐시 — 모든 RoslynCompletionProvider 인스턴스가 같은 MetadataReference 목록을 공유.
            var references = SharedReferences.Value;

            var projectInfo = ProjectInfo.Create(projectId, versionStamp,
                name: "VsmvvmEditorScratch",
                assemblyName: "VsmvvmEditorScratch",
                language: LanguageNames.CSharp,
                metadataReferences: references)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var solution = _workspace.CurrentSolution;
            if (_documentId != null && solution.GetDocument(_documentId) != null)
            {
                solution = solution.RemoveProject(_documentId.ProjectId);
            }

            solution = solution.AddProject(projectInfo);
            _documentId = DocumentId.CreateNewId(projectId, debugName: "Editor.cs");
            solution = solution.AddDocument(_documentId, "Editor.cs", SourceText.From(source ?? string.Empty));
            _workspace.TryApplyChanges(solution);
        }

        private static IReadOnlyList<MetadataReference> CollectMetadataReferences()
        {
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                if (string.IsNullOrEmpty(asm.Location)) continue;
                if (!File.Exists(asm.Location)) continue;
                if (!seen.Add(asm.Location)) continue;
                try { refs.Add(MetadataReference.CreateFromFile(asm.Location)); }
                catch { /* 깨진 dll은 무시 */ }
            }
            return refs;
        }

        private static CompletionItemKind InferKind(CompletionItem item)
        {
            // Roslyn은 Tags 컬렉션으로 항목 종류를 노출 (WellKnownTags 상수)
            foreach (var tag in item.Tags)
            {
                switch (tag)
                {
                    case "Keyword":   return CompletionItemKind.Keyword;
                    case "Namespace": return CompletionItemKind.Namespace;
                    case "Class":     return CompletionItemKind.Class;
                    case "Struct":    return CompletionItemKind.Struct;
                    case "Interface": return CompletionItemKind.Interface;
                    case "Enum":      return CompletionItemKind.Enum;
                    case "Delegate":  return CompletionItemKind.Class;
                    case "Method":    return CompletionItemKind.Method;
                    case "ExtensionMethod": return CompletionItemKind.Method;
                    case "Property":  return CompletionItemKind.Property;
                    case "Field":     return CompletionItemKind.Field;
                    case "Constant":  return CompletionItemKind.Field;
                    case "EnumMember": return CompletionItemKind.Field;
                    case "Event":     return CompletionItemKind.Event;
                    case "Local":     return CompletionItemKind.Variable;
                    case "Parameter": return CompletionItemKind.Variable;
                    case "TypeParameter": return CompletionItemKind.Variable;
                    case "Snippet":   return CompletionItemKind.Snippet;
                }
            }
            return CompletionItemKind.Text;
        }
    }

    /// <summary>Roslyn에 의존하지 않는 단순 POCO — 외부 UI(AvalonEdit CompletionWindow)에서 소비.</summary>
    public sealed class RoslynCompletionItem
    {
        public string DisplayText { get; }
        public string InsertionText { get; }
        public string Description { get; }
        public string SortText { get; }
        public CompletionItemKind Kind { get; }

        public RoslynCompletionItem(string displayText, string insertionText, string description, string sortText, CompletionItemKind kind)
        {
            DisplayText = displayText ?? string.Empty;
            InsertionText = string.IsNullOrEmpty(insertionText) ? DisplayText : insertionText;
            Description = description ?? string.Empty;
            SortText = sortText ?? DisplayText;
            Kind = kind;
        }
    }

    public enum CompletionItemKind
    {
        Text,
        Keyword,
        Namespace,
        Class,
        Struct,
        Interface,
        Enum,
        Method,
        Property,
        Field,
        Event,
        Variable,
        Snippet,
    }

    /// <summary>완성 트리거 종류. Roslyn의 동명 분류를 단순화.</summary>
    public enum CompletionTriggerKind
    {
        /// <summary>Ctrl+Space 같은 명시 호출.</summary>
        Invoke,
        /// <summary>문자 입력에 의한 자동 트리거 (예: '.', '(', '<').</summary>
        Insertion,
        /// <summary>문자 삭제 시 트리거.</summary>
        Deletion,
    }
}
