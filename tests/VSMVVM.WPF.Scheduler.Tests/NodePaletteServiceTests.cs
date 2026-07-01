using System.Linq;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.WPF.Scheduler.Services;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Tests
{
    [Collection(nameof(PaletteRegistryCollection))]
    public class NodePaletteServiceTests
    {
        [Fact]
        public void GetCategories_GroupsBuiltInNodes_ByCategory_AndOrders()
        {
            BuiltInNodes.EnsureRegistered();
            var svc = new NodePaletteService();

            var categories = svc.GetCategories();

            categories.Should().NotBeEmpty();
            // Flow / Diagnostics 카테고리에 빌트인 노드가 있어야 한다
            var flow = categories.FirstOrDefault(c => c.Name == "Flow");
            flow.Should().NotBeNull();
            flow.Entries.Should().Contain(e => e.TypeId == StartNode.TypeIdConst);
            flow.Entries.Should().Contain(e => e.TypeId == BranchNode.TypeIdConst);

            var diag = categories.FirstOrDefault(c => c.Name == "Diagnostics");
            diag.Should().NotBeNull();
            diag.Entries.Should().Contain(e => e.TypeId == LogNode.TypeIdConst);
        }

        [Fact]
        public void Find_ReturnsRegisteredEntry()
        {
            var svc = new NodePaletteService();

            var entry = svc.Find(StartNode.TypeIdConst);

            entry.Should().NotBeNull();
            entry.TypeId.Should().Be(StartNode.TypeIdConst);
            entry.Category.Should().Be("Flow");
        }

        [Fact]
        public void Find_UnknownTypeId_ReturnsNull()
        {
            var svc = new NodePaletteService();

            svc.Find("Bogus.Unknown").Should().BeNull();
        }

        // === 사용자 코드 노드 카테고리 ===
        // 정책: 멀티 조각 에디터 도입 이후, 사용자 노드도 [MethodNode(Category="...")] 의 값을 그대로 팔레트 카테고리로 사용.
        // (이전엔 무조건 "User Code" 로 묶었지만, 카테고리별 조각 관리가 가능해졌으니 분류 의도를 살리는 게 자연스러움.)

        [Fact]
        public void CustomFunctionNode_UsesMetadataCategory_AsIs()
        {
            try
            {
                BuiltInNodes.EnsureRegistered();
                // 사용자가 [MethodNode(Category="Math")] 로 등록한 함수 노드를 시뮬레이션.
                var method = typeof(NodePaletteServiceTests).GetMethod(nameof(DummyAdd),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var pins = VSMVVM.Core.Scheduler.Nodes.SignatureToPinsBuilder.Build(method);
                var meta = new VSMVVM.Core.Scheduler.Nodes.NodeMetadata(
                    "Test.UserDummyAdd", "DummyAdd", "Math", "user-defined", 0,
                    typeof(VSMVVM.Core.Scheduler.Nodes.CustomFunctionNode),
                    () => new VSMVVM.Core.Scheduler.Nodes.CustomFunctionNode("Test.UserDummyAdd", method, pins),
                    pins);
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.Register(meta);

                var svc = new NodePaletteService();
                var categories = svc.GetCategories();

                var math = categories.FirstOrDefault(c => c.Name == "Math");
                math.Should().NotBeNull("사용자 노드의 Category(Math) 가 그대로 팔레트 카테고리가 되어야 함");
                math.Entries.Should().Contain(e => e.TypeId == "Test.UserDummyAdd");
            }
            finally
            {
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests("Test.UserDummyAdd");
            }
        }

        [Fact]
        public void CustomFunctionNode_WithoutCategory_FallsBackToUserCode()
        {
            try
            {
                BuiltInNodes.EnsureRegistered();
                // Category 가 빈 사용자 노드는 fallback "User Code" 로.
                var method = typeof(NodePaletteServiceTests).GetMethod(nameof(DummyAdd),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var pins = VSMVVM.Core.Scheduler.Nodes.SignatureToPinsBuilder.Build(method);
                var meta = new VSMVVM.Core.Scheduler.Nodes.NodeMetadata(
                    "Test.NoCatDummy", "NoCatDummy", category: "", "user-defined", 0,
                    typeof(VSMVVM.Core.Scheduler.Nodes.CustomFunctionNode),
                    () => new VSMVVM.Core.Scheduler.Nodes.CustomFunctionNode("Test.NoCatDummy", method, pins),
                    pins);
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.Register(meta);

                var svc = new NodePaletteService();
                var userCode = svc.GetCategories().FirstOrDefault(c => c.Name == NodePaletteService.UserCodeCategory);

                userCode.Should().NotBeNull();
                userCode.Entries.Should().Contain(e => e.TypeId == "Test.NoCatDummy");
            }
            finally
            {
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests("Test.NoCatDummy");
            }
        }

        [Fact]
        public void CustomConstantNode_UsesMetadataCategory_AsIs()
        {
            try
            {
                BuiltInNodes.EnsureRegistered();
                var method = typeof(NodePaletteServiceTests).GetMethod(nameof(DummyPi),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var pins = VSMVVM.Core.Scheduler.Nodes.SignatureToPinsBuilder.BuildAsConstant(method);
                var meta = new VSMVVM.Core.Scheduler.Nodes.NodeMetadata(
                    "Test.UserDummyPi", "DummyPi", "Math", "user constant", 0,
                    typeof(VSMVVM.Core.Scheduler.Nodes.CustomConstantNode),
                    () => new VSMVVM.Core.Scheduler.Nodes.CustomConstantNode("Test.UserDummyPi", method, pins),
                    pins);
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.Register(meta);

                var svc = new NodePaletteService();
                var math = svc.GetCategories().FirstOrDefault(c => c.Name == "Math");

                math.Should().NotBeNull();
                math.Entries.Should().Contain(e => e.TypeId == "Test.UserDummyPi");
            }
            finally
            {
                VSMVVM.Core.Scheduler.Nodes.NodeMetadataRegistry.UnregisterForTests("Test.UserDummyPi");
            }
        }

        // 테스트용 더미 메소드 — Constant-like 와 Function-like 양쪽 시그니처.
        private static int DummyAdd(int a, int b) => a + b;
        private static double DummyPi() => 3.14;
    }
}
