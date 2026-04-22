using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// GDI+ 공용 렌더링 유틸리티.
    /// 둥근 사각형, 체크마크, 그림자 등 모든 VS컨트롤이 공유하는 드로잉 헬퍼.
    /// </summary>
    public static class DesignRenderer
    {
        #region Rounded Rectangle

        /// <summary>
        /// 둥근 모서리 사각형 GraphicsPath를 생성합니다.
        /// </summary>
        /// <param name="bounds">사각형 영역.</param>
        /// <param name="radius">모서리 반경 (px).</param>
        public static GraphicsPath CreateRoundedRect(RectangleF bounds, int radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2f;

            if (radius <= 0 || diameter >= bounds.Width || diameter >= bounds.Height)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);

            // Top-Left
            path.AddArc(arc, 180, 90);

            // Top-Right
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-Right
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-Left
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// 둥근 모서리 사각형 GraphicsPath를 생성합니다 (Rectangle 오버로드).
        /// </summary>
        public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            return CreateRoundedRect(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius);
        }

        #endregion

        #region Fill & Draw

        /// <summary>
        /// 둥근 사각형 배경을 채웁니다 (RectangleF, float — primary).
        /// Stroke 경로 안쪽으로 0.5px 당겨서 테두리 바깥 픽셀 bleed를 방지합니다.
        /// </summary>
        public static void FillRoundedRect(Graphics g, RectangleF bounds, Color color, float radius)
        {
            var fillBounds = RectangleF.Inflate(bounds, -0.5f, -0.5f);
            using var path = CreateRoundedRect(fillBounds, (int)Math.Round(radius));
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        /// <summary>
        /// 둥근 사각형 배경을 채웁니다 (Rectangle — 하위호환 forward).
        /// </summary>
        public static void FillRoundedRect(Graphics g, Rectangle bounds, Color color, int radius)
        {
            FillRoundedRect(g, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), color, radius);
        }

        /// <summary>
        /// 둥근 사각형 테두리를 그립니다 (RectangleF, float — primary).
        /// 1px AA pen이 경로 위에 걸쳐 클리핑되지 않도록 bounds를 borderWidth/2만큼 내측으로 Inflate합니다.
        /// </summary>
        public static void DrawRoundedRect(Graphics g, RectangleF bounds, Color color, float radius, float borderWidth = 1f)
        {
            float inset = borderWidth / 2f;
            var adjusted = RectangleF.Inflate(bounds, -inset, -inset);

            using var path = CreateRoundedRect(adjusted, (int)Math.Round(radius));
            using var pen = new Pen(color, borderWidth);
            g.DrawPath(pen, path);
        }

        /// <summary>
        /// 둥근 사각형 테두리를 그립니다 (Rectangle — 하위호환 forward).
        /// </summary>
        public static void DrawRoundedRect(Graphics g, Rectangle bounds, Color color, int radius, int borderWidth = 1)
        {
            DrawRoundedRect(g, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), color, radius, borderWidth);
        }

        #endregion

        #region Check Mark

        /// <summary>
        /// 체크마크를 그립니다 (✓).
        /// </summary>
        public static void DrawCheckMark(Graphics g, Rectangle bounds, Color color, float strokeWidth = 2f)
        {
            using var pen = new Pen(color, strokeWidth);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            float x = bounds.X;
            float y = bounds.Y;
            float w = bounds.Width;
            float h = bounds.Height;

            // 체크마크 형태: 좌하단 → 중앙하단 → 우상단
            var points = new PointF[]
            {
                new PointF(x + w * 0.2f, y + h * 0.5f),
                new PointF(x + w * 0.45f, y + h * 0.75f),
                new PointF(x + w * 0.8f, y + h * 0.25f)
            };

            g.DrawLines(pen, points);
        }

        #endregion

        #region Radio Dot

        /// <summary>
        /// 라디오 버튼 내부 원을 그립니다.
        /// </summary>
        public static void FillRadioDot(Graphics g, Rectangle bounds, Color color, int dotSize)
        {
            int x = bounds.X + (bounds.Width - dotSize) / 2;
            int y = bounds.Y + (bounds.Height - dotSize) / 2;

            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, x, y, dotSize, dotSize);
        }

        #endregion

        #region Region Clipping

        /// <summary>
        /// 컨트롤의 Region을 둥근 사각형으로 클리핑합니다.
        /// 자식 HWND가 모서리를 덮는 것을 OS-level에서 방지합니다.
        /// cornerRadius &lt;= 0 이거나 크기가 0 이하이면 Region을 제거합니다.
        /// </summary>
        public static void ApplyRoundedRegion(Control control, int cornerRadius)
        {
            if (control == null) return;
            if (control.Width <= 0 || control.Height <= 0 || cornerRadius <= 0)
            {
                control.Region = null;
                return;
            }

            var bounds = new Rectangle(0, 0, control.Width, control.Height);
            var path = CreateRoundedRect(bounds, cornerRadius);
            control.Region = new Region(path);
            path.Dispose();
        }

        #endregion

        #region Graphics Setup

        /// <summary>
        /// 고품질 렌더링을 위한 Graphics 초기화.
        /// </summary>
        public static void SetHighQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        #endregion

        #region Text

        /// <summary>
        /// 중앙 정렬 텍스트를 렌더링합니다.
        /// </summary>
        public static void DrawCenteredText(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            using var brush = new SolidBrush(color);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(text, font, brush, bounds, sf);
        }

        /// <summary>
        /// 좌측 정렬 텍스트를 렌더링합니다.
        /// </summary>
        public static void DrawLeftText(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            using var brush = new SolidBrush(color);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(text, font, brush, bounds, sf);
        }

        #endregion
    }
}
