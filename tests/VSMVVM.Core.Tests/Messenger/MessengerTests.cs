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

        private class RecipientWithHandlers
        {
            public int CountA;
            public int CountB;

            public void HandleA(object sender, TestMessage msg) => CountA++;
            public void HandleB(object sender, TestMessage msg) => CountB++;
        }

        [Fact]
        public void Register_Twice_SameRecipientSameHandler_InvokedOnce()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            var recipient = new RecipientWithHandlers();

            messenger.Register<TestMessage>(recipient, recipient.HandleA);
            messenger.Register<TestMessage>(recipient, recipient.HandleA);

            messenger.Send(new TestMessage { Data = "X" });

            recipient.CountA.Should().Be(1);
        }

        [Fact]
        public void Register_Twice_SameRecipientDifferentHandler_BothInvoked()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            var recipient = new RecipientWithHandlers();

            messenger.Register<TestMessage>(recipient, recipient.HandleA);
            messenger.Register<TestMessage>(recipient, recipient.HandleB);

            messenger.Send(new TestMessage { Data = "X" });

            recipient.CountA.Should().Be(1);
            recipient.CountB.Should().Be(1);
        }

        [Fact]
        public void Register_Twice_DifferentTokens_BothInvoked()
        {
            var messenger = new VSMVVM.Core.MVVM.Messenger();
            var recipient = new RecipientWithHandlers();

            messenger.Register<TestMessage>(recipient, "token-1", recipient.HandleA);
            messenger.Register<TestMessage>(recipient, "token-2", recipient.HandleA);

            messenger.Send(new TestMessage { Data = "X" }, "token-1");
            messenger.Send(new TestMessage { Data = "Y" }, "token-2");

            recipient.CountA.Should().Be(2);
        }
    }
}
