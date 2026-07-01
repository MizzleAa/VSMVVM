using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Runtime;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.Core.Scheduler.Tests.Runtime
{
    /// <summary>
    /// LogNode 가 단일 string Message 가 아닌 Format 템플릿 + 가변 ArgN 핀을 받아
    /// string.Format 결과를 출력하는 새 동작 검증.
    /// </summary>
    public class LogNodeTemplateTests
    {
        [Fact]
        public void DefaultArgCount_IsOne()
        {
            var node = new LogNode();
            node.ArgCount.Should().Be(1, "기본 ArgCount=1 — Format=\"{0}\" 와 자연 매치");
        }

        [Fact]
        public void Pins_IncludeFormat_And_ArgN_ByArgCount()
        {
            var node = new LogNode { ArgCount = 3 };

            var pinIds = node.Pins.Select(p => p.Id).ToArray();
            pinIds.Should().Contain("In");
            pinIds.Should().Contain("Then");
            pinIds.Should().Contain("Format");
            pinIds.Should().Contain("Arg0");
            pinIds.Should().Contain("Arg1");
            pinIds.Should().Contain("Arg2");
            pinIds.Should().NotContain("Arg3");
        }

        [Fact]
        public void ChangingArgCount_RebuildsPins()
        {
            var node = new LogNode();
            node.Pins.Select(p => p.Id).Should().Contain("Arg0").And.NotContain("Arg1");

            node.ArgCount = 4;

            var ids = node.Pins.Select(p => p.Id).ToArray();
            ids.Should().Contain("Arg3");
            ids.Should().NotContain("Arg4");
        }

        [Fact]
        public async Task Execute_WithFormatAndArgs_WritesFormattedMessageToLogSink()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = (LogNode)g.AddNode(LogNode.TypeIdConst, 0, 0);
            log.ArgCount = 2;
            ((NodeBase)log).SetLiteralInput("Format", "x = {0}, y = {1}");
            ((NodeBase)log).SetLiteralInput("Arg0", 42);
            ((NodeBase)log).SetLiteralInput("Arg1", "abc");
            var end = g.AddNode(EndNode.TypeIdConst, 0, 0);
            g.Connect(start.Id, "Then", log.Id, "In");
            g.Connect(log.Id, "Then", end.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };
            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed);
            sink.GetAll().Should().Contain(e => e.Message == "x = 42, y = abc");
        }

        [Fact]
        public async Task Execute_ZeroArgs_FormatAsLiteralMessage()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = (LogNode)g.AddNode(LogNode.TypeIdConst, 0, 0);
            log.ArgCount = 0;
            ((NodeBase)log).SetLiteralInput("Format", "hello");
            g.Connect(start.Id, "Then", log.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };
            await new SchedulerService().RunAsync(g, start.Id, ctx);

            sink.GetAll().Should().Contain(e => e.Message == "hello");
        }

        [Fact]
        public async Task Execute_NullFormat_LogsEmptyString_NoThrow()
        {
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = g.AddNode(LogNode.TypeIdConst, 0, 0);
            // Format 핀에 literal 안 줌 — default = "{0}" 사용. 그리고 Arg0 도 안 줌 → object default = null.
            // string.Format("{0}", null) 의 결과는 "" (null arg 는 string.Empty).
            g.Connect(start.Id, "Then", log.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };
            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            result.Status.Should().Be(ExecutionStatus.Completed, "throw 없이 정상 완료");
            sink.GetAll().Should().Contain(e => e.NodeTypeId == LogNode.TypeIdConst, "LogNode 엔트리 1건은 있어야");
        }

        [Fact]
        public async Task Execute_FormatErrorPlaceholderOutOfRange_DoesNotCrashRun()
        {
            // Format 에 {1} 있지만 Arg0 1개만 — string.Format 이 FormatException. RunAsync 는 graceful 실패 처리.
            BuiltInNodes.EnsureRegistered();
            var g = new NodeGraph();
            var start = g.AddNode(StartNode.TypeIdConst, 0, 0);
            var log = (LogNode)g.AddNode(LogNode.TypeIdConst, 0, 0);
            log.ArgCount = 1;
            ((NodeBase)log).SetLiteralInput("Format", "{0} {1}");
            ((NodeBase)log).SetLiteralInput("Arg0", "only");
            g.Connect(start.Id, "Then", log.Id, "In");

            var sink = new InMemorySchedulerLogSink();
            var ctx = new ExecutionContext(g) { LogSink = sink };
            var result = await new SchedulerService().RunAsync(g, start.Id, ctx);

            // 노드 throw → 그래프 실패. 그러나 RunAsync 가 catch 해서 ExecutionStatus.Failed 로 정상 반환.
            result.Status.Should().Be(ExecutionStatus.Failed);
            result.Error.Should().BeOfType<FormatException>();
        }

        [Fact]
        public void WriteState_ReadState_PreservesArgCount()
        {
            var src = new LogNode { ArgCount = 5 };

            string json;
            using (var ms = new System.IO.MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    src.WriteState(w);
                    w.WriteEndObject();
                }
                json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }

            using var doc = JsonDocument.Parse(json);
            var dst = new LogNode();
            dst.ReadState(doc.RootElement);

            dst.ArgCount.Should().Be(5);
            dst.Pins.Select(p => p.Id).Should().Contain("Arg4").And.NotContain("Arg5");
        }
    }
}
