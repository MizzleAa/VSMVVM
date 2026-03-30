using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Collections.Immutable;
using VSMVVM.Core.CodeGen.GenInfo;
using System.Linq;
using System.Collections.Generic;

namespace VSMVVM.Core.CodeGen
{
    /// <summary>
    /// [Property] 어트리뷰트가 적용된 필드에 대해 자동으로 public 프로퍼티를 생성하는 Source Generator.
    /// ConvMVVM2 대비: Undo/Redo 코드 제거, 병렬 처리, Warning-free 빌드.
    /// </summary>
    [Generator]
    internal sealed class PropertyCodeGen : IIncrementalGenerator
    {
        #region Constants

        private const string PropertyAttributeFullName = "VSMVVM.Core.Attributes.PropertyAttribute";
        private const string PropertyChangedForAttributeFullName = "VSMVVM.Core.Attributes.PropertyChangedForAttribute";
        private const string NotifyCanExecuteChangedForAttributeFullName = "VSMVVM.Core.Attributes.NotifyCanExecuteChangedForAttribute";

        #endregion

        #region Initialize

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsCandidateClass(s),
                    transform: static (ctx, _) => GetTargetClass(ctx))
                .Where(x => x != null);

            var compilationAndClasses = context.CompilationProvider
                .Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses,
                static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        #endregion

        #region Filter

        private static bool IsCandidateClass(SyntaxNode node)
        {
            if (!(node is ClassDeclarationSyntax classSyntax))
            {
                return false;
            }

            if (classSyntax.Members.Count == 0)
            {
                return false;
            }

            return classSyntax.Members.Any(m => m.AttributeLists.Count > 0);
        }

        private static ClassDeclarationSyntax GetTargetClass(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (var member in classDeclaration.Members)
            {
                foreach (var attributeList in member.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeSymbol)
                        {
                            var fullName = attributeSymbol.ContainingType.ToDisplayString();
                            if (fullName == PropertyAttributeFullName)
                            {
                                return classDeclaration;
                            }
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region Generator

        private static string GetNamespace(Compilation compilation, MemberDeclarationSyntax cls)
        {
            foreach (var ns in cls.Ancestors().OfType<NamespaceDeclarationSyntax>())
            {
                return ns.Name.ToString();
            }

            // File-scoped namespace 지원
            foreach (var ns in cls.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>())
            {
                return ns.Name.ToString();
            }

            return string.Empty;
        }

        private static List<AutoFieldInfo> GetFieldList(Compilation compilation, MemberDeclarationSyntax cls)
        {
            var fieldList = new List<AutoFieldInfo>();
            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (var field in cls.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                var hasPropertyAttribute = false;

                foreach (var attrList in field.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        if (model.GetSymbolInfo(attr).Symbol is IMethodSymbol attrSymbol)
                        {
                            if (attrSymbol.ContainingType.ToDisplayString() == PropertyAttributeFullName)
                            {
                                hasPropertyAttribute = true;
                                break;
                            }
                        }
                    }

                    if (hasPropertyAttribute)
                    {
                        break;
                    }
                }

                if (!hasPropertyAttribute)
                {
                    continue;
                }

                // PropertyChangedFor 대상 수집 + NotifyCanExecuteChangedFor 대상 수집 + 전달 어트리뷰트 수집
                var targetNames = new List<string>();
                var commandNames = new List<string>();
                var forwardedAttributes = new List<string>();

                foreach (var attrList in field.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        if (model.GetSymbolInfo(attr).Symbol is IMethodSymbol attrSymbol)
                        {
                            var attrFullName = attrSymbol.ContainingType.ToDisplayString();

                            if (attrFullName == PropertyChangedForAttributeFullName)
                            {
                                if (attr.ArgumentList != null)
                                {
                                    foreach (var arg in attr.ArgumentList.Arguments)
                                    {
                                        targetNames.Add(arg.ToString());
                                    }
                                }
                            }
                            else if (attrFullName == NotifyCanExecuteChangedForAttributeFullName)
                            {
                                if (attr.ArgumentList != null)
                                {
                                    foreach (var arg in attr.ArgumentList.Arguments)
                                    {
                                        var constValue = model.GetConstantValue(arg.Expression);
                                        if (constValue.HasValue && constValue.Value is string s)
                                        {
                                            commandNames.Add(s);
                                        }
                                        else
                                        {
                                            commandNames.Add(arg.Expression.ToString()
                                                .Replace("nameof(", "").TrimEnd(')'));
                                        }
                                    }
                                }
                            }
                            else if (attrFullName != PropertyAttributeFullName)
                            {
                                // Property, PropertyChangedFor, NotifyCanExecuteChangedFor 이외의 모든 어트리뷰트를 전달
                                forwardedAttributes.Add($"[{attr.ToFullString().Trim()}]");
                            }
                        }
                    }
                }

                foreach (var variable in field.Declaration.Variables)
                {
                    fieldList.Add(new AutoFieldInfo
                    {
                        Identifier = variable.Identifier.ValueText,
                        TypeName = field.Declaration.Type.ToString(),
                        TargetNames = targetNames,
                        ForwardedAttributes = forwardedAttributes,
                        CommandNames = commandNames
                    });
                }
            }

            return fieldList;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
            {
                return;
            }

            var distinctClasses = classes.Distinct();

            foreach (var cls in distinctClasses)
            {
                var usingDirectives = cls.SyntaxTree.GetCompilationUnitRoot().Usings;
                var usingText = usingDirectives.ToString();

                var clsNamespace = GetNamespace(compilation, cls);

                // partial 검증
                if (cls.Modifiers.Count == 0 || !cls.Modifiers.Any(t => t.ToString().Contains("partial")))
                {
                    ReportDiagnostic(context, "VSMVVM0001", "Class must be partial",
                        $"{clsNamespace}.{cls.Identifier.ValueText} class modifier must be partial");
                    continue;
                }

                // 기본 클래스 검증
                if (cls.BaseList == null || cls.BaseList.Types.Count == 0)
                {
                    ReportDiagnostic(context, "VSMVVM0002", "Class must inherit ViewModelBase",
                        $"{clsNamespace}.{cls.Identifier.ValueText} must inherit ViewModelBase or ObservableValidator");
                    continue;
                }

                // ObservableValidator 상속 여부 확인
                var isObservableValidator = cls.BaseList.Types
                    .Any(t => t.ToString().Contains("ObservableValidator"));

                var fieldList = GetFieldList(compilation, cls);
                if (fieldList.Count == 0)
                {
                    continue;
                }

                // 필드 네이밍 검증
                foreach (var field in fieldList)
                {
                    if (!field.Identifier.StartsWith("_"))
                    {
                        ReportDiagnostic(context, "VSMVVM0003", "Field must start with underscore",
                            $"{clsNamespace}.{cls.Identifier.ValueText}.{field.Identifier} must start with '_'");
                        continue;
                    }
                }

                // 코드 생성
                var propertyCode = new StringBuilder();

                foreach (var field in fieldList)
                {
                    var fieldName = field.Identifier.Substring(1);
                    var propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

                    // 전달 어트리뷰트 출력
                    foreach (var attr in field.ForwardedAttributes)
                    {
                        propertyCode.AppendLine($"        {attr}");
                    }

                    if (isObservableValidator)
                    {
                        // ObservableValidator: SetProperty 사용 (자동 DataAnnotation 검증)
                        propertyCode.AppendLine($@"
        public {field.TypeName} {propertyName}
        {{
            get => {field.Identifier};
            set => SetProperty(ref {field.Identifier}, value);
        }}
");
                    }
                    else
                    {
                    propertyCode.AppendLine($@"
        public {field.TypeName} {propertyName}
        {{
            get => {field.Identifier};
            set
            {{
                if (!EqualityComparer<{field.TypeName}>.Default.Equals({field.Identifier}, value))
                {{
                    {field.TypeName} oldValue = {field.Identifier};
                    On{propertyName}Changing(value);
                    On{propertyName}Changing(oldValue, value);
                    OnPropertyChanging();
                    {field.Identifier} = value;
                    On{propertyName}Changed(value);
                    On{propertyName}Changed(oldValue, value);
                    OnPropertyChanged();");

                    // PropertyChangedFor 대상
                    foreach (var targetName in field.TargetNames)
                    {
                        propertyCode.AppendLine($"                    OnPropertyChanged({targetName});");
                    }

                    // NotifyCanExecuteChangedFor 대상
                    foreach (var commandName in field.CommandNames)
                    {
                        propertyCode.AppendLine($"                    {commandName}?.RaiseCanExecuteChanged();");
                    }

                    propertyCode.AppendLine($@"                }}
            }}
        }}

        partial void On{propertyName}Changing({field.TypeName} value);
        partial void On{propertyName}Changing({field.TypeName} oldValue, {field.TypeName} newValue);
        partial void On{propertyName}Changed({field.TypeName} value);
        partial void On{propertyName}Changed({field.TypeName} oldValue, {field.TypeName} newValue);
");
                    } // end else (non-ObservableValidator)
                }

                var source = $@"// <auto-generated />
#nullable enable
{usingText}
using System.Collections.Generic;

namespace {clsNamespace}
{{
    partial class {cls.Identifier.ValueText}
    {{
{propertyCode}
    }}
}}
#nullable restore";

                context.AddSource(
                    $"{clsNamespace}.{cls.Identifier.ValueText}.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }

        private static void ReportDiagnostic(SourceProductionContext context, string id, string title, string message)
        {
            var descriptor = new DiagnosticDescriptor(id, title, message, "VSMVVM", DiagnosticSeverity.Error, true);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
        }

        #endregion
    }
}
