using System;
using System.Threading;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 모든 RelayCommand / AsyncRelayCommand 공통 — CanExecuteChanged 핸들러 호출을 capture 한
    /// <see cref="SynchronizationContext"/> 로 마샬링.
    /// <para>
    /// 동기 시나리오:
    ///   • Command 가 UI 스레드에서 생성되면 <see cref="SynchronizationContext.Current"/> capture.
    ///   • 백그라운드에서 RaiseCanExecuteChanged 호출 시 capture 한 context 에 Post.
    ///   • 같은 스레드에서 호출 시 Post 우회 — 즉시 실행.
    ///   • Capture 시점에 context 가 null 이면 (테스트/콘솔) 마샬링 없이 직접 실행.
    /// </para>
    /// <para>
    /// 이 가드가 없으면 WPF 의 <c>CanExecuteChangedEventManager.HandlerSink</c> 가 디스패처 access check 에서
    /// <c>InvalidOperationException</c> 을 던진다. 비동기 메시지 수신 → setter → NotifyCanExecuteChanged
    /// 경로가 백그라운드 스레드에서 실행될 때 흔한 버그.
    /// </para>
    /// </summary>
    internal static class CommandThreadMarshal
    {
        /// <summary>
        /// 호출 스레드와 captured context 의 스레드가 다르면 Post, 같으면 직접 호출. context 가 null 이면 직접 호출.
        /// </summary>
        public static void Invoke(SynchronizationContext capturedContext, EventHandler handler, object sender)
        {
            if (handler == null) return;
            if (capturedContext == null || capturedContext == SynchronizationContext.Current)
            {
                handler(sender, EventArgs.Empty);
            }
            else
            {
                capturedContext.Post(_ => handler(sender, EventArgs.Empty), null);
            }
        }
    }
}
