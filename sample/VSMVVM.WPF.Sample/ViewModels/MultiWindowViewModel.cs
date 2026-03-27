using System;
using System.Windows;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.WPF.Sample.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for multi-window management and cross-window state sync demo.

    /// </summary>

    public partial class MultiWindowViewModel : ViewModelBase
    {
        private readonly IServiceContainer _container;
        private readonly IMessenger _messenger;
        private readonly SharedCounterService _counter;

        [Property]
        private string _windowLog = "";

        [Property]
        private int _sharedCount;

        public MultiWindowViewModel(IServiceContainer container, IMessenger messenger, SharedCounterService counter)
        {
            _container = container;
            _messenger = messenger;
            _counter = counter;
            _sharedCount = _counter.Count;

            _messenger.Register<Message<int>>(this, OnCountChanged);
        }

        private void OnCountChanged(object sender, Message<int> message)
        {
            SharedCount = message.Value;
        }

        [RelayCommand]
        private void Increment()
        {
            _counter.Count++;
            SharedCount = _counter.Count;
            _messenger.Send(new Message<int>(_counter.Count));
            WindowLog += $"[Main] Count = {_counter.Count}\n";
        }

        [RelayCommand]
        private void OpenDialog()
        {
            var window = _container.GetService<Views.SubWindow>();
            window.ShowDialog();
            WindowLog += $"[Dialog] SubWindow closed\n";
        }

        [RelayCommand]
        private void OpenNonModal()
        {
            var window = _container.GetService<Views.SubWindow>();
            window.Show();
            WindowLog += $"[Show] SubWindow opened\n";
        }
    }
}
