using System;
using System.Windows;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>NodeGraphCanvas.ContextRequested 이벤트 인자. CanvasPoint는 줌/팬 적용 전 콘텐츠 좌표계.</summary>
    public sealed class NodeGraphContextRequestedEventArgs : EventArgs
    {
        public Point CanvasPoint { get; }

        public NodeGraphContextRequestedEventArgs(Point canvasPoint)
        {
            CanvasPoint = canvasPoint;
        }
    }
}
