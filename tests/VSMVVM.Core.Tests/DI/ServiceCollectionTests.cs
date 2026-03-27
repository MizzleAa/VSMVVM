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
    }
}
