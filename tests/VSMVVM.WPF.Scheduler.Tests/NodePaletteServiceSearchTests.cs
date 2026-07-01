using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.Services;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// 팔레트 검색 — Search(query) 가 DisplayName / TypeId / Category 의 case-insensitive Contains 로 필터.
    /// 빈 query → GetCategories() 와 동일.
    /// </summary>
    [Collection(nameof(PaletteRegistryCollection))]
    public class NodePaletteServiceSearchTests
    {
        public NodePaletteServiceSearchTests() => BuiltInNodes.EnsureRegistered();

        [Fact]
        public void Search_EmptyQuery_ReturnsAllCategories()
        {
            var svc = new NodePaletteService();

            var all = svc.GetCategories();
            var none = svc.Search("");
            var nullQ = svc.Search(null);

            none.Should().BeEquivalentTo(all);
            nullQ.Should().BeEquivalentTo(all);
        }

        [Fact]
        public void Search_MatchesDisplayName_CaseInsensitive()
        {
            var svc = new NodePaletteService();

            var result = svc.Search("LOG");

            // LogNode 의 DisplayName="Log" 포함 — 결과에 그 entry 만 가진 카테고리가 있어야.
            result.Should().NotBeEmpty();
            result.SelectMany(c => c.Entries).Should().Contain(e => e.TypeId == LogNode.TypeIdConst);
            // "Log" 가 안 들어간 다른 노드들 (Start, End) 는 entry 에서 빠져야.
            result.SelectMany(c => c.Entries).Should().NotContain(e => e.TypeId == StartNode.TypeIdConst);
        }

        [Fact]
        public void Search_MatchesTypeId()
        {
            var svc = new NodePaletteService();

            var result = svc.Search("Core.Branch");

            result.SelectMany(c => c.Entries).Should().Contain(e => e.TypeId == BranchNode.TypeIdConst);
        }

        [Fact]
        public void Search_MatchesCategory()
        {
            var svc = new NodePaletteService();

            // "Flow" 카테고리의 모든 entry 가 들어와야.
            var result = svc.Search("flow");

            result.Should().Contain(c => c.Name == "Flow");
            result.First(c => c.Name == "Flow").Entries.Should().NotBeEmpty();
        }

        [Fact]
        public void Search_NoMatch_ReturnsEmpty()
        {
            var svc = new NodePaletteService();

            var result = svc.Search("__nothing_matches_zzz__");

            result.Should().BeEmpty();
        }

        [Fact]
        public void Search_PreservesCategoryGrouping()
        {
            var svc = new NodePaletteService();

            // "Log" 검색은 Diagnostics 카테고리의 LogNode 만 매치 — 그 카테고리 안에 entry 가 들어가야 함
            // (entry 가 평면 list 가 아니라 카테고리별로 그룹화 유지).
            var result = svc.Search("Log");

            var diagnostics = result.FirstOrDefault(c => c.Name == "Diagnostics");
            diagnostics.Should().NotBeNull();
            diagnostics.Entries.Should().Contain(e => e.TypeId == LogNode.TypeIdConst);
        }
    }
}
