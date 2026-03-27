using System;
using System.Windows;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Host
{
    /// <summary>
    /// VSMVVM WPF 호스트. Fluent Builder 패턴으로 앱 라이프사이클을 구성합니다.
    /// 멀티 윈도우 독립 관리를 지원하는 서비스 중심 아키텍처.
    /// </summary>
    public sealed class VSMVVMHost
    {
        #region Fields

        private readonly AppBootstrapper _bootStrapper;
        private readonly string[] _args;
        private readonly IServiceCollection _serviceCollection;
        private readonly string _hostName;
        private Services.SplashService _splashService;

        #endregion

        #region Constructor

        private VSMVVMHost(string[] args, string hostName, AppBootstrapper bootStrapper)
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
        /// </summary>
        public event Action<string, string> OnModuleAddEvent;

        #endregion

        #region Static Factory

        /// <summary>
        /// WPF 호스트를 생성합니다.
        /// </summary>
        public static VSMVVMHost CreateHost<TBootstrapper, TApp>(string[] args, string hostName)
            where TBootstrapper : AppBootstrapper
            where TApp : Application
        {
            var bootStrapper = (TBootstrapper)Activator.CreateInstance(typeof(TBootstrapper));
            bootStrapper.ServiceCollection.AddSingleton<Application, TApp>();
            return new VSMVVMHost(args, hostName, bootStrapper);
        }

        /// <summary>
        /// Application 타입 없이 호스트를 생성합니다.
        /// </summary>
        public static VSMVVMHost CreateHost<TBootstrapper>(string[] args, string hostName)
            where TBootstrapper : AppBootstrapper
        {
            var bootStrapper = (TBootstrapper)Activator.CreateInstance(typeof(TBootstrapper));
            return new VSMVVMHost(args, hostName, bootStrapper);
        }

        #endregion

        #region Fluent Builder

        /// <summary>
        /// Bootstrapper 라이프사이클을 실행합니다.
        /// </summary>
        public VSMVVMHost Build()
        {
            _bootStrapper.Run();
            _splashService?.Close();
            return this;
        }

        /// <summary>
        /// ShutdownMode를 설정합니다.
        /// </summary>
        public VSMVVMHost ShutdownMode(ShutdownMode mode)
        {
            var app = ServiceLocator.GetServiceProvider().GetService<Application>();
            app.ShutdownMode = mode;
            return this;
        }

        /// <summary>
        /// Window를 팝업합니다.
        /// </summary>
        /// <param name="dialog">true이면 ShowDialog, false이면 Show.</param>
        public VSMVVMHost Popup<TWindow>(bool dialog = true) where TWindow : Window
        {
            var window = ServiceLocator.GetServiceProvider().GetService<TWindow>();

            if (dialog)
            {
                window.ShowDialog();
            }
            else
            {
                window.Show();
            }

            return this;
        }

        /// <summary>
        /// 이름으로 Window를 팝업합니다.
        /// </summary>
        public VSMVVMHost Popup(string name, bool dialog = true)
        {
            var window = (Window)ServiceLocator.GetServiceProvider().GetService(name);

            if (dialog)
            {
                window.ShowDialog();
            }
            else
            {
                window.Show();
            }

            return this;
        }

        /// <summary>
        /// 스플래시 윈도우를 사용하도록 설정합니다.
        /// 별도 STA 스레드에서 즉시 표시되며, Build() 완료 시 자동으로 닫힙니다.
        /// </summary>
        public VSMVVMHost UseSplash<TSplashWindow>() where TSplashWindow : Window, new()
        {
            _splashService = new Services.SplashService();
            _splashService.Show<TSplashWindow>();
            _serviceCollection.AddSingleton<ISplashService, Services.SplashService>(_splashService);
            return this;
        }

        /// <summary>
        /// 창 위치/크기 복원을 활성화합니다.
        /// </summary>
        public VSMVVMHost UseWindowPlacementRestore(string configName)
        {
            _serviceCollection.AddSingleton<Services.IWindowPlacementService>(
                c => new Services.WindowPlacementService(configName));
            return this;
        }

        /// <summary>
        /// Application.Run()을 실행합니다.
        /// </summary>
        public int RunApp()
        {
            var app = ServiceLocator.GetServiceProvider().GetService<Application>();
            return app.Run();
        }

        /// <summary>
        /// 메인 Window와 함께 Application.Run()을 실행합니다.
        /// </summary>
        public int RunApp<TWindow>() where TWindow : Window
        {
            var app = ServiceLocator.GetServiceProvider().GetService<Application>();
            var window = ServiceLocator.GetServiceProvider().GetService<TWindow>();
            return app.Run(window);
        }

        /// <summary>
        /// 앱을 종료합니다.
        /// </summary>
        public VSMVVMHost Shutdown()
        {
            var app = ServiceLocator.GetServiceProvider().GetService<Application>();
            app.Shutdown();
            return this;
        }

        #endregion

        #region Private Methods

        private void OnModuleAdded(string version, string name)
        {
            OnModuleAddEvent?.Invoke(version, name);
        }

        #endregion
    }
}
