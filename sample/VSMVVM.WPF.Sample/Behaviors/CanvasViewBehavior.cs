using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Sample.ViewModels;

#nullable enable
namespace VSMVVM.WPF.Sample.Behaviors
{
    /// <summary>
    /// ImageCanvas + LayeredCanvas 조작을 담당하는 Behavior.
    /// XAML에서 Attached Property로 연결하면 ViewModel에 ICanvasOperations를 자동 등록합니다.
    /// code-behind 없이 MVVM 패턴을 유지합니다.
    /// </summary>
    public static class CanvasViewBehavior
    {
        #region Attached Property

        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(CanvasViewBehavior),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
        public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                element.Loaded += (s, _) =>
                {
                    var handler = new CanvasOperationsHandler(element);
                    if (element.DataContext is CanvasViewModel vm)
                    {
                        vm.RegisterCanvasOperations(handler);
                    }
                };
            }
        }

        #endregion

        /// <summary>
        /// ICanvasOperations 구현. View-specific Canvas 조작을 담당합니다.
        /// </summary>
        private sealed class CanvasOperationsHandler : ICanvasOperations
        {
            private readonly FrameworkElement _root;
            private int _layerCounter = 0;
            private int _shapeCounter;

            public CanvasOperationsHandler(FrameworkElement root)
            {
                _root = root;
            }

            private ImageCanvas? FindCanvas() => _root.FindName("DemoCanvas") as ImageCanvas;

            private LayeredCanvas? FindLayeredCanvas()
            {
                var canvas = FindCanvas();
                if (canvas == null) return null;

                foreach (var child in canvas.Children)
                {
                    if (child is LayeredCanvas lc)
                        return lc;
                }
                return null;
            }

            public void FitToContent()
            {
                var canvas = FindCanvas();
                if (canvas == null) return;

                canvas.FitToContent();

                if (_root.DataContext is CanvasViewModel vm)
                {
                    vm.ZoomLevel = canvas.ZoomLevel;
                }
            }

            public void AddLayerToCanvas()
            {
                var layered = FindLayeredCanvas();
                if (layered == null) return;

                _layerCounter++;

                var offsetX = 30.0 + (_layerCounter * 40) % 300;
                var offsetY = 30.0 + (_layerCounter * 30) % 200;

                var layer = new CanvasLayer
                {
                    LayerName = $"Layer {_layerCounter}",
                    ZOrder = _layerCounter,
                    Width = 200,
                    Height = 150,
                    Background = System.Windows.Media.Brushes.Transparent
                };
                Canvas.SetLeft(layer, offsetX);
                Canvas.SetTop(layer, offsetY);

                layered.Children.Add(layer);
            }

            public void AddShapeToCanvas()
            {
                _shapeCounter++;
                var colors = new[] { "#89B4FA", "#F38BA8", "#A6E3A1", "#FAB387", "#CBA6F7" };
                var colorIndex = _shapeCounter % colors.Length;

                Shape shape;
                double shapeW, shapeH;
                if (_shapeCounter % 2 == 0)
                {
                    shapeW = 60; shapeH = 60;
                    shape = new Ellipse { Width = shapeW, Height = shapeH, Opacity = 0.8 };
                }
                else
                {
                    shapeW = 80; shapeH = 50;
                    shape = new Rectangle
                    {
                        Width = shapeW, Height = shapeH,
                        RadiusX = 4, RadiusY = 4, Opacity = 0.8
                    };
                }

                shape.Fill = new System.Windows.Media.BrushConverter()
                    .ConvertFromString(colors[colorIndex]) as System.Windows.Media.Brush;

                var layered = FindLayeredCanvas();
                if (layered != null)
                {
                    var layers = layered.GetLayers();
                    CanvasLayer? targetLayer = null;

                    if (_root.DataContext is CanvasViewModel vm && vm.SelectedLayer != null)
                    {
                        targetLayer = layered.FindLayer(vm.SelectedLayer.Name);
                    }

                    targetLayer ??= layers.Count > 0 ? layers[layers.Count - 1] : null;

                    if (targetLayer != null)
                    {
                        // 레이어 크기 기준으로 겹침 최소화하여 배치
                        var layerW = targetLayer.Width > 0 && !double.IsNaN(targetLayer.Width) ? targetLayer.Width : 200;
                        var layerH = targetLayer.Height > 0 && !double.IsNaN(targetLayer.Height) ? targetLayer.Height : 150;

                        var maxX = System.Math.Max(0, layerW - shapeW);
                        var maxY = System.Math.Max(0, layerH - shapeH);

                        var posX = (10 + (_shapeCounter * 45)) % maxX;
                        var posY = (10 + (_shapeCounter * 35)) % maxY;

                        Canvas.SetLeft(shape, posX);
                        Canvas.SetTop(shape, posY);
                        targetLayer.Children.Add(shape);
                        return;
                    }
                }

                Canvas.SetLeft(shape, 100 + (_shapeCounter * 30) % 600);
                Canvas.SetTop(shape, 80 + (_shapeCounter * 25) % 400);
                FindCanvas()?.Children.Add(shape);
            }

            public void SelectLayerOnCanvas(string layerName)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer == null) return;

                FindCanvas()?.SelectElement(layer);
            }

            public void SelectChildOnCanvas(string layerName, int childIndex)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer == null || childIndex < 0 || childIndex >= layer.Children.Count) return;

                var child = layer.Children[childIndex];
                FindCanvas()?.SelectElement(child);
            }

            public void DeleteLayerOnCanvas(string layerName)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer == null) return;

                var canvas = FindCanvas();
                if (canvas?.SelectedElement == layer)
                {
                    canvas.ClearSelection();
                }

                layered!.Children.Remove(layer);
            }

            public void MoveLayerUpOnCanvas(string layerName)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer != null)
                {
                    layered!.MoveLayerUp(layer);
                }
            }

            public void MoveLayerDownOnCanvas(string layerName)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer != null)
                {
                    layered!.MoveLayerDown(layer);
                }
            }

            public void ToggleLayerVisibilityOnCanvas(string layerName)
            {
                var layered = FindLayeredCanvas();
                var layer = layered?.FindLayer(layerName);
                if (layer != null)
                {
                    layer.Visibility = layer.Visibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }

            public List<LayerInfo> GetCurrentLayers()
            {
                var result = new List<LayerInfo>();
                var layered = FindLayeredCanvas();
                if (layered == null) return result;

                var defaultColors = new[] { "#89B4FA", "#F38BA8", "#A6E3A1", "#FAB387", "#CBA6F7", "#45475A" };

                foreach (var layer in layered.GetLayers().Reverse())
                {
                    var thumbColor = defaultColors[layer.ZOrder % defaultColors.Length];
                    var children = new List<ChildInfo>();

                    for (int i = 0; i < layer.Children.Count; i++)
                    {
                        var child = layer.Children[i];
                        var childColor = "#45475A";
                        var shapeType = child.GetType().Name;

                        if (child is Shape shape && shape.Fill != null)
                        {
                            childColor = shape.Fill.ToString();
                            if (i == 0) thumbColor = childColor;
                        }

                        children.Add(new ChildInfo
                        {
                            DisplayName = $"{shapeType} {i + 1}",
                            ShapeType = shapeType,
                            Color = childColor,
                            Index = i,
                            ParentLayerName = layer.LayerName ?? "(unnamed)"
                        });
                    }

                    result.Add(new LayerInfo
                    {
                        Name = layer.LayerName ?? "(unnamed)",
                        ZOrder = layer.ZOrder,
                        IsVisible = layer.Visibility == Visibility.Visible,
                        ThumbColor = thumbColor,
                        ChildCount = layer.Children.Count,
                        Children = children
                    });
                }
                return result;
            }
        }
    }
}
