using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 메인 윈도우 ViewModel. 네비게이션 및 AppBar 커스텀 버튼 데모.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly IRegionManager _regionManager;

        [Property]
        private string _title = "VSMVVM Sample App";

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

        #region Navigation Commands

        [RelayCommand]
        private void NavigateHome() => _regionManager.Navigate("MainRegion", "HomeView");

        [RelayCommand]
        private void NavigateControls() => _regionManager.Navigate("MainRegion", "ControlsView");

        [RelayCommand]
        private void NavigateComponents() => _regionManager.Navigate("MainRegion", "ComponentsView");

        [RelayCommand]
        private void NavigateSourceGen() => _regionManager.Navigate("MainRegion", "SourceGenView");

        [RelayCommand]
        private void NavigateMessenger() => _regionManager.Navigate("MainRegion", "MessengerView");

        [RelayCommand]
        private void NavigateDialog() => _regionManager.Navigate("MainRegion", "DialogView");

        [RelayCommand]
        private void NavigateNavigation() => _regionManager.Navigate("MainRegion", "NavigationView");

        [RelayCommand]
        private void NavigateValidation() => _regionManager.Navigate("MainRegion", "ValidationView");

        [RelayCommand]
        private void NavigateServices() => _regionManager.Navigate("MainRegion", "ServicesView");

        [RelayCommand]
        private void NavigateLifetime() => _regionManager.Navigate("MainRegion", "LifetimeView");

        [RelayCommand]
        private void NavigateDefaultDesign() => _regionManager.Navigate("MainRegion", "DefaultDesignView");

        [RelayCommand]
        private void NavigateLocalization() => _regionManager.Navigate("MainRegion", "LocalizationView");

        [RelayCommand]
        private void NavigateMultiWindow() => _regionManager.Navigate("MainRegion", "MultiWindowView");

        [RelayCommand]
        private void NavigateCanvas() => _regionManager.Navigate("MainRegion", "CanvasView");

        #endregion
    }
}
