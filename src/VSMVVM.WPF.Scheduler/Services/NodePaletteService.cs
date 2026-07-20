using System;
using System.Collections.Generic;
using System.Linq;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;

namespace VSMVVM.WPF.Scheduler.Services
{
    /// <summary>
    /// NodeMetadataRegistry 기반 팔레트. 호출 시점의 등록 상태를 스냅샷으로 반환.
    /// 빌트인 노드 자동 등록을 보장 (EnsureRegistered).
    /// 카테고리 = NodeMetadata.Category 그대로. 사용자 코드 노드도 [MethodNode(Category="OpenCV")] 의 값을 따른다.
    /// (이전엔 CustomFunctionNode 를 무조건 "User Code" 로 묶었지만, 멀티 조각 에디터 도입 후 카테고리별 분리가 필요해 제거.)
    /// </summary>
    public sealed class NodePaletteService : INodePaletteService
    {
        /// <summary>메타데이터 Category 가 비어있을 때 fallback 카테고리. UserCodeCategoryExtractor.FallbackCategory 와 일치.</summary>
        public const string UserCodeCategory = "User Code";

        /// <summary>팔레트에 노출하지 않을 TypeId 집합. 라이브러리 등록은 유지 — 그래프 로드/AddNode 는 정상 동작.
        /// 호스트가 부트스트랩 시점에 채워 특정 빌트인 노드를 UI 에서만 숨긴다.</summary>
        public HashSet<string> HiddenTypeIds { get; } = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<NodePaletteCategory> GetCategories()
        {
            BuiltInNodes.EnsureRegistered();

            return NodeMetadataRegistry.All
                .Where(m => !HiddenTypeIds.Contains(m.TypeId))
                .GroupBy(EffectiveCategory)
                .OrderBy(g => g.Key)
                .Select(g => new NodePaletteCategory(
                    g.Key,
                    g.OrderBy(m => m.DisplayName)
                     .Select(m => new NodePaletteEntry(m.TypeId, m.DisplayName, EffectiveCategory(m), m.Description))
                     .ToArray()))
                .ToArray();
        }

        public NodePaletteEntry Find(string typeId)
        {
            BuiltInNodes.EnsureRegistered();
            var meta = NodeMetadataRegistry.Get(typeId);
            return meta == null ? null
                : new NodePaletteEntry(meta.TypeId, meta.DisplayName, EffectiveCategory(meta), meta.Description);
        }

        public IReadOnlyList<NodePaletteCategory> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return GetCategories();

            // 매치 기준: DisplayName / TypeId / Category 의 case-insensitive Contains.
            // 카테고리 grouping 유지 + 빈 카테고리 제외.
            return GetCategories()
                .Select(c => new NodePaletteCategory(
                    c.Name,
                    c.Entries.Where(e => Matches(e, query)).ToArray()))
                .Where(c => c.Entries.Count > 0)
                .ToArray();
        }

        private static bool Matches(NodePaletteEntry e, string query)
        {
            return (e.DisplayName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (e.TypeId?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (e.Category?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        /// <summary>
        /// 메타데이터의 Category 를 그대로 사용. 빌트인이든 사용자 코드든 동일한 규칙 — 사용자가 코드 안에서
        /// 명시한 [MethodNode(Category="...")] 가 곧 팔레트 카테고리.
        /// Category 가 빈/null 이면 "User Code" 로 fallback (UserCodeCategoryExtractor 정책과 일관).
        /// </summary>
        private static string EffectiveCategory(NodeMetadata m)
        {
            return string.IsNullOrEmpty(m.Category) ? UserCodeCategory : m.Category;
        }
    }
}
