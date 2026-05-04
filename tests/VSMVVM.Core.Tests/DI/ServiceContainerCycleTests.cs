using System;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.DI
{
    /// <summary>
    /// 회귀 방지: 순환 의존이 있는 서비스 등록 시 .NET Lazy의 의미 불명한
    /// 'ValueFactory attempted to access the Value property of this instance.' 메시지 대신
    /// VSMVVM이 자체적으로 명확한 'Circular dependency detected' 메시지로 throw해야 한다.
    /// </summary>
    public class ServiceContainerCycleTests
    {
        public class CycleA
        {
            public CycleA(CycleB b) { }
        }

        public class CycleB
        {
            public CycleB(CycleA a) { }
        }

        public class SelfRef
        {
            // 자기 자신을 의존
            public SelfRef(SelfRef other) { }
        }

        public interface IFoo { }

        public class FooImpl : IFoo
        {
            public FooImpl(IFoo self) { }
        }

        [Fact]
        public void Cycle_Between_Two_Services_Throws_Clear_Message()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<CycleA>();
            sc.AddSingleton<CycleB>();
            var container = sc.CreateContainer();

            Action act = () => container.GetService<CycleA>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Circular dependency detected*",
                    "사용자에게 사이클 발생을 명확히 알려야 함")
                .And.Message.Should()
                    .Contain("CycleA").And.Contain("CycleB",
                    "사이클 경로에 양쪽 타입 이름이 모두 포함되어야 함");
        }

        [Fact]
        public void Self_Dependency_Throws_Clear_Message()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<SelfRef>();
            var container = sc.CreateContainer();

            Action act = () => container.GetService<SelfRef>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Circular dependency detected*");
        }

        [Fact]
        public void Cycle_Through_Interface_Throws_Clear_Message()
        {
            // IFoo → FooImpl → IFoo 순환
            var sc = new ServiceCollection();
            sc.AddSingleton<IFoo, FooImpl>();
            var container = sc.CreateContainer();

            Action act = () => container.GetService<IFoo>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Circular dependency detected*");
        }

        [Fact]
        public void After_Cycle_Detection_Subsequent_Resolutions_Still_Work()
        {
            // 순환 감지 후 ThreadLocal 체인이 정상 정리되어 다른 서비스 해석에 영향이 없어야 한다.
            var sc = new ServiceCollection();
            sc.AddSingleton<SelfRef>();
            sc.AddSingleton<CycleA>(); // 등록만, resolve는 안 함
            sc.AddSingleton<string>(_ => "ok");
            var container = sc.CreateContainer();

            try { container.GetService<SelfRef>(); } catch (InvalidOperationException) { }

            var ok = container.GetService<string>();
            ok.Should().Be("ok", "사이클 throw 후에도 다른 서비스는 정상 해석되어야 함");
        }
    }
}
