using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MessengerTests
{
    public class TestMessage : MessageBase
    {
        public string Data { get; set; }
    }

    public class OtherMessage : MessageBase
    {
        public int Value { get; set; }
    }

    public class MessengerTests
    {
        [Fact]
        public void Register_Send_InvokesCallback()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            string received = null;
            var recipient = new object();
            messenger.Register<TestMessage>(recipient, (sender, msg) => received = msg.Data);

            messenger.Send(new TestMessage { Data = "Hello" });

            received.Should().Be("Hello");
        }

        [Fact]
        public void Unregister_Send_DoesNotInvokeCallback()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            string received = null;
            var recipient = new object();
            messenger.Register<TestMessage>(recipient, (sender, msg) => received = msg.Data);
            messenger.Unregister<TestMessage>(recipient);

            messenger.Send(new TestMessage { Data = "Hello" });

            received.Should().BeNull();
        }

        [Fact]
        public void MultipleSubscribers_AllReceive()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            int count = 0;
            var sub1 = new object();
            var sub2 = new object();
            messenger.Register<TestMessage>(sub1, (s, m) => count++);
            messenger.Register<TestMessage>(sub2, (s, m) => count++);

            messenger.Send(new TestMessage { Data = "X" });

            count.Should().Be(2);
        }

        [Fact]
        public void DifferentMessageType_OnlyCorrectTypeReceived()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            bool testReceived = false;
            bool otherReceived = false;
            var recipient = new object();
            messenger.Register<TestMessage>(recipient, (s, m) => testReceived = true);
            messenger.Register<OtherMessage>(recipient, (s, m) => otherReceived = true);

            messenger.Send(new TestMessage { Data = "A" });

            testReceived.Should().BeTrue();
            otherReceived.Should().BeFalse();
        }
    }
}
