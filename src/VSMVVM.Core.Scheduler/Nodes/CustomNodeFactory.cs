using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VSMVVM.Core.Scheduler.Attributes;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 사용자(또는 호스트) 어셈블리에서 메소드를 발견하여 <see cref="CustomFunctionNode"/>로 래핑한 후
    /// <see cref="NodeMetadataRegistry"/>에 등록합니다.
    ///
    /// 발견 규칙:
    ///   1) [MethodNode("...")] 부착 메소드 — 명시적 등록
    ///   2) 옵션으로, public static 메소드 자동 등록 (RegisterPublicStaticMethods = true)
    ///
    /// 인스턴스 메소드도 등록 가능하지만 그래프 사용 시 노드의 <see cref="CustomFunctionNode.Instance"/>가 채워져 있어야 함.
    /// </summary>
    public static class CustomNodeFactory
    {
        public sealed class Options
        {
            /// <summary>true면 [MethodNode] 미부착 public static 메소드도 자동 등록.</summary>
            public bool RegisterPublicStaticMethods { get; set; }

            /// <summary>자동 등록 시 TypeId 접두사 (Assembly 단위 충돌 방지). 빈 값이면 풀네임 그대로.</summary>
            public string AutoTypeIdPrefix { get; set; }

            /// <summary>인스턴스 메소드 자동 등록 시 인스턴스 팩토리. null이면 인스턴스 메소드는 스킵.</summary>
            public Func<Type, object> InstanceFactory { get; set; }
        }

        /// <summary>
        /// 어셈블리 전체를 스캔하여 노드로 등록 가능한 메소드를 모두 등록합니다.
        /// 반환값: 등록된 TypeId 목록.
        /// </summary>
        public static IReadOnlyList<string> RegisterFromAssembly(Assembly assembly, Options options = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            options ??= new Options();

            var registered = new List<string>();

            foreach (var type in SafeGetTypes(assembly))
            {
                if (!type.IsClass) continue;
                if (type.IsAbstract && !type.IsSealed) continue; // 일반 abstract 제외, static class(=abstract+sealed)는 허용

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.IsSpecialName) continue; // getter/setter/operator 등 제외
                    if (method.IsAbstract) continue;
                    if (method.ContainsGenericParameters) continue; // 제네릭 메소드는 PoC 미지원

                    var meta = TryBuildMetadata(method, options);
                    if (meta == null) continue;

                    try
                    {
                        NodeMetadataRegistry.Register(meta);
                        registered.Add(meta.TypeId);
                    }
                    catch (InvalidOperationException)
                    {
                        // 중복 등록은 무시 (재컴파일 시나리오 — 호출자가 UnregisterForTests로 정리 가능)
                    }
                }

                // [ParameterNode] 부착 정적 필드 → CustomParameterNode 로 래핑.
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var paramAttr = field.GetCustomAttribute<ParameterNodeAttribute>();
                    if (paramAttr == null) continue;
                    if (!field.IsStatic) continue; // 인스턴스 필드는 silent skip (v1 정책)

                    var meta = TryBuildParameterMetadata(field, paramAttr);
                    if (meta == null) continue;

                    try
                    {
                        NodeMetadataRegistry.Register(meta);
                        registered.Add(meta.TypeId);
                    }
                    catch (InvalidOperationException)
                    {
                        // 중복 등록 무시 — 재컴파일 시나리오.
                    }
                }
            }

            return registered;
        }

        private static NodeMetadata TryBuildParameterMetadata(FieldInfo field, ParameterNodeAttribute attr)
        {
            var typeId = attr.Id;
            var displayName = attr.DisplayName ?? field.Name;
            var category = attr.Category ?? string.Empty;
            var description = attr.Description ?? string.Empty;
            var pins = CustomParameterNode.BuildPinsForField(field);

            return new NodeMetadata(
                typeId, displayName, category, description, defaultTimeoutMs: 0,
                clrType: typeof(CustomParameterNode),
                factory: () => new CustomParameterNode(typeId, field, pins),
                pins: pins);
        }

        private static NodeMetadata TryBuildMetadata(MethodInfo method, Options options)
        {
            var methodAttr = method.GetCustomAttribute<MethodNodeAttribute>();

            string typeId;
            string displayName;
            string category;
            string description;
            int timeoutMs;

            if (methodAttr != null)
            {
                typeId = methodAttr.Id;
                displayName = methodAttr.DisplayName ?? method.Name;
                category = methodAttr.Category ?? string.Empty;
                description = methodAttr.Description ?? string.Empty;
                timeoutMs = methodAttr.TimeoutMs;
            }
            else
            {
                if (!options.RegisterPublicStaticMethods) return null;
                if (!method.IsStatic) return null;

                var prefix = options.AutoTypeIdPrefix ?? string.Empty;
                typeId = string.IsNullOrEmpty(prefix)
                    ? $"{method.DeclaringType.FullName}.{method.Name}"
                    : $"{prefix}.{method.DeclaringType.Name}.{method.Name}";
                displayName = method.Name;
                category = method.DeclaringType.Name;
                description = string.Empty;
                timeoutMs = 0;
            }

            // 매개변수 0개 + non-void 반환 → Constant-like 모드로 분기.
            bool isConstantLike = SignatureToPinsBuilder.IsConstantLike(method);

            IReadOnlyList<PinDescriptor> pins;
            try
            {
                pins = isConstantLike
                    ? SignatureToPinsBuilder.BuildAsConstant(method)
                    : SignatureToPinsBuilder.Build(method);
            }
            catch (NotSupportedException)
            {
                return null; // ref/out/pointer 등 — 스킵
            }

            // 인스턴스 메소드면 InstanceFactory가 있어야 등록 가능
            object instanceSnapshot = null;
            if (!method.IsStatic)
            {
                if (options.InstanceFactory == null) return null;
                instanceSnapshot = options.InstanceFactory(method.DeclaringType);
                if (instanceSnapshot == null) return null;
            }

            var capturedInstance = instanceSnapshot;
            Func<INode> factory;
            Type clrType;
            if (isConstantLike)
            {
                clrType = typeof(CustomConstantNode);
                factory = () =>
                {
                    var node = new CustomConstantNode(typeId, method, pins);
                    if (!method.IsStatic) node.Instance = capturedInstance;
                    return node;
                };
            }
            else
            {
                clrType = typeof(CustomFunctionNode);
                factory = () =>
                {
                    var node = new CustomFunctionNode(typeId, method, pins);
                    if (!method.IsStatic) node.Instance = capturedInstance;
                    return node;
                };
            }

            return new NodeMetadata(typeId, displayName, category, description, timeoutMs,
                clrType, factory, pins);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
