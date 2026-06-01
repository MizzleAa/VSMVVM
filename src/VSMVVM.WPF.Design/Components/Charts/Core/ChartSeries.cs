using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public enum MarkerShape { Circle, Square, Triangle, Diamond, Cross }

    public class ChartSeries : DependencyObject
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ChartSeries),
                new PropertyMetadata(string.Empty, OnAnyChanged));

        public static readonly DependencyProperty BrushProperty =
            DependencyProperty.Register(nameof(Brush), typeof(Brush), typeof(ChartSeries),
                new PropertyMetadata(null, OnAnyChanged));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ChartSeries),
                new PropertyMetadata(1.5, OnAnyChanged));

        public static readonly DependencyProperty XValuesProperty =
            DependencyProperty.Register(nameof(XValues), typeof(IList<double>), typeof(ChartSeries),
                new PropertyMetadata(null, OnDataSourceChanged));

        public static readonly DependencyProperty YValuesProperty =
            DependencyProperty.Register(nameof(YValues), typeof(IList<double>), typeof(ChartSeries),
                new PropertyMetadata(null, OnDataSourceChanged));

        public static readonly DependencyProperty CategoryValuesProperty =
            DependencyProperty.Register(nameof(CategoryValues), typeof(IList<double>), typeof(ChartSeries),
                new PropertyMetadata(null, OnDataSourceChanged));

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(nameof(IsVisible), typeof(bool), typeof(ChartSeries),
                new PropertyMetadata(true, OnAnyChanged));

        public static readonly DependencyProperty MarkerSizeProperty =
            DependencyProperty.Register(nameof(MarkerSize), typeof(double), typeof(ChartSeries),
                new PropertyMetadata(0.0, OnAnyChanged));

        public static readonly DependencyProperty MarkerShapeProperty =
            DependencyProperty.Register(nameof(MarkerShape), typeof(MarkerShape), typeof(ChartSeries),
                new PropertyMetadata(MarkerShape.Circle, OnAnyChanged));

        /// <summary>
        /// 카테고리별 색. null 이면 <see cref="Brush"/> 한 색으로 그림 (기존 동작).
        /// 값이 있고 BarChart 같이 카테고리 인덱스를 갖는 차트에서, 카테고리 i 의 막대를 <c>BrushPerCategory[i]</c> 로 그린다.
        /// 길이가 카테고리보다 짧으면 그 뒤는 <see cref="Brush"/> 로 fallback.
        /// </summary>
        public static readonly DependencyProperty BrushPerCategoryProperty =
            DependencyProperty.Register(nameof(BrushPerCategory), typeof(IList<Brush>), typeof(ChartSeries),
                new PropertyMetadata(null, OnAnyChanged));

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public Brush Brush { get => (Brush)GetValue(BrushProperty); set => SetValue(BrushProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public IList<double> XValues { get => (IList<double>)GetValue(XValuesProperty); set => SetValue(XValuesProperty, value); }
        public IList<double> YValues { get => (IList<double>)GetValue(YValuesProperty); set => SetValue(YValuesProperty, value); }
        public IList<double> CategoryValues { get => (IList<double>)GetValue(CategoryValuesProperty); set => SetValue(CategoryValuesProperty, value); }
        public bool IsVisible { get => (bool)GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
        public double MarkerSize { get => (double)GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
        public MarkerShape MarkerShape { get => (MarkerShape)GetValue(MarkerShapeProperty); set => SetValue(MarkerShapeProperty, value); }
        public IList<Brush> BrushPerCategory { get => (IList<Brush>)GetValue(BrushPerCategoryProperty); set => SetValue(BrushPerCategoryProperty, value); }

        public event EventHandler DataChanged;
        public event EventHandler VisualChanged;

        // Internal buffer (List<double> 처럼 capacity 2x로 grow). live append 시 array 재할당 최소화.
        private double[] _xCache;
        private double[] _yCache;
        private int _count;
        private object _xSource; // 마지막으로 ToArray한 IList — 다른 instance가 들어오면 재로드
        private object _ySource;
        private bool? _isXSorted;
        private long _version;

        /// <summary>현재 유효 점 개수 (cache array.Length가 아니라 실제 사용 length).</summary>
        public int Count => _count;

        /// <summary>
        /// 데이터 변경(AppendPoint/TrimOldest/Replace/Notify) 마다 단조 증가하는 카운터.
        /// 차트의 downsample/render cache 키에 포함해서 "Count 가 동일해도 데이터가 바뀌었음" 을 감지할 때 사용.
        /// 예: ring-buffer 같은 슬라이딩 윈도우 — TrimOldest 후 Count 가 윈도우 크기로 고정되어 hash가 stale 해지는 케이스.
        /// </summary>
        public long Version => _version;

        /// <summary>차트가 그릴 때 호출. xs/ys array의 [0, count) 만 유효.</summary>
        public void GetArrays(out double[] xs, out double[] ys, out int count)
        {
            EnsureCacheLoaded();
            xs = _xCache ?? Array.Empty<double>();
            ys = _yCache ?? Array.Empty<double>();
            count = _count;
        }

        /// <summary>호환성 유지 — 기존 호출자용. count는 Math.Min(xs.Length, ys.Length).</summary>
        public void GetArrays(out double[] xs, out double[] ys)
        {
            GetArrays(out xs, out ys, out var c);
            // 기존 호출자들이 xs.Length를 N으로 쓰는 경우가 있어, capacity가 count보다 크면
            // trim된 array를 반환해 .Length가 N과 일치하도록 한다 (단 이건 array 새로 만듦 — append 경로 외에서만 호출됨).
            if (xs.Length != c || ys.Length != c)
            {
                var tx = new double[c];
                var ty = new double[c];
                Array.Copy(xs, 0, tx, 0, c);
                Array.Copy(ys, 0, ty, 0, c);
                xs = tx; ys = ty;
            }
        }

        public bool IsXSorted()
        {
            if (_isXSorted.HasValue) return _isXSorted.Value;
            EnsureCacheLoaded();
            if (_count < 2) { _isXSorted = true; return true; }
            for (var i = 1; i < _count; i++)
            {
                if (_xCache[i] < _xCache[i - 1]) { _isXSorted = false; return false; }
            }
            _isXSorted = true;
            return true;
        }

        /// <summary>
        /// O(1) amortized append — cache array 자체에 직접 추가하고 capacity가 부족하면 2x grow.
        /// 외부 IList(XValues/YValues)와는 동기화하지 않는다 — live tick 시나리오 전용.
        /// </summary>
        public void AppendPoint(double x, double y)
        {
            EnsureCacheLoaded();
            var cap = _xCache?.Length ?? 0;
            if (_count >= cap)
            {
                var newCap = cap == 0 ? 16 : cap * 2;
                Array.Resize(ref _xCache, newCap);
                Array.Resize(ref _yCache, newCap);
            }
            _xCache[_count] = x;
            _yCache[_count] = y;
            _count++;
            if (_isXSorted == true && _count >= 2 && x < _xCache[_count - 2]) _isXSorted = false;
            _version++;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 가장 오래된 점들을 drop해서 유효 점 개수를 최대 <paramref name="keep"/>개로 줄인다.
        /// live tick + 슬라이딩 윈도우 시나리오 — AppendPoint 후 호출해 메모리 무한 누적을 막는다.
        /// capacity(배열 길이)는 유지하고 _count 만 줄여 잦은 재할당을 피한다.
        /// keep &lt;= 0 이면 cache 비움. _count 가 이미 keep 이하면 no-op.
        /// </summary>
        public void TrimOldest(int keep)
        {
            EnsureCacheLoaded();
            if (keep <= 0)
            {
                _count = 0;
                _isXSorted = null;
                _version++;
                DataChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_count <= keep) return;
            var drop = _count - keep;
            // 뒤쪽 keep 개를 앞으로 당김 — Array.Copy 는 같은 array overlap 안전.
            Array.Copy(_xCache, drop, _xCache, 0, keep);
            Array.Copy(_yCache, drop, _yCache, 0, keep);
            _count = keep;
            // 뒤쪽 잔여값은 _count 밖이라 차트가 안 읽음 — 굳이 0 으로 클리어 안 함.
            _version++;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>외부 IList도 같이 변경 — Bar/Pie의 카테고리 값 mutation에서 사용.</summary>
        public void AppendData(double x, double y)
        {
            var xList = XValues;
            var yList = YValues;
            if (xList != null) xList.Add(x);
            if (yList != null) yList.Add(y);
            // cache 무효화 (외부 List 변경됨)
            InvalidateCache();
            _version++;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReplaceData(IList<double> xs, IList<double> ys)
        {
            XValues = xs;
            YValues = ys;
        }

        /// <summary>외부 IList 내용이 바뀌었을 때 호출 (Bar/Pie의 in-place mutation).</summary>
        public void NotifyDataChanged()
        {
            InvalidateCache();
            _version++;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InvalidateCache()
        {
            _xCache = null; _yCache = null;
            _xSource = null; _ySource = null;
            _count = 0;
            _isXSorted = null;
        }

        private void EnsureCacheLoaded()
        {
            var xv = XValues;
            var yv = YValues;
            if (_xCache != null && ReferenceEquals(xv, _xSource) && ReferenceEquals(yv, _ySource)) return;

            // 외부 IList → 내부 buffer 1회 복사 (이후 AppendPoint는 buffer 직접 grow).
            // X 또는 Y 한쪽만 있어도 동작 (Bar/Pie는 X=null이고 Y만 채움 — X는 인덱스로 fallback).
            var xn = xv?.Count ?? 0;
            var yn = yv?.Count ?? 0;
            var n = Math.Max(xn, yn);
            _xCache = n == 0 ? Array.Empty<double>() : new double[n];
            _yCache = n == 0 ? Array.Empty<double>() : new double[n];
            for (var i = 0; i < n; i++)
            {
                _xCache[i] = i < xn ? xv[i] : i;   // X 없으면 인덱스
                _yCache[i] = i < yn ? yv[i] : 0;   // Y 없으면 0
            }
            _count = n;
            _xSource = xv;
            _ySource = yv;
            _isXSorted = null;
        }

        private static void OnDataSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartSeries s)
            {
                s.InvalidateCache();
                s._version++;
                s.DataChanged?.Invoke(s, EventArgs.Empty);
            }
        }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChartSeries s) s.VisualChanged?.Invoke(s, EventArgs.Empty);
        }
    }
}
