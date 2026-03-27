using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 개별 레이어를 나타내는 Canvas. LayeredCanvas의 자식으로 사용됩니다.
    /// </summary>
    public class CanvasLayer : Canvas
    {
        public CanvasLayer()
        {
            ClipToBounds = true;
        }
        #region DependencyProperties

        public static readonly DependencyProperty LayerNameProperty =
            DependencyProperty.Register(
                nameof(LayerName),
                typeof(string),
                typeof(CanvasLayer),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ZOrderProperty =
            DependencyProperty.Register(
                nameof(ZOrder),
                typeof(int),
                typeof(CanvasLayer),
                new PropertyMetadata(0, OnZOrderChanged));

        public string LayerName
        {
            get => (string)GetValue(LayerNameProperty);
            set => SetValue(LayerNameProperty, value);
        }

        public int ZOrder
        {
            get => (int)GetValue(ZOrderProperty);
            set => SetValue(ZOrderProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnZOrderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CanvasLayer layer)
            {
                Canvas.SetZIndex(layer, (int)e.NewValue);
            }
        }

        #endregion
    }

    /// <summary>
    /// 레이어 기반 캔버스 컨트롤. CanvasLayer를 여러 개 겹쳐 사용합니다.
    /// 포토샵 스타일 레이어 관리 (순서 변경, 가시성 토글, 동적 추가/삭제)를 지원합니다.
    /// </summary>
    public class LayeredCanvas : Canvas
    {
        public LayeredCanvas()
        {
            ClipToBounds = true;
        }

        #region Public Methods

        /// <summary>
        /// 이름으로 레이어를 찾습니다.
        /// </summary>
        public CanvasLayer? FindLayer(string layerName)
        {
            foreach (var child in Children)
            {
                if (child is CanvasLayer layer && layer.LayerName == layerName)
                {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>
        /// 모든 CanvasLayer를 ZOrder 순으로 반환합니다.
        /// </summary>
        public IReadOnlyList<CanvasLayer> GetLayers()
        {
            var layers = new List<CanvasLayer>();
            foreach (var child in Children)
            {
                if (child is CanvasLayer layer)
                    layers.Add(layer);
            }
            return layers.OrderBy(l => l.ZOrder).ToList();
        }

        /// <summary>
        /// 레이어를 위로 이동합니다 (ZOrder 증가).
        /// </summary>
        public bool MoveLayerUp(CanvasLayer layer)
        {
            var layers = GetLayers();
            var index = layers.ToList().IndexOf(layer);
            if (index < 0 || index >= layers.Count - 1)
                return false;

            var above = layers[index + 1];
            var temp = layer.ZOrder;
            layer.ZOrder = above.ZOrder;
            above.ZOrder = temp;

            // 동일 ZOrder인 경우 강제 스왑
            if (layer.ZOrder == above.ZOrder)
            {
                layer.ZOrder = above.ZOrder + 1;
            }

            return true;
        }

        /// <summary>
        /// 레이어를 아래로 이동합니다 (ZOrder 감소).
        /// </summary>
        public bool MoveLayerDown(CanvasLayer layer)
        {
            var layers = GetLayers();
            var index = layers.ToList().IndexOf(layer);
            if (index <= 0)
                return false;

            var below = layers[index - 1];
            var temp = layer.ZOrder;
            layer.ZOrder = below.ZOrder;
            below.ZOrder = temp;

            // 동일 ZOrder인 경우 강제 스왑
            if (layer.ZOrder == below.ZOrder)
            {
                below.ZOrder = layer.ZOrder + 1;
            }

            return true;
        }

        /// <summary>
        /// 이름으로 레이어의 가시성을 설정합니다.
        /// </summary>
        public void SetLayerVisibility(string layerName, bool isVisible)
        {
            var layer = FindLayer(layerName);
            if (layer != null)
            {
                layer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 특정 레이어의 모든 자식을 제거합니다.
        /// </summary>
        public void ClearLayer(string layerName)
        {
            var layer = FindLayer(layerName);
            if (layer != null)
            {
                layer.Children.Clear();
            }
        }

        #endregion
    }
}
