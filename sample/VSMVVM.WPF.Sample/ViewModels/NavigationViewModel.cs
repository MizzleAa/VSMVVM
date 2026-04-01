using System;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// ViewModel for INavigateAware navigation lifecycle demo.
    /// </summary>

    public partial class NavigationViewModel : ViewModelBase, INavigateAware
    {
        [Property]
        private string _navigationLog = "";

        private void AppendLog(string message)
        {
            NavigationLog += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }

        public bool CanNavigate(NavigationContext context) => true;

        public void OnNavigatedTo(NavigationContext context)
        {
            AppendLog("OnNavigatedTo called");
            AppendLog($"  Region: MainRegion");
        }

        public void OnNavigatedFrom(NavigationContext context)
        {
            AppendLog("OnNavigatedFrom called");
        }
    }
}
