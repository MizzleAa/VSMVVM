using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.Core.Scheduler.Nodes;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// UserCodeCategoryExtractor — 컴파일된 어셈블리에서 [MethodNode(Category=...)] 빈도를 집계하여
    /// "이 코드 조각의 대표 카테고리" 를 결정한다.
    /// 사용자 코드 조각이 어느 팔레트 카테고리에 속하는지 자동 식별하는 용도.
    /// </summary>
    public sealed class UserCodeCategoryExtractorTests
    {
        // === 픽스처 — 각 시나리오를 대표하는 메서드 셋. 어셈블리 단위 reflection 으로 골라낼 수 있도록
        //    별도 정적 클래스로 격리해서 다른 테스트의 [MethodNode] 와 섞이지 않게 한다. ===

        public static class SingleOpenCv
        {
            [MethodNode("UCT.SingleOpenCv.A", Category = "OpenCV")]
            public static int A(int x) => x;
        }

        public static class AllOpenCv
        {
            [MethodNode("UCT.AllOpenCv.A", Category = "OpenCV")]
            public static int A(int x) => x;
            [MethodNode("UCT.AllOpenCv.B", Category = "OpenCV")]
            public static int B(int x) => x;
            [MethodNode("UCT.AllOpenCv.C", Category = "OpenCV")]
            public static int C(int x) => x;
        }

        public static class MixedOpenCvAndMath
        {
            [MethodNode("UCT.Mixed.A", Category = "OpenCV")]
            public static int A(int x) => x;
            [MethodNode("UCT.Mixed.B", Category = "OpenCV")]
            public static int B(int x) => x;
            [MethodNode("UCT.Mixed.C", Category = "Math")]
            public static int C(int x) => x;
        }

        public static class TieBreakerAlpha
        {
            // 2 vs 2 — 알파벳 순으로 "Alpha" 가 "Beta" 보다 앞이므로 PrimaryCategory == "Alpha".
            [MethodNode("UCT.Tie.A", Category = "Beta")]
            public static int A(int x) => x;
            [MethodNode("UCT.Tie.B", Category = "Beta")]
            public static int B(int x) => x;
            [MethodNode("UCT.Tie.C", Category = "Alpha")]
            public static int C(int x) => x;
            [MethodNode("UCT.Tie.D", Category = "Alpha")]
            public static int D(int x) => x;
        }

        public static class NoCategory
        {
            // Category 속성을 빈/null 로 두면 fallback "User Code" 로 처리되어야 한다.
            [MethodNode("UCT.NoCat.A")]
            public static int A(int x) => x;
            [MethodNode("UCT.NoCat.B")]
            public static int B(int x) => x;
        }

        public static class NoMethodNodeAttribute
        {
            // [MethodNode] 가 없는 클래스 — 어셈블리 reflection 시 카운트 0 이어야 한다.
            public static int Plain(int x) => x;
        }

        // === 테스트 ===

        [Fact]
        public void Single_method_with_category_returns_that_category_with_count_1()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(SingleOpenCv).Assembly,
                typeFilter: t => t == typeof(SingleOpenCv));

            result.PrimaryCategory.Should().Be("OpenCV");
            result.TotalMethodNodes.Should().Be(1);
            result.AllCategoryCounts.Should().ContainKey("OpenCV").WhoseValue.Should().Be(1);
        }

        [Fact]
        public void All_methods_in_same_category_returns_that_category_with_full_count()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(AllOpenCv).Assembly,
                typeFilter: t => t == typeof(AllOpenCv));

            result.PrimaryCategory.Should().Be("OpenCV");
            result.TotalMethodNodes.Should().Be(3);
            result.AllCategoryCounts["OpenCV"].Should().Be(3);
        }

        [Fact]
        public void Mixed_categories_returns_most_frequent()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(MixedOpenCvAndMath).Assembly,
                typeFilter: t => t == typeof(MixedOpenCvAndMath));

            result.PrimaryCategory.Should().Be("OpenCV"); // 2 > 1
            result.TotalMethodNodes.Should().Be(3);
            result.AllCategoryCounts["OpenCV"].Should().Be(2);
            result.AllCategoryCounts["Math"].Should().Be(1);
        }

        [Fact]
        public void Tied_frequencies_resolve_alphabetically_for_determinism()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(TieBreakerAlpha).Assembly,
                typeFilter: t => t == typeof(TieBreakerAlpha));

            // 2 vs 2 — 알파벳 순 첫 번째 = "Alpha".
            result.PrimaryCategory.Should().Be("Alpha");
            result.TotalMethodNodes.Should().Be(4);
        }

        [Fact]
        public void Methods_without_explicit_category_fallback_to_user_code()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(NoCategory).Assembly,
                typeFilter: t => t == typeof(NoCategory));

            // Category 비어있으면 NodePaletteService 의 fallback 과 일관되게 "User Code".
            result.PrimaryCategory.Should().Be(UserCodeCategoryExtractor.FallbackCategory);
            result.TotalMethodNodes.Should().Be(2);
            result.AllCategoryCounts[UserCodeCategoryExtractor.FallbackCategory].Should().Be(2);
        }

        [Fact]
        public void Assembly_without_any_method_node_returns_null_primary_and_zero_count()
        {
            var result = UserCodeCategoryExtractor.Extract(typeof(NoMethodNodeAttribute).Assembly,
                typeFilter: t => t == typeof(NoMethodNodeAttribute));

            result.PrimaryCategory.Should().BeNull();
            result.TotalMethodNodes.Should().Be(0);
            result.AllCategoryCounts.Should().BeEmpty();
        }

        [Fact]
        public void Null_assembly_throws()
        {
            var act = () => UserCodeCategoryExtractor.Extract(null);
            act.Should().Throw<System.ArgumentNullException>();
        }
    }
}
