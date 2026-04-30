using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Design.Components.Charts.Behaviors
{
    /// <summary>
    /// 시리즈 swatch 버튼이 클릭되면 Popup을 열고, popup 안의 ColorPicker가
    /// SeriesColorPicker.CurrentBrush 를 양방향 바인딩하여 색을 변경하면
    /// SeriesColorPicker가 원본 (ChartSeries.Brush / CandleSeries.BullBrush 등)에 다시 써준다.
    ///
    /// 사용 예 (XAML):
    ///   1) 시리즈 swatch:
    ///       chBeh:SeriesColorPicker.Series="{Binding}" chBeh:SeriesColorPicker.Popup="{Binding ElementName=ColorPopup}"
    ///   2) 캔들 양봉/음봉 swatch:
    ///       chBeh:SeriesColorPicker.CandleSeries="{Binding CandleSeries}"
    ///       chBeh:SeriesColorPicker.CandleColorRole="Bull"
    ///       chBeh:SeriesColorPicker.Popup="{Binding ElementName=ColorPopup}"
    ///   3) Popup 내 ColorPicker:
    ///       SelectedBrush="{Binding Path=(chBeh:SeriesColorPicker.CurrentBrush), ElementName=ColorPopup, Mode=TwoWay}"
    /// </summary>
    public static class SeriesColorPicker
    {
        public enum CandleRole { Bull, Bear }

        #region Series (button에 부착할 ChartSeries)

        public static readonly DependencyProperty SeriesProperty =
            DependencyProperty.RegisterAttached(
                "Series", typeof(ChartSeries), typeof(SeriesColorPicker),
                new PropertyMetadata(null, OnTriggerChanged));

        public static ChartSeries GetSeries(DependencyObject d) => (ChartSeries)d.GetValue(SeriesProperty);
        public static void SetSeries(DependencyObject d, ChartSeries v) => d.SetValue(SeriesProperty, v);

        #endregion

        #region CandleSeries + CandleColorRole

        public static readonly DependencyProperty CandleSeriesProperty =
            DependencyProperty.RegisterAttached(
                "CandleSeries", typeof(CandleSeries), typeof(SeriesColorPicker),
                new PropertyMetadata(null, OnTriggerChanged));

        public static CandleSeries GetCandleSeries(DependencyObject d) => (CandleSeries)d.GetValue(CandleSeriesProperty);
        public static void SetCandleSeries(DependencyObject d, CandleSeries v) => d.SetValue(CandleSeriesProperty, v);

        public static readonly DependencyProperty CandleColorRoleProperty =
            DependencyProperty.RegisterAttached(
                "CandleColorRole", typeof(CandleRole), typeof(SeriesColorPicker),
                new PropertyMetadata(CandleRole.Bull));

        public static CandleRole GetCandleColorRole(DependencyObject d) => (CandleRole)d.GetValue(CandleColorRoleProperty);
        public static void SetCandleColorRole(DependencyObject d, CandleRole v) => d.SetValue(CandleColorRoleProperty, v);

        #endregion

        #region Popup (button에 부착)

        public static readonly DependencyProperty PopupProperty =
            DependencyProperty.RegisterAttached(
                "Popup", typeof(Popup), typeof(SeriesColorPicker),
                new PropertyMetadata(null));

        public static Popup GetPopup(DependencyObject d) => (Popup)d.GetValue(PopupProperty);
        public static void SetPopup(DependencyObject d, Popup v) => d.SetValue(PopupProperty, v);

        #endregion

        #region CurrentColor (Popup에 부착 — VSMVVM.WPF.Controls.ColorPicker.SelectedColor 양방향 바인딩)

        public static readonly DependencyProperty CurrentColorProperty =
            DependencyProperty.RegisterAttached(
                "CurrentColor", typeof(Color), typeof(SeriesColorPicker),
                new FrameworkPropertyMetadata(Colors.Transparent,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnCurrentColorChanged));

        public static Color GetCurrentColor(DependencyObject d) => (Color)d.GetValue(CurrentColorProperty);
        public static void SetCurrentColor(DependencyObject d, Color v) => d.SetValue(CurrentColorProperty, v);

        private static void OnCurrentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup) return;
            if (popup.GetValue(SuppressApplyProperty) is true) return;
            ApplyColorToActiveSource(popup, (Color)e.NewValue);
        }

        // Popup에 임시로 보관할 active source 정보
        private static readonly DependencyProperty ActiveSourceProperty =
            DependencyProperty.RegisterAttached(
                "ActiveSource", typeof(object), typeof(SeriesColorPicker),
                new PropertyMetadata(null));

        private static readonly DependencyProperty ActiveRoleProperty =
            DependencyProperty.RegisterAttached(
                "ActiveRole", typeof(CandleRole), typeof(SeriesColorPicker),
                new PropertyMetadata(CandleRole.Bull));

        // Popup 열릴 때 CurrentColor를 채워 picker가 초기값을 갖되 OnCurrentColorChanged의
        // 역방향 적용은 막기 위한 플래그
        private static readonly DependencyProperty SuppressApplyProperty =
            DependencyProperty.RegisterAttached(
                "SuppressApply", typeof(bool), typeof(SeriesColorPicker),
                new PropertyMetadata(false));

        private static void ApplyColorToActiveSource(Popup popup, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var src = popup.GetValue(ActiveSourceProperty);
            if (src is ChartSeries series)
            {
                series.Brush = brush;
            }
            else if (src is CandleSeries candle)
            {
                var role = (CandleRole)popup.GetValue(ActiveRoleProperty);
                if (role == CandleRole.Bull) candle.BullBrush = brush;
                else candle.BearBrush = brush;
            }
        }

        private static Color BrushToColor(Brush b)
        {
            if (b is SolidColorBrush scb)
            {
                // Alpha=0 (transparent) 인 경우 picker 초기값으로 부적절 — 흰색 fallback
                if (scb.Color.A == 0) return Colors.White;
                return scb.Color;
            }
            return Colors.White;
        }

        #endregion

        #region Trigger (button click → popup open + active source 설정)

        private static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ButtonBase btn) return;
            btn.Click -= OnSwatchOpenClick;
            // Series 또는 CandleSeries 둘 중 하나라도 부착되면 click 핸들러 등록
            if (GetSeries(btn) != null || GetCandleSeries(btn) != null)
                btn.Click += OnSwatchOpenClick;
        }

        private static void OnSwatchOpenClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ButtonBase btn) return;
            var popup = GetPopup(btn);
            if (popup == null) return;

            var series = GetSeries(btn);
            var candle = GetCandleSeries(btn);

            popup.SetValue(SuppressApplyProperty, true);
            try
            {
                if (series != null)
                {
                    popup.SetValue(ActiveSourceProperty, series);
                    SetCurrentColor(popup, BrushToColor(series.Brush));
                }
                else if (candle != null)
                {
                    var role = GetCandleColorRole(btn);
                    popup.SetValue(ActiveSourceProperty, candle);
                    popup.SetValue(ActiveRoleProperty, role);
                    var sourceBrush = role == CandleRole.Bull ? candle.BullBrush : candle.BearBrush;
                    SetCurrentColor(popup, BrushToColor(sourceBrush));
                }
                else return;
            }
            finally { popup.SetValue(SuppressApplyProperty, false); }

            popup.PlacementTarget = btn;
            popup.Placement = PlacementMode.Bottom;
            popup.IsOpen = true;
        }

        #endregion
    }
}
