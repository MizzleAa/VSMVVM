using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MVVM
{
    /// <summary>
    /// 회귀 방지: WPF/멀티스레드 환경에서 ICommand 의 CanExecuteChanged 핸들러는
    /// 보통 UI 스레드 디스패처를 검증한다 (WPF CommandManager). 백그라운드 스레드에서
    /// RaiseCanExecuteChanged 가 호출되면 InvalidOperationException 으로 죽음.
    /// <para>
    /// 정책: Command 가 생성될 때 <see cref="SynchronizationContext.Current"/> 를 capture 하고,
    /// RaiseCanExecuteChanged 가 다른 스레드에서 호출되면 capture 한 context 에 Post.
    /// </para>
    /// </summary>
    public class RelayCommandThreadMarshalingTests
    {
        /// <summary>
        /// 단일 스레드 SynchronizationContext — Post 호출이 큐에 쌓이고 Drain 으로 실행.
        /// xunit 환경엔 기본 SynchronizationContext 가 없을 수 있어 테스트 안에서 명시적으로 set.
        /// </summary>
        private sealed class QueuedSyncContext : SynchronizationContext
        {
            public List<(SendOrPostCallback cb, object state)> Posted { get; } = new();

            public override void Post(SendOrPostCallback d, object state)
                => Posted.Add((d, state));

            public override void Send(SendOrPostCallback d, object state) => d(state);

            public void Drain()
            {
                foreach (var (cb, state) in Posted) cb(state);
                Posted.Clear();
            }
        }

        [Fact]
        public void RelayCommand_RaiseCanExecuteChanged_FromOtherThread_MarshalsToCapturedContext()
        {
            var captured = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(captured);

            try
            {
                var command = new RelayCommand(() => { });
                int handlerThread = 0;
                command.CanExecuteChanged += (_, _) => handlerThread = Thread.CurrentThread.ManagedThreadId;
                int uiThread = Thread.CurrentThread.ManagedThreadId;

                // 백그라운드 스레드에서 RaiseCanExecuteChanged 호출.
                int? bgThread = null;
                var t = new Thread(() =>
                {
                    bgThread = Thread.CurrentThread.ManagedThreadId;
                    command.RaiseCanExecuteChanged();
                });
                t.Start();
                t.Join();

                bgThread.Should().NotBe(uiThread, "테스트 전제 — 백그라운드 스레드가 UI 와 달라야 함");
                // 핸들러는 아직 직접 호출되지 않았어야 함 (큐에 Post 된 상태).
                handlerThread.Should().Be(0, "핸들러는 capture 한 context 에 Post 만 되고 즉시 실행되지 않아야 함");

                captured.Posted.Should().NotBeEmpty("RaiseCanExecuteChanged 는 captured context 에 Post 해야 함");
                captured.Drain();

                handlerThread.Should().Be(uiThread, "Drain 후 핸들러는 capture 한 context(=UI 스레드) 에서 실행되어야 함");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void AsyncRelayCommand_RaiseCanExecuteChanged_FromOtherThread_MarshalsToCapturedContext()
        {
            var captured = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(captured);

            try
            {
                var command = new AsyncRelayCommand(() => Task.CompletedTask);
                int handlerThread = 0;
                command.CanExecuteChanged += (_, _) => handlerThread = Thread.CurrentThread.ManagedThreadId;
                int uiThread = Thread.CurrentThread.ManagedThreadId;

                int? bgThread = null;
                var t = new Thread(() =>
                {
                    bgThread = Thread.CurrentThread.ManagedThreadId;
                    command.RaiseCanExecuteChanged();
                });
                t.Start();
                t.Join();

                bgThread.Should().NotBe(uiThread);
                handlerThread.Should().Be(0);

                captured.Posted.Should().NotBeEmpty();
                captured.Drain();
                handlerThread.Should().Be(uiThread);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public void RelayCommand_NoCapturedContext_FallsBackToSynchronousInvoke()
        {
            // SynchronizationContext.Current 가 null (테스트/콘솔/non-UI) 인 환경에서는 마샬링 없이 직접 호출.
            SynchronizationContext.SetSynchronizationContext(null);

            var command = new RelayCommand(() => { });
            int handlerThread = 0;
            command.CanExecuteChanged += (_, _) => handlerThread = Thread.CurrentThread.ManagedThreadId;

            int? bgThread = null;
            var t = new Thread(() =>
            {
                bgThread = Thread.CurrentThread.ManagedThreadId;
                command.RaiseCanExecuteChanged();
            });
            t.Start();
            t.Join();

            handlerThread.Should().Be(bgThread, "captured context 가 없으면 호출 스레드에서 직접 실행");
        }

        [Fact]
        public void RaiseCanExecuteChanged_SameThreadAsCaptured_InvokesSynchronously()
        {
            // 호출 스레드가 capture 한 context 의 스레드와 같으면 굳이 Post 할 필요 없이 즉시 호출.
            var captured = new QueuedSyncContext();
            SynchronizationContext.SetSynchronizationContext(captured);

            try
            {
                var command = new RelayCommand(() => { });
                int handlerCalled = 0;
                command.CanExecuteChanged += (_, _) => handlerCalled++;

                command.RaiseCanExecuteChanged(); // 같은 스레드.

                handlerCalled.Should().Be(1, "같은 스레드에서 호출 시 즉시 실행");
                captured.Posted.Should().BeEmpty("동일 스레드 시나리오에서는 Post 큐를 우회");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }
    }
}
