using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSMVVM.WPF.NoPattern.Sample.Views;

namespace VSMVVM.WPF.NoPattern.Sample
{
    public partial class MainWindow : Window
    {
        private Button? _activeMenu;
        private Brush? _defaultMenuBackground;
        private Brush? _defaultMenuForeground;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NavigateTo("Home");
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string viewKey)
            {
                NavigateTo(viewKey, btn);
            }
        }

        private void NavigateTo(string viewKey, Button? sourceButton = null)
        {
            ContentArea.Content = viewKey switch
            {
                "Home"          => new HomeView(),
                "Controls"      => new ControlsView(),
                "DefaultDesign" => new DefaultDesignView(),
                "Components"    => new ComponentsView(),
                "Validation"    => new ValidationView(),
                "Dialog"        => new DialogView(),
                "MultiWindow"   => new MultiWindowView(),
                _               => null
            };

            UpdateActiveMenu(sourceButton ?? FindButtonByTag(viewKey));
        }

        private void UpdateActiveMenu(Button? btn)
        {
            if (_activeMenu is not null)
            {
                _activeMenu.Background = _defaultMenuBackground;
                _activeMenu.Foreground = _defaultMenuForeground;
            }

            if (btn is null) return;

            _defaultMenuBackground = btn.Background;
            _defaultMenuForeground = btn.Foreground;

            btn.Background = (Brush)FindResource("AccentPrimary");
            btn.Foreground = (Brush)FindResource("BgPrimary");
            _activeMenu = btn;
        }

        private Button? FindButtonByTag(string tag)
        {
            foreach (var child in MenuPanel.Children)
            {
                if (child is Button b && b.Tag is string t && t == tag)
                    return b;
            }
            return null;
        }
    }
}
