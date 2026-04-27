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
        /// Zoom 또는 오프셋이 바뀔 때마다 발화합니다.
        /// </summary>
        event EventHandler ViewChanged;

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
    /// 값이 실제로 바뀔 때만 <see cref="ViewChanged"/>를 발화합니다.
    /// </summary>
    public sealed class ZoomService : IZoomService
    {
        #region Fields

        private double _zoomLevel = 1.0;
        private double _offsetX;
        private double _offsetY;
        private bool _suppressNotify;

        #endregion

        #region Properties

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (_zoomLevel == value) return;
                _zoomLevel = value;
                RaiseViewChanged();
            }
        }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (_offsetX == value) return;
                _offsetX = value;
                RaiseViewChanged();
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (_offsetY == value) return;
                _offsetY = value;
                RaiseViewChanged();
            }
        }

        #endregion

        #region Events

        public event EventHandler ViewChanged;

        #endregion

        #region IZoomService

        public Point ScreenToCanvas(Point screenPoint)
        {
            var x = (screenPoint.X - _offsetX) / _zoomLevel;
            var y = (screenPoint.Y - _offsetY) / _zoomLevel;
            return new Point(x, y);
        }

        public Point CanvasToScreen(Point canvasPoint)
        {
            var x = canvasPoint.X * _zoomLevel + _offsetX;
            var y = canvasPoint.Y * _zoomLevel + _offsetY;
            return new Point(x, y);
        }

        public void ResetView()
        {
            // 3개 필드 연속 변경 시 발화를 마지막 한 번으로 합치기
            _suppressNotify = true;
            try
            {
                ZoomLevel = 1.0;
                OffsetX = 0;
                OffsetY = 0;
            }
            finally
            {
                _suppressNotify = false;
            }
            RaiseViewChanged();
        }

        #endregion

        #region Private

        private void RaiseViewChanged()
        {
            if (_suppressNotify) return;
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
