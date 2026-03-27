using System.Windows;
using System.Windows.Controls;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// WPF Region 컨트롤. ContentControl 기반으로 IRegion을 구현합니다.
    /// RegionName DependencyProperty를 통해 RegionManager에 자동 등록됩니다.
    /// </summary>
    public class WPFRegion : ContentControl, IRegion
    {
        #region DependencyProperties

        /// <summary>
        /// Region 이름 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty RegionNameProperty =
            DependencyProperty.Register(
                nameof(RegionName),
                typeof(string),
                typeof(WPFRegion),
                new PropertyMetadata(null, OnRegionNameChanged));

        /// <summary>
        /// Region 이름.
        /// </summary>
        public string RegionName
        {
            get => (string)GetValue(RegionNameProperty);
            set => SetValue(RegionNameProperty, value);
        }

        #endregion

        #region Constructor

        /// <summary>
        /// WPFRegion의 기본 생성자.
        /// </summary>
        public WPFRegion()
        {
            Unloaded += OnUnloaded;
        }

        #endregion

        #region IRegion

        /// <summary>
        /// Region에 표시 중인 콘텐츠.
        /// </summary>
        object IRegion.Content
        {
            get => Content;
            set => Content = value;
        }

        #endregion

        #region Private Methods

        private static void OnRegionNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WPFRegion region && e.NewValue is string regionName && !string.IsNullOrEmpty(regionName))
            {
                var regionManager = ServiceLocator.GetServiceProvider().GetService<IRegionManager>();
                regionManager.Register(regionName, region);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RegionName))
            {
                var regionManager = ServiceLocator.GetServiceProvider().GetService<IRegionManager>();
                regionManager.Cleanup(RegionName);
            }
        }

        #endregion
    }
}
