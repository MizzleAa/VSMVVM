using System;
using System.Windows;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// Zoom/Pan 좌표 변환 서비스 인터페이스.
    /// </summary>
    public interface IZoomService
    {
        /// <summary>
        /// 현재 Zoom 레벨.
        /// </summary>
        double ZoomLevel { get; set; }

        /// <summary>
        /// X축 오프셋.
        /// </summary>
        double OffsetX { get; set; }

        /// <summary>
        /// Y축 오프셋.
        /// </summary>
        double OffsetY { get; set; }

        /// <summary>
        /// 화면 좌표를 캔버스 좌표로 변환.
        /// </summary>
        Point ScreenToCanvas(Point screenPoint);

        /// <summary>
        /// 캔버스 좌표를 화면 좌표로 변환.
        /// </summary>
        Point CanvasToScreen(Point canvasPoint);

        /// <summary>
        /// 뷰를 리셋합니다.
        /// </summary>
        void ResetView();
    }

    /// <summary>
    /// Zoom/Pan 좌표 변환 서비스 구현체.
    /// </summary>
    public sealed class ZoomService : IZoomService
    {
        #region Properties

        public double ZoomLevel { get; set; } = 1.0;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        #endregion

        #region IZoomService

        public Point ScreenToCanvas(Point screenPoint)
        {
            var x = (screenPoint.X - OffsetX) / ZoomLevel;
            var y = (screenPoint.Y - OffsetY) / ZoomLevel;
            return new Point(x, y);
        }

        public Point CanvasToScreen(Point canvasPoint)
        {
            var x = canvasPoint.X * ZoomLevel + OffsetX;
            var y = canvasPoint.Y * ZoomLevel + OffsetY;
            return new Point(x, y);
        }

        public void ResetView()
        {
            ZoomLevel = 1.0;
            OffsetX = 0;
            OffsetY = 0;
        }

        #endregion
    }
}
