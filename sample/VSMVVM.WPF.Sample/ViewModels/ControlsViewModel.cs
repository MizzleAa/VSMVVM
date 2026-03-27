using System.Collections.ObjectModel;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for the Controls showcase page.

    /// </summary>

    public partial class ControlsViewModel : ViewModelBase
    {
        public ObservableCollection<SampleItem> SampleItems { get; } = new()
        {
            new("Alpha", 100, "Active"),
            new("Beta", 250, "Inactive"),
            new("Gamma", 75, "Active"),
            new("Delta", 300, "Pending"),
            new("Epsilon", 180, "Active"),
        };
    }

    public class SampleItem
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public string Status { get; set; }

        public SampleItem(string name, int value, string status)
        {
            Name = name;
            Value = value;
            Status = status;
        }
    }
}
