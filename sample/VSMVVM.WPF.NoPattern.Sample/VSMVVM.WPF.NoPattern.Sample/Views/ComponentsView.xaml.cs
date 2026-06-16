using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSMVVMColorPicker = VSMVVM.WPF.Controls.ColorPicker;

namespace VSMVVM.WPF.NoPattern.Sample.Views
{
    public partial class ComponentsView : UserControl
    {
        public ComponentsView()
        {
            InitializeComponent();

            JsonEditor.Text = "{\n    \"name\": \"VSMVVM\",\n    \"version\": 1.27,\n    \"features\": [\"Design\", \"Controls\", \"Components\"],\n    \"enabled\": true\n}";

            DateTimePicker.SelectedDateTime = DateTime.Now;
            UpdateDateTimeLabel();
            var dtDescriptor = DependencyPropertyDescriptor.FromProperty(
                VSMVVM.WPF.Design.Components.DateTimePicker.SelectedDateTimeProperty,
                typeof(VSMVVM.WPF.Design.Components.DateTimePicker));
            dtDescriptor.AddValueChanged(DateTimePicker, (_, _) => UpdateDateTimeLabel());

            ColorPicker.SelectedColor = Colors.DodgerBlue;
            UpdateColorPreview();
            var colorDescriptor = DependencyPropertyDescriptor.FromProperty(
                VSMVVMColorPicker.SelectedColorProperty,
                typeof(VSMVVMColorPicker));
            colorDescriptor.AddValueChanged(ColorPicker, (_, _) => UpdateColorPreview());
        }

        private void LoadingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.IsLoading = LoadingCheckBox.IsChecked == true;
        }

        private void UpdateDateTimeLabel()
        {
            DateTimeLabel.Text = $"Selected: {DateTimePicker.SelectedDateTime:yyyy-MM-dd HH:mm:ss}";
        }

        private void UpdateColorPreview()
        {
            var brush = new SolidColorBrush(ColorPicker.SelectedColor);
            ColorPreview.Background = brush;
            ColorSampleButton.Background = brush;
        }
    }
}
