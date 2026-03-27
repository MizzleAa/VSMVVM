using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.Design.Components
{
    /// <summary>
    /// 도움말 정보를 팝업으로 표시하는 재사용 가능한 컴포넌트입니다.
    /// Title과 Description을 바인딩하여 사용합니다.
    /// </summary>
    public partial class InfoPopup : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(InfoPopup),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(InfoPopup),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public InfoPopup()
        {
            InitializeComponent();
        }
    }
}
