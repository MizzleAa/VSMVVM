using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    /// <summary>
    /// Phase K — 다형성 핀의 호환성은 NodeBase.BuildPins 가 ItemType 으로 치환한 후의 ValueType 에서 결정.
    /// 즉 PinCompatibility 는 기존 규칙만으로 충분 (IsAssignableFrom).
    /// 이 테스트는 회귀 방지용 — 다형성 노드 간 연결 시 동일/이종 ItemType 처리가 기대대로 동작.
    /// </summary>
    public class PinCompatibilityPolymorphismTests
    {
        private sealed class PolyNode : NodeBase, IPolymorphicNode
        {
            public override string TypeId => "Test.Poly";
            private static readonly PinDescriptor[] Spec = new[]
            {
                new PinDescriptor("In",  "In",  PinDirection.Input,  PinKind.Data, typeof(object), null, typeParameterName: "T"),
                new PinDescriptor("Out", "Out", PinDirection.Output, PinKind.Data, typeof(object), null, typeParameterName: "T"),
            };
            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => Spec;
            public Type ItemType { get; set; } = typeof(int);
            public IReadOnlyDictionary<string, Type> TypeArguments =>
                new Dictionary<string, Type> { ["T"] = ItemType };
            public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext c) => Task.FromResult(ExecutionFlow.Halt);
        }

        [Fact]
        public void TwoPolyNodes_SameItemType_AreCompatible()
        {
            var a = new PolyNode { ItemType = typeof(int) };
            var b = new PolyNode { ItemType = typeof(int) };
            var ok = PinCompatibility.CanConnect(Out(a), In(b), out var reason);
            ok.Should().BeTrue(reason);
        }

        [Fact]
        public void TwoPolyNodes_DifferentItemTypes_AreNotCompatible()
        {
            var a = new PolyNode { ItemType = typeof(int) };
            var b = new PolyNode { ItemType = typeof(string) };
            var ok = PinCompatibility.CanConnect(Out(a), In(b), out var reason);
            ok.Should().BeFalse();
            reason.Should().Contain("not assignable");
        }

        [Fact]
        public void PolyNode_AndStaticPinOfSameType_AreCompatible()
        {
            // 다형성 노드 (ItemType=int) 의 Out 핀 → 정적 int 핀.
            var a = new PolyNode { ItemType = typeof(int) };
            var staticNode = new TestHelpers.TestNode("Test.Static", TestHelpers.Pin.DataIn<int>("V"));
            var ok = PinCompatibility.CanConnect(Out(a), staticNode.Pins[0], out var reason);
            ok.Should().BeTrue(reason);
        }

        private static IPin In(INode n) => Find(n, "In");
        private static IPin Out(INode n) => Find(n, "Out");
        private static IPin Find(INode n, string id)
        {
            foreach (var p in n.Pins) if (p.Id == id) return p;
            throw new InvalidOperationException(id);
        }
    }
}
