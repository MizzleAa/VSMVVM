using System.Collections.Generic;

namespace VSMVVM.WPF.Scheduler.Services
{
    /// <summary>
    /// 노드 팔레트 (NodeMetadataRegistry에 등록된 노드 타입 목록을 사용자에게 보여주는 UI 서비스)의 추상화.
    /// 컨텍스트 메뉴, 사이드바, 검색 결과 등 다양한 UX에서 같은 데이터를 사용.
    /// </summary>
    public interface INodePaletteService
    {
        /// <summary>등록된 모든 노드 타입을 카테고리별로 묶어 반환.</summary>
        IReadOnlyList<NodePaletteCategory> GetCategories();

        /// <summary>TypeId로 한 항목을 찾는다.</summary>
        NodePaletteEntry Find(string typeId);

        /// <summary>
        /// 부분 일치 검색. DisplayName / TypeId / Category 중 하나라도 query 를 case-insensitive Contains 하면 매치.
        /// 매치된 항목만 가진 카테고리들을 반환 (카테고리 grouping 유지, 빈 카테고리 제외).
        /// 빈/null query → <see cref="GetCategories"/> 와 동일.
        /// </summary>
        IReadOnlyList<NodePaletteCategory> Search(string query);
    }

    /// <summary>팔레트의 한 항목 (한 노드 타입).</summary>
    public sealed class NodePaletteEntry
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }

        public NodePaletteEntry(string typeId, string displayName, string category, string description)
        {
            TypeId = typeId ?? string.Empty;
            DisplayName = displayName ?? typeId ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
        }
    }

    /// <summary>같은 Category를 가진 항목들의 묶음.</summary>
    public sealed class NodePaletteCategory
    {
        public string Name { get; }
        public IReadOnlyList<NodePaletteEntry> Entries { get; }

        public NodePaletteCategory(string name, IReadOnlyList<NodePaletteEntry> entries)
        {
            Name = name ?? string.Empty;
            Entries = entries ?? System.Array.Empty<NodePaletteEntry>();
        }
    }
}
