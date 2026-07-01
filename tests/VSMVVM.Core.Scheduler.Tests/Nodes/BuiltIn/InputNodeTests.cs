using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Nodes.BuiltIn
{
    /// <summary>
    /// Phase J — 외부 그래프 입력 노드. OutputNode 의 대칭:
    ///   • OutputNode: ctx.Outputs[Key] = Value 로 외부에 결과 노출.
    ///   • InputNode:  ctx.Inputs[Key] 에서 외부 제공 값을 꺼내 Value 출력.
    /// 호출자가 RunAsync 전에 ctx.Inputs 에 값을 채우고 그래프를 함수처럼 호출.
    /// </summary>
    public class InputNodeTests
    {
        [Fact]
        public void Registered_AsBuiltIn_UnderIOCategory()
        {
            BuiltInNodes.EnsureRegistered();
            var meta = NodeMetadataRegistry.Get(InputNode.TypeIdConst);
            meta.Should().NotBeNull();
            meta.Category.Should().Be("IO");
        }

        [Fact]
        public async Task ContextHasMatchingKey_ValuePinReturnsThatValue()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var input = g.AddNode(InputNode.TypeIdConst, 0, 0);
            ((NodeBase)input).SetLiteralInput("Key", "score");
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "received");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);

            g.Connect(start.Id, "Then", input.Id, "In");
            g.Connect(input.Id, "Then", output.Id, "In");
            g.Connect(input.Id, "Value", output.Id, "Value");
            g.Connect(output.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(g);
            ctx.Inputs["score"] = 42;

            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            result.Outputs["received"].Should().Be(42);
        }

        [Fact]
        public async Task ContextMissingKey_ValuePinReturnsDefault()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var input = g.AddNode(InputNode.TypeIdConst, 0, 0);
            ((NodeBase)input).SetLiteralInput("Key", "nope");
            ((NodeBase)input).SetLiteralInput("Default", "fallback");
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "received");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);

            g.Connect(start.Id, "Then", input.Id, "In");
            g.Connect(input.Id, "Then", output.Id, "In");
            g.Connect(input.Id, "Value", output.Id, "Value");
            g.Connect(output.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(g);
            // ctx.Inputs 에 "nope" 키 없음 → Default 반환.

            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Outputs["received"].Should().Be("fallback");
        }

        [Fact]
        public async Task AnyTypeAccepted_ObjectPin()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var input = g.AddNode(InputNode.TypeIdConst, 0, 0);
            ((NodeBase)input).SetLiteralInput("Key", "data");
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "back");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);

            g.Connect(start.Id, "Then", input.Id, "In");
            g.Connect(input.Id, "Then", output.Id, "In");
            g.Connect(input.Id, "Value", output.Id, "Value");
            g.Connect(output.Id, "Then", end.Id, "In");

            // 임의 타입 — string, List<int>, 사용자 객체 등.
            var custom = new List<int> { 1, 2, 3 };
            var ctx = new ExecutionContext(g);
            ctx.Inputs["data"] = custom;

            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Outputs["back"].Should().BeSameAs(custom);
        }

        [Fact]
        public async Task EmptyKey_TreatsAsKeyEqualsEmpty_LooksUpInputsEmptyKey()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var input = g.AddNode(InputNode.TypeIdConst, 0, 0);
            // Key 미설정 → default "" — ctx.Inputs[""] 가 없으면 Default 사용.
            ((NodeBase)input).SetLiteralInput("Default", "x");
            var output = g.AddNode(OutputNode.TypeIdConst, 0, 0);
            ((NodeBase)output).SetLiteralInput("Key", "k");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", input.Id, "In");
            g.Connect(input.Id, "Then", output.Id, "In");
            g.Connect(input.Id, "Value", output.Id, "Value");
            g.Connect(output.Id, "Then", end.Id, "In");

            var ctx = new ExecutionContext(g);
            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Outputs["k"].Should().Be("x");
        }
    }
}
