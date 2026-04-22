using System;
using System.Windows.Forms;
using VSMVVM.Core.MVVM;
using VSMVVM.WinForms.Design.Core;
using VSMVVM.WinForms.Design.Extensions;

namespace VSMVVM.WinForms.Host
{
    /// <summary>
    /// VSMVVM WinForms 호스트. Fluent Builder 패턴으로 앱 라이프사이클을 구성합니다.
    /// WPF VSMVVMHost와 동일한 API 구조를 제공합니다.
    ///
    /// 사용 예:
    /// <code>
    /// WinFormsHost.CreateHost&lt;MyBootstrapper&gt;(args, "MyApp")
    ///     .Build()
    ///     .RunApp&lt;MainForm&gt;();
    /// </code>
    /// </summary>
    public sealed class WinFormsHost
    {
        #region Fields

        private readonly AppBootstrapper _bootStrapper;
        private readonly string[] _args;
        private readonly IServiceCollection _serviceCollection;
        private readonly string _hostName;

        #endregion

        #region Constructor

        private WinFormsHost(string[] args, string hostName, AppBootstrapper bootStrapper)
        {
            _args = args;
            _bootStrapper = bootStrapper;
            _serviceCollection = bootStrapper.ServiceCollection;
            _hostName = hostName;

            _bootStrapper.OnModuleAddEvent += OnModuleAdded;
        }

        #endregion

        #region Events

        /// <summary>
        /// 모듈이 추가될 때 발생하는 이벤트. (version, name)
        /// WPF VSMVVMHost.OnModuleAddEvent에 대응.
        /// </summary>
        public event Action<string, string> OnModuleAddEvent;

        #endregion

        #region Static Factory

        /// <summary>
        /// WinForms 호스트를 생성합니다.
        /// WPF: VSMVVMHost.CreateHost&lt;TBootstrapper&gt;에 대응.
        /// </summary>
        public static WinFormsHost CreateHost<TBootstrapper>(string[] args, string hostName)
            where TBootstrapper : AppBootstrapper
        {
            var bootStrapper = (TBootstrapper)Activator.CreateInstance(typeof(TBootstrapper));
            return new WinFormsHost(args, hostName, bootStrapper);
        }

        #endregion

        #region Fluent Builder

        /// <summary>
        /// Bootstrapper 라이프사이클을 실행합니다.
        /// WPF: VSMVVMHost.Build()에 대응.
        /// WinForms 환경 초기화(HighDPI, 비주얼 스타일)와
        /// 프레임워크 서비스(IDispatcherService, IDialogService) 등록을 수행합니다.
        /// </summary>
        public WinFormsHost Build()
        {
            // WinForms 런타임 초기화
            // DPI 모드는 sample의 app.manifest (PerMonitorV2)에서 선언 — SetHighDpiMode는 불필요 (WFAC010).
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // WinForms 전용 서비스 자동 등록
            _serviceCollection.AddSingleton<IDispatcherService, Services.WinFormsDispatcherService>();
            _serviceCollection.AddSingleton<IDialogService, Services.WinFormsDialogService>();

            // 부트스트래퍼 실행
            _bootStrapper.Run();

            return this;
        }

        /// <summary>
        /// Form을 팝업합니다.
        /// WPF: VSMVVMHost.Popup&lt;TWindow&gt;에 대응.
        /// </summary>
        public WinFormsHost Popup<TForm>(bool dialog = true) where TForm : Form
        {
            var form = ServiceLocator.GetServiceProvider().GetService<TForm>();
            ApplyDesignToForm(form);

            if (dialog)
                form.ShowDialog();
            else
                form.Show();

            return this;
        }

        /// <summary>
        /// 이름으로 Form을 팝업합니다.
        /// WPF: VSMVVMHost.Popup(string)에 대응.
        /// </summary>
        public WinFormsHost Popup(string name, bool dialog = true)
        {
            var form = (Form)ServiceLocator.GetServiceProvider().GetService(name);
            ApplyDesignToForm(form);

            if (dialog)
                form.ShowDialog();
            else
                form.Show();

            return this;
        }

        /// <summary>
        /// 메인 Form으로 Application.Run()을 실행합니다.
        /// WPF: VSMVVMHost.RunApp&lt;TWindow&gt;에 대응.
        /// </summary>
        public int RunApp<TForm>() where TForm : Form
        {
            var form = ServiceLocator.GetServiceProvider().GetService<TForm>();
            ApplyDesignToForm(form);
            AutoWireViewModel(form);

            Application.Run(form);
            return 0;
        }

        /// <summary>
        /// 이름으로 메인 Form을 찾아 Application.Run()을 실행합니다.
        /// </summary>
        public int RunApp(string formName)
        {
            var form = (Form)ServiceLocator.GetServiceProvider().GetService(formName);
            ApplyDesignToForm(form);

            Application.Run(form);
            return 0;
        }

        /// <summary>
        /// 앱을 종료합니다.
        /// WPF: VSMVVMHost.Shutdown()에 대응.
        /// </summary>
        public WinFormsHost Shutdown()
        {
            Application.Exit();
            return this;
        }

        #endregion

        #region Private Methods

        private void OnModuleAdded(string version, string name)
        {
            OnModuleAddEvent?.Invoke(version, name);
        }

        /// <summary>
        /// Form에 VSMVVM Design 시스템을 적용합니다.
        /// </summary>
        private static void ApplyDesignToForm(Form form)
        {
            if (form == null) return;
            form.ApplyVSDefaults();
        }

        /// <summary>
        /// WPF ViewModelLocator.AutoWireViewModel 대응.
        /// ViewModelMapper에 등록된 매핑에 따라 ViewModel을 resolve하여 DataContext에 설정합니다.
        /// </summary>
        internal static void AutoWireViewModel(object view)
        {
            if (view == null) return;

            try
            {
                var sp = ServiceLocator.GetServiceProvider();
                var mapper = sp.GetService<IViewModelMapper>();
                var viewType = view.GetType();

                if (mapper.HasMapping(viewType))
                {
                    var vmType = mapper.GetViewModelType(viewType);
                    if (vmType != null)
                    {
                        var vm = sp.GetService(vmType);
                        // 리플렉션으로 DataContext 프로퍼티 설정 (WPF FrameworkElement.DataContext 대응)
                        var prop = viewType.GetProperty("DataContext");
                        prop?.SetValue(view, vm);
                    }
                }
            }
            catch { }
        }

        #endregion
    }
}
