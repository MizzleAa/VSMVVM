using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using FluentAssertions;
using VSMVVM.WPF.Design.Components.Charts;
using VSMVVM.WPF.Design.Components.Charts.Core;
using Xunit;

namespace VSMVVM.WPF.Tests
{
    // Histogram._seriesCounts (private int[][]) 캐시 무효화 회귀 테스트.
    // AnomalyDetection labeling 화면에서 두 분포가 X 축에 분리된 데이터일 때,
    // ChartSeries.IsVisible false→true 토글 후 막대가 다시 그려지지 않던 버그를 잡는다.
    // 패치 전: ChartBase 의 OnSeriesItemVisualChanged 가 Histogram._binningDirty 를 set 하지 못해
    //          ComputeSeriesArrayLengthsHash 가 (Count, IsVisible 비트) 만으로 캐시 hit → stale.
    // 패치 후: ChartBase 의 OnSeriesInvalidated hook 을 Histogram 이 override 해 _binningDirty=true.
    public class HistogramVisibilityToggleTests
    {
        // Anomaly Detection 처럼 두 분포가 분리된 합성 데이터.
        // Normal: 0.0~0.4 / Abnormal: 0.5~1.0 — 한쪽만 visible 일 때 Range 가 명확히 좁아짐.
        private static (List<double> normal, List<double> abnormal) BuildSeparatedScores()
        {
            var normal = new List<double>();
            var abnormal = new List<double>();
            for (var i = 0; i < 100; i++) normal.Add(0.05 + 0.003 * i);   // ~0.05..0.35
            for (var i = 0; i < 100; i++) abnormal.Add(0.55 + 0.004 * i); // ~0.55..0.95
            return (normal, abnormal);
        }

        private static Histogram BuildHistogram(IList<double> normal, IList<double> abnormal,
                                                out ChartSeries normalSeries, out ChartSeries abnormalSeries)
        {
            normalSeries = new ChartSeries { Title = "Normal", YValues = normal };
            abnormalSeries = new ChartSeries { Title = "Abnormal", YValues = abnormal };
            var h = new Histogram
            {
                BinCount = 30,
                Series = new ObservableCollection<ChartSeries> { normalSeries, abnormalSeries },
            };
            return h;
        }

        // _seriesCounts 의 한 시리즈 총합 — 그려질 막대 개수의 직접 척도.
        // EnsureBinned 가 private 이라 ComputeDataRange (public 신호 경로) 를 호출해 캐시를 갱신시킨 뒤
        // 리플렉션으로 _seriesCounts 를 읽는다. EnsureBinned 자체도 ComputeDataRange 가 첫 줄에서 호출함.
        private static int GetSeriesCountSum(Histogram h, int seriesIdx)
        {
            // ComputeDataRange 가 protected override 라 리플렉션 호출.
            var method = typeof(Histogram).GetMethod(
                "ComputeDataRange",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull("ComputeDataRange 가 Histogram 에 정의돼 있어야 함");
            var args = new object[] { 0.0, 0.0, 0.0, 0.0 };
            method!.Invoke(h, args);

            var field = typeof(Histogram).GetField(
                "_seriesCounts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull("_seriesCounts private 필드가 존재해야 함");
            var arr = (int[][])field!.GetValue(h);
            if (arr == null || seriesIdx >= arr.Length || arr[seriesIdx] == null) return 0;

            var total = 0;
            foreach (var c in arr[seriesIdx]) total += c;
            return total;
        }

        [StaFact(DisplayName = "ChartSeries.IsVisible false→true 토글 후 binning 캐시가 무효화되어 막대가 복원된다")]
        public void IsVisible_FalseToTrue_RestoresBinnedCounts()
        {
            var (normal, abnormal) = BuildSeparatedScores();
            var h = BuildHistogram(normal, abnormal, out var normalSeries, out _);

            // 1) 초기 — 두 시리즈 모두 visible. Normal 시리즈 카운트 총합이 데이터 개수와 일치.
            var initialNormalSum = GetSeriesCountSum(h, 0);
            initialNormalSum.Should().Be(normal.Count, "초기엔 Normal 의 모든 점이 bin 에 들어가야 함");

            // 2) "일치" 해제 — Range 가 Abnormal(0.55~0.95) 만으로 좁아진다.
            //    Normal 의 점들은 이 좁은 Range 밖이라 bin 0 이 된다 (stale 의 원인).
            normalSeries.IsVisible = false;
            var hiddenNormalSum = GetSeriesCountSum(h, 0);
            hiddenNormalSum.Should().Be(0, "Normal 가 숨김 + Range 가 Abnormal 만이라 Normal 점은 모두 bin 밖");

            // 3) "일치" 다시 체크 — Range 가 두 시리즈 합집합으로 다시 넓어지고,
            //    Normal 의 모든 점이 다시 bin 에 들어와야 한다.
            //    [패치 전] hash 가 1단계와 동일 → early return → hiddenNormalSum(0) 그대로 → FAIL
            //    [패치 후] OnSeriesInvalidated hook 이 _binningDirty=true → 재 binning → PASS
            normalSeries.IsVisible = true;
            var restoredNormalSum = GetSeriesCountSum(h, 0);
            restoredNormalSum.Should().Be(initialNormalSum,
                "IsVisible 복원 시 binning 캐시가 무효화되어 Normal 막대가 다시 그려져야 함");
        }

        [StaFact(DisplayName = "Series 컬렉션 교체 시 binning 캐시가 새 데이터를 반영한다")]
        public void SeriesCollectionSwap_InvalidatesBinning()
        {
            var (normal, abnormal) = BuildSeparatedScores();
            var h = BuildHistogram(normal, abnormal, out _, out _);

            // 첫 binning
            GetSeriesCountSum(h, 0).Should().Be(normal.Count);

            // 새로운 시리즈 인스턴스 + 다른 점 개수로 컬렉션 통째 교체 (그룹 reload 시나리오)
            var newNormal = new List<double> { 0.1, 0.2, 0.3 };
            var newAbnormal = new List<double> { 0.8, 0.9 };
            h.Series = new ObservableCollection<ChartSeries>
            {
                new ChartSeries { Title = "Normal", YValues = newNormal },
                new ChartSeries { Title = "Abnormal", YValues = newAbnormal },
            };

            GetSeriesCountSum(h, 0).Should().Be(newNormal.Count,
                "Series 교체 후엔 새 데이터의 점 개수가 _seriesCounts 에 반영돼야 함");
        }

        [StaFact(DisplayName = "ChartSeries.YValues 재할당 시 binning 캐시가 새 데이터를 반영한다")]
        public void YValuesReassignment_InvalidatesBinning()
        {
            var (normal, abnormal) = BuildSeparatedScores();
            var h = BuildHistogram(normal, abnormal, out var normalSeries, out _);

            GetSeriesCountSum(h, 0).Should().Be(normal.Count);

            // 시리즈 인스턴스 유지, YValues 만 새 List 로 재할당 — in-place 갱신 시나리오
            var newNormal = new List<double> { 0.1, 0.15, 0.2, 0.25 };
            normalSeries.YValues = newNormal;

            GetSeriesCountSum(h, 0).Should().Be(newNormal.Count,
                "YValues 재할당 후엔 새 점 개수가 _seriesCounts 에 반영돼야 함");
        }
    }
}
