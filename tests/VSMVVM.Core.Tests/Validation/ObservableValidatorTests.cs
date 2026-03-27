using System.ComponentModel.DataAnnotations;
using System.Linq;
using FluentAssertions;
using VSMVVM.Core.MVVM;
using Xunit;

namespace VSMVVM.Core.Tests.Validation
{
    public class TestValidatorViewModel : ObservableValidator
    {
        private string _name;

        [Required(ErrorMessage = "Name is required.")]
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters.")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public void Validate() => ValidateAllProperties();
    }

    public class ObservableValidatorTests
    {
        [Fact]
        public void ValidateAll_ValidData_HasErrorsIsFalse()
        {
            var vm = new TestValidatorViewModel { Name = "John" };

            vm.Validate();

            vm.HasErrors.Should().BeFalse();
        }

        [Fact]
        public void ValidateAll_InvalidData_HasErrorsIsTrue()
        {
            var vm = new TestValidatorViewModel { Name = null };

            vm.Validate();

            vm.HasErrors.Should().BeTrue();
        }

        [Fact]
        public void GetErrors_ReturnsErrorMessages()
        {
            var vm = new TestValidatorViewModel { Name = null };

            vm.Validate();

            var errors = vm.GetErrors(nameof(TestValidatorViewModel.Name))
                .Cast<string>().ToList();
            errors.Should().Contain("Name is required.");
        }
    }
}
