using System.Windows;
using System.Windows.Markup;

[assembly: XmlnsDefinition("https://github.com/your-org/vsmvvm", "VSMVVM.WPF.Controls")]
[assembly: XmlnsDefinition("https://github.com/your-org/vsmvvm", "VSMVVM.WPF.Controls.Tools")]
[assembly: XmlnsDefinition("https://github.com/your-org/vsmvvm", "VSMVVM.WPF.MarkupExtensions")]
[assembly: XmlnsDefinition("https://github.com/your-org/vsmvvm", "VSMVVM.WPF.Behaviors")]
[assembly: XmlnsDefinition("https://github.com/your-org/vsmvvm", "VSMVVM.WPF.Services")]

// Themes/Generic.xaml 자동 로드를 활성화. Control 기반 커스텀 컨트롤(ColorPicker 등)의
// DefaultStyleKey 에 대응하는 Style 을 WPF 가 이 어셈블리에서 찾도록 한다.
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly)]
