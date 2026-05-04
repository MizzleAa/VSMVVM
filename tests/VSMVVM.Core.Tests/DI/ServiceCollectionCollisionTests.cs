using System;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

// 의도적으로 같은 단순명을 가진 두 타입을 서로 다른 네임스페이스에 둔다.
namespace VSMVVM.Core.Tests.DI.NamespaceA
{
    public class Logger { }
}

namespace VSMVVM.Core.Tests.DI.NamespaceB
{
    public class Logger { }
}

namespace VSMVVM.Core.Tests.DI
{
    /// <summary>
    /// 회귀 방지: ServiceCollection은 Type.Name(단순명)을 GetService(string)/Navigate(string viewName)
    /// 등의 키로 사용한다. 서로 다른 네임스페이스의 동일 단순명 타입이 등록되면 조용히 덮어써져
    /// 잘못된 타입이 해석되는 사일런트 버그가 발생한다. 이를 명확한 예외로 감지해야 한다.
    /// </summary>
    public class ServiceCollectionCollisionTests
    {
        [Fact]
        public void Registering_Two_Types_With_Same_Simple_Name_Throws()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<NamespaceA.Logger>();

            Action act = () => sc.AddSingleton<NamespaceB.Logger>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Logger*", "충돌한 이름이 메시지에 포함되어야 함")
                .And.Message.Should().Contain("NamespaceA").And.Contain("NamespaceB");
        }

        [Fact]
        public void Re_Registering_Same_Type_Is_Allowed()
        {
            // 같은 타입을 다시 등록하는 건 정상 사용 패턴(덮어쓰기). 충돌이 아니다.
            var sc = new ServiceCollection();
            sc.AddSingleton<NamespaceA.Logger>();

            Action act = () => sc.AddSingleton<NamespaceA.Logger>();

            act.Should().NotThrow();
        }

        [Fact]
        public void GetService_By_Name_Returns_Correct_Type_When_No_Collision()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<NamespaceA.Logger>();
            var container = sc.CreateContainer();

            var resolved = container.GetService("Logger");

            resolved.Should().BeOfType<NamespaceA.Logger>();
        }
    }
}
