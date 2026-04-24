using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#nullable enable
namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// Tool → Canvas 상호작용 컨텍스트.
    /// Tool이 LayeredCanvas 내부에 직접 접근하지 않도록 중개합니다.
    /// </summary>
    public class CanvasToolContext
    {
        private readonly LayeredCanvas _canvas;

        public CanvasToolContext(LayeredCanvas canvas, CanvasLayer? targetLayer, MouseEventArgs e)
            : this(canvas, targetLayer, null, e) { }

        public CanvasToolContext(LayeredCanvas canvas, CanvasLayer? targetLayer, MaskLayer? targetMaskLayer, MouseEventArgs e)
        {
            _canvas = canvas;
            TargetLayer = targetLayer;
            TargetMaskLayer = targetMaskLayer;
            IsShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            IsCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        }

        /// <summary>현재 활성 레이어.</summary>
        public CanvasLayer? TargetLayer { get; }

        /// <summary>마스크 툴이 그릴 대상 MaskLayer. 없으면 null.</summary>
        public MaskLayer? TargetMaskLayer { get; }

        /// <summary>Shift 키 상태.</summary>
        public bool IsShiftDown { get; }

        /// <summary>Ctrl 키 상태.</summary>
        public bool IsCtrlDown { get; }

        /// <summary>타겟 레이어에 요소를 추가합니다.</summary>
        public void AddToLayer(UIElement element)
        {
            if (TargetLayer == null) return;
            Canvas.SetLeft(element, Canvas.GetLeft(element));
            Canvas.SetTop(element, Canvas.GetTop(element));
            TargetLayer.Children.Add(element);
        }

        /// <summary>타겟 레이어에서 요소를 제거합니다.</summary>
        public void RemoveFromLayer(UIElement element)
        {
            TargetLayer?.Children.Remove(element);
        }

        /// <summary>DrawingCompleted 이벤트를 트리거합니다.</summary>
        public void NotifyDrawingCompleted()
        {
            _canvas.RaiseDrawingCompleted();
        }

        /// <summary>Tool 전환을 요청합니다.</summary>
        public void RequestToolChange(ICanvasTool newTool)
        {
            _canvas.CurrentTool = newTool;
        }
    }
}
