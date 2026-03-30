using System;
using System.Windows;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.WPF.Sample.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// SubWindow ViewModel.
    /// Cross-window Messenger (Message&lt;string&gt;) + SharedCounter + StateStore(Redux) 데모.
    /// </summary>
    public partial class SubWindowViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SharedCounterService _counter;
        private readonly AppStateStore _stateStore;

        /// <summary>
        /// WeakReference 기반 Subscribe에서 GC 방지를 위한 강한 참조.
        /// </summary>
        private readonly Action<AppState> _onStateChanged;

        [Property]
        private string _windowId = "";

        [Property]
        private string _createdAt = "";

        // Shared Counter (Messenger<int>)
        [Property]
        private int _sharedCount;

        // Cross-window Messenger (Message<string>)
        [Property]
        private string _messageLog = "";

        [Property]
        private string _messageInput = "";

        // StateStore (Redux-style)
        [Property]
        private int _storeCounter;

        [Property]
        private string _storeLastAction = "";

        public SubWindowViewModel(IMessenger messenger, SharedCounterService counter, AppStateStore stateStore)
        {
            _messenger = messenger;
            _counter = counter;
            _stateStore = stateStore;

            WindowId = $"Window ID: {Guid.NewGuid().ToString("N").Substring(0, 8)}";
            CreatedAt = $"Created: {DateTime.Now:HH:mm:ss}";
            SharedCount = _counter.Count;

            // StateStore 초기값
            StoreCounter = _stateStore.State.Counter;
            StoreLastAction = _stateStore.State.LastAction;

            // Messenger 구독
            _messenger.Register<Message<int>>(this, OnCountChanged);
            _messenger.Register<Message<string>>(this, OnStringMessageReceived);

            // StateStore 구독 (강한 참조 유지 — WeakReference GC 방지)
            _onStateChanged = OnStateChanged;
            _stateStore.Subscribe(_onStateChanged);
        }

        private void OnCountChanged(object sender, Message<int> message)
        {
            SharedCount = message.Value;
        }

        private void OnStringMessageReceived(object sender, Message<string> message)
        {
            MessageLog += $"[{DateTime.Now:HH:mm:ss}] {message.Value}\n";
        }

        private void OnStateChanged(AppState state)
        {
            StoreCounter = state.Counter;
            StoreLastAction = state.LastAction;
        }

        #region Commands

        [RelayCommand]
        private void Increment()
        {
            _counter.Count++;
            SharedCount = _counter.Count;
            _messenger.Send(new Message<int>(_counter.Count));
        }

        [RelayCommand]
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageInput)) return;
            _messenger.Send(new Message<string>($"[Sub] {MessageInput}"));
            MessageInput = "";
        }

        [RelayCommand]
        private void StoreIncrement()
        {
            _stateStore.Increment();
        }

        [RelayCommand]
        private void StoreDecrement()
        {
            _stateStore.Decrement();
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

        #endregion
    }
}
