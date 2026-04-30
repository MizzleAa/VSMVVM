using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.WPF.Design.Components.Charts;
using VSMVVM.WPF.Design.Components.Charts.Core;

namespace VSMVVM.WPF.Sample.ViewModels
{
    public partial class ChartsDemoViewModel : ViewModelBase
    {
        [Property] private ObservableCollection<ChartSeries> _lineSeries;
        [Property] private ObservableCollection<ChartSeries> _scatterSeries;
        [Property] private ObservableCollection<ChartSeries> _barSeries;
        [Property] private ObservableCollection<ChartSeries> _pieSeries;
        [Property] private ChartAxis _lineXAxis;
        [Property] private ChartAxis _lineYAxis;
        [Property] private ChartAxis _scatterXAxis;
        [Property] private ChartAxis _scatterYAxis;
        [Property] private ChartAxis _barXAxis;
        [Property] private ChartAxis _barYAxis;
        [Property] private Orientation _barOrientation = Orientation.Vertical;
        [Property] private BarMode _barMode = BarMode.Grouped;
        [Property] private bool _isDoughnut = false;
        [Property] private PieLabelMode _pieLabelMode = PieLabelMode.Percent;
        [Property] private bool _barShowValues = false;
        [Property] private double[,] _confusionMatrix;
        [Property] private IList<string> _confusionLabels;
        [Property] private string _lineSeriesInfo;

        [Property] private double[,] _heatmapData;
        [Property] private double _heatmapXMin = -3;
        [Property] private double _heatmapXMax = 3;
        [Property] private double _heatmapYMin = -3;
        [Property] private double _heatmapYMax = 3;

        [Property] private double[,] _contourData;
        [Property] private double _contourXMin = -3;
        [Property] private double _contourXMax = 3;
        [Property] private double _contourYMin = -3;
        [Property] private double _contourYMax = 3;
        [Property] private int _contourLevelCount = 12;

        [Property] private ColorMap _selectedColorMap = ColorMap.Viridis;
        [Property] private ScatterDensityMode _scatterDensityMode = ScatterDensityMode.Off;

        [Property] private IList<TreemapItem> _treemapItems;

        // Tooltip Anchor (모든 차트 공유)
        [Property] private TooltipAnchor _tooltipAnchor = TooltipAnchor.FollowMouse;
        [Property] private bool _isTooltipOn = true;
        [Property] private bool _lineCrosshairOn = false;
        [Property] private ChartHoverMode _lineHoverMode = ChartHoverMode.NearestPoint;
        [Property] private bool _scatterCrosshairOn = false;
        [Property] private ChartHoverMode _scatterHoverMode = ChartHoverMode.NearestPoint;

        [Property] private ObservableCollection<ChartSeries> _histogramSeries;
        [Property] private ChartAxis _histogramXAxis;
        [Property] private ChartAxis _histogramYAxis;
        [Property] private object _histogramFitTrigger;
        [RelayCommand] private void FitHistogram() => HistogramFitTrigger = new object();

        // Axis font
        [Property] private FontFamily _axisFontFamily = new FontFamily("Segoe UI");
        [Property] private double _axisFontSize = 11;

        // Live update
        [Property] private bool _lineLiveOn;
        [Property] private bool _scatterLiveOn;
        [Property] private bool _barLiveOn;
        [Property] private bool _pieLiveOn;
        [Property] private bool _candleLiveOn;
        [Property] private double _liveHz = 10;

        // Fit triggers (Behavior가 변화를 감지해서 chart.FitToContent() 호출)
        [Property] private object _lineFitTrigger;
        [Property] private object _scatterFitTrigger;
        [Property] private object _barFitTrigger;
        [Property] private object _heatmapFitTrigger;
        [Property] private object _contourFitTrigger;
        [Property] private object _confusionFitTrigger;
        [Property] private object _treemapFitTrigger;
        [Property] private object _candlestickFitTrigger;

        [Property] private CandleSeries _candleSeries;

        // 모든 axis (AxisFontSyncBehavior에 일괄 전달)
        public IList<ChartAxis> AllAxes => new[] { LineXAxis, LineYAxis, ScatterXAxis, ScatterYAxis, BarXAxis, BarYAxis };

        [RelayCommand] private void FitLine() => LineFitTrigger = new object();
        [RelayCommand] private void FitScatter() => ScatterFitTrigger = new object();
        [RelayCommand] private void FitBar() => BarFitTrigger = new object();
        [RelayCommand] private void FitHeatmap() => HeatmapFitTrigger = new object();
        [RelayCommand] private void FitContour() => ContourFitTrigger = new object();
        [RelayCommand] private void FitConfusion() => ConfusionFitTrigger = new object();
        [RelayCommand] private void FitTreemap() => TreemapFitTrigger = new object();
        [RelayCommand] private void FitCandlestick() => CandlestickFitTrigger = new object();

        public IList<Orientation> BarOrientationOptions { get; } = new[] { Orientation.Vertical, Orientation.Horizontal };
        public IList<BarMode> BarModeOptions { get; } = new[] { BarMode.Grouped, BarMode.Stacked, BarMode.Overlay };
        public IList<ColorMap> ColorMapOptions { get; } = new[] {
            ColorMap.Viridis, ColorMap.Plasma, ColorMap.Inferno, ColorMap.Magma, ColorMap.Cividis };
        public IList<ScatterDensityMode> ScatterDensityModeOptions { get; } = new[] {
            ScatterDensityMode.Off, ScatterDensityMode.Alpha, ScatterDensityMode.Heatmap };
        public IList<PieLabelMode> PieLabelModeOptions { get; } = new[] {
            PieLabelMode.None, PieLabelMode.Percent, PieLabelMode.Value, PieLabelMode.Both };
        public IList<TooltipAnchor> TooltipAnchorOptions { get; } = new[] {
            TooltipAnchor.FollowMouse, TooltipAnchor.TopLeft, TooltipAnchor.TopRight,
            TooltipAnchor.BottomLeft, TooltipAnchor.BottomRight };
        public IList<FontFamily> FontFamilyOptions { get; } = new[] {
            new FontFamily("Segoe UI"), new FontFamily("Consolas"),
            new FontFamily("Cascadia Code"), new FontFamily("Verdana"), new FontFamily("Tahoma") };
        public IList<double> FontSizeOptions { get; } = new double[] { 9, 10, 11, 12, 13, 14, 16, 18 };

        // Live timer
        private readonly DispatcherTimer _liveTimer;
        private readonly Random _liveRng = new(123);
        private double _liveT;

        // Mutable backing lists for live updates
        private List<double> _lineXs1, _lineYs1, _lineXs2, _lineYs2;

        // Candlestick live state
        private double _candlePrice = 100;
        private DateTime _candleNextDate;

        public ChartsDemoViewModel()
        {
            BuildLineSeries();
            BuildScatterSeries();
            BuildBarSeries();
            BuildPieSeries();
            BuildConfusionMatrix();
            BuildHeatmap();
            BuildContour();
            BuildTreemap();
            BuildCandlestick();
            BuildHistogram();

            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, _liveHz)) };
            _liveTimer.Tick += OnLiveTick;
        }

        private void BuildLineSeries()
        {
            //const int N = 100_000;
            const int N = 100;
            var rng = new Random(42);
            _lineXs1 = new List<double>(N + 1024);
            _lineYs1 = new List<double>(N + 1024);
            _lineXs2 = new List<double>(N + 1024);
            _lineYs2 = new List<double>(N + 1024);
            for (var i = 0; i < N; i++)
            {
                var t = i * 0.0005;
                _lineXs1.Add(t);
                _lineYs1.Add(Math.Sin(t * 2) + (rng.NextDouble() - 0.5) * 0.3);
                _lineXs2.Add(t);
                _lineYs2.Add(Math.Cos(t * 1.5) * 0.8 + Math.Sin(t * 7) * 0.15 + (rng.NextDouble() - 0.5) * 0.2);
            }
            _liveT = N * 0.0005;
            LineSeries = new ObservableCollection<ChartSeries>
            {
                new ChartSeries { Title = "Sine + Noise", XValues = _lineXs1, YValues = _lineYs1, StrokeThickness = 1.2 },
                new ChartSeries { Title = "Cosine + Harmonic", XValues = _lineXs2, YValues = _lineYs2, StrokeThickness = 1.2 },
            };
            LineXAxis = new ChartAxis { Title = "Time (s)" };
            LineYAxis = new ChartAxis { Title = "Amplitude" };
            LineSeriesInfo = $"Each series: {N:N0} points (LTTB downsampling)";
        }

        private void BuildScatterSeries()
        {
            const int N = 100_000;
            var rng = new Random(7);
            var xa = new List<double>(N + 1024);
            var ya = new List<double>(N + 1024);
            var xb = new List<double>(N + 1024);
            var yb = new List<double>(N + 1024);
            for (var i = 0; i < N; i++)
            {
                xa.Add(NextGaussian(rng) * 1.5 + 2);
                ya.Add(NextGaussian(rng) * 0.8 + 1);
                xb.Add(NextGaussian(rng) * 1.0 - 2);
                yb.Add(NextGaussian(rng) * 1.6 - 1);
            }
            ScatterSeries = new ObservableCollection<ChartSeries>
            {
                new ChartSeries { Title = "Cluster A", XValues = xa, YValues = ya, MarkerSize = 3 },
                new ChartSeries { Title = "Cluster B", XValues = xb, YValues = yb, MarkerSize = 3, MarkerShape = MarkerShape.Square },
            };
            ScatterXAxis = new ChartAxis { Title = "X" };
            ScatterYAxis = new ChartAxis { Title = "Y" };
        }

        private static double NextGaussian(Random rng)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private void BuildBarSeries()
        {
            var categories = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var s1 = new ChartSeries { Title = "Sales", YValues = new List<double> { 12, 18, 9, 24, 30, 22, 16 } };
            var s2 = new ChartSeries { Title = "Returns", YValues = new List<double> { 2, 3, 1, 4, 5, 3, 2 } };
            var s3 = new ChartSeries { Title = "Refunds", YValues = new List<double> { 1, 0, 2, 1, 2, 0, 1 } };
            BarSeries = new ObservableCollection<ChartSeries> { s1, s2, s3 };
            BarXAxis = new ChartAxis { IsCategorical = true, Categories = categories, Title = "Day" };
            BarYAxis = new ChartAxis { Title = "Count" };
        }

        private void BuildPieSeries()
        {
            PieSeries = new ObservableCollection<ChartSeries>
            {
                new ChartSeries { Title = "Chrome", YValues = new List<double> { 65 } },
                new ChartSeries { Title = "Edge", YValues = new List<double> { 12 } },
                new ChartSeries { Title = "Safari", YValues = new List<double> { 14 } },
                new ChartSeries { Title = "Firefox", YValues = new List<double> { 6 } },
                new ChartSeries { Title = "Other", YValues = new List<double> { 3 } },
            };
        }

        private void BuildConfusionMatrix()
        {
            var labels = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            var n = labels.Length;
            var rng = new Random(11);
            var m = new double[n, n];
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                    m[i, j] = i == j ? 50 + rng.Next(0, 50) : rng.Next(0, 8);
            ConfusionMatrix = m;
            ConfusionLabels = labels;
        }

        private void BuildHeatmap()
        {
            const int N = 80;
            var data = new double[N, N];
            for (var i = 0; i < N; i++)
            {
                var y = HeatmapYMin + (HeatmapYMax - HeatmapYMin) * (i + 0.5) / N;
                for (var j = 0; j < N; j++)
                {
                    var x = HeatmapXMin + (HeatmapXMax - HeatmapXMin) * (j + 0.5) / N;
                    // 두 가우시안 블롭의 합 + 사인 패턴
                    var g1 = Math.Exp(-((x - 1) * (x - 1) + (y - 1) * (y - 1)) / 0.8);
                    var g2 = Math.Exp(-((x + 1) * (x + 1) + (y + 0.5) * (y + 0.5)) / 1.2);
                    data[i, j] = g1 + 0.7 * g2 + 0.05 * Math.Sin(x * 3) * Math.Cos(y * 2);
                }
            }
            HeatmapData = data;
        }

        private void BuildCandlestick()
        {
            const int N = 250;
            var rng = new Random(31);
            var times = new List<DateTime>(N);
            var opens = new List<double>(N);
            var highs = new List<double>(N);
            var lows = new List<double>(N);
            var closes = new List<double>(N);

            var price = 100.0;
            var date = new DateTime(2024, 1, 1);
            var skipped = 0;
            for (var i = 0; i < N; i++)
            {
                // 주말 스킵
                while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    date = date.AddDays(1);
                    skipped++;
                }
                var open = price;
                var change = (rng.NextDouble() - 0.48) * 4;  // 약간 상승 편향
                var close = Math.Max(1, open + change);
                var high = Math.Max(open, close) + rng.NextDouble() * 1.5;
                var low = Math.Min(open, close) - rng.NextDouble() * 1.5;
                low = Math.Max(0.5, low);

                times.Add(date);
                opens.Add(open);
                highs.Add(high);
                lows.Add(low);
                closes.Add(close);

                price = close;
                date = date.AddDays(1);
            }

            _candlePrice = price;
            _candleNextDate = date;

            var bull = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); bull.Freeze(); // Green500
            var bear = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); bear.Freeze(); // Red500
            CandleSeries = new CandleSeries
            {
                Title = "ACME Corp",
                Times = times,
                Opens = opens,
                Highs = highs,
                Lows = lows,
                Closes = closes,
                BullBrush = bull,
                BearBrush = bear,
            };
        }

        private void BuildHistogram()
        {
            const int N = 5000;
            var rng = new Random(13);
            var s1 = new List<double>(N);
            var s2 = new List<double>(N);
            var s3 = new List<double>(N);
            for (var i = 0; i < N; i++)
            {
                s1.Add(NextGaussian(rng) * 8 + 40);   // Cluster A
                s2.Add(NextGaussian(rng) * 6 + 80);   // Cluster B
                s3.Add(NextGaussian(rng) * 9 + 140);  // Cluster C
            }
            HistogramSeries = new ObservableCollection<ChartSeries>
            {
                new ChartSeries { Title = "Cluster A", YValues = s1 },
                new ChartSeries { Title = "Cluster B", YValues = s2 },
                new ChartSeries { Title = "Cluster C", YValues = s3 },
            };
            HistogramXAxis = new ChartAxis { Title = "Intervals" };
            HistogramYAxis = new ChartAxis { Title = "Number of Points" };
        }

        private void BuildTreemap()
        {
            // 가상의 OS 시장 점유율 (예시)
            TreemapItems = new List<TreemapItem>
            {
                new TreemapItem { Label = "Windows", Value = 1240 },
                new TreemapItem { Label = "macOS", Value = 410 },
                new TreemapItem { Label = "iOS", Value = 380 },
                new TreemapItem { Label = "Android", Value = 920 },
                new TreemapItem { Label = "Linux", Value = 95 },
                new TreemapItem { Label = "ChromeOS", Value = 78 },
                new TreemapItem { Label = "Others", Value = 32 },
                new TreemapItem { Label = "FreeBSD", Value = 12 },
                new TreemapItem { Label = "Solaris", Value = 5 },
            };
        }

        private void BuildContour()
        {
            const int N = 100;
            var data = new double[N, N];
            for (var i = 0; i < N; i++)
            {
                var y = ContourYMin + (ContourYMax - ContourYMin) * i / (N - 1);
                for (var j = 0; j < N; j++)
                {
                    var x = ContourXMin + (ContourXMax - ContourXMin) * j / (N - 1);
                    // peaks 함수 (Matlab style)
                    data[i, j] = 3 * (1 - x) * (1 - x) * Math.Exp(-(x * x) - (y + 1) * (y + 1))
                               - 10 * (x / 5 - x * x * x - y * y * y * y * y) * Math.Exp(-x * x - y * y)
                               - Math.Exp(-(x + 1) * (x + 1) - y * y) / 3;
                }
            }
            ContourData = data;
        }

        partial void OnLiveHzChanged(double value)
        {
            if (_liveTimer == null) return;
            var hz = Math.Max(0.5, Math.Min(60, value));
            _liveTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / hz);
        }

        partial void OnLineCrosshairOnChanged(bool value)
        {
            LineHoverMode = value ? ChartHoverMode.XUnified : ChartHoverMode.NearestPoint;
        }

        partial void OnScatterCrosshairOnChanged(bool value)
        {
            ScatterHoverMode = value ? ChartHoverMode.XUnified : ChartHoverMode.NearestPoint;
        }

        partial void OnLineLiveOnChanged(bool value) => UpdateTimerState();
        partial void OnScatterLiveOnChanged(bool value) => UpdateTimerState();
        partial void OnBarLiveOnChanged(bool value) => UpdateTimerState();
        partial void OnPieLiveOnChanged(bool value) => UpdateTimerState();
        partial void OnCandleLiveOnChanged(bool value) => UpdateTimerState();

        private void UpdateTimerState()
        {
            var anyOn = LineLiveOn || ScatterLiveOn || BarLiveOn || PieLiveOn || CandleLiveOn;
            if (anyOn && !_liveTimer.IsEnabled) _liveTimer.Start();
            else if (!anyOn && _liveTimer.IsEnabled) _liveTimer.Stop();
        }

        private void OnLiveTick(object sender, EventArgs e)
        {
            if (LineLiveOn) AppendLineTick();
            if (ScatterLiveOn) AppendScatterTick();
            if (BarLiveOn) MutateBarTick();
            if (PieLiveOn) MutatePieTick();
            if (CandleLiveOn) AppendCandleTick();
        }

        private void AppendCandleTick()
        {
            var series = CandleSeries;
            if (series == null) return;
            // 다음 영업일로 (주말 skip)
            while (_candleNextDate.DayOfWeek == DayOfWeek.Saturday || _candleNextDate.DayOfWeek == DayOfWeek.Sunday)
                _candleNextDate = _candleNextDate.AddDays(1);

            var open = _candlePrice;
            var change = (_liveRng.NextDouble() - 0.48) * 4;
            var close = Math.Max(1, open + change);
            var high = Math.Max(open, close) + _liveRng.NextDouble() * 1.5;
            var low = Math.Min(open, close) - _liveRng.NextDouble() * 1.5;
            low = Math.Max(0.5, low);

            (series.Times as IList<DateTime>)?.Add(_candleNextDate);
            (series.Opens as IList<double>)?.Add(open);
            (series.Highs as IList<double>)?.Add(high);
            (series.Lows as IList<double>)?.Add(low);
            (series.Closes as IList<double>)?.Add(close);

            _candlePrice = close;
            _candleNextDate = _candleNextDate.AddDays(1);

            series.NotifyDataChanged();
        }

        private void AppendLineTick()
        {
            _liveT += 0.0005;
            // O(1) amortized append — ChartSeries 내부 buffer 직접 grow.
            // _lineXs1/_lineYs1 List는 더 이상 동기화 안 함 (live 동안에는 series.Count가 정답).
            if (LineSeries != null && LineSeries.Count >= 2)
            {
                LineSeries[0].AppendPoint(_liveT, Math.Sin(_liveT * 2) + (_liveRng.NextDouble() - 0.5) * 0.3);
                LineSeries[1].AppendPoint(_liveT, Math.Cos(_liveT * 1.5) * 0.8 + (_liveRng.NextDouble() - 0.5) * 0.2);
            }
        }

        private void AppendScatterTick()
        {
            if (ScatterSeries == null) return;
            for (var k = 0; k < ScatterSeries.Count; k++)
            {
                var ser = ScatterSeries[k];
                if (ser == null) continue;
                var dx = k == 0 ? 2 : -2;
                var sx = k == 0 ? 1.5 : 1.0;
                var sy = k == 0 ? 0.8 : 1.6;
                for (var i = 0; i < 50; i++)
                {
                    ser.AppendPoint(NextGaussian(_liveRng) * sx + dx,
                                     NextGaussian(_liveRng) * sy + (k == 0 ? 1 : -1));
                }
            }
        }

        private void MutateBarTick()
        {
            if (BarSeries == null) return;
            foreach (var ser in BarSeries)
            {
                if (ser?.YValues is List<double> ys)
                {
                    for (var i = 0; i < ys.Count; i++)
                    {
                        var delta = (_liveRng.NextDouble() - 0.5) * 4;
                        ys[i] = Math.Max(0, ys[i] + delta);
                    }
                    ser.NotifyDataChanged();
                }
            }
        }

        private void MutatePieTick()
        {
            if (PieSeries == null) return;
            foreach (var ser in PieSeries)
            {
                if (ser?.YValues is List<double> ys && ys.Count > 0)
                {
                    var delta = (_liveRng.NextDouble() - 0.5) * 4;
                    ys[0] = Math.Max(1, ys[0] + delta);
                    ser.NotifyDataChanged();
                }
            }
        }
    }
}
