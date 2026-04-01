using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Controls.Tools;

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

        // ── Tool 인스턴스 (각자 독립 속성 보유) ──
        public SelectTool SelectTool { get; } = new();
        public PenTool PenTool { get; } = new();
        public RectangleTool RectangleTool { get; } = new();
        public RoundedRectangleTool RoundedRectangleTool { get; } = new();
        public EllipseTool EllipseTool { get; } = new();
        public ImageTool ImageTool { get; } = new();

        [Property]
        private double _zoomLevel = 1.0;

        [Property]
        private bool _isPanLocked = false;

        [Property]
        private string _canvasInfo = "Zoom: 1.0x";

        [Property]
        private LayerInfo? _selectedLayer;

        // ── 현재 활성 Tool ──
        [Property]
        private ICanvasTool _currentTool = null!;

        /// <summary>
        /// 레이어 목록.
        /// </summary>
        public ObservableCollection<LayerInfo> Layers { get; } = new();

        // ── 색상/두께 프리셋 (모든 Tool이 공유하는 UI 프리셋) ──
        public List<string> StrokeColorPresets { get; } = new()
            { "#89B4FA", "#F38BA8", "#A6E3A1", "#FAB387", "#CBA6F7" };
        public List<double> StrokeThicknessPresets { get; } = new() { 1, 2, 3, 5, 8 };

        public CanvasViewModel()
        {
            _currentTool = SelectTool;
        }

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
            UpdateCanvasInfo();
        }

        partial void OnCurrentToolChanged(ICanvasTool value)
        {
            UpdateCanvasInfo();
        }

        private void UpdateCanvasInfo()
        {
            var toolName = CurrentTool?.Mode.ToString() ?? "Select";
            var hint = CurrentTool?.Mode != CanvasToolMode.Select ? " | Ctrl+Drag = Pan" : "";
            CanvasInfo = $"Zoom: {ZoomLevel:F2}x | {toolName}{hint}";
        }

        partial void OnSelectedLayerChanged(LayerInfo? value)
        {
            if (value != null)
            {
                _canvasOps?.SelectLayerOnCanvas(value.Name);
            }
        }

        // ── 도구 전환 커맨드 ──
        [RelayCommand] private void SetToolSelect()       => CurrentTool = SelectTool;
        [RelayCommand] private void SetToolPen()           => CurrentTool = PenTool;
        [RelayCommand] private void SetToolRectangle()     => CurrentTool = RectangleTool;
        [RelayCommand] private void SetToolRoundedRect()   => CurrentTool = RoundedRectangleTool;
        [RelayCommand] private void SetToolEllipse()       => CurrentTool = EllipseTool;
        [RelayCommand] private void SetToolImage()         => CurrentTool = ImageTool;

        // ── 도구 속성 변경 커맨드 ──
        [RelayCommand]
        private void SetStrokeColor(string? colorHex)
        {
            if (colorHex == null || CurrentTool is not Controls.Tools.CanvasToolBase tool) return;
            try
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
                brush.Freeze();
                tool.StrokeColor = brush;
            }
            catch { }
        }

        [RelayCommand]
        private void SetFillColor(string? colorHex)
        {
            if (CurrentTool is not Controls.Tools.CanvasToolBase tool) return;
            if (colorHex == "Transparent" || string.IsNullOrEmpty(colorHex))
            {
                tool.FillColor = System.Windows.Media.Brushes.Transparent;
                return;
            }
            try
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
                brush.Freeze();
                tool.FillColor = brush;
            }
            catch { }
        }

        [RelayCommand]
        private void SetStrokeThickness(object? value)
        {
            if (CurrentTool is not Controls.Tools.CanvasToolBase tool) return;
            if (value is double d) tool.StrokeThickness = d;
            else if (value is int i) tool.StrokeThickness = i;
            else if (double.TryParse(value?.ToString(), out var parsed)) tool.StrokeThickness = parsed;
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
