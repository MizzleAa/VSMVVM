using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// Phase K — 다형성 List 노드 베이스. ItemType 속성이 변경되면 InvalidatePins 로 핀 재빌드.
    /// 핀 디스크립터에서 Item 은 "T" placeholder, List/Out 은 "TList" placeholder 사용.
    /// TypeArguments dict 가 ItemType → T 매핑과 List&lt;ItemType&gt; → TList 매핑을 동시에 제공.
    /// </summary>
    public abstract class PolymorphicListNodeBase : NodeBase, IPolymorphicNode, INodeInstancePropertyHost
    {
        private Type _itemType = typeof(object);

        /// <summary>이 노드 인스턴스가 다루는 요소 타입. 변경 시 핀 재빌드.</summary>
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

        public IReadOnlyDictionary<string, Type> TypeArguments
        {
            get
            {
                var d = new Dictionary<string, Type>(2)
                {
                    ["T"] = _itemType,
                    ["TList"] = typeof(List<>).MakeGenericType(_itemType),
                };
                return d;
            }
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

        /// <summary>현재 ItemType 으로 비어있는 List 인스턴스 생성 (null 입력 폴백용).</summary>
        protected IList CreateEmptyList() =>
            (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_itemType));

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
    }

    /// <summary>List&lt;T&gt;.Add — Item 추가 후 동일 리스트 참조를 Out 으로.</summary>
    public sealed class ListAddNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.Add";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("List", "List", PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Item", "Item", PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
            new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Out",  "Out",  PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "TList"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List") ?? CreateEmptyList();
            var item = context.GetInput<object>(this, "Item");
            list.Add(item);
            context.SetOutput(this, "Out", list);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.Add", "Collections",
            "Appends Item to a List<T> (T chosen per instance).", 0,
            typeof(ListAddNode), () => new ListAddNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>List&lt;T&gt;[Index] → Item (pull, 범위 밖이면 default).</summary>
    public sealed class ListGetNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.Get";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("List",  "List",  PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Index", "Index", PinDirection.Input,  PinKind.Data, typeof(int),    0),
            new PinDescriptor("Item",  "Item",  PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "T"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List");
            var idx = context.GetInput<int>(this, "Index");
            object item = (list != null && idx >= 0 && idx < list.Count) ? list[idx] : DefaultForItemType();
            context.SetOutput(this, "Item", item);
            return Task.CompletedTask;
        }

        private object DefaultForItemType() => ItemType.IsValueType ? Activator.CreateInstance(ItemType) : null;

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.Get", "Collections",
            "Reads List<T>[Index]. Out of range returns default(T).", 0,
            typeof(ListGetNode), () => new ListGetNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>List&lt;T&gt;.Count.</summary>
    public sealed class ListCountNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.Count";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("List",  "List",  PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Count", "Count", PinDirection.Output, PinKind.Data, typeof(int),    0),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List");
            context.SetOutput(this, "Count", list?.Count ?? 0);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.Count", "Collections",
            "Returns List<T>.Count.", 0,
            typeof(ListCountNode), () => new ListCountNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>List&lt;T&gt;.Clear — 동일 리스트 참조를 Out 으로.</summary>
    public sealed class ListClearNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.Clear";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",   "In",   PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("List", "List", PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Out",  "Out",  PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "TList"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List");
            list?.Clear();
            context.SetOutput(this, "Out", list);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.Clear", "Collections",
            "Clears all items from a List<T>.", 0,
            typeof(ListClearNode), () => new ListClearNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>List&lt;T&gt;.Contains(Item) → bool.</summary>
    public sealed class ListContainsNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.Contains";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("List",   "List",   PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Item",   "Item",   PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
            new PinDescriptor("Result", "Result", PinDirection.Output, PinKind.Data, typeof(bool),   false),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List");
            var item = context.GetInput<object>(this, "Item");
            context.SetOutput(this, "Result", list != null && list.Contains(item));
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.Contains", "Collections",
            "Returns true if List<T> contains Item.", 0,
            typeof(ListContainsNode), () => new ListContainsNode(), PinSpec,
            typeParameters: new[] { "T" });
    }

    /// <summary>List&lt;T&gt;.RemoveAt(Index) — 동일 리스트 참조 통과.</summary>
    public sealed class ListRemoveAtNode : PolymorphicListNodeBase
    {
        public const string TypeIdConst = "Core.List.RemoveAt";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("List",  "List",  PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "TList"),
            new PinDescriptor("Index", "Index", PinDirection.Input,  PinKind.Data, typeof(int),    0),
            new PinDescriptor("Then",  "Then",  PinDirection.Output, PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Out",   "Out",   PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "TList"),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var list = (IList)context.GetInput<object>(this, "List");
            var idx = context.GetInput<int>(this, "Index");
            if (list != null && idx >= 0 && idx < list.Count)
            {
                list.RemoveAt(idx);
            }
            context.SetOutput(this, "Out", list);
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "List.RemoveAt", "Collections",
            "Removes element at Index. No-op if out of range.", 0,
            typeof(ListRemoveAtNode), () => new ListRemoveAtNode(), PinSpec,
            typeParameters: new[] { "T" });
    }
}
