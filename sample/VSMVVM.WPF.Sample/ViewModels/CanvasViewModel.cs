using System.Collections.ObjectModel;
using System.Linq;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

#nullable enable
namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Canvas 조작 인터페이스.
    /// View가 이 인터페이스를 구현하여 ViewModel에 주입합니다.
    /// </summary>
    public interface ICanvasOperations
    {
        void FitToContent();
        void AddLayerToCanvas();
        void AddShapeToCanvas();
        void DeleteLayerOnCanvas(string layerName);
        void SelectLayerOnCanvas(string layerName);
        void SelectChildOnCanvas(string layerName, int childIndex);
        void MoveLayerUpOnCanvas(string layerName);
        void MoveLayerDownOnCanvas(string layerName);
        void ToggleLayerVisibilityOnCanvas(string layerName);
        System.Collections.Generic.List<LayerInfo> GetCurrentLayers();
    }

    /// <summary>
    /// 자식 도형 정보.
    /// </summary>
    public class ChildInfo
    {
        public string DisplayName { get; set; } = "";
        public string ShapeType { get; set; } = "";
        public string Color { get; set; } = "#45475A";
        public int Index { get; set; }
        public string ParentLayerName { get; set; } = "";
    }

    /// <summary>
    /// 레이어 패널 바인딩용 데이터 모델. 포토샵 스타일 패널 지원.
    /// </summary>
    public class LayerInfo
    {
        public string Name { get; set; } = "";
        public int ZOrder { get; set; }
        public bool IsVisible { get; set; } = true;
        public string ThumbColor { get; set; } = "#45475A";
        public int ChildCount { get; set; }
        public System.Collections.Generic.List<ChildInfo> Children { get; set; } = new();
    }

    /// <summary>
    /// Canvas 데모 ViewModel. MVVM 패턴으로 커맨드/프로퍼티 관리.
    /// </summary>
    public partial class CanvasViewModel : ViewModelBase
    {
        private ICanvasOperations? _canvasOps;

        [Property]
        private double _zoomLevel = 1.0;

        [Property]
        private bool _isPanLocked = false;

        [Property]
        private string _canvasInfo = "Zoom: 1.0x";

        [Property]
        private LayerInfo? _selectedLayer;

        /// <summary>
        /// 레이어 목록.
        /// </summary>
        public ObservableCollection<LayerInfo> Layers { get; } = new();

        /// <summary>
        /// View에서 Canvas 조작 인터페이스를 등록합니다.
        /// </summary>
        public void RegisterCanvasOperations(ICanvasOperations ops)
        {
            _canvasOps = ops;
            RefreshLayers();
        }

        partial void OnZoomLevelChanged(double value)
        {
            CanvasInfo = $"Zoom: {value:F2}x";
        }

        partial void OnSelectedLayerChanged(LayerInfo? value)
        {
            if (value != null)
            {
                _canvasOps?.SelectLayerOnCanvas(value.Name);
            }
        }

        [RelayCommand]
        private void SelectChild(ChildInfo? child)
        {
            if (child == null) return;
            _canvasOps?.SelectChildOnCanvas(child.ParentLayerName, child.Index);
        }

        [RelayCommand]
        private void FitToContent()
        {
            _canvasOps?.FitToContent();
        }

        [RelayCommand]
        private void AddLayer()
        {
            _canvasOps?.AddLayerToCanvas();
            RefreshLayers();
        }

        [RelayCommand]
        private void AddShape()
        {
            _canvasOps?.AddShapeToCanvas();
            RefreshLayers();
        }

        [RelayCommand]
        private void MoveLayerUp()
        {
            if (SelectedLayer == null) return;
            _canvasOps?.MoveLayerUpOnCanvas(SelectedLayer.Name);
            RefreshLayers();
        }

        [RelayCommand]
        private void MoveLayerDown()
        {
            if (SelectedLayer == null) return;
            _canvasOps?.MoveLayerDownOnCanvas(SelectedLayer.Name);
            RefreshLayers();
        }

        [RelayCommand]
        private void ToggleLayerVisibility(LayerInfo? layer)
        {
            if (layer == null) return;
            _canvasOps?.ToggleLayerVisibilityOnCanvas(layer.Name);
            RefreshLayers();
        }

        [RelayCommand]
        private void DeleteLayer()
        {
            if (SelectedLayer == null) return;
            _canvasOps?.DeleteLayerOnCanvas(SelectedLayer.Name);
            SelectedLayer = null;
            RefreshLayers();
        }


        private void RefreshLayers()
        {
            if (_canvasOps == null) return;

            var selectedName = SelectedLayer?.Name;
            Layers.Clear();

            foreach (var info in _canvasOps.GetCurrentLayers())
            {
                Layers.Add(info);
            }

            if (selectedName != null)
            {
                SelectedLayer = Layers.FirstOrDefault(l => l.Name == selectedName);
            }
        }
    }
}
