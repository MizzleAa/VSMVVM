using System;
using System.Windows.Media;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for VSMVVM.WPF custom components demo.

    /// </summary>

    public partial class ComponentsViewModel : ViewModelBase
    {
        [Property]
        private bool _isLoading;

        [Property]
        private string _jsonText = "{\n    \"name\": \"VSMVVM\",\n    \"version\": \"1.0.0\",\n    \"features\": [\n        \"Source Generator\",\n        \"Region Navigation\",\n        \"Design System\"\n    ],\n    \"active\": true\n}";

        [Property]
        private DateTime _selectedDateTime = DateTime.Now;

        [Property]
        private Color _selectedDemoColor = Color.FromRgb(59, 130, 246);

        public Brush SelectedDemoBrush => new SolidColorBrush(SelectedDemoColor);

        partial void OnSelectedDemoColorChanged(Color value)
        {
            OnPropertyChanged(nameof(SelectedDemoBrush));
        }
    }
}
