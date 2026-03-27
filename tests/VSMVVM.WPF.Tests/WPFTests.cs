using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.WPF.Tests
{
    public class ServiceLocatorTests
    {
        [Fact]
        public void SetAndGet_ReturnsSameContainer()
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<ServiceCollection>();
            var container = sc.CreateContainer();
            ServiceLocator.SetServiceProvider(container);

            var resolved = ServiceLocator.GetServiceProvider();

            resolved.Should().BeSameAs(container);
        }
    }

    public class DialogResultTests
    {
        [Fact]
        public void DialogResultType_HasExpectedValues()
        {
            DialogResultType.OK.Should().Be(DialogResultType.OK);
            DialogResultType.Cancel.Should().Be(DialogResultType.Cancel);
            DialogResultType.Yes.Should().Be(DialogResultType.Yes);
            DialogResultType.No.Should().Be(DialogResultType.No);
            DialogResultType.None.Should().Be(DialogResultType.None);
        }

        [Fact]
        public void DialogResult_CarriesData()
        {
            var result = new DialogResult<string>(DialogResultType.OK, "test");

            result.Result.Should().Be(DialogResultType.OK);
            result.Data.Should().Be("test");
        }
    }
}
