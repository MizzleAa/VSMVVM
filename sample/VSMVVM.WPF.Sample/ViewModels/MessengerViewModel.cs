using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for IMessenger pub/sub messaging demo.

    /// </summary>

    public partial class MessengerViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;

        [Property]
        private string _messageInput = "";

        [Property]
        private string _sendLog = "";

        [Property]
        private string _receiveLog = "";

        public MessengerViewModel(IMessenger messenger)
        {
            _messenger = messenger;
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
    }
}
