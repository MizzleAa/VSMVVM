using System.Windows.Media;
using VSMVVM.Core.MVVM;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// 세그멘테이션 라벨 클래스. 마스크의 픽셀값(0~255)이 Index에 매핑된다.
    /// Index 0은 배경으로 예약되어 있다.
    /// </summary>
    public class LabelClass : ViewModelBase
    {
        private int _index;
        private string _name = string.Empty;
        private Color _color = Colors.Transparent;
        private bool _isVisible = true;
        private bool _isSelected;

        /// <summary>0~255 범위의 라벨 인덱스. 0 = 배경.</summary>
        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        /// <summary>표시 이름.</summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>렌더링 색상.</summary>
        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        /// <summary>가시성 토글.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        /// <summary>UI 상 선택 상태.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }
}
