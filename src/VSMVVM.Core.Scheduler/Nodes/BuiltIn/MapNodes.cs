using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    public sealed class MapSetNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Set<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Map",   "Map",   PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Key",   "Key",   PinDirection.Input,  PinKind.Data, typeof(TKey),   default(TKey)),
            new PinDescriptor("Value", "Value", PinDirection.Input,  PinKind.Data, typeof(TValue), default(TValue)),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Out",   "Out",   PinDirection.Output, PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map") ?? new Dictionary<TKey, TValue>();
            var k = context.GetInput<TKey>(this, "Key");
            var v = context.GetInput<TValue>(this, "Value");
            if (k != null) map[k] = v;
            context.SetOutput(this, "Out", map);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Set", "Collections",
            $"Assigns Map[Key] = Value.", 0,
            typeof(MapSetNode<TKey, TValue>), () => new MapSetNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapGetNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Get<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Map",   "Map",   PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Key",   "Key",   PinDirection.Input,  PinKind.Data, typeof(TKey),   default(TKey)),
            new PinDescriptor("Value", "Value", PinDirection.Output, PinKind.Data, typeof(TValue), default(TValue)),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            var k = context.GetInput<TKey>(this, "Key");
            TValue v = (map != null && k != null && map.TryGetValue(k, out var found)) ? found : default;
            context.SetOutput(this, "Value", v);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Get", "Collections",
            $"Reads Map[Key]. Missing key returns default(TValue).", 0,
            typeof(MapGetNode<TKey, TValue>), () => new MapGetNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapContainsKeyNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.ContainsKey<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Map",    "Map",    PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Key",    "Key",    PinDirection.Input,  PinKind.Data, typeof(TKey),   default(TKey)),
            new PinDescriptor("Result", "Result", PinDirection.Output, PinKind.Data, typeof(bool),   false),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            var k = context.GetInput<TKey>(this, "Key");
            context.SetOutput(this, "Result", map != null && k != null && map.ContainsKey(k));
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.ContainsKey", "Collections",
            $"Returns Map.ContainsKey(Key).", 0,
            typeof(MapContainsKeyNode<TKey, TValue>), () => new MapContainsKeyNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapRemoveNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Remove<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void), null),
            new PinDescriptor("Map",  "Map",  PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Key",  "Key",  PinDirection.Input,  PinKind.Data, typeof(TKey),   default(TKey)),
            new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            new PinDescriptor("Out",  "Out",  PinDirection.Output, PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            var k = context.GetInput<TKey>(this, "Key");
            if (map != null && k != null) map.Remove(k);
            context.SetOutput(this, "Out", map);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Remove", "Collections",
            $"Removes entry by Key. No-op if missing.", 0,
            typeof(MapRemoveNode<TKey, TValue>), () => new MapRemoveNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapKeysNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Keys<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Map",  "Map",  PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Keys", "Keys", PinDirection.Output, PinKind.Data, typeof(List<TKey>), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            var list = map != null ? map.Keys.ToList() : new List<TKey>();
            context.SetOutput(this, "Keys", list);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Keys", "Collections",
            $"Returns Map.Keys as List<TKey>.", 0,
            typeof(MapKeysNode<TKey, TValue>), () => new MapKeysNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapValuesNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Values<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Map",    "Map",    PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Values", "Values", PinDirection.Output, PinKind.Data, typeof(List<TValue>), null),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            var list = map != null ? map.Values.ToList() : new List<TValue>();
            context.SetOutput(this, "Values", list);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Values", "Collections",
            $"Returns Map.Values as List<TValue>.", 0,
            typeof(MapValuesNode<TKey, TValue>), () => new MapValuesNode<TKey, TValue>(), PinSpec);
    }

    public sealed class MapCountNode<TKey, TValue> : NodeBase
    {
        public static readonly string TypeIdConst =
            "Core.Map.Count<" + PinTypeInfo.ComputeStableName(typeof(TKey)) + "," + PinTypeInfo.ComputeStableName(typeof(TValue)) + ">";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Map",   "Map",   PinDirection.Input,  PinKind.Data, typeof(Dictionary<TKey, TValue>), null),
            new PinDescriptor("Count", "Count", PinDirection.Output, PinKind.Data, typeof(int),  0),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var map = context.GetInput<Dictionary<TKey, TValue>>(this, "Map");
            context.SetOutput(this, "Count", map?.Count ?? 0);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, $"Map<{typeof(TKey).Name},{typeof(TValue).Name}>.Count", "Collections",
            $"Returns Map.Count.", 0,
            typeof(MapCountNode<TKey, TValue>), () => new MapCountNode<TKey, TValue>(), PinSpec);
    }
}
