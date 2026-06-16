using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class HomeView : UserControl
    {
        private int _count;

        public HomeView()
        {
            InitializeComponent();
        }

        private void CounterButton_Click(object sender, RoutedEventArgs e)
        {
            _count++;
            CountText.Text = _count.ToString();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _count = 0;
            CountText.Text = _count.ToString();
        }
    }
}
