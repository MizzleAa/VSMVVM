using System.Windows;

namespace VSMVVM.WPF.NoPattern.Sample
{
    public partial class SubWindow : Window
    {
        public SubWindow()
        {
            InitializeComponent();
            UpdateCount();
            App.SharedCountChanged += UpdateCount;
            Closed += (_, _) => App.SharedCountChanged -= UpdateCount;
        }

        private void UpdateCount()
        {
            Dispatcher.Invoke(() => CountText.Text = App.SharedCount.ToString());
        }

        private void Increment_Click(object sender, RoutedEventArgs e)
        {
            App.SharedCount++;
        }
    }
}
