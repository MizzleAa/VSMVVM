using System;
using System.ComponentModel;
using System.Windows;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// ViewModelLocator 부착 프로퍼티. View의 DataContext를 자동으로 ViewModel에 바인딩합니다.
    /// </summary>
    public static class ViewModelLocator
    {
        #region AutoWireViewModel

        /// <summary>
        /// AutoWireViewModel 부착 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty AutoWireViewModelProperty =
            DependencyProperty.RegisterAttached(
                "AutoWireViewModel",
                typeof(bool),
                typeof(ViewModelLocator),
                new PropertyMetadata(false, OnAutoWireViewModelChanged));

        /// <summary>
        /// AutoWireViewModel 값을 가져옵니다.
        /// </summary>
        public static bool GetAutoWireViewModel(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoWireViewModelProperty);
        }

        /// <summary>
        /// AutoWireViewModel 값을 설정합니다.
        /// </summary>
        public static void SetAutoWireViewModel(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoWireViewModelProperty, value);
        }

        #endregion

        #region UseNamePatternMapper

        /// <summary>
        /// UseNamePatternMapper 부착 프로퍼티. View 이름 패턴으로 ViewModel을 자동 매핑합니다.
        /// </summary>
        public static readonly DependencyProperty UseNamePatternMapperProperty =
            DependencyProperty.RegisterAttached(
                "UseNamePatternMapper",
                typeof(bool),
                typeof(ViewModelLocator),
                new PropertyMetadata(false));

        /// <summary>
        /// UseNamePatternMapper 값을 가져옵니다.
        /// </summary>
        public static bool GetUseNamePatternMapper(DependencyObject obj)
        {
            return (bool)obj.GetValue(UseNamePatternMapperProperty);
        }

        /// <summary>
        /// UseNamePatternMapper 값을 설정합니다.
        /// </summary>
        public static void SetUseNamePatternMapper(DependencyObject obj, bool value)
        {
            obj.SetValue(UseNamePatternMapperProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnAutoWireViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                return;
            }

            if (!(d is FrameworkElement element))
            {
                return;
            }

            // Designer 모드에서는 동작하지 않음 (Visual Studio Designer가 ServiceLocator 없이 XAML 파싱)
            if (DesignerProperties.GetIsInDesignMode(element))
            {
                return;
            }

            // 이미 DataContext가 설정되어 있으면 덮어쓰지 않음 (재진입/재사용 시 ViewModel 누수 방지)
            if (element.DataContext != null)
            {
                return;
            }

            // 즉시 시도. XAML에서 AutoWireViewModel을 다른 binding 속성보다 앞에 선언하면
            // 자식 컨트롤의 binding 평가 전에 DataContext가 설정되어 binding 에러 누적이 방지됨.
            if (TryAssignDataContext(element))
            {
                return;
            }

            // 즉시 할당이 실패한 경우 (ServiceLocator 미초기화 등) Initialized 이벤트에서 재시도.
            // 자식 binding의 1차 평가 전에 DataContext를 채울 수 있는 마지막 시점.
            element.Initialized += OnElementInitialized;
        }

        private static void OnElementInitialized(object sender, EventArgs e)
        {
            if (!(sender is FrameworkElement element))
            {
                return;
            }

            element.Initialized -= OnElementInitialized;

            if (element.DataContext != null)
            {
                return;
            }

            TryAssignDataContext(element);
        }

        /// <summary>
        /// View 타입에 대응하는 ViewModel을 resolve하여 DataContext에 설정합니다.
        /// 성공 시 true, ServiceLocator 미초기화/매핑 없음 등으로 실패 시 false를 반환합니다.
        /// </summary>
        private static bool TryAssignDataContext(FrameworkElement element)
        {
            IServiceContainer serviceProvider;
            try
            {
                serviceProvider = ServiceLocator.GetServiceProvider();
            }
            catch (InvalidOperationException)
            {
                // ServiceLocator가 아직 초기화되지 않음 (앱 시작 매우 이른 시점 등)
                return false;
            }

            var viewType = element.GetType();

            // 1. IViewModelMapper 매핑 우선
            var viewModelMapper = serviceProvider.GetService<IViewModelMapper>();
            if (viewModelMapper != null && viewModelMapper.HasMapping(viewType))
            {
                var vmType = viewModelMapper.GetViewModelType(viewType);
                if (vmType != null)
                {
                    var vm = serviceProvider.GetService(vmType);
                    if (vm != null)
                    {
                        element.DataContext = vm;
                        return true;
                    }
                }
            }

            // 2. UseNamePatternMapper가 활성화된 경우 이름 패턴 매핑
            if (GetUseNamePatternMapper(element))
            {
                var viewName = viewType.Name;
                var viewModelName = ResolveViewModelName(viewName);

                if (!string.IsNullOrEmpty(viewModelName))
                {
                    var vmKeyType = serviceProvider.KeyType(viewModelName);
                    if (vmKeyType != null)
                    {
                        var vm = serviceProvider.GetService(vmKeyType);
                        if (vm != null)
                        {
                            element.DataContext = vm;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// View 이름에서 ViewModel 이름을 유추합니다.
        /// MainView → MainViewModel, SettingsView → SettingsViewModel
        /// </summary>
        private static string ResolveViewModelName(string viewName)
        {
            const string viewSuffix = "View";
            const string viewModelSuffix = "ViewModel";

            if (viewName.EndsWith(viewSuffix) && !viewName.EndsWith(viewModelSuffix))
            {
                return viewName + "Model";
            }

            return viewName + viewModelSuffix;
        }

        #endregion
    }
}
