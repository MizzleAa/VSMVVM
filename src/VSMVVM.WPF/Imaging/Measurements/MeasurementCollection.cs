using System.Collections.ObjectModel;

#nullable enable
namespace VSMVVM.WPF.Imaging.Measurements
{
    /// <summary>측정 아이템 컬렉션. 단조증가 ID 발번.</summary>
    public class MeasurementCollection : ObservableCollection<MeasurementBase>
    {
        private uint _nextId = 1;

        public uint NextId() => _nextId++;
    }
}
