using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using VSMVVM.Core.MVVM;

namespace VSMVVM.Core.Tests.MVVM
{
    /// <summary>
    /// IRegionManager 네비게이션 히스토리 (Back/Forward) 및 DisplayName 변환 테스트.
    /// </summary>
    public class RegionManagerTests
    {
        #region Test Doubles

        private sealed class MockRegion : IRegion
        {
            public object Content { get; set; }
        }

        private class ViewA { }
        private class ViewB { }
        private class ViewC { }
        private class DefaultDesignView { }
        private class SourceGenView { }
        private class MultiWindowView { }

        private sealed class TrackingNavigateAwareVm : INavigateAware
        {
            public bool AllowNavigateIn { get; set; } = true;
            public int OnNavigatedFromCount { get; private set; }
            public int OnNavigatedToCount { get; private set; }

            public bool CanNavigate(NavigationContext context) => AllowNavigateIn;
            public void OnNavigatedFrom(NavigationContext context) => OnNavigatedFromCount++;
            public void OnNavigatedTo(NavigationContext context) => OnNavigatedToCount++;
        }

        // RegionManager가 view.GetType().GetProperty("DataContext")로 읽으므로
        // 테스트용 view에 DataContext 프로퍼티가 필요하다.
        private sealed class ViewWithVm
        {
            public object DataContext { get; set; }
        }

        #endregion

        #region Helpers

        private static (IRegionManager regionManager, MockRegion region) CreateTestSetup()
        {
            var sc = new ServiceCollection();
            sc.AddTransient<ViewA>();
            sc.AddTransient<ViewB>();
            sc.AddTransient<ViewC>();
            sc.AddTransient<DefaultDesignView>();
            sc.AddTransient<SourceGenView>();
            sc.AddTransient<MultiWindowView>();

            // RegionManager는 internal이므로 AppBootstrapper를 통해 등록되어야 하지만,
            // 테스트에서는 IRegionManager를 직접 구현한 테스트헬퍼를 사용합니다.
            // 대신 ServiceLocator에 직접 설정합니다.
            var container = sc.CreateContainer();
            ServiceLocator.SetServiceProvider(container);

            // IRegionManager는 AppBootstrapper가 등록하므로, 새 인스턴스를 수동 생성합니다.
            // RegionManager가 internal이므로 Reflection으로 생성합니다.
            var rmType = typeof(IRegionManager).Assembly.GetType("VSMVVM.Core.MVVM.RegionManager");
            var rm = (IRegionManager)System.Activator.CreateInstance(rmType);

            var region = new MockRegion();
            rm.Register("TestRegion", region);

            return (rm, region);
        }

        #endregion

        #region Navigation History — GoBack / GoForward

        [Fact]
        public void CanGoBack_InitialState_ReturnsFalse()
        {
            var (rm, _) = CreateTestSetup();
            rm.CanGoBack("TestRegion").Should().BeFalse();
        }

        [Fact]
        public void CanGoForward_InitialState_ReturnsFalse()
        {
            var (rm, _) = CreateTestSetup();
            rm.CanGoForward("TestRegion").Should().BeFalse();
        }

        [Fact]
        public void Navigate_ThenCanGoBack_ReturnsTrue()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));

            rm.CanGoBack("TestRegion").Should().BeTrue();
        }

        [Fact]
        public void GoBack_RestoresPreviousView()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));

            rm.GoBack("TestRegion");

            region.Content.Should().BeOfType<ViewA>();
        }

        [Fact]
        public void GoBack_ThenCanGoForward_ReturnsTrue()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));

            rm.GoBack("TestRegion");

            rm.CanGoForward("TestRegion").Should().BeTrue();
        }

        [Fact]
        public void GoForward_RestoresForwardView()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));
            rm.GoBack("TestRegion");

            rm.GoForward("TestRegion");

            region.Content.Should().BeOfType<ViewB>();
        }

        [Fact]
        public void Navigate_AfterGoBack_ClearsForwardStack()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));
            rm.GoBack("TestRegion");

            rm.Navigate("TestRegion", typeof(ViewC));

            rm.CanGoForward("TestRegion").Should().BeFalse();
        }

        [Fact]
        public void GoBack_MultipleSteps_TraversesFullHistory()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));
            rm.Navigate("TestRegion", typeof(ViewC));

            rm.GoBack("TestRegion"); // C → B
            rm.GoBack("TestRegion"); // B → A

            region.Content.Should().BeOfType<ViewA>();
            rm.CanGoBack("TestRegion").Should().BeFalse();
        }

        [Fact]
        public void GoBack_WithEmptyStack_DoesNothing()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));

            rm.GoBack("TestRegion");

            region.Content.Should().BeOfType<ViewA>();
        }

        [Fact]
        public void GoForward_WithEmptyStack_DoesNothing()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));

            rm.GoForward("TestRegion");

            region.Content.Should().BeOfType<ViewA>();
        }

        #endregion

        #region GetCurrentViewTypeName / GetCurrentViewDisplayName

        [Fact]
        public void GetCurrentViewTypeName_ReturnsTypeName()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));

            rm.GetCurrentViewTypeName("TestRegion").Should().Be("ViewA");
        }

        [Fact]
        public void GetCurrentViewDisplayName_PascalCase_DefaultDesign()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(DefaultDesignView));

            rm.GetCurrentViewDisplayName("TestRegion").Should().Be("Default Design");
        }

        [Fact]
        public void GetCurrentViewDisplayName_PascalCase_SourceGen()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(SourceGenView));

            rm.GetCurrentViewDisplayName("TestRegion").Should().Be("Source Gen");
        }

        [Fact]
        public void GetCurrentViewDisplayName_PascalCase_MultiWindow()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(MultiWindowView));

            rm.GetCurrentViewDisplayName("TestRegion").Should().Be("Multi Window");
        }

        [Fact]
        public void GetCurrentViewDisplayName_EmptyWhenNoNavigation()
        {
            var (rm, _) = CreateTestSetup();

            rm.GetCurrentViewDisplayName("TestRegion").Should().BeEmpty();
        }

        [Fact]
        public void GetCurrentViewDisplayName_UpdatesAfterGoBack()
        {
            var (rm, _) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(DefaultDesignView));
            rm.Navigate("TestRegion", typeof(SourceGenView));

            rm.GoBack("TestRegion");

            rm.GetCurrentViewDisplayName("TestRegion").Should().Be("Default Design");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void CanGoBack_UnregisteredRegion_ReturnsFalse()
        {
            var (rm, _) = CreateTestSetup();
            rm.CanGoBack("NonExistent").Should().BeFalse();
        }

        [Fact]
        public void CanGoForward_UnregisteredRegion_ReturnsFalse()
        {
            var (rm, _) = CreateTestSetup();
            rm.CanGoForward("NonExistent").Should().BeFalse();
        }

        [Fact]
        public void GetCurrentViewDisplayName_UnregisteredRegion_ReturnsEmpty()
        {
            var (rm, _) = CreateTestSetup();
            rm.GetCurrentViewDisplayName("NonExistent").Should().BeEmpty();
        }

        [Fact]
        public void GoBack_GoForward_RoundTrip()
        {
            var (rm, region) = CreateTestSetup();
            rm.Navigate("TestRegion", typeof(ViewA));
            rm.Navigate("TestRegion", typeof(ViewB));
            rm.Navigate("TestRegion", typeof(ViewC));

            // C → B → A → B → C
            rm.GoBack("TestRegion");
            rm.GoBack("TestRegion");
            rm.GoForward("TestRegion");
            rm.GoForward("TestRegion");

            region.Content.Should().BeOfType<ViewC>();
            rm.CanGoForward("TestRegion").Should().BeFalse();
            rm.CanGoBack("TestRegion").Should().BeTrue();
        }

        #endregion

        #region CanNavigate gating

        [Fact]
        public void Navigate_NextViewRefuses_DoesNotTearDownPrevView()
        {
            // CanNavigate 체크가 prevView 정리/히스토리 변경 *전*에 일어나야 한다는 회귀 테스트.
            var sc = new ServiceCollection();

            var prevVm = new TrackingNavigateAwareVm();
            var nextVm = new TrackingNavigateAwareVm { AllowNavigateIn = false };

            sc.AddTransient<ViewWithVm>(_ => new ViewWithVm { DataContext = prevVm });
            // 두 view 타입을 따로 등록해야 하므로 일회성 서로게이트 타입 사용.
            sc.AddTransient<NextViewSurrogate>(_ => new NextViewSurrogate { DataContext = nextVm });

            var container = sc.CreateContainer();
            ServiceLocator.SetServiceProvider(container);

            var rmType = typeof(IRegionManager).Assembly.GetType("VSMVVM.Core.MVVM.RegionManager");
            var rm = (IRegionManager)System.Activator.CreateInstance(rmType);

            var region = new MockRegion();
            rm.Register("TestRegion", region);

            rm.Navigate("TestRegion", typeof(ViewWithVm));
            region.Content.Should().BeOfType<ViewWithVm>();
            prevVm.OnNavigatedToCount.Should().Be(1);

            // 새 뷰가 거부 → prevView는 그대로 유지되어야 한다.
            rm.Navigate("TestRegion", typeof(NextViewSurrogate));

            region.Content.Should().BeOfType<ViewWithVm>("새 뷰가 거부했으므로 region content는 변경되지 않아야 한다");
            prevVm.OnNavigatedFromCount.Should().Be(0, "새 뷰가 거부했으므로 OnNavigatedFrom이 호출되지 않아야 한다");
            rm.CanGoBack("TestRegion").Should().BeFalse("새 뷰가 거부했으므로 BackStack이 변경되지 않아야 한다");
        }

        private sealed class NextViewSurrogate
        {
            public object DataContext { get; set; }
        }

        private sealed class OrderTrackingVm : INavigateAware, ICleanup
        {
            public List<string> Events { get; } = new List<string>();

            public bool CanNavigate(NavigationContext context) => true;
            public void OnNavigatedTo(NavigationContext context) => Events.Add("OnNavigatedTo");
            public void OnNavigatedFrom(NavigationContext context) => Events.Add("OnNavigatedFrom");
            public void Cleanup() => Events.Add("Cleanup");
        }

        [Fact]
        public void GoBack_ICleanupAndINavigateAware_OrderIsCorrect()
        {
            // current view가 ICleanup + INavigateAware일 때 OnNavigatedFrom → Cleanup 순서.
            var sc = new ServiceCollection();
            var firstVm = new OrderTrackingVm();
            var secondVm = new OrderTrackingVm();

            sc.AddTransient<ViewWithVm>(_ => new ViewWithVm { DataContext = firstVm });
            sc.AddTransient<NextViewSurrogate>(_ => new NextViewSurrogate { DataContext = secondVm });

            var container = sc.CreateContainer();
            ServiceLocator.SetServiceProvider(container);

            var rmType = typeof(IRegionManager).Assembly.GetType("VSMVVM.Core.MVVM.RegionManager");
            var rm = (IRegionManager)System.Activator.CreateInstance(rmType);
            var region = new MockRegion();
            rm.Register("TestRegion", region);

            rm.Navigate("TestRegion", typeof(ViewWithVm));
            rm.Navigate("TestRegion", typeof(NextViewSurrogate));
            secondVm.Events.Clear();

            rm.GoBack("TestRegion");

            // GoBack 시 secondVm이 current → prev. OnNavigatedFrom이 Cleanup보다 먼저 와야 함.
            var fromIdx = secondVm.Events.IndexOf("OnNavigatedFrom");
            var cleanupIdx = secondVm.Events.IndexOf("Cleanup");
            fromIdx.Should().BeGreaterOrEqualTo(0);
            cleanupIdx.Should().BeGreaterOrEqualTo(0);
            fromIdx.Should().BeLessThan(cleanupIdx, "OnNavigatedFrom이 Cleanup보다 먼저 호출되어야 한다");
        }

        #endregion
    }
}
