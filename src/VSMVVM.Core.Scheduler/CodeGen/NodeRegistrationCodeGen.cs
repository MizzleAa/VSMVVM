using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VSMVVM.Core.Scheduler.CodeGen.GenInfo;
using Compilation = Microsoft.CodeAnalysis.Compilation;

namespace VSMVVM.Core.Scheduler.CodeGen
{
    /// <summary>
    /// [Node] 부착 클래스에 대해:
    ///   1) 노드별 partial을 emit (override GetPinDescriptors / TypeId).
    ///   2) 어셈블리당 1개의 __VsmvvmNodeRegistration.g.cs 를 emit하여 [ModuleInitializer]로
    ///      NodeMetadataRegistry.Register(...) 호출.
    ///   3) netstandard2.0 호환을 위해 ModuleInitializerAttribute internal 폴리필도 함께 emit.
    /// </summary>
    [Generator]
    internal sealed class NodeRegistrationCodeGen : IIncrementalGenerator
    {
        private const string NodeAttributeFullName = "VSMVVM.Core.Scheduler.Attributes.NodeAttribute";
        private const string ExecInputPinAttributeFullName = "VSMVVM.Core.Scheduler.Attributes.ExecInputPinAttribute";
        private const string ExecOutputPinAttributeFullName = "VSMVVM.Core.Scheduler.Attributes.ExecOutputPinAttribute";
        private const string InputPinAttributeFullName = "VSMVVM.Core.Scheduler.Attributes.InputPinAttribute";
        private const string OutputPinAttributeFullName = "VSMVVM.Core.Scheduler.Attributes.OutputPinAttribute";

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

        private static bool IsCandidateClass(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0;
        }

        private static ClassDeclarationSyntax GetTargetClass(GeneratorSyntaxContext context)
        {
            var cls = (ClassDeclarationSyntax)context.Node;
            foreach (var attrList in cls.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol sym &&
                        sym.ContainingType.ToDisplayString() == NodeAttributeFullName)
                    {
                        return cls;
                    }
                }
            }
            return null;
        }

        private static void Execute(Microsoft.CodeAnalysis.Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes,
                                    SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty) return;

            var collected = new List<NodeInfo>();

            foreach (var cls in classes.Distinct())
            {
                var info = ExtractNodeInfo(compilation, cls, context);
                if (info != null)
                {
                    collected.Add(info);
                    EmitPartialClass(cls, info, context);
                }
            }

            if (collected.Count > 0)
            {
                EmitAssemblyRegistration(compilation, collected, context);
                EmitModuleInitializerPolyfill(context);
            }
        }

        #region Extraction

        private static NodeInfo ExtractNodeInfo(Microsoft.CodeAnalysis.Compilation compilation, ClassDeclarationSyntax cls,
                                                SourceProductionContext context)
        {
            var model = compilation.GetSemanticModel(cls.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(cls) as INamedTypeSymbol;
            if (classSymbol == null) return null;

            // partial 검증
            if (!cls.Modifiers.Any(m => m.ValueText == "partial"))
            {
                ReportDiagnostic(context, cls.Identifier.GetLocation(),
                    "VSMVVMS0001", "Node class must be partial",
                    $"Class '{classSymbol.ToDisplayString()}' with [Node] must be declared partial.");
                return null;
            }

            // NodeBase 상속 검증 (transitively)
            if (!InheritsFromNodeBase(classSymbol))
            {
                ReportDiagnostic(context, cls.Identifier.GetLocation(),
                    "VSMVVMS0002", "Node class must inherit NodeBase",
                    $"Class '{classSymbol.ToDisplayString()}' with [Node] must inherit VSMVVM.Core.Scheduler.Nodes.NodeBase.");
                return null;
            }

            // [Node] 인자 파싱
            AttributeData nodeAttrData = null;
            foreach (var ad in classSymbol.GetAttributes())
            {
                if (ad.AttributeClass?.ToDisplayString() == NodeAttributeFullName)
                {
                    nodeAttrData = ad;
                    break;
                }
            }
            if (nodeAttrData == null) return null;

            string typeId = null;
            if (nodeAttrData.ConstructorArguments.Length > 0)
            {
                typeId = nodeAttrData.ConstructorArguments[0].Value as string;
            }
            if (string.IsNullOrEmpty(typeId))
            {
                ReportDiagnostic(context, cls.Identifier.GetLocation(),
                    "VSMVVMS0003", "[Node] requires a non-empty Id",
                    $"[Node] on '{classSymbol.ToDisplayString()}' must specify a non-empty Id.");
                return null;
            }

            var info = new NodeInfo
            {
                Namespace = classSymbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : classSymbol.ContainingNamespace.ToDisplayString(),
                ClassName = classSymbol.Name,
                FullClassName = classSymbol.ToDisplayString(),
                TypeId = typeId,
                DisplayName = typeId,
                Category = string.Empty,
                Description = string.Empty,
                TimeoutMs = 0,
            };

            foreach (var named in nodeAttrData.NamedArguments)
            {
                switch (named.Key)
                {
                    case "DisplayName":
                        info.DisplayName = named.Value.Value as string ?? info.DisplayName;
                        break;
                    case "Category":
                        info.Category = named.Value.Value as string ?? string.Empty;
                        break;
                    case "Description":
                        info.Description = named.Value.Value as string ?? string.Empty;
                        break;
                    case "TimeoutMs":
                        if (named.Value.Value is int ms) info.TimeoutMs = ms;
                        break;
                }
            }

            // 핀 멤버 수집 (속성 + 필드 — 베이스 클래스의 핀도 포함).
            CollectPinsFromHierarchy(classSymbol, info, context);

            return info;
        }

        private static bool InheritsFromNodeBase(INamedTypeSymbol symbol)
        {
            const string baseFullName = "VSMVVM.Core.Scheduler.Nodes.NodeBase";
            var current = symbol.BaseType;
            while (current != null)
            {
                if (current.ToDisplayString() == baseFullName) return true;
                current = current.BaseType;
            }
            return false;
        }

        private static void CollectPinsFromHierarchy(INamedTypeSymbol classSymbol, NodeInfo info,
                                                     SourceProductionContext context)
        {
            // 베이스 → 파생 순으로 핀 누적 (베이스가 먼저).
            var chain = new List<INamedTypeSymbol>();
            var cur = classSymbol;
            while (cur != null && cur.ToDisplayString() != "VSMVVM.Core.Scheduler.Nodes.NodeBase" &&
                   cur.ToDisplayString() != "object")
            {
                chain.Add(cur);
                cur = cur.BaseType;
            }
            chain.Reverse();

            foreach (var type in chain)
            {
                foreach (var member in type.GetMembers())
                {
                    if (member is IPropertySymbol || member is IFieldSymbol)
                    {
                        var pin = TryExtractPin(member);
                        if (pin != null) info.Pins.Add(pin);
                    }
                }
            }
        }

        private static NodePinInfo TryExtractPin(ISymbol member)
        {
            AttributeData chosen = null;
            string chosenAttrName = null;
            foreach (var ad in member.GetAttributes())
            {
                var name = ad.AttributeClass?.ToDisplayString();
                if (name == ExecInputPinAttributeFullName ||
                    name == ExecOutputPinAttributeFullName ||
                    name == InputPinAttributeFullName ||
                    name == OutputPinAttributeFullName)
                {
                    chosen = ad;
                    chosenAttrName = name;
                    break;
                }
            }
            if (chosen == null) return null;

            var pin = new NodePinInfo
            {
                Id = member.Name,
                DisplayName = member.Name,
                DefaultValueLiteral = "null",
            };

            switch (chosenAttrName)
            {
                case ExecInputPinAttributeFullName:
                    pin.Kind = PinKindGen.Exec;
                    pin.Direction = PinDirectionGen.Input;
                    pin.ValueTypeName = "void";
                    break;
                case ExecOutputPinAttributeFullName:
                    pin.Kind = PinKindGen.Exec;
                    pin.Direction = PinDirectionGen.Output;
                    pin.ValueTypeName = "void";
                    break;
                case InputPinAttributeFullName:
                    pin.Kind = PinKindGen.Data;
                    pin.Direction = PinDirectionGen.Input;
                    pin.ValueTypeName = GetMemberTypeFullName(member);
                    break;
                case OutputPinAttributeFullName:
                    pin.Kind = PinKindGen.Data;
                    pin.Direction = PinDirectionGen.Output;
                    pin.ValueTypeName = GetMemberTypeFullName(member);
                    break;
            }

            foreach (var named in chosen.NamedArguments)
            {
                if (named.Key == "DisplayName" && named.Value.Value is string dn && dn.Length > 0)
                {
                    pin.DisplayName = dn;
                }
                else if (named.Key == "DefaultValue" && named.Value.Value != null)
                {
                    pin.DefaultValueLiteral = ToCSharpLiteral(named.Value.Value);
                }
            }

            return pin;
        }

        private static string GetMemberTypeFullName(ISymbol member)
        {
            ITypeSymbol type = member is IPropertySymbol p ? p.Type :
                               member is IFieldSymbol f ? f.Type : null;
            if (type == null) return "object";
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string ToCSharpLiteral(object value)
        {
            switch (value)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case char c: return "'" + c + "'";
                case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture) + "d";
                case float f: return f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f";
                case decimal m: return m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
                case long l: return l.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L";
                default: return value.ToString();
            }
        }

        #endregion

        #region Emission

        private static void EmitPartialClass(ClassDeclarationSyntax cls, NodeInfo info,
                                             SourceProductionContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable disable");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using VSMVVM.Core.Scheduler.Nodes;");
            sb.AppendLine("using VSMVVM.Core.Scheduler.Pins;");
            sb.AppendLine();

            var hasNs = !string.IsNullOrEmpty(info.Namespace);
            if (hasNs)
            {
                sb.Append("namespace ").Append(info.Namespace).AppendLine();
                sb.AppendLine("{");
            }

            sb.Append("    partial class ").AppendLine(info.ClassName);
            sb.AppendLine("    {");
            sb.Append("        public override string TypeId => \"").Append(info.TypeId).AppendLine("\";");
            sb.AppendLine();
            sb.AppendLine("        internal static readonly PinDescriptor[] __VsmvvmPins = new PinDescriptor[]");
            sb.AppendLine("        {");
            for (int i = 0; i < info.Pins.Count; i++)
            {
                var p = info.Pins[i];
                var dirText = p.Direction == PinDirectionGen.Input ? "PinDirection.Input" : "PinDirection.Output";
                var kindText = p.Kind == PinKindGen.Exec ? "PinKind.Exec" : "PinKind.Data";
                var typeofText = p.Kind == PinKindGen.Exec ? "typeof(void)" : $"typeof({p.ValueTypeName})";
                var sep = i == info.Pins.Count - 1 ? "" : ",";
                sb.Append("            new PinDescriptor(\"").Append(p.Id).Append("\", \"")
                  .Append(p.DisplayName).Append("\", ")
                  .Append(dirText).Append(", ").Append(kindText).Append(", ")
                  .Append(typeofText).Append(", ").Append(p.DefaultValueLiteral)
                  .Append(")").AppendLine(sep);
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => __VsmvvmPins;");
            sb.AppendLine("    }");

            if (hasNs) sb.AppendLine("}");
            sb.AppendLine("#nullable restore");

            var hint = (hasNs ? info.Namespace + "." : "") + info.ClassName + ".Node.g.cs";
            context.AddSource(hint, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void EmitAssemblyRegistration(Microsoft.CodeAnalysis.Compilation compilation, List<NodeInfo> nodes,
                                                     SourceProductionContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable disable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using VSMVVM.Core.Scheduler.Nodes;");
            sb.AppendLine("using VSMVVM.Core.Scheduler.Pins;");
            sb.AppendLine();

            // 어셈블리 식별자(중복 방지). 사용자 어셈블리 이름을 그대로 클래스 suffix로 사용하지 않고 fixed name.
            sb.AppendLine("namespace VSMVVM.Core.Scheduler.__Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class __VsmvvmNodeRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        [ModuleInitializer]");
            sb.AppendLine("        internal static void Register()");
            sb.AppendLine("        {");

            foreach (var n in nodes)
            {
                sb.Append("            NodeMetadataRegistry.Register(new NodeMetadata(")
                  .Append("\"").Append(n.TypeId).Append("\", ")
                  .Append("\"").Append(EscapeStr(n.DisplayName)).Append("\", ")
                  .Append("\"").Append(EscapeStr(n.Category)).Append("\", ")
                  .Append("\"").Append(EscapeStr(n.Description)).Append("\", ")
                  .Append(n.TimeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
                  .Append("typeof(").Append(n.FullClassName).Append("), ")
                  .Append("() => new ").Append(n.FullClassName).Append("(), ")
                  .Append(n.FullClassName).Append(".__VsmvvmPins")
                  .AppendLine("));");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine("#nullable restore");

            context.AddSource("__VsmvvmNodeRegistration.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void EmitModuleInitializerPolyfill(SourceProductionContext context)
        {
            // netstandard2.0/네트표준 환경에서 ModuleInitializerAttribute가 없을 수 있다.
            // internal 클래스로 정의하므로 System 측 public 타입과 충돌하지 않음.
            var src = @"// <auto-generated />
// VSMVVM.Core.Scheduler: ModuleInitializerAttribute polyfill for older TFMs.
// internal로 정의하여 .NET 5+ 환경의 System 정의와도 공존 가능.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : global::System.Attribute { }
}
#endif
";
            context.AddSource("__VsmvvmModuleInitializerPolyfill.g.cs", SourceText.From(src, Encoding.UTF8));
        }

        #endregion

        private static string EscapeStr(string s) =>
            (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static void ReportDiagnostic(SourceProductionContext context, Location location,
                                             string id, string title, string message)
        {
            var descriptor = new DiagnosticDescriptor(id, title, message, "VSMVVM.Scheduler",
                DiagnosticSeverity.Error, true);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location ?? Location.None));
        }
    }
}
