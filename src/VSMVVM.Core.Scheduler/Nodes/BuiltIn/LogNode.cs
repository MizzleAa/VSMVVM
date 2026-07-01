using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// Format 템플릿 + 가변 Arg0/Arg1/... 핀을 받아 <c>string.Format(format, args)</c> 결과를
    /// ILoggerService + SchedulerLogPanel(LogSink) 양쪽에 기록 후 Then 발화.
    /// <para>
    /// <see cref="ArgCount"/> 가 핀 수를 결정 — 기본 1. 변경 시 핀 즉시 재생성 (NodeBase.InvalidatePins).
    /// Arg* 핀 타입은 <c>object</c> — 어떤 자료형이든 받아 .ToString() 결과를 템플릿에 삽입.
    /// </para>
    /// </summary>
    public sealed class LogNode : NodeBase, IDynamicPinCountNode
    {
        public const string TypeIdConst = "Core.Log";
        public override string TypeId => TypeIdConst;

        private int _argCount = 1;

        /// <summary>가변 Arg 핀 개수. 0 이상. 변경 시 핀 즉시 재생성.</summary>
        public int ArgCount
        {
            get => _argCount;
            set
            {
                if (value < 0) value = 0;
                if (_argCount == value) return;
                _argCount = value;
                InvalidatePins();
            }
        }

        // IDynamicPinCountNode — 인스펙터의 +/- 버튼이 ArgCount 를 가변 핀 수로 다룸.
        int IDynamicPinCountNode.DynamicPinCount
        {
            get => ArgCount;
            set => ArgCount = value;
        }
        string IDynamicPinCountNode.DynamicPinCountLabel => "Args";
        int IDynamicPinCountNode.MinDynamicPinCount => 0;
        int IDynamicPinCountNode.MaxDynamicPinCount => 16;

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors()
        {
            // 고정 3개 + ArgCount 개 = 3 + N.
            // (구 Message 핀은 제거 — Format + Args 단일 흐름. 구 JSON 의 Message 는 NodeGraphSerializer 가 Format 으로 마이그레이션.)
            var list = new List<PinDescriptor>(3 + _argCount)
            {
                new PinDescriptor("In",     "In",     PinDirection.Input,  PinKind.Exec, typeof(void),   null),
                new PinDescriptor("Format", "Format", PinDirection.Input,  PinKind.Data, typeof(string), "{0}"),
                new PinDescriptor("Then",   "Then",   PinDirection.Output, PinKind.Exec, typeof(void),   null),
            };
            for (int i = 0; i < _argCount; i++)
            {
                list.Add(new PinDescriptor($"Arg{i}", $"Arg{i}", PinDirection.Input, PinKind.Data, typeof(object), null));
            }
            return list;
        }

        public override Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            var format = context.GetInput<string>(this, "Format") ?? string.Empty;
            string msg;
            if (_argCount == 0)
            {
                msg = format; // 인자 없으면 템플릿 자체가 메시지.
            }
            else
            {
                var args = new object[_argCount];
                for (int i = 0; i < _argCount; i++)
                {
                    args[i] = context.GetInput<object>(this, $"Arg{i}");
                }
                msg = string.Format(CultureInfo.InvariantCulture, format, args);
            }

            context.Logger?.Info(msg);
            context.LogSink?.Write(new SchedulerLogEntry(
                DateTimeOffset.UtcNow, SchedulerLogLevel.Info,
                context.RunId, Id, TypeIdConst, msg, exception: null));

            if (context.Variables.TryGetValue("__LogCapture", out var existing) && existing is List<string> list)
            {
                list.Add(msg);
            }
            return Task.FromResult(ExecutionFlow.Continue("Then"));
        }

        // === 직렬화 — ArgCount 보존 ===

        public override void WriteState(Utf8JsonWriter writer)
        {
            writer.WriteNumber("argCount", _argCount);
        }

        public override void ReadState(JsonElement state)
        {
            if (state.ValueKind != JsonValueKind.Object) return;
            if (state.TryGetProperty("argCount", out var ac) && ac.ValueKind == JsonValueKind.Number)
            {
                ArgCount = ac.GetInt32(); // setter 가 InvalidatePins 호출.
            }
        }

        // 팔레트 등록용 spec — default ArgCount=1 인 노드의 표면. 인스턴스가 ArgCount 변경하면 그 인스턴스의
        // Pins 만 갱신 (metadata 의 spec 은 변경 X — 팔레트 표시/직렬화 호환 용도).
        internal static readonly PinDescriptor[] DefaultPinSpec = new[]
        {
            new PinDescriptor("In",     "In",     PinDirection.Input,  PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Format", "Format", PinDirection.Input,  PinKind.Data, typeof(string), "{0}"),
            new PinDescriptor("Then",   "Then",   PinDirection.Output, PinKind.Exec, typeof(void),   null),
            new PinDescriptor("Arg0",   "Arg0",   PinDirection.Input,  PinKind.Data, typeof(object), null),
        };

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Log", "Diagnostics",
            "string.Format(Format, Arg0..ArgN-1) 결과를 LogSink + Logger 에 기록.",
            0, typeof(LogNode), () => new LogNode(), DefaultPinSpec);
    }
}
