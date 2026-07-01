using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 어셈블리 로드 시 ModuleInitializer가 채우는 글로벌 노드 타입 레지스트리.
    /// 직렬화/팔레트/팩토리가 모두 이 레지스트리를 통해 노드 타입을 해석합니다.
    /// </summary>
    public static class NodeMetadataRegistry
    {
        private static readonly ConcurrentDictionary<string, NodeMetadata> _byTypeId =
            new ConcurrentDictionary<string, NodeMetadata>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<Type, NodeMetadata> _byClrType =
            new ConcurrentDictionary<Type, NodeMetadata>();

        /// <summary>
        /// 노드 메타데이터 등록. 중복된 TypeId면 InvalidOperationException.
        /// </summary>
        public static void Register(NodeMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            if (!_byTypeId.TryAdd(metadata.TypeId, metadata))
            {
                throw new InvalidOperationException(
                    $"Node type id '{metadata.TypeId}' is already registered.");
            }
            _byClrType[metadata.ClrType] = metadata;
        }

        /// <summary>등록되지 않은 typeId면 null.</summary>
        public static NodeMetadata Get(string typeId)
        {
            if (typeId == null) return null;
            return _byTypeId.TryGetValue(typeId, out var m) ? m : null;
        }

        public static NodeMetadata GetByClrType(Type clrType)
        {
            if (clrType == null) return null;
            return _byClrType.TryGetValue(clrType, out var m) ? m : null;
        }

        public static IReadOnlyList<NodeMetadata> All => _byTypeId.Values.ToArray();

        /// <summary>
        /// 테스트 전용: 특정 TypeId의 등록만 제거. ModuleInitializer가 한 번만 실행되므로
        /// 글로벌 등록을 보존하기 위해 selective unregister 만 제공.
        /// </summary>
        public static bool UnregisterForTests(string typeId)
        {
            if (typeId == null) return false;
            if (!_byTypeId.TryRemove(typeId, out var meta)) return false;
            _byClrType.TryRemove(meta.ClrType, out _);
            return true;
        }
    }
}
