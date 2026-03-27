using System.ComponentModel.DataAnnotations;
using System.Linq;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for DataAnnotation validation with Property source gen.

    /// </summary>

    public partial class ValidationViewModel : ObservableValidator
    {
        [Property]
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Name must be 2-50 characters.")]
        private string _name = "";

        [Property]
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        private string _email = "";

        [Property]
        [Required(ErrorMessage = "Age is required.")]
        [Range(1, 150, ErrorMessage = "Age must be between 1 and 150.")]
        private int _age;

        [Property]
        private string _submitResult = "";

        public string NameError => GetFirstError(nameof(Name));
        public string EmailError => GetFirstError(nameof(Email));
        public string AgeError => GetFirstError(nameof(Age));

        private string GetFirstError(string propertyName)
        {
            var errors = GetErrors(propertyName)?.Cast<string>();
            return errors?.FirstOrDefault() ?? "";
        }

        [RelayCommand]
        private void Submit()
        {
            ValidateAllProperties();
            OnPropertyChanged(nameof(NameError));
            OnPropertyChanged(nameof(EmailError));
            OnPropertyChanged(nameof(AgeError));

            SubmitResult = HasErrors ? "Validation failed." : $"Submitted: {Name}, {Email}, Age {Age}";
        }
    }
}
