using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// Phase K — 다형성 Variable 노드 베이스. ItemType + VariableName 둘 다 인스턴스 속성.
    /// VariableName 설정 시 NodeGraph 의 변수 정의에서 타입을 자동 동기화한다 (선택적).
    /// </summary>
    public abstract class PolymorphicVariableNodeBase : NodeBase, IPolymorphicNode, INodeInstancePropertyHost
    {
        private Type _itemType = typeof(object);
        private string _variableName;

        /// <summary>변수 값의 CLR 타입. VariableName 이 그래프 변수와 매칭되면 그 정의의 타입과 동기화.</summary>
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

        /// <summary>참조할 그래프 변수 이름.</summary>
        public string VariableName
        {
            get => _variableName;
            set => _variableName = value;
        }

        public IReadOnlyDictionary<string, Type> TypeArguments =>
            new Dictionary<string, Type> { ["T"] = _itemType };

        public override void WriteState(Utf8JsonWriter writer)
        {
            if (_variableName != null) writer.WriteString("varName", _variableName);
            writer.WriteString("itemType", PinTypeInfo.ComputeStableName(_itemType));
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("varName", out var n) && n.ValueKind == JsonValueKind.String)
            {
                _variableName = n.GetString();
            }
            if (state.TryGetProperty("itemType", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var resolved = PinTypeInfo.ResolveStableName(t.GetString());
                if (resolved != null) ItemType = resolved;
            }
        }

        protected object DefaultForItemType() => _itemType.IsValueType ? Activator.CreateInstance(_itemType) : null;

        public IReadOnlyList<NodeInstancePropertyDescriptor> GetInstanceProperties()
        {
            return new[]
            {
                new NodeInstancePropertyDescriptor(
                    id: "VariableName",
                    displayName: "Variable",
                    kind: NodeInstancePropertyKind.VariableName,
                    getValue: () => _variableName,
                    setValue: v => _variableName = v),
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
    }

    /// <summary>그래프 변수 값을 출력. 변수가 없으면 ItemType 의 default.</summary>
    public sealed class GetVariableNode : PolymorphicVariableNodeBase
    {
        public const string TypeIdConst = "Core.Variable.Get";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Value", "Value", PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "T"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var name = VariableName;
            if (string.IsNullOrEmpty(name))
            {
                context.SetOutput(this, "Value", DefaultForItemType());
                return Task.CompletedTask;
            }
            if (context.Variables.TryGetValue(name, out var raw) && raw != null && ItemType.IsInstanceOfType(raw))
            {
                context.SetOutput(this, "Value", raw);
            }
            else if (context.Graph?.Variables != null && context.Graph.Variables.TryGetValue(name, out var def))
            {
                var v = (def.DefaultValue != null && ItemType.IsInstanceOfType(def.DefaultValue)) ? def.DefaultValue : DefaultForItemType();
                context.Variables[name] = v;
                context.SetOutput(this, "Value", v);
            }
            else
            {
                context.SetOutput(this, "Value", DefaultForItemType());
            }
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Get", "Variables",
            "Reads a graph variable (type chosen per instance).", 0,
            typeof(GetVariableNode), () => new GetVariableNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>그래프 변수에 값을 쓰고 Then 발화.</summary>
    public sealed class SetVariableNode : PolymorphicVariableNodeBase
    {
        public const string TypeIdConst = "Core.Variable.Set";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void),   null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var name = VariableName;
            if (!string.IsNullOrEmpty(name))
            {
                context.Variables[name] = context.GetInput<object>(this, "Value");
            }
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Set", "Variables",
            "Writes a graph variable (type chosen per instance).", 0,
            typeof(SetVariableNode), () => new SetVariableNode(), PinSpec,
            typeParameters: new[] { "T" });
    }
}
