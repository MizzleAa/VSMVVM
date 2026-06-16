using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class ValidationView : UserControl
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled);

        public ValidationView()
        {
            InitializeComponent();
        }

        private void Field_Changed(object sender, TextChangedEventArgs e)
        {
            Validate();
        }

        private bool Validate()
        {
            var nameOk = ValidateName(out var nameMsg);
            NameError.Text = nameMsg;

            var emailOk = ValidateEmail(out var emailMsg);
            EmailError.Text = emailMsg;

            var ageOk = ValidateAge(out var ageMsg);
            AgeError.Text = ageMsg;

            return nameOk && emailOk && ageOk;
        }

        private bool ValidateName(out string error)
        {
            var v = NameBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(v)) { error = "Name is required."; return false; }
            if (v.Length is < 2 or > 50) { error = "Name must be 2-50 characters."; return false; }
            error = "";
            return true;
        }

        private bool ValidateEmail(out string error)
        {
            var v = EmailBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(v)) { error = "Email is required."; return false; }
            if (!EmailRegex.IsMatch(v)) { error = "Invalid email format."; return false; }
            error = "";
            return true;
        }

        private bool ValidateAge(out string error)
        {
            var v = AgeBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(v)) { error = "Age is required."; return false; }
            if (!int.TryParse(v, out var n)) { error = "Age must be a number."; return false; }
            if (n is < 1 or > 150) { error = "Age must be between 1 and 150."; return false; }
            error = "";
            return true;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (Validate())
            {
                SubmitResult.Text = $"Saved: {NameBox.Text} <{EmailBox.Text}>, age {AgeBox.Text}";
            }
            else
            {
                SubmitResult.Text = "Please fix the errors above.";
            }
        }
    }
}
