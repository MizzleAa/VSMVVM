using System;
using System.Windows.Forms;
using VSMVVM.Core.MVVM;
using VSMVVM.WinForms.Design.Core;

namespace VSMVVM.WinForms.Controls
{
    /// <summary>
    /// WinForms Region 컨트롤. Panel 기반으로 IRegion을 구현합니다.
    /// WPF WPFRegion(ContentControl 기반)과 동일한 역할을 합니다.
    /// RegionName 설정 시 RegionManager에 자동 등록됩니다.
    /// </summary>
    public class WinFormsRegion : Panel, IRegion
    {
        #region Fields

        private string _regionName;
        private object _content;

        #endregion

        #region Properties

        /// <summary>
        /// Region 이름. 설정 시 RegionManager에 자동 등록됩니다.
        /// WPF WPFRegion.RegionNameProperty에 대응합니다.
        /// </summary>
        public string RegionName
        {
            get => _regionName;
            set
            {
                if (_regionName == value) return;

                // 기존 등록 해제
                if (!string.IsNullOrEmpty(_regionName))
                {
                    try
                    {
                        var regionManager = ServiceLocator.GetServiceProvider()?.GetService<IRegionManager>();
                        regionManager?.Cleanup(_regionName);
                    }
                    catch { }
                }

                _regionName = value;

                // 새 이름으로 등록
                if (!string.IsNullOrEmpty(_regionName))
                {
                    try
                    {
                        var regionManager = ServiceLocator.GetServiceProvider()?.GetService<IRegionManager>();
                        regionManager?.Register(_regionName, this);
                    }
                    catch { }
                }
            }
        }

        #endregion

        #region IRegion

        /// <summary>
        /// Region에 표시 중인 콘텐츠.
        /// WPF ContentControl.Content에 대응합니다.
        /// UserControl 인스턴스를 설정하면 패널 내부에 표시됩니다.
        /// </summary>
        object IRegion.Content
        {
            get => _content;
            set
            {
                _content = value;
                SetContent(value);
            }
        }

        #endregion

        #region Constructor

        public WinFormsRegion()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Dock = DockStyle.Fill;

            // 테마 적용
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        }

        #endregion

        #region Content Management

        private void SetContent(object content)
        {
            // 기존 자식 컨트롤 제거 및 정리
            SuspendLayout();

            foreach (Control child in Controls)
            {
                // ICleanup 지원 (WPF DataContext 패턴 대응)
                CleanupViewModel(child);
            }

            Controls.Clear();

            // 새 콘텐츠 추가
            if (content is Control control)
            {
                control.Dock = DockStyle.Fill;

                // WPF ViewModelLocator.AutoWireViewModel="True" 대응
                // View → ViewModel 자동 바인딩
                VSMVVM.WinForms.Host.WinFormsHost.AutoWireViewModel(control);

                Controls.Add(control);

                // IAsyncInitializable, INavigateAware 지원
                InitializeViewModel(control);
            }

            ResumeLayout(true);
        }

        /// <summary>
        /// UserControl의 DataContext 프로퍼티를 통해 ViewModel의 INavigateAware.OnNavigatedTo를 호출합니다.
        /// </summary>
        private static void InitializeViewModel(Control control)
        {
            var dataContext = GetDataContext(control);
            if (dataContext == null) return;

            if (dataContext is INavigateAware navigateAware)
            {
                navigateAware.OnNavigatedTo(null);
            }

            if (dataContext is IAsyncInitializable asyncInit)
            {
                _ = asyncInit.InitializeAsync();
            }
        }

        /// <summary>
        /// UserControl의 DataContext 프로퍼티를 통해 ViewModel의 ICleanup.Cleanup을 호출합니다.
        /// </summary>
        private static void CleanupViewModel(Control control)
        {
            var dataContext = GetDataContext(control);
            if (dataContext is ICleanup cleanup)
            {
                cleanup.Cleanup();
            }
        }

        /// <summary>
        /// 리플렉션으로 UserControl의 DataContext 프로퍼티를 가져옵니다.
        /// WPF FrameworkElement.DataContext에 대응합니다.
        /// </summary>
        private static object GetDataContext(Control control)
        {
            var prop = control.GetType().GetProperty("DataContext");
            return prop?.GetValue(control);
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            var theme = ThemeManager.Current;
            BackColor = theme.BgPrimary;
        }

        private void OnThemeChanged()
        {
            if (InvokeRequired)
                BeginInvoke(new Action(ApplyTheme));
            else
                ApplyTheme();
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // RegionManager에서 등록 해제
                if (!string.IsNullOrEmpty(_regionName))
                {
                    try
                    {
                        var regionManager = ServiceLocator.GetServiceProvider()?.GetService<IRegionManager>();
                        regionManager?.Cleanup(_regionName);
                    }
                    catch { }
                }

                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
