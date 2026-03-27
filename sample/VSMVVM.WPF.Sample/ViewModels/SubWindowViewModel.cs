using System;
using System.Windows;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.WPF.Sample.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for SubWindow with shared counter display.

    /// </summary>

    public partial class SubWindowViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SharedCounterService _counter;

        [Property]
        private string _windowId = "";

        [Property]
        private string _createdAt = "";

        [Property]
        private int _sharedCount;

        public SubWindowViewModel(IMessenger messenger, SharedCounterService counter)
        {
            _messenger = messenger;
            _counter = counter;

            WindowId = $"Window ID: {Guid.NewGuid().ToString("N").Substring(0, 8)}";
            CreatedAt = $"Created: {DateTime.Now:HH:mm:ss}";
            SharedCount = _counter.Count;

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
        }

        [RelayCommand]
        private void Close()
        {
            _messenger.UnregisterAll(this);
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (w.DataContext == this)
                    {
                        w.Close();
                        break;
                    }
                }
            });
        }
    }
}
