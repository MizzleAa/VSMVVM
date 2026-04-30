using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VSMVVM.WPF.Design.Components.Charts.Core
{
    public class CandleSeries : DependencyObject
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(CandleSeries),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TimesProperty =
            DependencyProperty.Register(nameof(Times), typeof(IList<DateTime>), typeof(CandleSeries),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty OpensProperty =
            DependencyProperty.Register(nameof(Opens), typeof(IList<double>), typeof(CandleSeries),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty HighsProperty =
            DependencyProperty.Register(nameof(Highs), typeof(IList<double>), typeof(CandleSeries),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty LowsProperty =
            DependencyProperty.Register(nameof(Lows), typeof(IList<double>), typeof(CandleSeries),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty ClosesProperty =
            DependencyProperty.Register(nameof(Closes), typeof(IList<double>), typeof(CandleSeries),
                new PropertyMetadata(null, OnDataChanged));

        public static readonly DependencyProperty BullBrushProperty =
            DependencyProperty.Register(nameof(BullBrush), typeof(Brush), typeof(CandleSeries),
                new PropertyMetadata(null, OnVisualChanged));

        public static readonly DependencyProperty BearBrushProperty =
            DependencyProperty.Register(nameof(BearBrush), typeof(Brush), typeof(CandleSeries),
                new PropertyMetadata(null, OnVisualChanged));

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public IList<DateTime> Times { get => (IList<DateTime>)GetValue(TimesProperty); set => SetValue(TimesProperty, value); }
        public IList<double> Opens { get => (IList<double>)GetValue(OpensProperty); set => SetValue(OpensProperty, value); }
        public IList<double> Highs { get => (IList<double>)GetValue(HighsProperty); set => SetValue(HighsProperty, value); }
        public IList<double> Lows { get => (IList<double>)GetValue(LowsProperty); set => SetValue(LowsProperty, value); }
        public IList<double> Closes { get => (IList<double>)GetValue(ClosesProperty); set => SetValue(ClosesProperty, value); }
        public Brush BullBrush { get => (Brush)GetValue(BullBrushProperty); set => SetValue(BullBrushProperty, value); }
        public Brush BearBrush { get => (Brush)GetValue(BearBrushProperty); set => SetValue(BearBrushProperty, value); }

        public event EventHandler DataChanged;
        public event EventHandler VisualChanged;

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CandleSeries s) s.VisualChanged?.Invoke(s, EventArgs.Empty);
        }

        private double[] _oArr, _hArr, _lArr, _cArr;
        private object _oSrc, _hSrc, _lSrc, _cSrc;

        public int Count
        {
            get
            {
                EnsureArrays();
                return Math.Min(Math.Min(_oArr?.Length ?? 0, _hArr?.Length ?? 0),
                                Math.Min(_lArr?.Length ?? 0, _cArr?.Length ?? 0));
            }
        }

        public void GetArrays(out double[] o, out double[] h, out double[] l, out double[] c)
        {
            EnsureArrays();
            o = _oArr; h = _hArr; l = _lArr; c = _cArr;
        }

        private void EnsureArrays()
        {
            if (!ReferenceEquals(Opens, _oSrc)) { _oArr = ToArr(Opens); _oSrc = Opens; }
            if (!ReferenceEquals(Highs, _hSrc)) { _hArr = ToArr(Highs); _hSrc = Highs; }
            if (!ReferenceEquals(Lows, _lSrc)) { _lArr = ToArr(Lows); _lSrc = Lows; }
            if (!ReferenceEquals(Closes, _cSrc)) { _cArr = ToArr(Closes); _cSrc = Closes; }
        }

        private static double[] ToArr(IList<double> list)
        {
            if (list == null) return Array.Empty<double>();
            if (list is double[] a) return a;
            var r = new double[list.Count];
            for (var i = 0; i < r.Length; i++) r[i] = list[i];
            return r;
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CandleSeries s)
            {
                s._oArr = s._hArr = s._lArr = s._cArr = null;
                s._oSrc = s._hSrc = s._lSrc = s._cSrc = null;
                s.DataChanged?.Invoke(s, EventArgs.Empty);
            }
        }

        public void NotifyDataChanged()
        {
            _oArr = _hArr = _lArr = _cArr = null;
            _oSrc = _hSrc = _lSrc = _cSrc = null;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
