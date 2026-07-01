using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using VSMVVM.Core.Scheduler.Graph;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Serialization
{
    /// <summary>
    /// NodeGraph &lt;-&gt; JSON 직렬화. PoC 단계의 라운드트립을 보장합니다.
    ///
    /// 보존 항목:
    ///   - 노드 (Id, TypeId, 위치 X/Y)
    ///   - 연결 (Id, source/target node+pin, Kind)
    ///   - 미연결 입력 핀의 사용자 LiteralInputs
    ///   - NodeBase.WriteState/ReadState 후크로 노출된 인스턴스 상태
    ///
    /// 미보존:
    ///   - 실행 컨텍스트(ExecutionContext.DataCache)
    ///   - 노드 클래스 자체 (NodeMetadataRegistry에 호출자가 사전 등록해야 함)
    /// </summary>
    public static class NodeGraphSerializer
    {
        public static string Serialize(NodeGraph graph, JsonSerializerOptions options = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var dto = ToDto(graph, options ?? NodeGraphJsonOptions.Default);
            return JsonSerializer.Serialize(dto, options ?? NodeGraphJsonOptions.Default);
        }

        public static NodeGraph Deserialize(string json, JsonSerializerOptions options = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var dto = JsonSerializer.Deserialize<NodeGraphDto>(json, options ?? NodeGraphJsonOptions.Default);
            return FromDto(dto, options ?? NodeGraphJsonOptions.Default);
        }

        public static void Save(NodeGraph graph, Stream stream, JsonSerializerOptions options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            var json = Serialize(graph, options);
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static NodeGraph Load(Stream stream, JsonSerializerOptions options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var json = reader.ReadToEnd();
            return Deserialize(json, options);
        }

        #region ToDto

        private static NodeGraphDto ToDto(NodeGraph graph, JsonSerializerOptions options)
        {
            var dto = new NodeGraphDto
            {
                Schema = NodeGraphDto.CurrentSchema,
                Id = graph.Id,
                Name = graph.Name,
            };

            foreach (var node in graph.Nodes)
            {
                var layout = graph.Layouts.TryGetValue(node.Id, out var l) ? l : new NodeLayout(0, 0);
                var nodeDto = new NodeDto
                {
                    Id = node.Id,
                    TypeId = node.TypeId,
                    X = layout.X,
                    Y = layout.Y,
                };

                // LiteralInputs 직렬화. 키별로 SerializeToElement.
                if (node is NodeBase nb)
                {
                    foreach (var kv in nb.LiteralInputs)
                    {
                        nodeDto.Inputs[kv.Key] = JsonSerializer.SerializeToElement(kv.Value, options);
                    }

                    // WriteState 후크
                    using var ms = new MemoryStream();
                    using (var writer = new Utf8JsonWriter(ms))
                    {
                        writer.WriteStartObject();
                        nb.WriteState(writer);
                        writer.WriteEndObject();
                    }
                    var stateBytes = ms.ToArray();
                    using var stateDoc = JsonDocument.Parse(stateBytes);
                    // 빈 객체가 아닌 경우에만 State 설정 (clutter 방지)
                    if (stateDoc.RootElement.EnumerateObject().MoveNext())
                    {
                        nodeDto.State = stateDoc.RootElement.Clone();
                    }
                }

                dto.Nodes.Add(nodeDto);
            }

            foreach (var c in graph.Connections)
            {
                dto.Connections.Add(new ConnectionDto
                {
                    Id = c.Id,
                    SourceNodeId = c.SourceNodeId,
                    SourcePinId = c.SourcePinId,
                    TargetNodeId = c.TargetNodeId,
                    TargetPinId = c.TargetPinId,
                    Kind = c.Kind,
                });
            }

            // 그래프 변수 정의
            foreach (var kv in graph.Variables)
            {
                var v = kv.Value;
                // DefaultValue 가 직렬화 불가 타입(예: OpenCvSharp.Mat — 포인터/ref struct 필드 포함) 이면
                // SerializeToElement 가 NotSupportedException 을 던질 수 있으므로 fallback 으로 null element 저장.
                // Load 시에는 이 null 이 그대로 default 처리됨.
                JsonElement defaultElem;
                try
                {
                    defaultElem = JsonSerializer.SerializeToElement(v.DefaultValue, options);
                }
                catch
                {
                    defaultElem = JsonDocument.Parse("null").RootElement.Clone();
                }
                dto.Variables.Add(new GraphVariableDto
                {
                    Name = v.Name,
                    TypeStableName = PinTypeInfo.ComputeStableName(v.ClrType),
                    DefaultValue = defaultElem,
                });
            }

            // Extras — 호스트 메타데이터 그대로 통과.
            foreach (var kv in graph.Extras)
            {
                dto.Extras[kv.Key] = kv.Value;
            }

            return dto;
        }

        #endregion

        #region FromDto

        private static NodeGraph FromDto(NodeGraphDto dto, JsonSerializerOptions options)
        {
            if (dto == null) throw new InvalidOperationException("Deserialized NodeGraphDto is null.");
            if (dto.Schema != NodeGraphDto.CurrentSchema)
            {
                throw new UnsupportedSchemaException(dto.Schema, NodeGraphDto.CurrentSchema);
            }

            // 빌트인 노드 자동 등록 (호출자가 잊었을 경우 안전망)
            BuiltInNodes.EnsureRegistered();

            var graph = new NodeGraph(dto.Id) { Name = dto.Name };

            // 노드 생성. Id는 dto에서 복원.
            // pin 타입을 알아야 Inputs JsonElement → 강타입 변환을 할 수 있으므로
            // NodeMetadataRegistry.Pins를 참고하여 매핑한다.
            foreach (var nodeDto in dto.Nodes)
            {
                var meta = NodeMetadataRegistry.Get(nodeDto.TypeId)
                    ?? throw new UnknownNodeTypeException(nodeDto.TypeId, nodeDto.Id);

                var node = meta.Factory();
                if (!(node is NodeBase nb))
                {
                    throw new InvalidOperationException(
                        $"Node type '{nodeDto.TypeId}' factory must produce a NodeBase-derived instance for deserialization.");
                }

                nb.SetIdForDeserialization(nodeDto.Id);

                // LiteralInputs 복원
                if (nodeDto.Inputs != null)
                {
                    foreach (var kv in nodeDto.Inputs)
                    {
                        // 구 LogNode 호환: "Message" 키 → "Format" 으로 자동 마이그레이션.
                        // (Phase I 에서 LogNode 의 Message 핀 제거 — Format + Args 단일 흐름.)
                        var pinKey = (nodeDto.TypeId == "Core.Log" && kv.Key == "Message") ? "Format" : kv.Key;

                        var pinDesc = FindPinDescriptor(meta.Pins, pinKey);
                        if (pinDesc == null) continue; // 알 수 없는 핀은 무시
                        if (pinDesc.Kind != PinKind.Data || pinDesc.Direction != PinDirection.Input) continue;

                        // null/undefined 면 메타데이터 수집 시도조차 안 함 (직렬화 불가 타입에서 NotSupportedException 방지).
                        if (kv.Value.ValueKind == JsonValueKind.Null || kv.Value.ValueKind == JsonValueKind.Undefined)
                        {
                            continue;
                        }
                        object value;
                        try
                        {
                            value = kv.Value.Deserialize(pinDesc.ValueType, options);
                        }
                        catch
                        {
                            // JsonException(타입 불일치) + NotSupportedException(직렬화 불가 타입) 모두 스킵.
                            continue;
                        }
                        if (value != null)
                        {
                            nb.SetLiteralInput(pinKey, value);
                        }
                    }
                }

                // WriteState/ReadState 후크
                if (nodeDto.State.HasValue && nodeDto.State.Value.ValueKind == JsonValueKind.Object)
                {
                    nb.ReadState(nodeDto.State.Value);
                }

                graph.AddNode(nb, nodeDto.X, nodeDto.Y);
            }

            // 연결 복원
            foreach (var cDto in dto.Connections)
            {
                graph.Connect(cDto.SourceNodeId, cDto.SourcePinId, cDto.TargetNodeId, cDto.TargetPinId);
            }

            // 변수 정의 복원
            if (dto.Variables != null)
            {
                foreach (var vDto in dto.Variables)
                {
                    var t = PinTypeInfo.ResolveStableName(vDto.TypeStableName);
                    if (t == null) continue; // 알 수 없는 타입은 스킵 (호스트가 외부 타입 미등록)

                    object defaultVal = null;
                    var elem = vDto.DefaultValue;
                    // null/undefined element 는 메타데이터 수집 자체를 피하기 위해 Deserialize 호출하지 않음 —
                    // OpenCvSharp.Mat 같은 직렬화 불가(포인터/ref struct) 타입에서도 안전하게 default 처리.
                    if (elem.ValueKind != JsonValueKind.Null && elem.ValueKind != JsonValueKind.Undefined)
                    {
                        try { defaultVal = elem.Deserialize(t, options); }
                        catch { /* 직렬화 불가/타입 불일치 시 default(null) */ }
                    }
                    graph.AddVariable(vDto.Name, t, defaultVal);
                }
            }

            // Extras 복원 — 호스트가 키 의미를 알고 활용.
            if (dto.Extras != null)
            {
                foreach (var kv in dto.Extras)
                {
                    graph.Extras[kv.Key] = kv.Value;
                }
            }

            return graph;
        }

        private static PinDescriptor FindPinDescriptor(IReadOnlyList<PinDescriptor> pins, string pinId)
        {
            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i].Id == pinId) return pins[i];
            }
            return null;
        }

        #endregion
    }
}
