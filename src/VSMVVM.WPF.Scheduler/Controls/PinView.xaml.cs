using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSMVVM.WPF.Scheduler.ViewModels;

namespace VSMVVM.WPF.Scheduler.Controls
{
    /// <summary>
    /// 한 핀의 시각 표현. Exec(삼각형) 또는 Data(타입별 색 원) + 라벨.
    /// 마우스 다운/업 시 PinConnectionRoutedEvents의 라우티드 이벤트를 발화하여 NodeGraphCanvas가 드래그-앤-드롭 연결을 처리하도록 한다.
    /// </summary>
    public partial class PinView : UserControl
    {
        public PinView()
        {
            InitializeComponent();
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not PinViewModel pin) return;
            RaiseEvent(new PinConnectionRoutedEventArgs(
                PinConnectionRoutedEvents.ConnectionDragStartedEvent, this, pin, e.GetPosition(this)));
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not PinViewModel pin) return;
            RaiseEvent(new PinConnectionRoutedEventArgs(
                PinConnectionRoutedEvents.ConnectionDragCompletedEvent, this, pin, e.GetPosition(this)));
            e.Handled = true;
        }

        /// <summary>호버 시각 강조 — 핀 글리프 외곽에 링 표시. (NodeGraphCanvas 의 마우스 캡처 도중에는 발화 안 함.)</summary>
        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (DataContext is PinViewModel pin) pin.IsHovered = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (DataContext is PinViewModel pin) pin.IsHovered = false;
        }
    }
}
