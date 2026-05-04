using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 앱 Bootstrapper 추상 클래스. 모듈 자동 탐색, 서비스 등록, Region 매핑을 라이프사이클로 관리합니다.
    /// </summary>
    public abstract class AppBootstrapper
    {
        #region Fields

        private readonly HashSet<IModule> _modules = new HashSet<IModule>();
        private readonly ServiceCollection _serviceCollection = new ServiceCollection();
        private readonly List<string> _moduleLoadPaths = new List<string>();
        private readonly HashSet<string> _assemblyNames = new HashSet<string>();
        private readonly HashSet<string> _loadedAssemblyNames = new HashSet<string>();
        private readonly List<string> _moduleRejectNames = new List<string>();
        private bool _enableAutoModuleSearch;

        #endregion

        #region Properties

        /// <summary>
        /// 서비스 등록에 사용되는 ServiceCollection.
        /// </summary>
        public IServiceCollection ServiceCollection => _serviceCollection;

        #endregion

        #region Events

        /// <summary>
        /// 모듈이 추가될 때 발생하는 이벤트. (version, name)
        /// </summary>
        public event Action<string, string> OnModuleAddEvent;

        /// <summary>
        /// 모듈 어셈블리 로드가 실패할 때 발생하는 이벤트. (assemblyPath, exception)
        /// 구독하지 않으면 Debug.WriteLine으로 폴백된다. 절대로 silent swallow하지 않는다.
        /// </summary>
        public event Action<string, Exception> OnModuleLoadFailedEvent;

        #endregion

        #region Abstract Methods

        /// <summary>
        /// View-ViewModel 매핑을 수행합니다.
        /// </summary>
        protected abstract void ViewModelMapping(IViewModelMapper viewModelMapper);

        /// <summary>
        /// 서비스를 등록합니다.
        /// </summary>
        protected abstract void RegisterServices(IServiceCollection serviceCollection);

        /// <summary>
        /// Region 매핑을 수행합니다.
        /// </summary>
        protected abstract void RegionMapping(IRegionManager regionManager);

        /// <summary>
        /// 앱 시작 시 호출됩니다.
        /// </summary>
        protected abstract void OnStartUp(IServiceContainer container);

        /// <summary>
        /// 모듈을 등록합니다.
        /// </summary>
        protected abstract void RegisterModules();

        #endregion

        #region Protected Methods

        /// <summary>
        /// 모듈 인스턴스를 직접 등록합니다.
        /// </summary>
        protected void RegisterModule<T>(T module) where T : IModule
        {
            if (_modules.Any(m => m.ModuleName == module.ModuleName))
            {
                return;
            }

            _modules.Add(module);
        }

        /// <summary>
        /// 모듈을 타입으로 등록합니다.
        /// </summary>
        protected void RegisterModule<T>() where T : IModule
        {
            var module = (IModule)Activator.CreateInstance(typeof(T));
            RegisterModule(module);
        }

        /// <summary>
        /// 탐색 대상 어셈블리 이름을 추가합니다.
        /// </summary>
        protected void AddAssemblyName(string assemblyName)
        {
            _assemblyNames.Add(assemblyName);
        }

        /// <summary>
        /// 절대 경로의 모듈 탐색 디렉토리를 추가합니다.
        /// </summary>
        protected void AddModulePath(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"Module path does not exist: {path}");
            }

            _moduleLoadPaths.Add(path);
        }

        /// <summary>
        /// 현재 실행 디렉토리 기준 상대 경로의 모듈 디렉토리를 추가합니다.
        /// </summary>
        protected void AddModuleRelativePath(string folder)
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(currentPath, folder);

            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"Module path does not exist: {path}");
            }

            _moduleLoadPaths.Add(path);
        }

        /// <summary>
        /// 현재 실행 디렉토리를 모듈 탐색 경로에 추가합니다.
        /// </summary>
        protected void AddModuleCurrentPath()
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            _moduleLoadPaths.Add(currentPath);
        }

        /// <summary>
        /// 특정 모듈 이름을 탐색에서 제외합니다.
        /// </summary>
        protected void RejectModule(string name)
        {
            _moduleRejectNames.Add(name);
        }

        /// <summary>
        /// 자동 모듈 탐색을 활성화합니다.
        /// </summary>
        protected void EnableAutoModuleSearch(bool enable)
        {
            _enableAutoModuleSearch = enable;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Bootstrapper 라이프사이클을 실행합니다.
        /// </summary>
        public void Run()
        {
            var container = _serviceCollection.CreateContainer();

            // 프레임워크 핵심 서비스를 먼저 등록한다.
            // ServiceLocator publish가 등록보다 앞설 경우, 조기에 ServiceLocator를 통해
            // IViewModelMapper 등을 조회하는 코드(ViewModelLocator 등)가 미등록 상태와 만나
            // InvalidOperationException을 받게 된다.
            _serviceCollection.AddSingleton<IRegionManager, RegionManager>();
            _serviceCollection.AddSingleton<ILocalizeService, LocalizeService>();
            _serviceCollection.AddSingleton<IViewModelMapper, ViewModelMapper>();
            _serviceCollection.AddSingleton<IMessenger, Messenger>();
            _serviceCollection.AddSingleton<IServiceContainer>(container);

            // 핵심 서비스가 모두 등록된 후에 ServiceLocator publish.
            ServiceLocator.SetServiceProvider(container);

            var regionManager = container.GetService<IRegionManager>();
            var viewModelMapper = container.GetService<IViewModelMapper>();

            // 라이프사이클 순서 실행
            RegisterModules();

            if (_enableAutoModuleSearch)
            {
                LoadAssemblies();
            }

            ViewModelMapping(viewModelMapper);
            foreach (var module in _modules)
            {
                module.ViewModelMapping(viewModelMapper);
            }

            RegisterServices(_serviceCollection);
            foreach (var module in _modules)
            {
                module.RegisterServices(_serviceCollection);
            }

            RegionMapping(regionManager);
            foreach (var module in _modules)
            {
                module.RegionMapping(regionManager);
            }

            OnStartUp(container);
            foreach (var module in _modules)
            {
                module.OnStartUp(container);
            }
        }

        #endregion

        #region Private Methods

        private void LoadAssemblies()
        {
            foreach (var modulePath in _moduleLoadPaths)
            {
                var moduleFiles = Directory.GetFiles(modulePath, "*.dll");

                foreach (var moduleFile in moduleFiles)
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(moduleFile);

                    if (!_assemblyNames.Contains(assemblyName))
                    {
                        continue;
                    }

                    if (_loadedAssemblyNames.Contains(assemblyName))
                    {
                        continue;
                    }

                    try
                    {
                        var assembly = Assembly.LoadFrom(moduleFile);
                        var moduleTypes = assembly.GetTypes()
                            .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        foreach (var moduleType in moduleTypes)
                        {
                            var plugin = (IModule)Activator.CreateInstance(moduleType);

                            if (_modules.Any(m => m.ModuleName == plugin.ModuleName))
                            {
                                continue;
                            }

                            if (_moduleRejectNames.Contains(plugin.ModuleName))
                            {
                                continue;
                            }

                            _modules.Add(plugin);
                            OnModuleAddEvent?.Invoke(plugin.ModuleVersion, plugin.ModuleName);
                        }

                        _loadedAssemblyNames.Add(assemblyName);
                    }
                    catch (Exception ex)
                    {
                        // 어셈블리 로드 실패는 디버깅이 매우 어려운 silent failure가 되기 쉬우므로
                        // 진단 hook으로 노출하고, 미구독 시에도 Debug 출력은 남긴다.
                        if (OnModuleLoadFailedEvent != null)
                        {
                            OnModuleLoadFailedEvent.Invoke(moduleFile, ex);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AppBootstrapper] Failed to load module assembly '{moduleFile}': {ex}");
                        }
                    }
                }
            }
        }

        #endregion
    }
}
