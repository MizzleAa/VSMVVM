using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VSMVVM.WPF.Controls.Tools
{
    /// <summary>
    /// 이미지 배치 도구. 클릭 시 OpenFileDialog로 이미지 선택 후 캔버스에 배치.
    /// 배치 후 자동으로 Select 모드로 전환합니다.
    /// </summary>
    public class ImageTool : ICanvasTool
    {
        public CanvasToolMode Mode => CanvasToolMode.Image;
        public Cursor ToolCursor => Cursors.Hand;

        public bool OnMouseDown(CanvasToolContext ctx, Point position, MouseButtonEventArgs e)
        {
            if (ctx.IsCtrlDown) return false;
            if (ctx.TargetLayer == null) return false;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Image",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All Files|*.*"
            };

            if (dialog.ShowDialog() != true) return true;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dialog.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var layerPos = GetLayerLocalPosition(ctx, position);

                var image = new Image
                {
                    Source = bitmap,
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight,
                    Stretch = Stretch.Fill
                };

                Canvas.SetLeft(image, layerPos.X);
                Canvas.SetTop(image, layerPos.Y);
                ctx.TargetLayer.Children.Add(image);

                ctx.NotifyDrawingCompleted();
            }
            catch
            {
                // 이미지 로드 실패 시 무시
            }

            return true;
        }

        public void OnMouseMove(CanvasToolContext ctx, Point position, MouseEventArgs e) { }
        public void OnMouseUp(CanvasToolContext ctx, Point position, MouseButtonEventArgs e) { }

        private static Point GetLayerLocalPosition(CanvasToolContext ctx, Point canvasPosition)
        {
            if (ctx.TargetLayer == null) return canvasPosition;
            var layerLeft = Canvas.GetLeft(ctx.TargetLayer);
            var layerTop = Canvas.GetTop(ctx.TargetLayer);
            if (double.IsNaN(layerLeft)) layerLeft = 0;
            if (double.IsNaN(layerTop)) layerTop = 0;
            return new Point(canvasPosition.X - layerLeft, canvasPosition.Y - layerTop);
        }
    }
}
