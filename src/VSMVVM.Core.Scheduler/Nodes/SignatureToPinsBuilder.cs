using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// MethodInfo 시그니처에서 PinDescriptor[]를 자동 추출하는 헬퍼.
    /// 규칙:
    ///   - exec-in "In", exec-out "Then" 자동 추가
    ///   - 메소드 파라미터 → 데이터 입력 핀 (이름=파라미터명, 타입=파라미터형, DefaultValue=파라미터 기본값 또는 type default)
    ///   - 반환값 → 데이터 출력 핀 "Result"
    ///     - void / Task → "Result" 생략
    ///     - Task&lt;T&gt; → 타입 T로 unwrap
    /// 거부 사유:
    ///   - ref / out / in 파라미터 → NotSupportedException
    ///   - 포인터 / by-ref 반환 → NotSupportedException
    /// </summary>
    public static class SignatureToPinsBuilder
    {
        public const string ExecInputPinId = "In";
        public const string ExecOutputPinId = "Then";
        public const string ResultPinId = "Result";
        public const string ConstantOutPinId = "Out";

        /// <summary>
        /// 매개변수 0개 + non-void 반환 → "Constant-like" 메소드 (exec 흐름 없이 pull 데이터 소스로 동작).
        /// </summary>
        public static bool IsConstantLike(MethodInfo method)
        {
            if (method == null) return false;
            if (method.GetParameters().Length != 0) return false;
            return GetEffectiveReturnType(method) != typeof(void);
        }

        /// <summary>
        /// Constant-like 메소드용 핀 빌드 — exec 핀 없이 데이터 출력 1개 "Out".
        /// 매개변수가 있거나 void 반환이면 NotSupportedException.
        /// </summary>
        public static IReadOnlyList<PinDescriptor> BuildAsConstant(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (method.GetParameters().Length != 0)
            {
                throw new NotSupportedException(
                    $"Method '{method.Name}' has parameters — use Build() for function-style nodes.");
            }
            var effective = GetEffectiveReturnType(method);
            if (effective == typeof(void))
            {
                throw new NotSupportedException(
                    $"Method '{method.Name}' returns void — cannot be a constant-like node.");
            }
            if (effective.IsByRef || effective.IsPointer)
            {
                throw new NotSupportedException(
                    $"Method '{method.Name}' return type '{effective.Name}' is not supported.");
            }
            return new[]
            {
                new PinDescriptor(ConstantOutPinId, ConstantOutPinId,
                    PinDirection.Output, PinKind.Data, effective, null),
            };
        }

        /// <summary>
        /// 메소드의 효과적인 "반환 타입"을 반환. void/Task → typeof(void), Task&lt;T&gt; → T, 그 외 → 반환 타입 그대로.
        /// </summary>
        public static Type GetEffectiveReturnType(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var rt = method.ReturnType;
            if (rt == typeof(void)) return typeof(void);
            if (rt == typeof(Task) || rt == typeof(ValueTask)) return typeof(void);

            if (rt.IsGenericType)
            {
                var def = rt.GetGenericTypeDefinition();
                if (def == typeof(Task<>) || def == typeof(ValueTask<>))
                {
                    return rt.GetGenericArguments()[0];
                }
            }

            return rt;
        }

        /// <summary>주어진 MethodInfo에서 PinDescriptor 목록을 빌드.</summary>
        public static IReadOnlyList<PinDescriptor> Build(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var pins = new List<PinDescriptor>();

            pins.Add(new PinDescriptor(ExecInputPinId, ExecInputPinId,
                PinDirection.Input, PinKind.Exec, typeof(void), null));

            foreach (var p in method.GetParameters())
            {
                if (p.ParameterType.IsByRef)
                {
                    throw new NotSupportedException(
                        $"Method '{method.Name}' parameter '{p.Name}': ref/out/in parameters are not supported as data pins.");
                }
                if (p.ParameterType.IsPointer)
                {
                    throw new NotSupportedException(
                        $"Method '{method.Name}' parameter '{p.Name}': pointer types are not supported as data pins.");
                }

                var defaultValue = p.HasDefaultValue ? p.DefaultValue : GetTypeDefault(p.ParameterType);

                pins.Add(new PinDescriptor(
                    id: p.Name,
                    displayName: p.Name,
                    direction: PinDirection.Input,
                    kind: PinKind.Data,
                    valueType: p.ParameterType,
                    defaultValue: defaultValue));
            }

            pins.Add(new PinDescriptor(ExecOutputPinId, ExecOutputPinId,
                PinDirection.Output, PinKind.Exec, typeof(void), null));

            var effective = GetEffectiveReturnType(method);
            if (effective != typeof(void))
            {
                if (effective.IsByRef || effective.IsPointer)
                {
                    throw new NotSupportedException(
                        $"Method '{method.Name}' return type '{effective.Name}' is not supported.");
                }

                pins.Add(new PinDescriptor(
                    id: ResultPinId,
                    displayName: ResultPinId,
                    direction: PinDirection.Output,
                    kind: PinKind.Data,
                    valueType: effective,
                    defaultValue: null));
            }

            return pins;
        }

        private static object GetTypeDefault(Type t)
        {
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }
    }
}
