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
        public void ChangeLocale_FiresLocaleChanged()
        {
            var svc = CreateService();
            string received = null;
            Action<string> handler = locale => received = locale;
            svc.LocaleChanged += handler;

            svc.ChangeLocale("ko-KR");

            received.Should().Be("ko-KR");
        }

        [Fact]
        public void RemoveHandler_StopsNotification()
        {
            var svc = CreateService();
            int callCount = 0;
            Action<string> handler = _ => callCount++;
            svc.LocaleChanged += handler;
            svc.LocaleChanged -= handler;

            svc.ChangeLocale("en-US");

            callCount.Should().Be(0);
        }

        [Fact]
        public void LocaleChanged_HandlerSubscriberIsNotKeptAlive()
        {
            // 회귀 테스트: 약참조 기반이라 구독자 객체가 GC 가능해야 한다.
            var svc = CreateService();

            var weakSubscriber = SubscribeWithThrowawaySubscriber(svc);

            for (int i = 0; i < 5 && weakSubscriber.IsAlive; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
                GC.WaitForPendingFinalizers();
            }

            weakSubscriber.IsAlive.Should().BeFalse(
                "약참조 이벤트라면 외부 강참조가 사라지면 구독자가 GC되어야 한다");
        }

        // 람다가 enclosing local variable을 캡처하지 않게 별도 메서드로 분리.
        private static WeakReference SubscribeWithThrowawaySubscriber(ILocalizeService svc)
        {
            var subscriber = new EventSubscriber();
            svc.LocaleChanged += subscriber.OnChanged;
            return new WeakReference(subscriber);
        }

        private sealed class EventSubscriber
        {
            public void OnChanged(string locale) { }
        }
    }
}
