using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// HSV + Alpha 를 지원하는 인라인 ColorPicker 컨트롤.
    /// SV pad (포화도/명도), Hue 슬라이더, Alpha 슬라이더, RGBA 숫자 입력, Hex 입력, 미리보기 swatch 로 구성.
    /// SelectedColor 는 드래그 중 실시간 갱신되며, 부모(Popup 등)가 OK/Cancel 을 별도로 제공한다.
    /// </summary>
    [TemplatePart(Name = PartSvPad, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartSvBase, Type = typeof(Rectangle))]
    [TemplatePart(Name = PartSvThumb, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartHueTrack, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartHueThumb, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartAlphaTrack, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartAlphaThumb, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PartRText, Type = typeof(TextBox))]
    [TemplatePart(Name = PartGText, Type = typeof(TextBox))]
    [TemplatePart(Name = PartBText, Type = typeof(TextBox))]
    [TemplatePart(Name = PartAText, Type = typeof(TextBox))]
    [TemplatePart(Name = PartHexText, Type = typeof(TextBox))]
    public sealed class ColorPicker : Control
    {
        public const string PartSvPad = "PART_SvPad";
        public const string PartSvBase = "PART_SvBase";
        public const string PartSvThumb = "PART_SvThumb";
        public const string PartHueTrack = "PART_HueTrack";
        public const string PartHueThumb = "PART_HueThumb";
        public const string PartAlphaTrack = "PART_AlphaTrack";
        public const string PartAlphaThumb = "PART_AlphaThumb";
        public const string PartRText = "PART_R";
        public const string PartGText = "PART_G";
        public const string PartBText = "PART_B";
        public const string PartAText = "PART_A";
        public const string PartHexText = "PART_Hex";

        private FrameworkElement? _svPad;
        private Rectangle? _svBase;
        private FrameworkElement? _svThumb;
        private FrameworkElement? _hueTrack;
        private FrameworkElement? _hueThumb;
        private FrameworkElement? _alphaTrack;
        private FrameworkElement? _alphaThumb;
        private TextBox? _rText;
        private TextBox? _gText;
        private TextBox? _bText;
        private TextBox? _aText;
        private TextBox? _hexText;

        // HSV 내부 상태. SelectedColor 의 RGB 로부터 파생되지만, thumb 위치 계산을 위해 직접 보관한다.
        // (회색 계열 색은 hue 가 undefined 이므로 RGB 만으로는 pad thumb 복원이 불가 → 내부 보관 필요)
        private double _hue;        // 0~360
        private double _saturation; // 0~1
        private double _value;      // 0~1
        private byte _alpha = 255;

        private bool _syncing; // 내부 갱신 루프 방지

        #region DependencyProperties

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(
                    Colors.Red,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker cp) cp.OnSelectedColorChanged((Color)e.NewValue);
        }

        #endregion

        static ColorPicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(typeof(ColorPicker)));
        }

        public ColorPicker()
        {
            Focusable = false;
            // 초기 HSV 는 SelectedColor(Red) 기준.
            SyncHsvFromColor(SelectedColor, forceHue: true);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            DetachEvents();

            _svPad = GetTemplateChild(PartSvPad) as FrameworkElement;
            _svBase = GetTemplateChild(PartSvBase) as Rectangle;
            _svThumb = GetTemplateChild(PartSvThumb) as FrameworkElement;
            _hueTrack = GetTemplateChild(PartHueTrack) as FrameworkElement;
            _hueThumb = GetTemplateChild(PartHueThumb) as FrameworkElement;
            _alphaTrack = GetTemplateChild(PartAlphaTrack) as FrameworkElement;
            _alphaThumb = GetTemplateChild(PartAlphaThumb) as FrameworkElement;
            _rText = GetTemplateChild(PartRText) as TextBox;
            _gText = GetTemplateChild(PartGText) as TextBox;
            _bText = GetTemplateChild(PartBText) as TextBox;
            _aText = GetTemplateChild(PartAText) as TextBox;
            _hexText = GetTemplateChild(PartHexText) as TextBox;

            AttachEvents();
            UpdateAllFromHsv();
        }

        private void AttachEvents()
        {
            if (_svPad != null)
            {
                _svPad.MouseLeftButtonDown += SvPad_MouseLeftButtonDown;
                _svPad.MouseMove += SvPad_MouseMove;
                _svPad.MouseLeftButtonUp += SvPad_MouseLeftButtonUp;
                _svPad.SizeChanged += (_, __) => UpdateSvThumbPosition();
            }
            if (_hueTrack != null)
            {
                _hueTrack.MouseLeftButtonDown += HueTrack_MouseLeftButtonDown;
                _hueTrack.MouseMove += HueTrack_MouseMove;
                _hueTrack.MouseLeftButtonUp += HueTrack_MouseLeftButtonUp;
                _hueTrack.SizeChanged += (_, __) => UpdateHueThumbPosition();
            }
            if (_alphaTrack != null)
            {
                _alphaTrack.MouseLeftButtonDown += AlphaTrack_MouseLeftButtonDown;
                _alphaTrack.MouseMove += AlphaTrack_MouseMove;
                _alphaTrack.MouseLeftButtonUp += AlphaTrack_MouseLeftButtonUp;
                _alphaTrack.SizeChanged += (_, __) => UpdateAlphaThumbPosition();
            }
            if (_rText != null) _rText.LostFocus += OnRgbTextLostFocus;
            if (_gText != null) _gText.LostFocus += OnRgbTextLostFocus;
            if (_bText != null) _bText.LostFocus += OnRgbTextLostFocus;
            if (_aText != null) _aText.LostFocus += OnRgbTextLostFocus;
            if (_hexText != null)
            {
                _hexText.LostFocus += OnHexTextLostFocus;
                _hexText.KeyDown += OnHexTextKeyDown;
            }
        }

        private void DetachEvents()
        {
            if (_svPad != null)
            {
                _svPad.MouseLeftButtonDown -= SvPad_MouseLeftButtonDown;
                _svPad.MouseMove -= SvPad_MouseMove;
                _svPad.MouseLeftButtonUp -= SvPad_MouseLeftButtonUp;
            }
            if (_hueTrack != null)
            {
                _hueTrack.MouseLeftButtonDown -= HueTrack_MouseLeftButtonDown;
                _hueTrack.MouseMove -= HueTrack_MouseMove;
                _hueTrack.MouseLeftButtonUp -= HueTrack_MouseLeftButtonUp;
            }
            if (_alphaTrack != null)
            {
                _alphaTrack.MouseLeftButtonDown -= AlphaTrack_MouseLeftButtonDown;
                _alphaTrack.MouseMove -= AlphaTrack_MouseMove;
                _alphaTrack.MouseLeftButtonUp -= AlphaTrack_MouseLeftButtonUp;
            }
            if (_rText != null) _rText.LostFocus -= OnRgbTextLostFocus;
            if (_gText != null) _gText.LostFocus -= OnRgbTextLostFocus;
            if (_bText != null) _bText.LostFocus -= OnRgbTextLostFocus;
            if (_aText != null) _aText.LostFocus -= OnRgbTextLostFocus;
            if (_hexText != null)
            {
                _hexText.LostFocus -= OnHexTextLostFocus;
                _hexText.KeyDown -= OnHexTextKeyDown;
            }
        }

        #region SV Pad drag

        private void SvPad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_svPad == null) return;
            _svPad.CaptureMouse();
            UpdateSvFromMouse(e.GetPosition(_svPad));
        }

        private void SvPad_MouseMove(object sender, MouseEventArgs e)
        {
            if (_svPad == null || !_svPad.IsMouseCaptured) return;
            UpdateSvFromMouse(e.GetPosition(_svPad));
        }

        private void SvPad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_svPad == null) return;
            _svPad.ReleaseMouseCapture();
        }

        private void UpdateSvFromMouse(Point p)
        {
            if (_svPad == null) return;
            double w = _svPad.ActualWidth;
            double h = _svPad.ActualHeight;
            if (w <= 0 || h <= 0) return;
            double sx = Math.Max(0, Math.Min(w, p.X));
            double sy = Math.Max(0, Math.Min(h, p.Y));
            _saturation = sx / w;
            _value = 1.0 - (sy / h);
            CommitHsv();
        }

        #endregion

        #region Hue slider drag

        private void HueTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_hueTrack == null) return;
            _hueTrack.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(_hueTrack));
        }

        private void HueTrack_MouseMove(object sender, MouseEventArgs e)
        {
            if (_hueTrack == null || !_hueTrack.IsMouseCaptured) return;
            UpdateHueFromMouse(e.GetPosition(_hueTrack));
        }

        private void HueTrack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_hueTrack == null) return;
            _hueTrack.ReleaseMouseCapture();
        }

        private void UpdateHueFromMouse(Point p)
        {
            if (_hueTrack == null) return;
            double w = _hueTrack.ActualWidth;
            if (w <= 0) return;
            double x = Math.Max(0, Math.Min(w, p.X));
            _hue = (x / w) * 360.0;
            if (_hue >= 360.0) _hue = 0;
            CommitHsv();
        }

        #endregion

        #region Alpha slider drag

        private void AlphaTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_alphaTrack == null) return;
            _alphaTrack.CaptureMouse();
            UpdateAlphaFromMouse(e.GetPosition(_alphaTrack));
        }

        private void AlphaTrack_MouseMove(object sender, MouseEventArgs e)
        {
            if (_alphaTrack == null || !_alphaTrack.IsMouseCaptured) return;
            UpdateAlphaFromMouse(e.GetPosition(_alphaTrack));
        }

        private void AlphaTrack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_alphaTrack == null) return;
            _alphaTrack.ReleaseMouseCapture();
        }

        private void UpdateAlphaFromMouse(Point p)
        {
            if (_alphaTrack == null) return;
            double w = _alphaTrack.ActualWidth;
            if (w <= 0) return;
            double x = Math.Max(0, Math.Min(w, p.X));
            _alpha = (byte)Math.Round((x / w) * 255.0);
            CommitHsv();
        }

        #endregion

        #region RGB / Hex TextBox input

        private void OnRgbTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (_rText == null || _gText == null || _bText == null || _aText == null) return;
            byte r = ParseByte(_rText.Text, SelectedColor.R);
            byte g = ParseByte(_gText.Text, SelectedColor.G);
            byte b = ParseByte(_bText.Text, SelectedColor.B);
            byte a = ParseByte(_aText.Text, SelectedColor.A);
            var color = Color.FromArgb(a, r, g, b);
            _alpha = a;
            SyncHsvFromColor(color, forceHue: false);
            CommitHsv();
        }

        private void OnHexTextLostFocus(object sender, RoutedEventArgs e) => ApplyHexFromText();
        private void OnHexTextKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyHexFromText();
        }

        private void ApplyHexFromText()
        {
            if (_hexText == null) return;
            if (TryParseHex(_hexText.Text, out var c))
            {
                _alpha = c.A;
                SyncHsvFromColor(c, forceHue: false);
                CommitHsv();
            }
            else
            {
                // 파싱 실패: 현재 색으로 텍스트 되돌림.
                UpdateHexTextFromColor();
            }
        }

        private static byte ParseByte(string? text, byte fallback)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return (byte)Math.Max(0, Math.Min(255, v));
            return fallback;
        }

        private static bool TryParseHex(string? text, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = text.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            // 형식: RRGGBB (A=255) 또는 AARRGGBB.
            if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v6))
            {
                byte r = (byte)((v6 >> 16) & 0xFF);
                byte g = (byte)((v6 >> 8) & 0xFF);
                byte b = (byte)(v6 & 0xFF);
                color = Color.FromArgb(0xFF, r, g, b);
                return true;
            }
            if (s.Length == 8 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v8))
            {
                byte a = (byte)((v8 >> 24) & 0xFF);
                byte r = (byte)((v8 >> 16) & 0xFF);
                byte g = (byte)((v8 >> 8) & 0xFF);
                byte b = (byte)(v8 & 0xFF);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }
            return false;
        }

        #endregion

        #region HSV/RGB 변환 & 동기화

        /// <summary>SelectedColor(DP) 가 외부에서 바뀌었을 때 내부 HSV/UI 를 재동기화.</summary>
        private void OnSelectedColorChanged(Color c)
        {
            if (_syncing) return;
            _alpha = c.A;
            SyncHsvFromColor(c, forceHue: false);
            UpdateAllFromHsv(pushSelectedColor: false);
        }

        /// <summary>RGB → HSV. 회색 계열(S=0)이면 기존 hue 유지해 thumb 위치 보존.</summary>
        private void SyncHsvFromColor(Color c, bool forceHue)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                if (max == r) h = 60.0 * (((g - b) / delta) % 6.0);
                else if (max == g) h = 60.0 * (((b - r) / delta) + 2.0);
                else h = 60.0 * (((r - g) / delta) + 4.0);
                if (h < 0) h += 360.0;
                _hue = h;
            }
            else if (forceHue)
            {
                _hue = 0;
            }
            // 회색이면 hue 유지.

            _value = max;
            _saturation = max > 0 ? delta / max : 0;
        }

        /// <summary>HSV → RGB. h:0~360, s:0~1, v:0~1.</summary>
        private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            if (s <= 0)
            {
                byte gray = (byte)Math.Round(v * 255.0);
                return (gray, gray, gray);
            }
            double hh = h;
            if (hh >= 360.0) hh = 0;
            hh /= 60.0;
            int i = (int)Math.Floor(hh);
            double ff = hh - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - (s * ff));
            double t = v * (1.0 - (s * (1.0 - ff)));
            double r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return ((byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0));
        }

        /// <summary>현재 HSV 로부터 SelectedColor 갱신 + 모든 UI 동기화.</summary>
        private void CommitHsv()
        {
            UpdateAllFromHsv(pushSelectedColor: true);
        }

        private void UpdateAllFromHsv(bool pushSelectedColor = true)
        {
            var (r, g, b) = HsvToRgb(_hue, _saturation, _value);
            var color = Color.FromArgb(_alpha, r, g, b);

            if (pushSelectedColor && color != SelectedColor)
            {
                _syncing = true;
                try { SelectedColor = color; }
                finally { _syncing = false; }
            }

            UpdateSvPadBackground();
            UpdateAlphaTrackBackground();
            UpdateSvThumbPosition();
            UpdateHueThumbPosition();
            UpdateAlphaThumbPosition();
            UpdateRgbTextFromColor(color);
            UpdateHexTextFromColor(color);
        }

        private void UpdateSvPadBackground()
        {
            if (_svBase == null) return;
            // Base: 좌측 white → 우측 순색 hue 가로 그라데이션.
            // Overlay(검정 세로 그라데이션)는 Template 고정이라 코드 미관여.
            var (r, g, b) = HsvToRgb(_hue, 1.0, 1.0);
            var pureHue = Color.FromRgb(r, g, b);
            var lg = new LinearGradientBrush(Colors.White, pureHue, new Point(0, 0), new Point(1, 0));
            lg.Freeze();
            _svBase.Fill = lg;
        }

        private void UpdateAlphaTrackBackground()
        {
            if (_alphaTrack == null) return;
            var (r, g, b) = HsvToRgb(_hue, _saturation, _value);
            var solid = Color.FromRgb(r, g, b);
            var gradient = new LinearGradientBrush(
                Color.FromArgb(0, r, g, b),
                solid,
                new Point(0, 0),
                new Point(1, 0));
            gradient.Freeze();
            _alphaTrack.SetValue(Panel.BackgroundProperty, gradient);
        }

        private void UpdateSvThumbPosition()
        {
            if (_svPad == null || _svThumb == null) return;
            double w = _svPad.ActualWidth;
            double h = _svPad.ActualHeight;
            if (w <= 0 || h <= 0) return;
            double x = _saturation * w;
            double y = (1.0 - _value) * h;
            Canvas.SetLeft(_svThumb, x - _svThumb.ActualWidth / 2);
            Canvas.SetTop(_svThumb, y - _svThumb.ActualHeight / 2);
        }

        private void UpdateHueThumbPosition()
        {
            if (_hueTrack == null || _hueThumb == null) return;
            double w = _hueTrack.ActualWidth;
            if (w <= 0) return;
            double x = (_hue / 360.0) * w;
            Canvas.SetLeft(_hueThumb, x - _hueThumb.ActualWidth / 2);
        }

        private void UpdateAlphaThumbPosition()
        {
            if (_alphaTrack == null || _alphaThumb == null) return;
            double w = _alphaTrack.ActualWidth;
            if (w <= 0) return;
            double x = (_alpha / 255.0) * w;
            Canvas.SetLeft(_alphaThumb, x - _alphaThumb.ActualWidth / 2);
        }

        private void UpdateRgbTextFromColor(Color c)
        {
            if (_rText != null) _rText.Text = c.R.ToString(CultureInfo.InvariantCulture);
            if (_gText != null) _gText.Text = c.G.ToString(CultureInfo.InvariantCulture);
            if (_bText != null) _bText.Text = c.B.ToString(CultureInfo.InvariantCulture);
            if (_aText != null) _aText.Text = c.A.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateHexTextFromColor(Color c)
        {
            if (_hexText == null) return;
            _hexText.Text = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        private void UpdateHexTextFromColor() => UpdateHexTextFromColor(SelectedColor);

        #endregion
    }
}
