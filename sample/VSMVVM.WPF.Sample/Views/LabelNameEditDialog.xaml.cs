using System.Windows;

namespace VSMVVM.WPF.Sample.Views
{
    public partial class LabelNameEditDialog : Window
    {
        public LabelNameEditDialog()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
        }
    }
}
