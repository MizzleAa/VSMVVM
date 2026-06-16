using System.Windows.Controls;
using VSMVVM.WPF.NoPattern.Sample.Models;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class ControlsView : UserControl
    {
        public ControlsView()
        {
            InitializeComponent();

            var items = SampleItem.CreateDemoList();
            SampleListView.ItemsSource = items;
            SampleDataGrid.ItemsSource = items;
        }
    }
}
