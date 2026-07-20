using System;
using System.Collections.Generic;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>차트 시리즈의 한 데이터 포인트.</summary>
    public readonly struct ChartPoint
    {
        public int X { get; }
        public double Y { get; }
        public ChartPoint(int x, double y) { X = x; Y = y; }
    }

    /// <summary>매트릭스 스냅샷 (혼동행렬 등) — Values + 행/열 라벨.</summary>
    public sealed class ChartMatrix
    {
        public double[,] Values { get; }
        public IReadOnlyList<string> RowLabels { get; }
        public IReadOnlyList<string> ColumnLabels { get; }
        public string RowAxisTitle { get; }
        public string ColumnAxisTitle { get; }

        public ChartMatrix(double[,] values, IReadOnlyList<string> rowLabels, IReadOnlyList<string> columnLabels,
                           string rowAxisTitle, string columnAxisTitle)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
            RowLabels = rowLabels ?? Array.Empty<string>();
            ColumnLabels = columnLabels ?? Array.Empty<string>();
            RowAxisTitle = rowAxisTitle ?? "Actual";
            ColumnAxisTitle = columnAxisTitle ?? "Predicted";
        }
    }

    /// <summary>
    /// ChartViewNode 의 실시간 스트리밍 데이터를 ViewName 별로 누적 보관.
    /// LineChart 용: ViewName → SeriesName → List&lt;ChartPoint&gt;.
    /// ConfusionMatrix 용: ViewName → ChartMatrix (마지막 push 만 유지).
    /// Run 시작 시 자동 Clear (SampleWorkspaceViewModel.ConfigureContext 가 호출).
    /// </summary>
    public sealed class ChartSnapshotStore
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Dictionary<string, List<ChartPoint>>> _series = new();
        private readonly Dictionary<string, ChartMatrix> _matrices = new();

        public void PushPoint(string viewName, string seriesName, int x, double y)
        {
            if (string.IsNullOrEmpty(viewName) || string.IsNullOrEmpty(seriesName)) return;
            lock (_sync)
            {
                if (!_series.TryGetValue(viewName, out var bySeries))
                {
                    bySeries = new Dictionary<string, List<ChartPoint>>();
                    _series[viewName] = bySeries;
                }
                if (!bySeries.TryGetValue(seriesName, out var list))
                {
                    list = new List<ChartPoint>();
                    bySeries[seriesName] = list;
                }
                list.Add(new ChartPoint(x, y));
            }
            Changed?.Invoke(this, viewName);
        }

        public void PushMatrix(string viewName, ChartMatrix matrix)
        {
            if (string.IsNullOrEmpty(viewName) || matrix == null) return;
            lock (_sync)
            {
                _matrices[viewName] = matrix;
            }
            Changed?.Invoke(this, viewName);
        }

        /// <summary>ViewName 의 모든 시리즈 스냅샷. 시리즈명 → 포인트 배열.</summary>
        public IReadOnlyDictionary<string, ChartPoint[]> GetSeries(string viewName)
        {
            lock (_sync)
            {
                if (!_series.TryGetValue(viewName, out var bySeries)) return null;
                var res = new Dictionary<string, ChartPoint[]>(bySeries.Count);
                foreach (var kv in bySeries) res[kv.Key] = kv.Value.ToArray();
                return res;
            }
        }

        public ChartMatrix GetMatrix(string viewName)
        {
            lock (_sync)
            {
                return _matrices.TryGetValue(viewName, out var m) ? m : null;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _series.Clear();
                _matrices.Clear();
            }
        }

        /// <summary>데이터 추가/클리어 시 발화 — 인수 = 변경된 ViewName. UI 가 갱신 트리거로 사용.</summary>
        public event EventHandler<string> Changed;
    }

    /// <summary>
    /// 사용자 스니펫이 학습 중 호출하는 static 헬퍼.
    /// SampleWorkspaceViewModel 이 Run 시작 시 Attach(store) 호출, 종료 시 Detach.
    /// Attach 안 되어 있으면 no-op — 테스트/단독 실행에서 안전.
    /// </summary>
    public static class ChartLog
    {
        private static ChartSnapshotStore _current;
        private static readonly object _lock = new object();

        public static void Attach(ChartSnapshotStore store)
        {
            lock (_lock) { _current = store; }
        }

        public static void Detach()
        {
            lock (_lock) { _current = null; }
        }

        /// <summary>사용자 스니펫이 매 에폭마다 호출 — (epoch, value) 를 (viewName, seriesName) 시리즈에 append.</summary>
        public static void Push(string viewName, int epoch, string seriesName, double value)
        {
            ChartSnapshotStore store;
            lock (_lock) { store = _current; }
            store?.PushPoint(viewName, seriesName, epoch, value);
        }

        /// <summary>매트릭스 push — 혼동행렬 등. int[][] 편의 오버로드.</summary>
        public static void PushMatrix(string viewName, int[][] matrix, string[] rowLabels, string[] columnLabels,
                                      string rowAxisTitle = "Actual", string columnAxisTitle = "Predicted")
        {
            if (matrix == null || matrix.Length == 0) return;
            int rows = matrix.Length;
            int cols = matrix[0]?.Length ?? 0;
            var values = new double[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols && j < matrix[i].Length; j++)
                    values[i, j] = matrix[i][j];
            PushMatrix(viewName, values, rowLabels, columnLabels, rowAxisTitle, columnAxisTitle);
        }

        public static void PushMatrix(string viewName, double[,] matrix, string[] rowLabels, string[] columnLabels,
                                      string rowAxisTitle = "Actual", string columnAxisTitle = "Predicted")
        {
            ChartSnapshotStore store;
            lock (_lock) { store = _current; }
            store?.PushMatrix(viewName, new ChartMatrix(matrix, rowLabels, columnLabels, rowAxisTitle, columnAxisTitle));
        }
    }
}
