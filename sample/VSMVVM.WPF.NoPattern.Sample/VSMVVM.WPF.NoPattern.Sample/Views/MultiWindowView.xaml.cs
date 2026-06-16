using System;
using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class MultiWindowView : UserControl
    {
        public MultiWindowView()
        {
            InitializeComponent();
            UpdateCount();
            App.SharedCountChanged += UpdateCount;
            Unloaded += (_, _) => App.SharedCountChanged -= UpdateCount;
        }

        private void UpdateCount()
        {
            Dispatcher.Invoke(() => CountText.Text = App.SharedCount.ToString());
        }

        private void IncrementMain_Click(object sender, RoutedEventArgs e)
        {
            App.SharedCount++;
        }

        private void OpenModal_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            var w = new SubWindow { Owner = owner };
            AppendLog($"Dialog opened at {DateTime.Now:HH:mm:ss}");
            w.ShowDialog();
            AppendLog($"Dialog closed at {DateTime.Now:HH:mm:ss}");
        }

        private void OpenNonModal_Click(object sender, RoutedEventArgs e)
        {
            var w = new SubWindow { Owner = Window.GetWindow(this) };
            AppendLog($"Non-modal opened at {DateTime.Now:HH:mm:ss}");
            w.Show();
        }

        private void AppendLog(string message)
        {
            LogText.Text = string.IsNullOrEmpty(LogText.Text)
                ? message
                : $"{LogText.Text}\n{message}";
        }
    }
}
