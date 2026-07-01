using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// Phase K — IPolymorphicNode 가 TypeArguments dict 로 placeholder 핀의 실제 타입을 결정.
    /// NodeBase.BuildPins 는 다형성 핀을 만나면 TypeArguments[name] 으로 ValueType 치환하여 강타입 DataPin 생성.
    /// 인스턴스의 ItemType 변경 → InvalidatePins → 다음 Pins 접근 시 새 핀 컬렉션.
    /// </summary>
    public class PolymorphicNodeBuildPinsTests
    {
        // 테스트용 다형성 노드: 단일 T placeholder + Get/Set 핀.
        private sealed class TestPolyNode : NodeBase, IPolymorphicNode
        {
            public override string TypeId => "Test.Poly";

            private static readonly PinDescriptor[] Spec = new[]
            {
                new PinDescriptor("In",    "In",    PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
                new PinDescriptor("Out",   "Out",   PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "T"),
                new PinDescriptor("Other", "Other", PinDirection.Input,  PinKind.Data, typeof(string), ""),  // 정적 핀
            };

            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => Spec;

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

            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
                => Task.FromResult(ExecutionFlow.Halt);
        }

        [Fact]
        public void DefaultItemType_BuildsIntPin()
        {
            var node = new TestPolyNode();
            node.Pins.Should().HaveCount(3);
            FindPin(node, "In").ValueType.Should().Be(typeof(int));
            FindPin(node, "Out").ValueType.Should().Be(typeof(int));
            FindPin(node, "Other").ValueType.Should().Be(typeof(string));  // 정적 핀 그대로
        }

        [Fact]
        public void ChangeItemType_RebuildsPinsWithNewType()
        {
            var node = new TestPolyNode();
            node.ItemType = typeof(double);
            FindPin(node, "In").ValueType.Should().Be(typeof(double));
            FindPin(node, "Out").ValueType.Should().Be(typeof(double));
            FindPin(node, "Other").ValueType.Should().Be(typeof(string));
        }

        [Fact]
        public void MissingTypeArgument_FallsBackToObject()
        {
            // TypeArguments 가 T 를 제공하지 않으면 placeholder 가 object 로 남는다 (안전 폴백).
            var node = new MissingArgNode();
            FindPin(node, "In").ValueType.Should().Be(typeof(object));
        }

        private sealed class MissingArgNode : NodeBase, IPolymorphicNode
        {
            public override string TypeId => "Test.MissingArg";
            private static readonly PinDescriptor[] Spec = new[]
            {
                new PinDescriptor("In", "In", PinDirection.Input, PinKind.Data, typeof(object), null, typeParameterName: "T"),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => Spec;
            public IReadOnlyDictionary<string, Type> TypeArguments => new Dictionary<string, Type>();
            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
                => Task.FromResult(ExecutionFlow.Halt);
        }

        private static IPin FindPin(INode node, string id)
        {
            foreach (var p in node.Pins)
                if (p.Id == id) return p;
            throw new InvalidOperationException($"Pin '{id}' not found.");
        }
    }
}
