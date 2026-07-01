using System;

namespace VSMVVM.Core.Scheduler.Serialization
{
    /// <summary>역직렬화 도중 NodeMetadataRegistry에 등록되지 않은 TypeId를 만났습니다.</summary>
    public sealed class UnknownNodeTypeException : Exception
    {
        public string TypeId { get; }
        public Guid NodeId { get; }

        public UnknownNodeTypeException(string typeId, Guid nodeId)
            : base($"Unknown node type id '{typeId}' for node {nodeId}. " +
                   "Register the node type (BuiltInNodes.EnsureRegistered / CustomNodeFactory.RegisterFromAssembly / source generator) before deserializing.")
        {
            TypeId = typeId;
            NodeId = nodeId;
        }
    }

    /// <summary>NodeGraphDto의 $schema 값이 현재 코드가 지원하지 않는 버전입니다.</summary>
    public sealed class UnsupportedSchemaException : Exception
    {
        public int FoundSchema { get; }
        public int SupportedSchema { get; }

        public UnsupportedSchemaException(int found, int supported)
            : base($"NodeGraph JSON $schema {found} is not supported (current code supports {supported}).")
        {
            FoundSchema = found;
            SupportedSchema = supported;
        }
    }
}
