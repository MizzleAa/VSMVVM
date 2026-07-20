using System.Windows.Controls;
using System.Windows.Input;
using VSMVVM.WPF.Sample.ViewModels;

namespace VSMVVM.WPF.Sample.Views
{
    public partial class SampleDemoPickerView : UserControl
    {
        public SampleDemoPickerView()
        {
            InitializeComponent();
        }

        /// <summary>ListBox 아이템 더블클릭 = OK 로 확정. VM.AcceptCommand 가 RequestClose 로 다이얼로그 종료.</summary>
        private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SampleDemoPickerViewModel vm) return;
            var cmd = vm.AcceptCommand;
            if (cmd != null && cmd.CanExecute(null)) cmd.Execute(null);
        }
    }
}
