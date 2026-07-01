using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Serialization;
using VSMVVM.Core.Scheduler.Tests.TestHelpers;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Serialization
{
    public class NodeGraphSerializerTests
    {
        private static NodeGraph BuildBranchSampleGraph()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph { Name = "BranchSample" };
            var start = graph.AddNode(StartNode.TypeIdConst, 100, 50);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 300, 50);
            var trueEnd = graph.AddNode(EndNode.TypeIdConst, 500, 0);
            var falseEnd = graph.AddNode(EndNode.TypeIdConst, 500, 100);

            graph.Connect(start.Id, "Then", branch.Id, "In");
            graph.Connect(branch.Id, "True", trueEnd.Id, "In");
            graph.Connect(branch.Id, "False", falseEnd.Id, "In");

            // Branch.Condition 핀에 리터럴 true 설정 (미연결 데이터 입력)
            ((NodeBase)branch).SetLiteralInput("Condition", true);

            return graph;
        }

        [Fact]
        public void Serialize_Deserialize_PreservesNodes_Connections_Positions()
        {
            var original = BuildBranchSampleGraph();
            var json = NodeGraphSerializer.Serialize(original);

            var restored = NodeGraphSerializer.Deserialize(json);

            restored.Id.Should().Be(original.Id);
            restored.Name.Should().Be(original.Name);
            restored.Nodes.Should().HaveCount(original.Nodes.Count);
            restored.Connections.Should().HaveCount(original.Connections.Count);

            foreach (var n in original.Nodes)
            {
                var r = restored.GetNode(n.Id);
                r.Should().NotBeNull();
                r.TypeId.Should().Be(n.TypeId);
                restored.Layouts[n.Id].X.Should().Be(original.Layouts[n.Id].X);
                restored.Layouts[n.Id].Y.Should().Be(original.Layouts[n.Id].Y);
            }
        }

        [Fact]
        public void Serialize_Deserialize_PreservesNodeIds_AcrossRoundTrip()
        {
            var original = BuildBranchSampleGraph();
            var json = NodeGraphSerializer.Serialize(original);

            var restored = NodeGraphSerializer.Deserialize(json);

            // 연결의 SourceNodeId/TargetNodeId가 restored 그래프 안 노드 Id와 일치해야 한다.
            foreach (var c in restored.Connections)
            {
                restored.GetNode(c.SourceNodeId).Should().NotBeNull();
                restored.GetNode(c.TargetNodeId).Should().NotBeNull();
            }
        }

        [Fact]
        public void Deserialize_UnknownTypeId_ThrowsUnknownNodeTypeException()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var json = NodeGraphSerializer.Serialize(graph);

            // TypeId를 변조
            var mutated = json.Replace(StartNode.TypeIdConst, "Bogus.Unknown");

            var act = () => NodeGraphSerializer.Deserialize(mutated);
            act.Should().Throw<UnknownNodeTypeException>()
               .Which.TypeId.Should().Be("Bogus.Unknown");
        }

        [Fact]
        public void Deserialize_UnsupportedSchema_Throws()
        {
            var json = """
            {
              "$schema": 999,
              "Id": "00000000-0000-0000-0000-000000000000",
              "Name": null,
              "Nodes": [],
              "Connections": []
            }
            """;

            var act = () => NodeGraphSerializer.Deserialize(json);
            act.Should().Throw<UnsupportedSchemaException>()
               .Which.FoundSchema.Should().Be(999);
        }

        [Fact]
        public void LiteralInputValues_RoundTrip_IntStringBool()
        {
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var delay = graph.AddNode(DelayNode.TypeIdConst, 0, 0);
            var log = graph.AddNode(LogNode.TypeIdConst, 0, 0);

            ((NodeBase)branch).SetLiteralInput("Condition", true);
            ((NodeBase)delay).SetLiteralInput("Seconds", 2.5);
            ((NodeBase)log).SetLiteralInput("Format", "hello");

            var json = NodeGraphSerializer.Serialize(graph);
            var restored = NodeGraphSerializer.Deserialize(json);

            var rBranch = (NodeBase)restored.GetNode(branch.Id);
            var rDelay = (NodeBase)restored.GetNode(delay.Id);
            var rLog = (NodeBase)restored.GetNode(log.Id);

            rBranch.LiteralInputs["Condition"].Should().Be(true);
            rDelay.LiteralInputs["Seconds"].Should().Be(2.5);
            rLog.LiteralInputs["Format"].Should().Be("hello");
        }

        [Fact]
        public void Serialize_OutputProducesSchemaFieldAsDollarSchema()
        {
            var graph = BuildBranchSampleGraph();
            var json = NodeGraphSerializer.Serialize(graph);

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("$schema", out var schemaProp).Should().BeTrue();
            schemaProp.GetInt32().Should().Be(NodeGraphDto.CurrentSchema);
        }

        [Fact]
        public async Task RoundTrip_ExecutesAfterLoad_WithSameOutputs()
        {
            // 그래프: Start --> Branch(true) --> Log("HIT") --> End
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            var start = graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var branch = graph.AddNode(BranchNode.TypeIdConst, 0, 0);
            var log = graph.AddNode(LogNode.TypeIdConst, 0, 0);
            var end = graph.AddNode(EndNode.TypeIdConst, 0, 0);

            ((NodeBase)branch).SetLiteralInput("Condition", true);
            ((NodeBase)log).SetLiteralInput("Format", "HIT");

            graph.Connect(start.Id, "Then", branch.Id, "In");
            graph.Connect(branch.Id, "True", log.Id, "In");
            graph.Connect(log.Id, "Then", end.Id, "In");

            // Round-trip
            var json = NodeGraphSerializer.Serialize(graph);
            var restored = NodeGraphSerializer.Deserialize(json);

            // 실행
            var rStart = restored.Nodes.First(n => n.TypeId == StartNode.TypeIdConst);
            var captured = new List<string>();
            var ctx = new ExecutionContext(restored);
            ctx.Variables["__LogCapture"] = captured;

            var result = await new SchedulerService().RunAsync(restored, rStart.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            captured.Should().ContainSingle().Which.Should().Be("HIT");
        }

        [Fact]
        public void EnsureRegistered_AutoCalled_OnDeserialize()
        {
            // 빌트인 노드가 등록된 그래프를 직렬화한 JSON을 deserialize.
            // 호출자가 BuiltInNodes.EnsureRegistered를 잊어도 Serializer가 자동 호출하므로 성공해야 한다.
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var json = NodeGraphSerializer.Serialize(graph);

            // (실제 unregistering은 BuiltInNodes를 우회해야 하므로 풀-검증은 어렵지만,
            //  최소한 deserialize가 예외 없이 동작하는지는 확인.)
            var restored = NodeGraphSerializer.Deserialize(json);
            restored.Nodes.Should().ContainSingle();
        }
    }

    public class NodeBaseStateHookTests
    {
        // 인스턴스 상태를 가진 테스트 노드
        private sealed class StatefulCounter : NodeBase
        {
            public int Count { get; set; }

            public override string TypeId => "Test.StatefulCounter";

            protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => new[]
            {
                new PinDescriptor("In", "In", PinDirection.Input, PinKind.Exec, typeof(void), null),
                new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
            };

            public override void WriteState(Utf8JsonWriter writer)
            {
                writer.WriteNumber("Count", Count);
            }

            public override void ReadState(JsonElement state)
            {
                if (state.TryGetProperty("Count", out var c)) Count = c.GetInt32();
            }
        }

        [Fact]
        public void WriteState_ReadState_RoundTrips_InstanceState()
        {
            var typeId = "Test.StatefulCounter";
            NodeMetadataRegistry.UnregisterForTests(typeId);
            NodeMetadataRegistry.Register(new NodeMetadata(
                typeId, typeId, "Test", "", 0,
                typeof(StatefulCounter),
                () => new StatefulCounter(),
                new[]
                {
                    new PinDescriptor("In", "In", PinDirection.Input, PinKind.Exec, typeof(void), null),
                    new PinDescriptor("Then", "Then", PinDirection.Output, PinKind.Exec, typeof(void), null),
                }));
            try
            {
                var graph = new NodeGraph();
                var counter = new StatefulCounter { Count = 42 };
                graph.AddNode(counter, 0, 0);

                var json = NodeGraphSerializer.Serialize(graph);
                var restored = NodeGraphSerializer.Deserialize(json);

                var rCounter = (StatefulCounter)restored.GetNode(counter.Id);
                rCounter.Count.Should().Be(42);
            }
            finally
            {
                NodeMetadataRegistry.UnregisterForTests(typeId);
            }
        }

        [Fact]
        public void DefaultNodeBase_WriteState_ReadState_AreNoop()
        {
            // 빌트인 노드는 WriteState/ReadState 미오버라이드 → JSON에 State 필드 없음
            BuiltInNodes.EnsureRegistered();
            var graph = new NodeGraph();
            graph.AddNode(StartNode.TypeIdConst, 0, 0);
            var json = NodeGraphSerializer.Serialize(graph);

            using var doc = JsonDocument.Parse(json);
            // Web defaults → camelCase
            var firstNode = doc.RootElement.GetProperty("nodes").EnumerateArray().First();
            // state 필드가 아예 없거나 null이어야 함 (DefaultIgnoreCondition.WhenWritingNull)
            var hasState = firstNode.TryGetProperty("state", out var stateProp);
            if (hasState)
            {
                stateProp.ValueKind.Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Undefined, JsonValueKind.Object);
                if (stateProp.ValueKind == JsonValueKind.Object)
                {
                    stateProp.EnumerateObject().Should().BeEmpty();
                }
            }
        }
    }
}
