using System.Windows;
using System.Windows.Controls;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 인스펙터의 컬렉션 스냅샷 상세 뷰. DataContext = <see cref="CollectionDetailViewModel"/>.
    /// 브레드크럼 + ListView (GridView) 로 현재 계층 표시, 셀 더블클릭 → 안쪽 컬렉션으로 드릴다운.
    /// </summary>
    public partial class CollectionDetailWindow : Window
    {
        public CollectionDetailWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        /// <summary>ListView 더블클릭 → 클릭된 행이 컬렉션이면 DrillDownCommand 실행.</summary>
        private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!(DataContext is CollectionDetailViewModel vm)) return;
            if (!(RowsList.SelectedItem is CollectionRow row)) return;
            if (!row.HasChildren) return;
            if (vm.DrillDownCommand.CanExecute(row))
                vm.DrillDownCommand.Execute(row);
        }
    }
}
