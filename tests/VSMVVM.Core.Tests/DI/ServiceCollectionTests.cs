using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.DI
{
    public interface ITestService { string Name { get; } }
    public class TestServiceA : ITestService { public string Name => "A"; }
    public class TestServiceB : ITestService { public string Name => "B"; }
    public class DependentService
    {
        public ITestService Inner { get; }
        public DependentService(ITestService inner) => Inner = inner;
    }

    public class ServiceCollectionTests
    {
        [Fact]
        public void Singleton_ReturnsSameInstance()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ITestService, TestServiceA>();
            var container = sc.CreateContainer();

            var a = container.GetService<ITestService>();
            var b = container.GetService<ITestService>();

            a.Should().BeSameAs(b);
        }

        [Fact]
        public void Transient_ReturnsDifferentInstances()
        {
            var sc = new ServiceCollection();
            sc.AddTransient<ITestService, TestServiceA>();
            var container = sc.CreateContainer();

            var a = container.GetService<ITestService>();
            var b = container.GetService<ITestService>();

            a.Should().NotBeSameAs(b);
        }

        [Fact]
        public void Instance_ReturnsSameObject()
        {
            var sc = new ServiceCollection();
            var instance = new TestServiceA();
            sc.AddSingleton<ITestService, TestServiceA>(instance);
            var container = sc.CreateContainer();

            var resolved = container.GetService<ITestService>();

            resolved.Should().BeSameAs(instance);
        }

        [Fact]
        public void Factory_InvokesFactory()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ITestService, TestServiceA>(c => new TestServiceB());
            var container = sc.CreateContainer();

            var resolved = container.GetService<ITestService>();

            resolved.Name.Should().Be("B");
        }

        [Fact]
        public void ConstructorInjection_ResolvesAutomatically()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ITestService, TestServiceA>();
            sc.AddSingleton<DependentService>();
            var container = sc.CreateContainer();

            var resolved = container.GetService<DependentService>();

            resolved.Inner.Should().NotBeNull();
            resolved.Inner.Name.Should().Be("A");
        }

        [Fact]
        public void UnregisteredService_ThrowsInvalidOperationException()
        {
            var sc = new ServiceCollection();
            var container = sc.CreateContainer();

            var act = () => container.GetService<ITestService>();

            act.Should().Throw<System.InvalidOperationException>();
        }

        [Fact]
        public void InterfaceAndImplementation_ResolvesCorrectly()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ITestService, TestServiceA>();
            var container = sc.CreateContainer();

            var resolved = container.GetService<ITestService>();

            resolved.Should().BeOfType<TestServiceA>();
        }

        public class MultiCtorService
        {
            public ITestService Inner { get; }
            public bool UsedSimple { get; }

            public MultiCtorService() { UsedSimple = true; }
            public MultiCtorService(ITestService inner) { Inner = inner; }
        }

        [Fact]
        public void ConstructorSelection_FallsBackToResolvableCtor()
        {
            // 회귀 테스트: 가장 큰 ctor가 미등록 의존성을 요구하면, resolvable한 더 작은 ctor로 fallback해야 한다.
            // ITestService를 일부러 등록하지 않는다.
            var sc = new ServiceCollection();
            sc.AddSingleton<MultiCtorService>();
            var container = sc.CreateContainer();

            var resolved = container.GetService<MultiCtorService>();

            resolved.UsedSimple.Should().BeTrue("ITestService가 미등록이므로 0-param ctor로 fallback해야 한다");
            resolved.Inner.Should().BeNull();
        }

        [Fact]
        public void ConstructorSelection_PrefersLargestSatisfiableCtor()
        {
            // ITestService가 등록되면 더 큰 ctor를 선택해야 한다.
            var sc = new ServiceCollection();
            sc.AddSingleton<ITestService, TestServiceA>();
            sc.AddSingleton<MultiCtorService>();
            var container = sc.CreateContainer();

            var resolved = container.GetService<MultiCtorService>();

            resolved.UsedSimple.Should().BeFalse();
            resolved.Inner.Should().NotBeNull();
        }

        public class DisposableSingleton : System.IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }

        [Fact]
        public void Dispose_DisposesCachedDisposableSingletons()
        {
            // 회귀 테스트: ServiceContainer.Dispose는 캐시된 IDisposable singleton 인스턴스도 함께 정리해야 한다.
            var sc = new ServiceCollection();
            sc.AddSingleton<DisposableSingleton>();
            var container = (System.IDisposable)sc.CreateContainer();
            var resolvedContainer = (IServiceContainer)container;

            var instance = resolvedContainer.GetService<DisposableSingleton>();
            instance.DisposeCount.Should().Be(0);

            container.Dispose();

            instance.DisposeCount.Should().Be(1, "캐시된 IDisposable singleton은 컨테이너 dispose 시 함께 dispose되어야 한다");
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<DisposableSingleton>();
            var container = (System.IDisposable)sc.CreateContainer();
            var resolvedContainer = (IServiceContainer)container;
            var instance = resolvedContainer.GetService<DisposableSingleton>();

            container.Dispose();
            container.Dispose();
            container.Dispose();

            instance.DisposeCount.Should().Be(1, "Dispose는 멱등이어야 한다");
        }

        [Fact]
        public void Dispose_NotResolvedSingletons_AreNotDisposed()
        {
            // 등록만 되고 한 번도 resolve된 적 없는 IDisposable은 dispose하지 않아야 한다 (인스턴스 자체가 없음).
            var sc = new ServiceCollection();
            sc.AddSingleton<DisposableSingleton>();
            var container = (System.IDisposable)sc.CreateContainer();

            // resolve 안 함

            var act = () => container.Dispose();
            act.Should().NotThrow();
        }
    }
}
