using System.Threading;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Scheduler.Compilation;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.Core.Scheduler.Scripting;
using VSMVVM.WPF.Sample.ViewModels;
using VSMVVM.WPF.Sample.Views;
using VSMVVM.WPF.Sample.Services;
using VSMVVM.WPF.Scheduler.Services;
using VSMVVM.WPF.Services;

namespace VSMVVM.WPF.Sample
{
    /// <summary>
    /// Application bootstrapper for DI registration, View-ViewModel mapping, and Region configuration.
    /// </summary>

    public class Bootstrapper : AppBootstrapper
    {
        protected override void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<IDispatcherService, WPFDispatcherService>();
            serviceCollection.AddSingleton<IUndoRedoService, UndoRedoService>();
            serviceCollection.AddSingleton<IFileDialogService, WpfFileDialogService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();

            serviceCollection.AddTransient<MainWindow>();
            serviceCollection.AddTransient<HomeView>();
            serviceCollection.AddTransient<ControlsView>();
            serviceCollection.AddTransient<ComponentsView>();
            serviceCollection.AddTransient<SourceGenView>();
            serviceCollection.AddTransient<MessengerView>();
            serviceCollection.AddTransient<DialogView>();
            serviceCollection.AddTransient<NavigationView>();
            serviceCollection.AddTransient<ValidationView>();
            serviceCollection.AddTransient<ServicesView>();
            serviceCollection.AddTransient<LifetimeView>();
            serviceCollection.AddTransient<DefaultDesignView>();
            serviceCollection.AddTransient<LocalizationView>();
            serviceCollection.AddTransient<MultiWindowView>();
            serviceCollection.AddTransient<CanvasView>();
            serviceCollection.AddTransient<ImageViewerDemoView>();
            serviceCollection.AddTransient<ChartsDemoView>();
            serviceCollection.AddTransient<LoggingView>();
            serviceCollection.AddTransient<SubWindow>();
            serviceCollection.AddTransient<SchedulerDemoView>();
            serviceCollection.AddTransient<UserCodeEditorWindow>();
            serviceCollection.AddTransient<VariablesManagerWindow>();
            serviceCollection.AddTransient<ImageViewWindow>();
            serviceCollection.AddTransient<ChartViewWindow>();
            serviceCollection.AddTransient<SampleDemoPickerView>();

            serviceCollection.AddTransient<MainViewModel>();
            serviceCollection.AddTransient<HomeViewModel>();
            serviceCollection.AddTransient<ControlsViewModel>();
            serviceCollection.AddTransient<ComponentsViewModel>();
            serviceCollection.AddTransient<SourceGenViewModel>();
            serviceCollection.AddTransient<MessengerViewModel>();
            serviceCollection.AddTransient<DialogViewModel>();
            serviceCollection.AddTransient<NavigationViewModel>();
            serviceCollection.AddTransient<ValidationViewModel>();
            serviceCollection.AddTransient<ServicesViewModel>();
            serviceCollection.AddTransient<LifetimeViewModel>();
            serviceCollection.AddTransient<DefaultDesignViewModel>();
            serviceCollection.AddTransient<LocalizationViewModel>();
            serviceCollection.AddTransient<MultiWindowViewModel>();
            serviceCollection.AddTransient<CanvasViewModel>();
            serviceCollection.AddTransient<ImageViewerDemoViewModel>();
            serviceCollection.AddTransient<ChartsDemoViewModel>();
            serviceCollection.AddTransient<LoggingViewModel>();
            serviceCollection.AddTransient<SubWindowViewModel>();
            serviceCollection.AddTransient<SchedulerDemoViewModel>();
            serviceCollection.AddTransient<UserCodeEditorWindowViewModel>();
            serviceCollection.AddTransient<VariablesManagerWindowViewModel>();
            serviceCollection.AddTransient<ImageViewWindowViewModel>();
            serviceCollection.AddTransient<ChartViewWindowViewModel>();
            serviceCollection.AddTransient<SampleDemoPickerViewModel>();

            // === Scheduler 서비스 (Phase 9 PoC) ===
            serviceCollection.AddSingleton<IMessenger, Messenger>();
            serviceCollection.AddSingleton<ISchedulerService, SchedulerService>();
            serviceCollection.AddSingleton<ICompilationService, RoslynCompilationService>();
            serviceCollection.AddSingleton<INodePaletteService, NodePaletteService>();

            // DI Lifetime demo services
            serviceCollection.AddSingleton<SingletonDemo>();
            serviceCollection.AddScoped<ScopedDemo>();
            serviceCollection.AddTransient<TransientDemo>();

            // Shared module for multi-window demo
            serviceCollection.AddSingleton<SharedCounterService>();

            // Logger service
            serviceCollection.AddSingleton<ILoggerService, SampleLoggerService>();

            // Startup demo services
            serviceCollection.AddSingleton<StartupServiceA>();
            serviceCollection.AddSingleton<StartupServiceB>();

            // StateStore (Redux-style)
            serviceCollection.AddSingleton<AppStateStore>();
        }

        protected override void ViewModelMapping(IViewModelMapper viewModelMapper)
        {
            viewModelMapper.Register<MainWindow, MainViewModel>();
            viewModelMapper.Register<HomeView, HomeViewModel>();
            viewModelMapper.Register<ControlsView, ControlsViewModel>();
            viewModelMapper.Register<ComponentsView, ComponentsViewModel>();
            viewModelMapper.Register<SourceGenView, SourceGenViewModel>();
            viewModelMapper.Register<MessengerView, MessengerViewModel>();
            viewModelMapper.Register<DialogView, DialogViewModel>();
            viewModelMapper.Register<NavigationView, NavigationViewModel>();
            viewModelMapper.Register<ValidationView, ValidationViewModel>();
            viewModelMapper.Register<ServicesView, ServicesViewModel>();
            viewModelMapper.Register<LifetimeView, LifetimeViewModel>();
            viewModelMapper.Register<DefaultDesignView, DefaultDesignViewModel>();
            viewModelMapper.Register<LocalizationView, LocalizationViewModel>();
            viewModelMapper.Register<MultiWindowView, MultiWindowViewModel>();
            viewModelMapper.Register<CanvasView, CanvasViewModel>();
            viewModelMapper.Register<ImageViewerDemoView, ImageViewerDemoViewModel>();
            viewModelMapper.Register<ChartsDemoView, ChartsDemoViewModel>();
            viewModelMapper.Register<LoggingView, LoggingViewModel>();
            viewModelMapper.Register<SubWindow, SubWindowViewModel>();
            viewModelMapper.Register<SchedulerDemoView, SchedulerDemoViewModel>();
            viewModelMapper.Register<UserCodeEditorWindow, UserCodeEditorWindowViewModel>();
            viewModelMapper.Register<VariablesManagerWindow, VariablesManagerWindowViewModel>();
            viewModelMapper.Register<ImageViewWindow, ImageViewWindowViewModel>();
            viewModelMapper.Register<ChartViewWindow, ChartViewWindowViewModel>();
            viewModelMapper.Register<SampleDemoPickerView, SampleDemoPickerViewModel>();
        }

        protected override void RegionMapping(IRegionManager regionManager)
        {
            regionManager.Mapping<HomeView>("MainRegion");
        }

        protected override void OnStartUp(IServiceContainer container)
        {
            var splash = container.GetService<ISplashService>();

            // Service A 초기화
            splash?.Report("Initializing Configuration Service...", 0.0);
            var serviceA = container.GetService<StartupServiceA>();
            serviceA.Initialize();

            splash?.Report("Configuration Service ready.", 0.4);
            Thread.Sleep(300);

            // Service B 초기화
            splash?.Report("Initializing Plugin Service...", 0.5);
            var serviceB = container.GetService<StartupServiceB>();
            serviceB.Initialize();

            splash?.Report("Plugin Service ready.", 0.9);
            Thread.Sleep(300);

            splash?.Report("All services initialized. Starting application...", 1.0);
            Thread.Sleep(400);
        }

        protected override void RegisterModules() { }
    }
}
