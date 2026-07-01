using System.Text.Json;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Serialization;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Serialization
{
    /// <summary>
    /// NodeGraph.Extras — 호스트가 자유롭게 메타데이터를 JSON 안에 같이 보관/로드.
    /// 한 파일로 모든 것 (Sample 의 UserCode 보존 같은 시나리오).
    /// </summary>
    public class GraphExtrasRoundTripTests
    {
        public GraphExtrasRoundTripTests() => BuiltInNodes.EnsureRegistered();

        [Fact]
        public void Extras_String_RoundTrip()
        {
            var g = new NodeGraph();
            g.Extras["userCode"] = JsonSerializer.SerializeToElement("hello world");

            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);

            loaded.Extras.Should().ContainKey("userCode");
            loaded.Extras["userCode"].GetString().Should().Be("hello world");
        }

        [Fact]
        public void Extras_Multiple_Keys_Preserved()
        {
            var g = new NodeGraph();
            g.Extras["a"] = JsonSerializer.SerializeToElement(42);
            g.Extras["b"] = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 });

            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);

            loaded.Extras["a"].GetInt32().Should().Be(42);
            loaded.Extras["b"].EnumerateArray().Should().HaveCount(3);
        }

        [Fact]
        public void Extras_Empty_NoExtraJsonNoise()
        {
            // 비어있으면 직렬화 JSON 에 'extras' 키가 빈 객체로만 — 라운드트립 후에도 비어있음.
            var g = new NodeGraph();
            var json = NodeGraphSerializer.Serialize(g);
            var loaded = NodeGraphSerializer.Deserialize(json);
            loaded.Extras.Should().BeEmpty();
        }
    }
}
