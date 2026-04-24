using System;
using System.Windows;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 줌/팬이 가능한 뷰포트 컨트롤이 노출해야 하는 계약.
    /// <see cref="MiniMapControl"/> 같은 "뷰포트 추종" 컴포넌트가 의존한다.
    /// </summary>
    public interface IZoomPanViewport
    {
        /// <summary>현재 줌 배율 (1.0 = 원본).</summary>
        double ZoomLevel { get; }

        /// <summary>콘텐츠 폭(캔버스 좌표계 기준). FitToContent가 갱신한다.</summary>
        double ContentWidth { get; }

        /// <summary>콘텐츠 높이(캔버스 좌표계 기준).</summary>
        double ContentHeight { get; }

        /// <summary>뷰포트(표시 영역)의 실제 폭. MiniMap이 뷰포트 사각형을 환산할 때 사용.</summary>
        double ViewportWidth { get; }

        /// <summary>뷰포트(표시 영역)의 실제 높이.</summary>
        double ViewportHeight { get; }

        /// <summary>화면(뷰포트) 좌표를 콘텐츠(캔버스) 좌표로 역변환한다.</summary>
        Point ScreenToCanvas(Point screenPoint);

        /// <summary>OffsetX/Y (콘텐츠를 뷰포트에 대해 이동한 양)를 직접 지정한다. MiniMap의 클릭 네비게이션용.</summary>
        void SetOffset(double x, double y);

        /// <summary>ZoomLevel을 설정한다. 구현체는 MinZoom/MaxZoom 으로 clamp 하고 뷰포트 중심 pivot 보정을 수행한다.</summary>
        void SetZoom(double zoom);

        /// <summary>모든 콘텐츠를 뷰포트에 맞춰 zoom + center.</summary>
        void FitToContent();

        /// <summary>주어진 캔버스 좌표계 사각형을 뷰포트 중앙에 맞춰 zoom + center.</summary>
        void ZoomToBounds(Rect bounds, double padding = 0.8);

        /// <summary>Zoom 또는 pan 상태가 바뀔 때마다 발화한다.</summary>
        event EventHandler? ViewportChanged;

        /// <summary>뷰포트의 <see cref="FrameworkElement.SizeChanged"/> 에 상응 — MiniMap이 구독 편의용.</summary>
        event SizeChangedEventHandler? SizeChanged;
    }
}
