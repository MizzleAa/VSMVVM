using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Serialization
{
    /// <summary>NodeGraph의 JSON 표현. $schema 필드로 마이그레이션을 위한 버전 추적.</summary>
    public sealed class NodeGraphDto
    {
        /// <summary>현재 코드가 지원하는 스키마 버전.</summary>
        public const int CurrentSchema = 1;

        [JsonPropertyName("$schema")]
        public int Schema { get; set; } = CurrentSchema;

        public Guid Id { get; set; }
        public string Name { get; set; }

        public List<NodeDto> Nodes { get; set; } = new();
        public List<ConnectionDto> Connections { get; set; } = new();

        /// <summary>그래프 변수 정의. UE Blueprint 의 Variables 패널.</summary>
        public List<GraphVariableDto> Variables { get; set; } = new();

        /// <summary>호스트 자유 메타데이터. Core 는 통과만 (키 의미 모름).</summary>
        public Dictionary<string, JsonElement> Extras { get; set; } = new();
    }

    /// <summary>그래프 변수의 JSON 표현. StableName 으로 타입 안정성 확보.</summary>
    public sealed class GraphVariableDto
    {
        public string Name { get; set; }
        /// <summary>PinTypeInfo.ComputeStableName 결과. 역직렬화 시 Type 으로 환원.</summary>
        public string TypeStableName { get; set; }
        public JsonElement DefaultValue { get; set; }
    }

    /// <summary>그래프에 속한 한 노드의 JSON 표현.</summary>
    public sealed class NodeDto
    {
        public Guid Id { get; set; }
        public string TypeId { get; set; }

        /// <summary>레이아웃 좌표 (그래프의 NodeLayout에 1:1 매핑).</summary>
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>미연결 데이터 입력 핀의 사용자 리터럴 값. 키=핀 id.</summary>
        public Dictionary<string, JsonElement> Inputs { get; set; } = new();

        /// <summary>NodeBase.WriteState/ReadState 후크용 자유 형식 상태. 미사용 시 null (Undefined JsonElement는 serializer가 거부함).</summary>
        public JsonElement? State { get; set; }
    }

    /// <summary>그래프 연결의 JSON 표현.</summary>
    public sealed class ConnectionDto
    {
        public Guid Id { get; set; }
        public Guid SourceNodeId { get; set; }
        public string SourcePinId { get; set; }
        public Guid TargetNodeId { get; set; }
        public string TargetPinId { get; set; }
        public PinKind Kind { get; set; }
    }
}
