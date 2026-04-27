using System.Windows;
using VSMVVM.Core.MVVM;

#nullable enable
namespace VSMVVM.WPF.Imaging.Measurements
{
    /// <summary>측정 아이템 베이스. 길이·각도 공통 속성.</summary>
    public abstract class MeasurementBase : ViewModelBase
    {
        private string _displayName = string.Empty;
        private bool _isVisible = true;
        private bool _isSelected;

        public uint Id { get; init; }

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        /// <summary>측정 값 (길이: px, 각도: °).</summary>
        public abstract double Value { get; }

        /// <summary>단위 문자열. "px" / "°".</summary>
        public abstract string Unit { get; }

        /// <summary>Bounding box (픽셀 좌표).</summary>
        public abstract Rect BoundingBox { get; }

        /// <summary>측정을 구성하는 endpoint 목록을 순서대로 반환.</summary>
        public abstract System.Collections.Generic.IReadOnlyList<Point> GetEndpoints();

        /// <summary>index 위치의 endpoint 를 새 좌표로 이동.</summary>
        public abstract void SetEndpoint(int index, Point point);

        /// <summary>모든 endpoint 를 (dx, dy) 만큼 평행이동.</summary>
        public abstract void Translate(double dx, double dy);
    }
}
