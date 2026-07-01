using System.Windows;
using VSMVVM.WPF.Scheduler.Services;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// NodeGraphCanvas 가 팔레트 항목 드롭을 받았을 때 발화하는 라우티드 이벤트의 인자.
    /// 호스트(WorkspaceView)가 캐치하여 워크스페이스에 노드 추가 위임.
    /// </summary>
    public sealed class NodePaletteDropEventArgs : RoutedEventArgs
    {
        /// <summary>드롭된 팔레트 항목.</summary>
        public NodePaletteEntry Entry { get; }

        /// <summary>드롭 위치 — 캔버스 좌표계 (zoom/pan 반영).</summary>
        public Point CanvasPosition { get; }

        public NodePaletteDropEventArgs(RoutedEvent routedEvent, object source,
                                        NodePaletteEntry entry, Point canvasPosition)
            : base(routedEvent, source)
        {
            Entry = entry;
            CanvasPosition = canvasPosition;
        }
    }

    public delegate void NodePaletteDropEventHandler(object sender, NodePaletteDropEventArgs e);

    /// <summary>NodeGraphCanvas → 호스트 라우티드 이벤트.</summary>
    public static class NodePaletteDropRoutedEvents
    {
        /// <summary>드래그된 NodePaletteEntry 가 캔버스에 드롭됨. 호스트가 그 좌표에 노드 생성.</summary>
        public static readonly RoutedEvent PaletteEntryDroppedEvent =
            EventManager.RegisterRoutedEvent("PaletteEntryDropped",
                RoutingStrategy.Bubble, typeof(NodePaletteDropEventHandler),
                typeof(NodePaletteDropRoutedEvents));
    }

    /// <summary>WPF DataObject 의 표준 format 키 — 팔레트 드래그 페이로드.</summary>
    public static class NodePaletteDragFormats
    {
        public const string PaletteEntry = "vsmvvm.scheduler.PaletteEntry";
    }
}
