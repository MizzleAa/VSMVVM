using System.Collections.Generic;
using System.Windows;
using VSMVVM.Core.MVVM;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// 마스크 내 하나의 연결 영역(instance). COCO instance segmentation 스타일로
    /// 같은 라벨 클래스 안에서도 독립적으로 선택·편집된다.
    /// </summary>
    public class MaskInstance : ViewModelBase
    {
        private int _labelIndex;
        private LabelClass? _label;
        private Rect _boundingBox;
        private int _pixelCount;
        private bool _isSelected;
        private bool _isVisible = true;

        /// <summary>인스턴스 ID. 0은 배경 예약. 단조증가로 발번한다.</summary>
        public uint Id { get; init; }

        /// <summary>속한 라벨 클래스 인덱스. 색상은 Labels 에서 lookup.</summary>
        public int LabelIndex
        {
            get => _labelIndex;
            set { _labelIndex = value; OnPropertyChanged(); }
        }

        /// <summary>속한 라벨 클래스 참조. MaskLayer 가 세팅한다. XAML 바인딩용(Name/Color).</summary>
        public LabelClass? Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        /// <summary>마스크 픽셀 좌표계의 bounding box. 인스턴스 편집 시 재계산된다.</summary>
        public Rect BoundingBox
        {
            get => _boundingBox;
            set { _boundingBox = value; OnPropertyChanged(); }
        }

        /// <summary>인스턴스가 차지하는 픽셀 수. 스크린샷 UI 의 Area 에 해당.</summary>
        public int PixelCount
        {
            get => _pixelCount;
            set { _pixelCount = value; OnPropertyChanged(); }
        }

        /// <summary>UI 상 선택 상태.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        /// <summary>인스턴스 단위 가시성 토글.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        private IList<IList<Point>>? _polygonContours;

        /// <summary>
        /// 다중 contour. 인덱스 0 = 외곽, ≥1 = hole 들. 더블클릭 vertex 편집 진입 시 마스크 픽셀에서 즉석 추출.
        /// even-odd rasterize 가 hole 을 자동 처리하므로 winding 방향 강제 불필요.
        /// </summary>
        public IList<IList<Point>>? PolygonContours
        {
            get => _polygonContours;
            set { _polygonContours = value; OnPropertyChanged(); OnPropertyChanged(nameof(PolygonPoints)); }
        }

        /// <summary>
        /// 외곽 contour (PolygonContours[0]) 의 호환 wrapper. 기존 호출자(PolygonMaskTool / MagneticLassoTool / Snapshot 등)
        /// 가 외곽만 다룬다는 가정으로 set 시 PolygonContours[0] 만 교체하고 hole 은 유지.
        /// </summary>
        public IList<Point>? PolygonPoints
        {
            get => (_polygonContours != null && _polygonContours.Count > 0) ? _polygonContours[0] : null;
            set
            {
                if (value == null)
                {
                    _polygonContours = null;
                }
                else
                {
                    if (_polygonContours == null) _polygonContours = new List<IList<Point>>();
                    if (_polygonContours.Count == 0) _polygonContours.Add(value);
                    else _polygonContours[0] = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(PolygonContours));
            }
        }
    }
}