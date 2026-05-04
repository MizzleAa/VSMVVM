using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.MVVM
{
    /// <summary>
    /// IDispatcherService 인터페이스 계약과 DI 해석 동작을 검증한다.
    /// 인터페이스이므로 동작 검증은 테스트용 가짜 구현체(FakeDispatcher)로 수행한다.
    /// </summary>
    public class DispatcherServiceTests
    {
        private sealed class FakeDispatcher : IDispatcherService
        {
            public int InvokeCallCount;
            public int InvokeAsyncCallCount;
            public int CheckAccessCallCount;
            public bool CheckAccessReturnValue = true;

            public void Invoke(Action action)
            {
                Interlocked.Increment(ref InvokeCallCount);
                action?.Invoke();
            }

            public void InvokeAsync(Action action)
            {
                Interlocked.Increment(ref InvokeAsyncCallCount);
                action?.Invoke();
            }

            public bool CheckAccess()
            {
                Interlocked.Increment(ref CheckAccessCallCount);
                return CheckAccessReturnValue;
            }
        }

        [Fact]
        public void IDispatcherService_Defines_Invoke_With_Action_Parameter()
        {
            var method = typeof(IDispatcherService).GetMethod(nameof(IDispatcherService.Invoke));

            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(void));
            var parameters = method.GetParameters();
            parameters.Should().HaveCount(1);
            parameters[0].ParameterType.Should().Be(typeof(Action));
        }

        [Fact]
        public void IDispatcherService_Defines_InvokeAsync_With_Action_Parameter()
        {
            var method = typeof(IDispatcherService).GetMethod(nameof(IDispatcherService.InvokeAsync));

            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(void));
            var parameters = method.GetParameters();
            parameters.Should().HaveCount(1);
            parameters[0].ParameterType.Should().Be(typeof(Action));
        }

        [Fact]
        public void IDispatcherService_Defines_CheckAccess_Returning_Bool()
        {
            var method = typeof(IDispatcherService).GetMethod(nameof(IDispatcherService.CheckAccess));

            method.Should().NotBeNull();
            method!.ReturnType.Should().Be(typeof(bool));
            method.GetParameters().Should().BeEmpty();
        }

        [Fact]
        public void IDispatcherService_Is_Public_Interface()
        {
            var type = typeof(IDispatcherService);

            type.IsInterface.Should().BeTrue();
            type.IsPublic.Should().BeTrue();
        }

        [Fact]
        public void Invoke_Executes_The_Action()
        {
            var dispatcher = new FakeDispatcher();
            var executed = false;

            dispatcher.Invoke(() => executed = true);

            executed.Should().BeTrue();
            dispatcher.InvokeCallCount.Should().Be(1);
        }

        [Fact]
        public void InvokeAsync_Executes_The_Action()
        {
            var dispatcher = new FakeDispatcher();
            var executed = false;

            dispatcher.InvokeAsync(() => executed = true);

            executed.Should().BeTrue();
            dispatcher.InvokeAsyncCallCount.Should().Be(1);
        }

        [Fact]
        public void CheckAccess_Returns_Configured_Value()
        {
            var dispatcher = new FakeDispatcher { CheckAccessReturnValue = false };

            var result = dispatcher.CheckAccess();

            result.Should().BeFalse();
            dispatcher.CheckAccessCallCount.Should().Be(1);
        }

        [Fact]
        public void Container_Resolves_Registered_DispatcherService_As_Singleton()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IDispatcherService, FakeDispatcher>();
            var container = sc.CreateContainer();

            var first = container.GetService<IDispatcherService>();
            var second = container.GetService<IDispatcherService>();

            first.Should().NotBeNull();
            first.Should().BeOfType<FakeDispatcher>();
            first.Should().BeSameAs(second);
        }

        [Fact]
        public void Container_Returns_Provided_Instance_When_Registered_As_Instance()
        {
            var instance = new FakeDispatcher();
            var sc = new ServiceCollection();
            sc.AddSingleton<IDispatcherService, FakeDispatcher>(instance);
            var container = sc.CreateContainer();

            var resolved = container.GetService<IDispatcherService>();

            resolved.Should().BeSameAs(instance);
        }

        [Fact]
        public void Concurrent_Singleton_Resolution_Yields_Single_Instance()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IDispatcherService, FakeDispatcher>();
            var container = sc.CreateContainer();

            var resolved = Enumerable.Range(0, 32)
                .AsParallel()
                .WithDegreeOfParallelism(16)
                .Select(_ => container.GetService<IDispatcherService>())
                .ToArray();

            resolved.Should().OnlyContain(x => x != null);
            resolved.Distinct().Should().HaveCount(1);
        }
    }
}
