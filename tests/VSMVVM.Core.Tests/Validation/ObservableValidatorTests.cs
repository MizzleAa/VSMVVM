using System.Collections.Generic;
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

        [Fact]
        public void ValidateAll_ErrorClearedOnRevalidate_FiresErrorsChanged()
        {
            // 회귀 테스트: 직전 검증에서 에러가 있던 프로퍼티가 새 검증에서 클리어되면
            // ErrorsChanged가 발화되어야 UI가 stale 상태로 남지 않는다.
            var vm = new TestValidatorViewModel { Name = null };
            vm.Validate(); // 첫 검증: Name에 에러

            var changedProperties = new List<string>();
            vm.ErrorsChanged += (s, e) => changedProperties.Add(e.PropertyName);

            vm.Name = "John"; // 유효한 값
            vm.Validate(); // 재검증: Name 에러 클리어되어야 함

            vm.HasErrors.Should().BeFalse();
            changedProperties.Should().Contain(nameof(TestValidatorViewModel.Name),
                "에러가 사라진 프로퍼티에 대해서도 ErrorsChanged가 발화되어야 한다");
        }
    }
}
