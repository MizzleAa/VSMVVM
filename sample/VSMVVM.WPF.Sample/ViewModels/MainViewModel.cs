using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 메인 윈도우 ViewModel. 네비게이션 히스토리(Back/Forward) + 사이드바 활성 상태.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly IRegionManager _regionManager;

        [Property]
        private string _title = "VSMVVM Sample App";

        [Property]
        private string _currentViewName = "Home";

        [Property]
        private bool _canGoBack;

        [Property]
        private bool _canGoForward;

        [Property]
        private string _activeMenu = "Home";

        #endregion

        #region Constructor

        public MainViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        #endregion

        #region AppBar Commands

        /// <summary>
        /// AppBar 커스텀 버튼 클릭 시 타이틀에 앱 정보를 토글 표시합니다.
        /// </summary>
        [RelayCommand]
        private void ToggleInfo()
        {
            if (Title.Contains("["))
            {
                Title = "VSMVVM Sample App";
            }
            else
            {
                Title = $"VSMVVM Sample App  [.NET {Environment.Version} | {DateTime.Now:HH:mm:ss}]";
            }
        }

        #endregion

        #region Navigation History

        [RelayCommand]
        private void NavigateBack()
        {
            _regionManager.GoBack("MainRegion");
            SyncCurrentViewAfterHistoryNav();
        }

        [RelayCommand]
        private void NavigateForward()
        {
            _regionManager.GoForward("MainRegion");
            SyncCurrentViewAfterHistoryNav();
        }

        /// <summary>
        /// Back/Forward 후 현재 View 이름을 동기화합니다.
        /// </summary>
        private void SyncCurrentViewAfterHistoryNav()
        {
            var displayName = _regionManager.GetCurrentViewDisplayName("MainRegion");
            CurrentViewName = displayName;
            ActiveMenu = displayName;
            RefreshNavigationState();
        }

        private void RefreshNavigationState()
        {
            CanGoBack = _regionManager.CanGoBack("MainRegion");
            CanGoForward = _regionManager.CanGoForward("MainRegion");
            NavigateBackCommand?.RaiseCanExecuteChanged();
            NavigateForwardCommand?.RaiseCanExecuteChanged();
        }

        #endregion

        #region Navigation Commands

        private void DoNavigate(string viewName)
        {
            _regionManager.Navigate("MainRegion", viewName);
            var displayName = _regionManager.GetCurrentViewDisplayName("MainRegion");
            CurrentViewName = displayName;
            ActiveMenu = displayName;
            RefreshNavigationState();
        }

        [RelayCommand]
        private void NavigateHome() => DoNavigate("HomeView");

        [RelayCommand]
        private void NavigateControls() => DoNavigate("ControlsView");

        [RelayCommand]
        private void NavigateComponents() => DoNavigate("ComponentsView");

        [RelayCommand]
        private void NavigateSourceGen() => DoNavigate("SourceGenView");

        [RelayCommand]
        private void NavigateMessenger() => DoNavigate("MessengerView");

        [RelayCommand]
        private void NavigateDialog() => DoNavigate("DialogView");

        [RelayCommand]
        private void NavigateNavigation() => DoNavigate("NavigationView");

        [RelayCommand]
        private void NavigateValidation() => DoNavigate("ValidationView");

        [RelayCommand]
        private void NavigateServices() => DoNavigate("ServicesView");

        [RelayCommand]
        private void NavigateLifetime() => DoNavigate("LifetimeView");

        [RelayCommand]
        private void NavigateDefaultDesign() => DoNavigate("DefaultDesignView");

        [RelayCommand]
        private void NavigateLocalization() => DoNavigate("LocalizationView");

        [RelayCommand]
        private void NavigateMultiWindow() => DoNavigate("MultiWindowView");

        [RelayCommand]
        private void NavigateCanvas() => DoNavigate("CanvasView");

        [RelayCommand]
        private void NavigateImageViewer() => DoNavigate("ImageViewerDemoView");

        [RelayCommand]
        private void NavigateLogging() => DoNavigate("LoggingView");

        #endregion
    }
}
