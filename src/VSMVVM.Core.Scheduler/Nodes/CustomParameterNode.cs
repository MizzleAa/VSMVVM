using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 정적 필드를 1핀 출력 노드로 래핑. <see cref="CustomConstantNode"/> 의 필드 버전.
    /// Exec 핀 없음 — pull 평가(EvaluateAsync)에서 필드 값을 읽어 "Out" 핀에 저장.
    /// </summary>
    public sealed class CustomParameterNode : NodeBase
    {
        public const string OutPinId = "Out";

        private readonly string _typeId;
        private readonly FieldInfo _field;
        private readonly IReadOnlyList<PinDescriptor> _pins;

        public override string TypeId => _typeId;

        public CustomParameterNode(string typeId, FieldInfo field, IReadOnlyList<PinDescriptor> pins)
        {
            _typeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _pins = pins ?? throw new ArgumentNullException(nameof(pins));
            if (!_field.IsStatic)
                throw new ArgumentException(
                    $"CustomParameterNode requires a static field; got '{field.DeclaringType?.Name}.{field.Name}'.",
                    nameof(field));
        }

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => _pins;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var value = _field.GetValue(null);
            InvokeSetOutputGeneric(context, OutPinId, _field.FieldType, value);
            return Task.CompletedTask;
        }

        private static readonly ConcurrentDictionary<Type, MethodInfo> _setOutputCache
            = new ConcurrentDictionary<Type, MethodInfo>();
        private static readonly MethodInfo _setOutputOpen =
            typeof(ExecutionContext).GetMethod(nameof(ExecutionContext.SetOutput));

        private void InvokeSetOutputGeneric(ExecutionContext ctx, string pinId, Type valueType, object value)
        {
            var closed = _setOutputCache.GetOrAdd(valueType, t => _setOutputOpen.MakeGenericMethod(t));
            closed.Invoke(ctx, new object[] { this, pinId, value });
        }

        /// <summary>주어진 정적 필드에 대해 단일 출력 핀 spec 을 생성.</summary>
        public static IReadOnlyList<PinDescriptor> BuildPinsForField(FieldInfo field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            return new[]
            {
                new PinDescriptor(OutPinId, OutPinId, PinDirection.Output, PinKind.Data, field.FieldType, null),
            };
        }
    }
}
