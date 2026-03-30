using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Messenger pub/sub 데모.
    /// 같은 View 내 송수신 + SubWindow 열어서 크로스 윈도우 메시징 확인.
    /// </summary>
    public partial class MessengerViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly IServiceContainer _container;

        [Property]
        private string _messageInput = "";

        [Property]
        private string _sendLog = "";

        [Property]
        private string _receiveLog = "";

        public MessengerViewModel(IMessenger messenger, IServiceContainer container)
        {
            _messenger = messenger;
            _container = container;
            _messenger.Register<Message<string>>(this, OnMessageReceived);
        }

        private void OnMessageReceived(object sender, Message<string> message)
        {
            ReceiveLog += $"[{DateTime.Now:HH:mm:ss}] Received: {message.Value}\n";
        }

        [RelayCommand]
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageInput))
            {
                return;
            }

            _messenger.Send(new Message<string>(MessageInput));
            SendLog += $"[{DateTime.Now:HH:mm:ss}] Sent: {MessageInput}\n";
            MessageInput = "";
        }

        [RelayCommand]
        private void OpenSubWindow()
        {
            var window = _container.GetService<Views.SubWindow>();
            window.Show();
        }
    }
}
