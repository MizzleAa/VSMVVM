using System;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MVVM
{
    public class LocalizeServiceTests
    {
        private static ILocalizeService CreateService()
        {
            var t = typeof(ILocalizeService).Assembly.GetType("VSMVVM.Core.MVVM.LocalizeService");
            return (ILocalizeService)Activator.CreateInstance(t);
        }

        [Fact]
        public void ChangeLocale_FiresSubscriber()
        {
            var svc = CreateService();
            string received = null;
            using var sub = svc.Subscribe(locale => received = locale);

            svc.ChangeLocale("ko-KR");

            received.Should().Be("ko-KR");
        }

        [Fact]
        public void DisposeSubscription_StopsNotification()
        {
            var svc = CreateService();
            int callCount = 0;
            var sub = svc.Subscribe(_ => callCount++);
            sub.Dispose();

            svc.ChangeLocale("en-US");

            callCount.Should().Be(0);
        }

        [Fact]
        public void Subscribe_KeepsHandlerAlive_UntilDispose()
        {
            // strong-ref API: IDisposable 살아있는 동안 콜백도 살아있어야 한다.
            // (WeakReference 시절의 GC 의존 동작은 폐기 — 이제는 명시적 Dispose 가 단일 진입점)
            var svc = CreateService();
            int callCount = 0;
            var sub = svc.Subscribe(_ => callCount++);

            for (int i = 0; i < 3; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }

            svc.ChangeLocale("en-US");
            callCount.Should().Be(1, "Dispose 호출 전이라면 GC 후에도 콜백이 살아있어야 한다");

            sub.Dispose();
        }

        [Fact]
        public void Subscriber_ThrowsException_OtherSubscribersStillInvoked()
        {
            // 회귀 테스트: 한 핸들러의 예외가 다른 핸들러를 막지 않아야 한다.
            var svc = CreateService();
            int laterCallCount = 0;
            using var sub1 = svc.Subscribe(_ => throw new InvalidOperationException("boom"));
            using var sub2 = svc.Subscribe(_ => laterCallCount++);

            // ChangeLocale 자체는 throw하지 않아야 한다 (핸들러 예외는 격리되어야 함).
            Action act = () => svc.ChangeLocale("en-US");
            act.Should().NotThrow();
            laterCallCount.Should().Be(1, "앞 핸들러가 throw해도 뒤 핸들러는 호출되어야 한다");
        }
    }
}