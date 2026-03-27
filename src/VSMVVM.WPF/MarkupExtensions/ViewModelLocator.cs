using System;
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

            var viewType = element.GetType();
            var serviceProvider = ServiceLocator.GetServiceProvider();

            // 1. IViewModelMapper 매핑 우선
            var viewModelMapper = serviceProvider.GetService<IViewModelMapper>();
            if (viewModelMapper.HasMapping(viewType))
            {
                var vmType = viewModelMapper.GetViewModelType(viewType);
                if (vmType != null)
                {
                    element.DataContext = serviceProvider.GetService(vmType);
                    return;
                }
            }

            // 2. UseNamePatternMapper가 활성화된 경우 이름 패턴 매핑
            if (GetUseNamePatternMapper(d))
            {
                var viewName = viewType.Name;
                var viewModelName = ResolveViewModelName(viewName);

                if (!string.IsNullOrEmpty(viewModelName))
                {
                    var vmKeyType = serviceProvider.KeyType(viewModelName);
                    if (vmKeyType != null)
                    {
                        element.DataContext = serviceProvider.GetService(vmKeyType);
                    }
                }
            }
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
