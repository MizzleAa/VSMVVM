using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 사용자(또는 호스트) 메소드를 그래프 노드로 래핑.
    /// 핀 구성은 SignatureToPinsBuilder가 결정하고, ExecuteAsync에서 reflection으로 메소드를 호출합니다.
    /// 동일 MethodInfo로 여러 인스턴스를 만들 수 있도록 디스크립터/메소드를 클래스 외부에서 주입.
    ///
    /// 비동기 메소드(Task / Task&lt;T&gt;)는 await 후 결과를 SetOutput.
    /// 인스턴스 메소드는 <see cref="Instance"/>가 설정되어 있어야 함 (CustomNodeFactory에서 주입).
    /// </summary>
    public sealed class CustomFunctionNode : NodeBase
    {
        private readonly string _typeId;
        private readonly MethodInfo _method;
        private readonly IReadOnlyList<PinDescriptor> _pins;
        private readonly ParameterInfo[] _parameters;

        /// <summary>인스턴스 메소드용 대상 객체. 정적 메소드면 null.</summary>
        public object Instance { get; set; }

        /// <summary>이 노드가 래핑하는 원본 메서드. UI에서 "Open Code" 등 소스 역추적 용도.</summary>
        public MethodInfo Method => _method;

        public override string TypeId => _typeId;

        public CustomFunctionNode(string typeId, MethodInfo method, IReadOnlyList<PinDescriptor> pins)
        {
            _typeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _pins = pins ?? throw new ArgumentNullException(nameof(pins));
            _parameters = method.GetParameters();
        }

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => _pins;

        public override async Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            // 파라미터 값 풀
            var args = new object[_parameters.Length];
            for (int i = 0; i < _parameters.Length; i++)
            {
                var p = _parameters[i];
                args[i] = InvokeGetInputGeneric(context, p.Name, p.ParameterType);
            }

            var result = _method.Invoke(_method.IsStatic ? null : Instance, args);

            // 비동기 unwrap
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
            else if (IsValueTaskOfT(result, out var vtAwaiter))
            {
                result = await vtAwaiter.ConfigureAwait(false);
            }

            // 반환값을 Result 핀에 저장 (void/Task면 핀 자체가 없음).
            // 튜플 반환이면 각 요소를 요소명(Item1..N 폴백) 핀에 분배.
            var effective = SignatureToPinsBuilder.GetEffectiveReturnType(_method);
            if (effective != typeof(void))
            {
                if (SignatureToPinsBuilder.IsValueTuple(effective))
                {
                    WriteTupleOutputs(context, effective, result);
                }
                else
                {
                    InvokeSetOutputGeneric(context, SignatureToPinsBuilder.ResultPinId, effective, result);
                }
            }

            return ExecutionFlow.Continue(SignatureToPinsBuilder.ExecOutputPinId);
        }

        private void WriteTupleOutputs(ExecutionContext ctx, Type tupleType, object tupleValue)
        {
            var elementTypes = tupleType.GetGenericArguments();
            var names = SignatureToPinsBuilder.ReadTupleElementNames(_method);
            for (int i = 0; i < elementTypes.Length; i++)
            {
                var name = (names != null && i < names.Count && !string.IsNullOrEmpty(names[i]))
                    ? names[i]
                    : "Item" + (i + 1);
                // ValueTuple의 요소는 Item1..Item7 (+ Rest). CLR 은 GetFields 순서를 보장하지 않으므로 이름으로 조회.
                var itemFieldName = "Item" + (i + 1);
                var f = tupleType.GetField(itemFieldName, BindingFlags.Public | BindingFlags.Instance);
                var value = (tupleValue == null || f == null) ? null : f.GetValue(tupleValue);
                InvokeSetOutputGeneric(ctx, name, elementTypes[i], value);
            }
        }

        // 강타입 GetInput<T>을 reflection으로 호출. _typeCache로 GenericMethodInfo를 캐시하여 핫 패스 최적화.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> _getInputCache
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> _setOutputCache
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>();
        private static readonly MethodInfo _getInputOpen = typeof(ExecutionContext).GetMethod(nameof(ExecutionContext.GetInput));
        private static readonly MethodInfo _setOutputOpen = typeof(ExecutionContext).GetMethod(nameof(ExecutionContext.SetOutput));

        private object InvokeGetInputGeneric(ExecutionContext ctx, string pinId, Type valueType)
        {
            var closed = _getInputCache.GetOrAdd(valueType, t => _getInputOpen.MakeGenericMethod(t));
            return closed.Invoke(ctx, new object[] { this, pinId });
        }

        private void InvokeSetOutputGeneric(ExecutionContext ctx, string pinId, Type valueType, object value)
        {
            var closed = _setOutputCache.GetOrAdd(valueType, t => _setOutputOpen.MakeGenericMethod(t));
            closed.Invoke(ctx, new object[] { this, pinId, value });
        }

        private static object UnwrapTaskResult(Task task)
        {
            // Task<T>의 동적 구현체(AsyncStateMachineBox<T> 등)는 Task<T>를 상속하므로
            // 상속 체인을 따라 Task<T>를 찾아 Result 프로퍼티를 가져온다.
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

        private static bool IsValueTaskOfT(object result, out Task<object> awaiter)
        {
            awaiter = null;
            if (result == null) return false;
            var t = result.GetType();
            if (!t.IsGenericType) return false;
            if (t.GetGenericTypeDefinition() != typeof(ValueTask<>)) return false;

            // ValueTask<T>.AsTask() → Task<T>
            var asTask = t.GetMethod(nameof(ValueTask<object>.AsTask));
            var taskTyped = asTask.Invoke(result, null);

            // 결과를 object로 박싱하여 await 가능하도록 래퍼 Task 생성
            awaiter = WrapAsObjectTask((Task)taskTyped);
            return true;
        }

        private static Task<object> WrapAsObjectTask(Task taskTyped)
        {
            // Task<T>.Result는 await 후 동기 접근 가능
            return taskTyped.ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception!.GetBaseException();
                var tt = t.GetType();
                if (tt.IsGenericType && tt.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return tt.GetProperty(nameof(Task<object>.Result))?.GetValue(t);
                }
                return null;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
