using System.Windows;

namespace VSMVVM.WPF.Sample.Views
{
    /// <summary>ChartViewNode 의 실시간 데이터를 LineChart 또는 ConfusionMatrix 로 표시.</summary>
    public partial class ChartViewWindow : Window
    {
        public ChartViewWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
