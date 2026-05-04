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
    /// [RelayCommand] 어트리뷰트가 적용된 메서드에 대해 자동으로 RelayCommand 프로퍼티를 생성하는 Source Generator.
    /// CanExecute 지원.
    /// </summary>
    [Generator]
    internal sealed class RelayCommandCodeGen : IIncrementalGenerator
    {
        #region Constants

        private const string RelayCommandAttributeFullName = "VSMVVM.Core.Attributes.RelayCommandAttribute";

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

            return classSyntax.Members.Any(m => m.AttributeLists.Count > 0);
        }

        private static ClassDeclarationSyntax GetTargetClass(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (var member in classDeclaration.Members)
            {
                foreach (var attrList in member.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        if (context.SemanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol attrSymbol)
                        {
                            if (attrSymbol.ContainingType.ToDisplayString() == RelayCommandAttributeFullName)
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

            foreach (var ns in cls.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>())
            {
                return ns.Name.ToString();
            }

            return string.Empty;
        }

        private static List<AutoMethodInfo> GetMethodList(Compilation compilation, MemberDeclarationSyntax cls, SourceProductionContext context, string clsNamespace, string clsName)
        {
            var methodList = new List<AutoMethodInfo>();
            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (var method in cls.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                string canExecuteName = null;
                var hasAttribute = false;

                foreach (var attrList in method.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        if (model.GetSymbolInfo(attr).Symbol is IMethodSymbol attrSymbol)
                        {
                            if (attrSymbol.ContainingType.ToDisplayString() == RelayCommandAttributeFullName)
                            {
                                hasAttribute = true;

                                // CanExecute 프로퍼티 추출
                                if (attr.ArgumentList != null)
                                {
                                    foreach (var arg in attr.ArgumentList.Arguments)
                                    {
                                        if (arg.NameEquals != null && arg.NameEquals.Name.ToString() == "CanExecute")
                                        {
                                            var constValue = model.GetConstantValue(arg.Expression);
                                            if (constValue.HasValue && constValue.Value is string s)
                                            {
                                                canExecuteName = s;
                                            }
                                            else
                                            {
                                                canExecuteName = arg.Expression.ToString()
                                                    .Replace("nameof(", "").TrimEnd(')');
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        }
                    }

                    if (hasAttribute)
                    {
                        break;
                    }
                }

                if (!hasAttribute)
                {
                    continue;
                }

                // [RelayCommand]는 void 반환만 허용. Action으로 wrap하므로 비-void는 컴파일 에러를 유발한다.
                var returnType = method.ReturnType.ToString();
                if (returnType != "void")
                {
                    ReportDiagnostic(context, "VSMVVM0010", "[RelayCommand] method must return void",
                        $"{clsNamespace}.{clsName}.{method.Identifier.ValueText} must return void to be wrapped as RelayCommand. Use [AsyncRelayCommand] for Task-returning methods.");
                    continue;
                }

                // 파라미터 타입 확인 + ref/out/in 한정자 금지 (Action<T>와 시그니처 매칭 안 됨)
                string parameterType = null;
                if (method.ParameterList.Parameters.Count > 1)
                {
                    ReportDiagnostic(context, "VSMVVM0011", "[RelayCommand] method must have 0 or 1 parameter",
                        $"{clsNamespace}.{clsName}.{method.Identifier.ValueText} has {method.ParameterList.Parameters.Count} parameters; RelayCommand supports only 0 or 1.");
                    continue;
                }
                if (method.ParameterList.Parameters.Count == 1)
                {
                    var p = method.ParameterList.Parameters[0];
                    if (p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword) || m.IsKind(SyntaxKind.InKeyword)))
                    {
                        ReportDiagnostic(context, "VSMVVM0012", "[RelayCommand] parameter must not be ref/out/in",
                            $"{clsNamespace}.{clsName}.{method.Identifier.ValueText} parameter '{p.Identifier.ValueText}' must not have ref/out/in modifier.");
                        continue;
                    }
                    parameterType = p.Type?.ToString();
                }

                // 같은 메서드 이름이 두 번 이상 나오면 backing field 충돌. overload는 지원하지 않는다.
                if (methodList.Any(m => m.MethodName == method.Identifier.ValueText))
                {
                    ReportDiagnostic(context, "VSMVVM0013", "[RelayCommand] does not support method overloads",
                        $"{clsNamespace}.{clsName} has multiple [RelayCommand] methods named '{method.Identifier.ValueText}'. Rename one to avoid backing-field conflict.");
                    continue;
                }

                methodList.Add(new AutoMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    ReturnType = returnType,
                    CanExecuteName = canExecuteName,
                    IsAsync = false,
                    ParameterType = parameterType
                });
            }

            return methodList;
        }

        private static void ReportDiagnostic(SourceProductionContext context, string id, string title, string message)
        {
            var descriptor = new DiagnosticDescriptor(id, title, message, "VSMVVM", DiagnosticSeverity.Error, true);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None));
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
                var methodList = GetMethodList(compilation, cls, context, clsNamespace, cls.Identifier.ValueText);

                if (methodList.Count == 0)
                {
                    continue;
                }

                var commandCode = new StringBuilder();

                foreach (var method in methodList)
                {
                    var commandName = method.MethodName + "Command";
                    var backingField = "_" + char.ToLower(commandName[0]) + commandName.Substring(1);

                    if (method.ParameterType != null)
                    {
                        // 제네릭 커맨드
                        var canExecutePart = method.CanExecuteName != null
                            ? $", {method.CanExecuteName}"
                            : string.Empty;

                        commandCode.AppendLine($@"
        private VSMVVM.Core.MVVM.RelayCommand<{method.ParameterType}>? {backingField};
        public VSMVVM.Core.MVVM.RelayCommand<{method.ParameterType}> {commandName} => {backingField} ?? ({backingField} = new VSMVVM.Core.MVVM.RelayCommand<{method.ParameterType}>({method.MethodName}{canExecutePart}));");
                    }
                    else
                    {
                        // 비제네릭 커맨드
                        var canExecutePart = method.CanExecuteName != null
                            ? $", {method.CanExecuteName}"
                            : string.Empty;

                        commandCode.AppendLine($@"
        private VSMVVM.Core.MVVM.RelayCommand? {backingField};
        public VSMVVM.Core.MVVM.RelayCommand {commandName} => {backingField} ?? ({backingField} = new VSMVVM.Core.MVVM.RelayCommand({method.MethodName}{canExecutePart}));");
                    }
                }

                // Nested 클래스 + generic type params + constraints까지 정확히 감싼다.
                var wrappedClassBody = WrapInClassNesting(cls, commandCode.ToString());

                var source = $@"// <auto-generated />
#nullable enable
{usingText}

namespace {clsNamespace}
{{
{wrappedClassBody}
}}
#nullable restore";

                // 같은 partial class가 여러 파일로 분할돼도 hint name 충돌 없도록 파일 식별자 추가.
                var fileHint = SanitizeFileHint(cls.SyntaxTree.FilePath);
                context.AddSource(
                    $"{clsNamespace}.{GetFullClassPath(cls)}__{fileHint}.RelayCommand.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }

        private static string WrapInClassNesting(ClassDeclarationSyntax cls, string innerBody)
        {
            var chain = cls.Ancestors().OfType<ClassDeclarationSyntax>().Reverse().ToList();
            chain.Add(cls);

            var sb = new StringBuilder();
            string indent = "    ";
            for (int i = 0; i < chain.Count; i++)
            {
                var c = chain[i];
                var typeParams = c.TypeParameterList?.ToString() ?? string.Empty;
                var constraints = c.ConstraintClauses.Count > 0
                    ? " " + string.Join(" ", c.ConstraintClauses.Select(cc => cc.ToString()))
                    : string.Empty;
                sb.Append(indent).Append("partial class ").Append(c.Identifier.ValueText)
                  .Append(typeParams).Append(constraints).AppendLine();
                sb.Append(indent).AppendLine("{");
                indent += "    ";
            }

            sb.AppendLine(innerBody);

            for (int i = 0; i < chain.Count; i++)
            {
                indent = indent.Substring(4);
                sb.Append(indent).AppendLine("}");
            }

            return sb.ToString();
        }

        private static string GetFullClassPath(ClassDeclarationSyntax cls)
        {
            var chain = cls.Ancestors().OfType<ClassDeclarationSyntax>().Reverse().ToList();
            chain.Add(cls);
            return string.Join(".", chain.Select(c => c.Identifier.ValueText));
        }

        private static string SanitizeFileHint(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "_";
            var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            }
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        #endregion
    }
}
