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
        public void ConcurrentReadAndWrite_NeverShowsHasResponseTrueWithDefaultResponse()
        {
            // 회귀 테스트: 한 스레드가 Reply 도중 다른 스레드가 HasResponse=true를 보고
            // Response를 읽었을 때 default 값을 보면 안 된다 (memory reorder 방지).
            // RequestMessage 인스턴스를 많이 만들어 reader가 stale을 볼 가능성을 높인다.
            const int N = 1000;
            int violations = 0;

            System.Threading.Tasks.Parallel.For(0, N, _ =>
            {
                var msg = new RequestMessage<string>();
                var done = new System.Threading.ManualResetEventSlim(false);

                var writer = System.Threading.Tasks.Task.Run(() =>
                {
                    done.Wait();
                    msg.Reply("payload");
                });

                var reader = System.Threading.Tasks.Task.Run(() =>
                {
                    done.Wait();
                    while (!msg.HasResponse) { /* spin */ }
                    var v = msg.Response;
                    if (v != "payload")
                    {
                        System.Threading.Interlocked.Increment(ref violations);
                    }
                });

                done.Set();
                System.Threading.Tasks.Task.WaitAll(writer, reader);
            });

            violations.Should().Be(0, "Reply가 끝났음을 HasResponse가 신호하면 Response는 반드시 그 값을 보여야 한다");
        }
    }
}
