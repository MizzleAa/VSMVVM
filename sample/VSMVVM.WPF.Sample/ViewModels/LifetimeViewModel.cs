using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.WPF.Sample.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// ViewModel for DI lifetime comparison demo (Singleton/Scoped/Transient).
    /// </summary>

    public partial class LifetimeViewModel : ViewModelBase
    {
        private readonly IServiceContainer _container;

        [Property]
        private string _singletonId = "-";

        [Property]
        private int _singletonCount;

        [Property]
        private string _scopedId = "-";

        [Property]
        private int _scopedCount;

        [Property]
        private string _transientId = "-";

        [Property]
        private int _transientCount;

        [Property]
        private string _resolveLog = "";

        public LifetimeViewModel(IServiceContainer container)
        {
            _container = container;
        }

        [RelayCommand]
        private void ResolveSingleton()
        {
            var instance = _container.GetService<SingletonDemo>();
            SingletonId = instance.InstanceId;
            SingletonCount++;
            ResolveLog += $"[Singleton] ID={instance.InstanceId} (always same)\n";
        }

        [RelayCommand]
        private void ResolveScoped()
        {
            var instance = _container.GetService<ScopedDemo>();
            ScopedId = instance.InstanceId;
            ScopedCount++;
            ResolveLog += $"[Scoped] ID={instance.InstanceId}\n";
        }

        [RelayCommand]
        private void ResolveTransient()
        {
            var instance = _container.GetService<TransientDemo>();
            TransientId = instance.InstanceId;
            TransientCount++;
            ResolveLog += $"[Transient] ID={instance.InstanceId} (new each time)\n";
        }
    }
}
