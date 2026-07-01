using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// Phase L — 다형성 상수 노드. 단일 typeId "Core.Constant" + 인스턴스의 ItemType 속성으로 타입 결정.
    /// Phase K 의 List/Variable 패턴과 동일.
    /// <para>
    /// Value 입력 핀 (편집 가능 리터럴) → Out 출력 핀 그대로. Exec 핀 없음 (pull 평가).
    /// </para>
    /// </summary>
    public sealed class ConstantNode : NodeBase, IPolymorphicNode, INodeInstancePropertyHost
    {
        public const string TypeIdConst = "Core.Constant";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
            new PinDescriptor("Out",   "Out",   PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "T"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        private Type _itemType = typeof(int);
        public Type ItemType
        {
            get => _itemType;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (_itemType == value) return;
                _itemType = value;
                InvalidatePins();
            }
        }

        public IReadOnlyDictionary<string, Type> TypeArguments =>
            new Dictionary<string, Type> { ["T"] = _itemType };

        public override Task EvaluateAsync(ExecutionContext context)
        {
            context.SetOutput(this, "Out", context.GetInput<object>(this, "Value"));
            return Task.CompletedTask;
        }

        public override void WriteState(Utf8JsonWriter writer)
        {
            writer.WriteString("itemType", PinTypeInfo.ComputeStableName(_itemType));
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("itemType", out var n) && n.ValueKind == JsonValueKind.String)
            {
                var t = PinTypeInfo.ResolveStableName(n.GetString());
                if (t != null) ItemType = t;
            }
        }

        public IReadOnlyList<NodeInstancePropertyDescriptor> GetInstanceProperties()
        {
            return new[]
            {
                new NodeInstancePropertyDescriptor(
                    id: "ItemType",
                    displayName: "Item Type",
                    kind: NodeInstancePropertyKind.Type,
                    getValue: () => PinTypeInfo.ComputeStableName(_itemType),
                    setValue: stableName =>
                    {
                        var t = PinTypeInfo.ResolveStableName(stableName);
                        if (t != null) ItemType = t;
                    }),
            };
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Constant", "Math",
            "A constant value (type chosen per instance). Editable via the Value literal.", 0,
            typeof(ConstantNode), () => new ConstantNode(), PinSpec,
            typeParameters: new[] { "T" });
    }
}
