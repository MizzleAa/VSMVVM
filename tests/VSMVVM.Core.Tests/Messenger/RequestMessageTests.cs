using System;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Messenger
{
    public class RequestMessageTests
    {
        [Fact]
        public void Reply_SetsResponse()
        {
            var msg = new RequestMessage<string>();
            msg.HasResponse.Should().BeFalse();

            msg.Reply("hello");

            msg.HasResponse.Should().BeTrue();
            msg.Response.Should().Be("hello");
        }

        [Fact]
        public void Response_BeforeReply_Throws()
        {
            var msg = new RequestMessage<string>();
            var act = () => msg.Response;
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Reply_Twice_Throws()
        {
            var msg = new RequestMessage<string>();
            msg.Reply("first");

            var act = () => msg.Reply("second");
            act.Should().Throw<InvalidOperationException>();
            msg.Response.Should().Be("first", "두 번째 Reply가 throw해도 첫 번째 응답은 유지되어야 한다");
        }

        [Fact]
        public async System.Threading.Tasks.Task ConcurrentReadAndWrite_NeverShowsHasResponseTrueWithDefaultResponse()
        {
            // 회귀 테스트: HasResponse=true이면 Response는 반드시 Reply가 설정한 값이어야 한다 (memory reorder 방지).
            const int N = 100;
            int violations = 0;

            for (int i = 0; i < N; i++)
            {
                var msg = new RequestMessage<string>();
                var start = new System.Threading.ManualResetEventSlim(false);

                var writer = System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    msg.Reply("payload");
                });

                var reader = System.Threading.Tasks.Task.Run(() =>
                {
                    start.Wait();
                    var sw = new System.Threading.SpinWait();
                    while (!msg.HasResponse)
                    {
                        sw.SpinOnce();
                    }
                    var v = msg.Response;
                    if (v != "payload")
                    {
                        System.Threading.Interlocked.Increment(ref violations);
                    }
                });

                start.Set();
                var both = System.Threading.Tasks.Task.WhenAll(writer, reader);
                var winner = await System.Threading.Tasks.Task.WhenAny(both, System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(2)));
                winner.Should().BeSameAs(both, "writer/reader 모두 2초 내에 끝나야 한다");
            }

            violations.Should().Be(0, "Reply가 끝났음을 HasResponse가 신호하면 Response는 반드시 그 값을 보여야 한다");
        }
    }
}
