using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.WPF.Design.Components.Charts.Core;
using VSMVVM.WPF.Sample.Scheduler;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// ChartViewNode 창의 ViewModel — ChartSnapshotStore 를 구독하고 ViewName + Kind 에 맞게 LineChart 또는
    /// ConfusionMatrix 데이터를 갱신. store.Changed 는 임의 스레드에서 올 수 있어 Dispatcher 로 마샬링.
    /// </summary>
    public partial class ChartViewWindowViewModel : ViewModelBase
    {
        private readonly Dispatcher _dispatcher;
        private ChartSnapshotStore _store;
        private readonly Dictionary<string, ChartSeries> _seriesByName = new(StringComparer.Ordinal);

        [Property] private string _viewName = "chart";
        [Property] private ChartKind _kind = ChartKind.Line;
        [Property] private string _status = "(no data — run the graph)";

        /// <summary>LineChart 바인딩 대상. Kind=Line 일 때 사용.</summary>
        public ObservableCollection<ChartSeries> LineSeries { get; } = new();

        /// <summary>ConfusionMatrix 바인딩 대상 (double[,]). Kind=ConfusionMatrix 일 때 사용.</summary>
        [Property] private double[,] _matrix;
        [Property] private IList<string> _rowLabels;
        [Property] private IList<string> _columnLabels;
        [Property] private string _rowAxisTitle = "Actual";
        [Property] private string _columnAxisTitle = "Predicted";

        public bool IsLine => Kind == ChartKind.Line;
        public bool IsMatrix => Kind == ChartKind.ConfusionMatrix;
        public bool IsHeatmap => Kind == ChartKind.Heatmap;

        public event EventHandler RequestClose;

        public ChartViewWindowViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>WindowService 가 ShowWindow 시 자동 주입 — (ViewName, Kind, Store).</summary>
        public (string ViewName, ChartKind Kind, ChartSnapshotStore Store) DialogParameter
        {
            get => (ViewName, Kind, _store);
            set
            {
                ViewName = string.IsNullOrEmpty(value.ViewName) ? "chart" : value.ViewName;
                Kind = value.Kind;
                if (_store != null) _store.Changed -= OnStoreChanged;
                _store = value.Store;
                if (_store != null) _store.Changed += OnStoreChanged;
                OnPropertyChanged(nameof(IsLine));
                OnPropertyChanged(nameof(IsMatrix));
                OnPropertyChanged(nameof(IsHeatmap));
                RefreshFromStore();
            }
        }

        public object DialogResultData => null;

        partial void OnKindChanged(ChartKind value)
        {
            OnPropertyChanged(nameof(IsLine));
            OnPropertyChanged(nameof(IsMatrix));
            OnPropertyChanged(nameof(IsHeatmap));
        }

        private void OnStoreChanged(object sender, string changedView)
        {
            if (changedView != ViewName) return;
            if (_dispatcher.CheckAccess()) RefreshFromStore();
            else _dispatcher.BeginInvoke(new Action(RefreshFromStore));
        }

        private void RefreshFromStore()
        {
            if (_store == null) return;
            if (Kind == ChartKind.Line) RefreshLine();
            else RefreshMatrix(); // ConfusionMatrix + Heatmap 모두 double[,] Matrix 바인딩 공유.
        }

        private void RefreshLine()
        {
            var series = _store.GetSeries(ViewName);
            if (series == null || series.Count == 0)
            {
                LineSeries.Clear();
                _seriesByName.Clear();
                Status = $"(no data for '{ViewName}' — run the graph)";
                return;
            }

            // 기존 시리즈 재사용 (chart 애니메이션/축 스케일 안정성) — 신규만 add.
            foreach (var kv in series)
            {
                if (!_seriesByName.TryGetValue(kv.Key, out var s))
                {
                    s = new ChartSeries
                    {
                        Title = kv.Key,
                        XValues = new List<double>(),
                        YValues = new List<double>(),
                        StrokeThickness = 1.5,
                    };
                    _seriesByName[kv.Key] = s;
                    LineSeries.Add(s);
                }

                // YValues/XValues 를 통째로 교체 — 매 에폭마다 리스트 재할당은 부담이 없음 (Iris 규모).
                var xs = new List<double>(kv.Value.Length);
                var ys = new List<double>(kv.Value.Length);
                foreach (var p in kv.Value) { xs.Add(p.X); ys.Add(p.Y); }
                s.XValues = xs;
                s.YValues = ys;
                s.NotifyDataChanged();
            }

            int totalPoints = 0;
            foreach (var kv in series) totalPoints += kv.Value.Length;
            Status = $"'{ViewName}' — {series.Count} series, {totalPoints} points";
        }

        private void RefreshMatrix()
        {
            var m = _store.GetMatrix(ViewName);
            if (m == null)
            {
                Matrix = null;
                Status = $"(no matrix for '{ViewName}' — run the graph)";
                return;
            }
            Matrix = m.Values;
            RowLabels = new List<string>(m.RowLabels);
            ColumnLabels = new List<string>(m.ColumnLabels);
            RowAxisTitle = m.RowAxisTitle;
            ColumnAxisTitle = m.ColumnAxisTitle;
            int r = m.Values.GetLength(0), c = m.Values.GetLength(1);
            Status = $"'{ViewName}' — {r}×{c} matrix";
        }
    }
}
