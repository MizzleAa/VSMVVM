using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// Constant-like 메소드(매개변수 없음 + non-void 반환)를 그래프 노드로 래핑.
    /// Exec 핀 없음 — pull 평가(EvaluateAsync)에서 메소드 호출 후 결과를 "Out" 핀에 저장.
    /// 사용자가 [MethodNode] 부여한 매개변수 없는 메소드를 CustomNodeFactory 가 본 클래스로 등록.
    /// </summary>
    public sealed class CustomConstantNode : NodeBase
    {
        private readonly string _typeId;
        private readonly MethodInfo _method;
        private readonly IReadOnlyList<PinDescriptor> _pins;

        /// <summary>인스턴스 메소드용 대상 객체. 정적 메소드면 null.</summary>
        public object Instance { get; set; }

        public override string TypeId => _typeId;

        public CustomConstantNode(string typeId, MethodInfo method, IReadOnlyList<PinDescriptor> pins)
        {
            _typeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _pins = pins ?? throw new ArgumentNullException(nameof(pins));
            if (method.GetParameters().Length != 0)
            {
                throw new ArgumentException(
                    $"CustomConstantNode requires a parameterless method; got '{method.Name}'.", nameof(method));
            }
        }

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => _pins;

        public override async Task EvaluateAsync(ExecutionContext context)
        {
            var result = _method.Invoke(_method.IsStatic ? null : Instance, Array.Empty<object>());

            // 비동기 메소드 unwrap
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                result = UnwrapTaskResult(task);
            }
            else if (result is ValueTask vt)
            {
                await vt.ConfigureAwait(false);
                result = null;
            }

            var effective = SignatureToPinsBuilder.GetEffectiveReturnType(_method);
            if (effective != typeof(void))
            {
                InvokeSetOutputGeneric(context, SignatureToPinsBuilder.ConstantOutPinId, effective, result);
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> _setOutputCache
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>();
        private static readonly MethodInfo _setOutputOpen = typeof(ExecutionContext).GetMethod(nameof(ExecutionContext.SetOutput));

        private void InvokeSetOutputGeneric(ExecutionContext ctx, string pinId, Type valueType, object value)
        {
            var closed = _setOutputCache.GetOrAdd(valueType, t => _setOutputOpen.MakeGenericMethod(t));
            closed.Invoke(ctx, new object[] { this, pinId, value });
        }

        private static object UnwrapTaskResult(Task task)
        {
            var t = task.GetType();
            while (t != null && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return t.GetProperty(nameof(Task<object>.Result))?.GetValue(task);
                }
                t = t.BaseType;
            }
            return null;
        }
    }
}
