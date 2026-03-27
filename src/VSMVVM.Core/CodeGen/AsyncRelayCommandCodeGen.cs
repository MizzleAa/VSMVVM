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
    /// [AsyncRelayCommand] 어트리뷰트가 적용된 비동기 메서드에 대해 AsyncRelayCommand 프로퍼티를 생성하는 Source Generator.
    /// IsRunning + CanExecute 지원.
    /// </summary>
    [Generator]
    internal sealed class AsyncRelayCommandCodeGen : IIncrementalGenerator
    {
        #region Constants

        private const string AsyncRelayCommandAttributeFullName = "VSMVVM.Core.Attributes.AsyncRelayCommandAttribute";

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
                            if (attrSymbol.ContainingType.ToDisplayString() == AsyncRelayCommandAttributeFullName)
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

        private static List<AutoMethodInfo> GetMethodList(Compilation compilation, MemberDeclarationSyntax cls)
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
                            if (attrSymbol.ContainingType.ToDisplayString() == AsyncRelayCommandAttributeFullName)
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

                // 파라미터 타입 확인
                string parameterType = null;
                if (method.ParameterList.Parameters.Count == 1)
                {
                    parameterType = method.ParameterList.Parameters[0].Type?.ToString();
                }

                methodList.Add(new AutoMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    ReturnType = method.ReturnType.ToString(),
                    CanExecuteName = canExecuteName,
                    IsAsync = true,
                    ParameterType = parameterType
                });
            }

            return methodList;
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
                var methodList = GetMethodList(compilation, cls);

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
                        // 제네릭 비동기 커맨드
                        var canExecutePart = method.CanExecuteName != null
                            ? $", {method.CanExecuteName}"
                            : string.Empty;

                        commandCode.AppendLine($@"
        private VSMVVM.Core.MVVM.AsyncRelayCommand<{method.ParameterType}> {backingField};
        public VSMVVM.Core.MVVM.AsyncRelayCommand<{method.ParameterType}> {commandName} => {backingField} ?? ({backingField} = new VSMVVM.Core.MVVM.AsyncRelayCommand<{method.ParameterType}>({method.MethodName}{canExecutePart}));");
                    }
                    else
                    {
                        // 비제네릭 비동기 커맨드
                        var canExecutePart = method.CanExecuteName != null
                            ? $", {method.CanExecuteName}"
                            : string.Empty;

                        commandCode.AppendLine($@"
        private VSMVVM.Core.MVVM.AsyncRelayCommand {backingField};
        public VSMVVM.Core.MVVM.AsyncRelayCommand {commandName} => {backingField} ?? ({backingField} = new VSMVVM.Core.MVVM.AsyncRelayCommand({method.MethodName}{canExecutePart}));");
                    }
                }

                var source = $@"{usingText}

namespace {clsNamespace}
{{
    partial class {cls.Identifier.ValueText}
    {{
{commandCode}
    }}
}}";

                context.AddSource(
                    $"{clsNamespace}.{cls.Identifier.ValueText}.AsyncRelayCommand.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }

        #endregion
    }
}
