using System.Windows;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>핀 드래그 연결 라우티드 이벤트의 인자. 시작/종료 핀 ViewModel을 운반.</summary>
    public sealed class PinConnectionRoutedEventArgs : RoutedEventArgs
    {
        public PinViewModel Pin { get; }
        public Point ScreenPoint { get; }

        public PinConnectionRoutedEventArgs(RoutedEvent routedEvent, object source,
                                            PinViewModel pin, Point screenPoint)
            : base(routedEvent, source)
        {
            Pin = pin;
            ScreenPoint = screenPoint;
        }
    }

    public delegate void PinConnectionRoutedEventHandler(object sender, PinConnectionRoutedEventArgs e);

    /// <summary>핀 드래그 연결 이벤트 정의. PinView가 발화하고 NodeGraphCanvas가 듣는다.</summary>
    public static class PinConnectionRoutedEvents
    {
        public static readonly RoutedEvent ConnectionDragStartedEvent =
            EventManager.RegisterRoutedEvent("PinConnectionDragStarted",
                RoutingStrategy.Bubble, typeof(PinConnectionRoutedEventHandler),
                typeof(PinConnectionRoutedEvents));

        public static readonly RoutedEvent ConnectionDragCompletedEvent =
            EventManager.RegisterRoutedEvent("PinConnectionDragCompleted",
                RoutingStrategy.Bubble, typeof(PinConnectionRoutedEventHandler),
                typeof(PinConnectionRoutedEvents));
    }
}
