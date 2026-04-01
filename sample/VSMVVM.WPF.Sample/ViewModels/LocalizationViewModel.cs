using System.Resources;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// ViewModel for ILocalizeService locale switching demo.
    /// </summary>
    public partial class LocalizationViewModel : ViewModelBase
    {
        private readonly ILocalizeService _localize;

        [Property]
        private string _currentLocale = "";

        [Property]
        private string _greeting = "";

        [Property]
        private string _dynamicText = "";

        [Property]
        private string _appTitle = "";

        public LocalizationViewModel(ILocalizeService localize)
        {
            _localize = localize;
            _localize.SetResourceManager(new ResourceManager(
                "VSMVVM.WPF.Sample.Resources.Resource",
                typeof(LocalizationViewModel).Assembly));
            _localize.LocaleChanged += OnLocaleChanged;

            UpdateDisplayTexts();
        }

        [RelayCommand]
        private void SwitchToEnglish()
        {
            _localize.ChangeLocale("en");
        }

        [RelayCommand]
        private void SwitchToKorean()
        {
            _localize.ChangeLocale("ko");
        }

        private void OnLocaleChanged(string locale)
        {
            UpdateDisplayTexts();
        }

        private void UpdateDisplayTexts()
        {
            CurrentLocale = _localize.CurrentLocale;
            Greeting = _localize.GetString("GREETING");
            DynamicText = _localize.GetString("DYNAMIC_TEXT");
            AppTitle = _localize.GetString("APP_TITLE");
        }
    }
}
