using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class DialogView : UserControl
    {
        public DialogView()
        {
            InitializeComponent();
        }

        private void ShowMessage_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            MessageBox.Show(owner, "Hello from MessageBox!", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResultText.Text = "Result: OK";
        }

        private void ShowConfirm_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = MessageBox.Show(owner, "Do you want to continue?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            DialogResultText.Text = $"Result: {result}";
        }

        private void ShowOKCancel_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var result = MessageBox.Show(owner, "Save changes before closing?", "Save",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            DialogResultText.Text = $"Result: {result}";
        }
    }
}
