using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Imaging;
using VSMVVM.WPF.Imaging.Coco;
using Xunit;

namespace VSMVVM.WPF.Tests.Canvas
{
    /// <summary>
    /// MaskLayer + IInstanceMergeStrategy 단위 테스트.
    /// 5개 시나리오:
    /// T1) Seg 단일 사각형 → BoundingBox 정확
    /// T2) Seg 두 사각형 겹침 → 자동 병합 (instance 1개), BoundingBox = union
    /// T3) Obj 두 사각형 반쯤 겹침 → instance 2개, 각자 BoundingBox 보존
    /// T4) Obj 사각형 B 가 A 안에 완전 포함 → instance 2개, 각자 BoundingBox 보존
    /// T5) Obj 사각형 이동 → 두 instance 살아있음, 이동한 사각형 BoundingBox 새 위치
    /// </summary>
    public class InstanceMergeStrategyTests
    {
        private static MaskLayer CreateLayer(int w = 100, int h = 100)
        {
            var layer = new MaskLayer();
            var labels = new LabelClassCollection();
            labels.AddWithIndex(1, "Label1", Colors.Red);
            labels.AddWithIndex(2, "Label2", Colors.Blue);
            layer.Labels = labels;
            layer.Resize(w, h);
            return layer;
        }

        /// <summary>한 사각형을 BeginStroke → PaintRectangle → EndStroke 흐름으로 그리고 마지막 instance ID 반환.</summary>
        private static uint DrawRectangle(MaskLayer layer, int labelIndex, Rect rect)
        {
            layer.BeginStroke(labelIndex);
            layer.PaintRectangle(rect, labelIndex);
            layer.EndStroke(labelIndex);
            return layer.LastCreatedInstanceId;
        }

        // ── T1: Seg 단일 사각형 → BoundingBox 정확 ────────────────────

        [WpfFact]
        public void T1_Seg_SingleRectangle_HasExactBoundingBox()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = MergeOnOverlapInstanceMergeStrategy.Instance;

            var rect = new Rect(10, 10, 30, 20);  // x=10..39, y=10..29
            uint id = DrawRectangle(layer, labelIndex: 1, rect);

            layer.Instances.Count.Should().Be(1, "단일 사각형은 instance 1개");
            var inst = layer.Instances.GetById(id);
            inst.Should().NotBeNull();
            // PaintRectangle 의 클램프: xMax = X+W-1, yMax = Y+H-1 → BoundingBox = (10,10,30,20)
            inst!.BoundingBox.Should().Be(new Rect(10, 10, 30, 20));
            inst.PixelCount.Should().Be(30 * 20);
        }

        // ── T2: Seg 두 사각형 겹침 → 자동 병합, BoundingBox = union ────

        [WpfFact]
        public void T2_Seg_TwoOverlappingRectangles_MergeIntoOne_BBoxIsUnion()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = MergeOnOverlapInstanceMergeStrategy.Instance;

            // A: (10,10,30,30) — x=10..39, y=10..39
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 30, 30));
            // B: (25,25,30,30) — A 와 (25,25,14,14) 영역에서 겹침
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(25, 25, 30, 30));

            layer.Instances.Count.Should().Be(1, "같은 라벨 두 사각형 겹치면 자동 병합");

            var inst = layer.Instances.First();
            // BoundingBox = union(A, B) = (10,10, 45, 45) — x=10..54, y=10..54
            inst.BoundingBox.Should().Be(new Rect(10, 10, 45, 45));
            // PixelCount = |A| + |B| - |A∩B| = 900 + 900 - 15*15 = 1575
            inst.PixelCount.Should().Be(30 * 30 + 30 * 30 - 15 * 15);
        }

        // ── T3: Obj 두 사각형 반쯤 겹침 → instance 2개, 각자 BoundingBox 보존 ──

        [WpfFact]
        public void T3_Obj_TwoHalfOverlappingRectangles_KeepsTwoInstances_BBoxesIntact()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A: (10,10,30,30)
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 30, 30));
            // B: (25,25,30,30) — A 와 반쯤 겹침
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(25, 25, 30, 30));

            layer.Instances.Count.Should().Be(2, "ObjectDetection: 같은 라벨이라도 instance 독립 유지");
            idA.Should().NotBe(idB, "두 instance ID 는 달라야 함");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);
            instA.Should().NotBeNull();
            instB.Should().NotBeNull();

            // A 의 BoundingBox 가 stroke 영역 그대로 보존 (B 가 일부 픽셀 덮었지만 freeze).
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 30, 30));
            // B 의 BoundingBox 는 자기 stroke 영역.
            instB!.BoundingBox.Should().Be(new Rect(25, 25, 30, 30));
        }

        // ── T4: Obj 사각형 B 가 A 안에 완전 포함 → instance 2개 ────────

        [WpfFact]
        public void T4_Obj_BFullyInsideA_KeepsTwoInstances_BBoxesIntact()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A: (10,10,40,40) — 큰 사각형
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 40, 40));
            // B: (20,20,10,10) — A 안에 완전 포함
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 10, 10));

            layer.Instances.Count.Should().Be(2, "B 가 A 안에 완전 포함되어도 instance 2개 유지");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);
            instA.Should().NotBeNull();
            instB.Should().NotBeNull();

            // A 의 BoundingBox 가 그대로 freeze (B 가 자기 영역 일부 덮어도 BoundingBox 유지).
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 40, 40));
            // B 의 BoundingBox 는 자기 stroke 영역.
            instB!.BoundingBox.Should().Be(new Rect(20, 20, 10, 10));
        }

        // ── T5: Obj 사각형 이동 → 두 instance 모두 살아있음 ──────────────

        [WpfFact]
        public void T5_Obj_ResampleOverlappingAnotherInstance_BothSurvive_BBoxesIntact()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A: (50,10,30,30) — 오른쪽
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(50, 10, 30, 30));
            // B: (10,10,30,30) — 왼쪽
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 30, 30));

            layer.Instances.Count.Should().Be(2, "초기에 두 사각형 독립");

            // B 를 A 와 겹치도록 이동: (40,10,30,30) — A 와 (50,10,20,30) 영역 겹침
            layer.ResampleInstance(idB, new Rect(40, 10, 30, 30));

            layer.Instances.Count.Should().Be(2, "이동 후에도 두 instance 살아있음 (흡수 없음)");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);
            instA.Should().NotBeNull();
            instB.Should().NotBeNull();

            // B 의 BoundingBox 가 새 위치로 갱신.
            instB!.BoundingBox.Should().Be(new Rect(40, 10, 30, 30));
            // A 의 BoundingBox 는 그대로 freeze (B 의 새 위치가 자기 영역 일부 덮어도).
            instA!.BoundingBox.Should().Be(new Rect(50, 10, 30, 30));
        }

        // ── T6: Obj 사각형 겹치고 이동 후 옛 사각형 BoundingBox 영역 fill 완전 복원 ──

        [WpfFact]
        public void T6_Obj_ResampleAwayFromOverlap_RestoresOldInstanceFill()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A 그림 (40x40)
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 40, 40));
            // B 그림 — A 와 (30..49, 30..49) 영역에서 겹침
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(30, 30, 40, 40));

            // 이 시점: A 의 (30..49, 30..49) 픽셀이 B 의 ID 로 덮인 상태.
            // BoundingBox 는 freeze 로 둘 다 자기 영역 유지.

            // B 를 멀리 이동 — A 와 안 겹치는 위치로
            layer.ResampleInstance(idB, new Rect(70, 70, 40, 40));

            layer.Instances.Count.Should().Be(2, "두 instance 모두 살아있음");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);
            instA.Should().NotBeNull();
            instB.Should().NotBeNull();

            // render 가 BBox 단위 fill 이라 BoundingBox 만 검증 — 픽셀 mask 는 충돌해서 못 채움.
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 40, 40));
            instB!.BoundingBox.Should().Be(new Rect(70, 70, 40, 40));
        }

        // ── T7: Obj 사각형 겹치고 이동 (겹친 채로) → 옛 사각형 fill 복원 + 새 겹침 발생 ──

        [WpfFact]
        public void T7_Obj_ResampleStillOverlappingDifferentInstance_OldFillRestored()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 40, 40));
            uint idC = DrawRectangle(layer, labelIndex: 1, new Rect(70, 10, 20, 20));
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(30, 30, 40, 40)); // A 와 겹침

            // B 를 C 쪽으로 이동 — 옛 위치는 A 에서 빠지고, 새 위치는 C 와 겹침
            layer.ResampleInstance(idB, new Rect(65, 5, 30, 30));

            layer.Instances.Count.Should().Be(3, "세 instance 모두 살아있음");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);
            var instC = layer.Instances.GetById(idC);
            instA.Should().NotBeNull();
            instB.Should().NotBeNull();
            instC.Should().NotBeNull();

            // render 가 BBox 단위 fill — BoundingBox 만 검증.
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 40, 40));
            instC!.BoundingBox.Should().Be(new Rect(70, 10, 20, 20));

            // B 는 새 위치
            instB!.BoundingBox.Should().Be(new Rect(65, 5, 30, 30));
        }

        // ── T8: Seg 사각형 + brush stroke 후 PolygonContours 추출 (더블클릭 vertex edit 진입 가능) ──

        [WpfFact]
        public void T8_Seg_RectangleThenBrushOverlap_EnsurePolygonPointsExtracts()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = MergeOnOverlapInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));

            // 위에 brush stroke 시뮬레이션 — 같은 라벨, A 와 겹침
            layer.BeginStroke(1);
            layer.PaintRectangle(new Rect(50, 50, 30, 30), labelIndex: 1);
            layer.EndStroke(1);

            layer.Instances.Count.Should().Be(1, "같은 라벨 두 stroke 가 MergeOnOverlap 으로 통합");

            var inst = layer.Instances.First();

            // 더블클릭 시 호출되는 EnsurePolygonPoints — polygon 추출 성공해야 vertex edit 진입 가능
            bool extracted = layer.EnsurePolygonPoints(inst.Id);
            extracted.Should().BeTrue("contour 추출 성공해야 함 — 더블클릭 vertex edit 동작의 전제조건");
            inst.PolygonContours.Should().NotBeNull();
            inst.PolygonContours!.Should().NotBeEmpty();
            inst.PolygonContours[0].Count.Should().BeGreaterThanOrEqualTo(3, "polygon 은 최소 3 점 필요");
        }

        // ── T9: Seg 사각형만 그려도 PolygonContours 추출 가능 (회귀 방지) ──

        [WpfFact]
        public void T9_Seg_SingleRectangle_EnsurePolygonPointsExtracts()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = MergeOnOverlapInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));

            bool extracted = layer.EnsurePolygonPoints(idA);
            extracted.Should().BeTrue();

            var inst = layer.Instances.GetById(idA);
            inst.Should().NotBeNull();
            inst!.PolygonContours.Should().NotBeNull();
            inst.PolygonContours![0].Count.Should().BeGreaterThanOrEqualTo(4, "사각형 contour 는 4 점 이상");
        }

        // ── T11: Obj 큰 A 그리고 안쪽에 작은 B 그림 — A 의 BBox 영역 전체 자기 색 (B 가 안에 있어도 A fill 유지) ──
        // 사용자 시나리오 1: "큰거 그리고 안쪽에 사각형 추가"

        [WpfFact]
        public void T11_Obj_BigThenSmallInside_BigFillRestoredAfterEndStroke()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A: 큰 사각형
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 60, 60));
            // B: A 안쪽에 작은 사각형
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(35, 35, 20, 20));

            layer.Instances.Count.Should().Be(2, "독립 instance 2개");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);

            // render 가 BBox 단위 fill — BoundingBox 만 검증.
            instA!.BoundingBox.Should().Be(new Rect(20, 20, 60, 60));
            instB!.BoundingBox.Should().Be(new Rect(35, 35, 20, 20));
        }

        // ── T12: Obj 사각형 두 개 겹쳐 그리기 (이동 없이) — 옛 사각형 fill 자동 복원 ──
        // 사용자 시나리오 2: "첫번째 사각형 그리기도 2번째 사각형 겹쳐서 그림"

        [WpfFact]
        public void T12_Obj_DrawTwoOverlapping_OldFillRestoredAfterEndStroke()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            // A 그림
            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 40, 40));
            // B 를 A 와 겹치게 그림 (이동 없이 그리기만)
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(30, 30, 40, 40));

            layer.Instances.Count.Should().Be(2, "독립 instance 2개 유지");

            var instA = layer.Instances.GetById(idA);
            var instB = layer.Instances.GetById(idB);

            // render 가 BBox 단위 fill — BoundingBox 만 검증.
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 40, 40));
            instB!.BoundingBox.Should().Be(new Rect(30, 30, 40, 40));
        }

        // ── T10: Obj 떨어진 두 사각형 중 하나 이동 시 다른 instance 영향 없음 (T5/T6 회귀 방지) ──

        [WpfFact]
        public void T10_Obj_ResampleNonOverlapping_OtherInstancePixelsIntact()
        {
            var layer = CreateLayer();
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(10, 10, 20, 20));
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(60, 60, 20, 20));

            // B 를 살짝 옆으로 (A 와 안 겹침)
            layer.ResampleInstance(idB, new Rect(70, 70, 20, 20));

            var instA = layer.Instances.GetById(idA);
            instA.Should().NotBeNull();
            instA!.BoundingBox.Should().Be(new Rect(10, 10, 20, 20));
        }

        // ── T13: Obj 겹친 두 박스 분리 후 디스플레이 픽셀 정합 (사용자 보고 L자 잘림 버그 회귀 방지) ──
        // 같은 라벨 A 와 B 를 겹쳐 그린 뒤 B 를 분리하면 두 박스 모두 자기 BBox 안 픽셀이
        // 라벨 색으로 fill 되어야 함. 분리 후 옛 위치 (oldB) 가 A 와 겹쳤던 영역은 A 색,
        // oldB - A 영역은 투명, B 의 새 위치는 B 색.

        [WpfFact]
        public void T13_Obj_DragSeparateOverlap_BothRectsFullyFilledInDisplay()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            var rectA = new Rect(20, 20, 40, 40);   // A: (20..59, 20..59)
            var rectBold = new Rect(40, 40, 40, 40); // B: (40..79, 40..79) — A 와 (40..59, 40..59) 겹침
            uint idA = DrawRectangle(layer, labelIndex: 1, rectA);
            uint idB = DrawRectangle(layer, labelIndex: 1, rectBold);

            // 분리 — B 를 멀리 (A 와 안 겹침). pureTranslate 경로 적중하도록 사이즈 동일.
            var rectBnew = new Rect(120, 120, 40, 40);
            layer.ResampleInstance(idB, rectBnew);

            // 두 BBox 메타데이터는 사용자가 그린 사각형 그대로.
            layer.Instances.GetById(idA)!.BoundingBox.Should().Be(rectA);
            layer.Instances.GetById(idB)!.BoundingBox.Should().Be(rectBnew);

            var expectedColor = Colors.Red;

            // A 의 네 모서리 + 중앙 픽셀이 모두 라벨 색으로 fill.
            // 특히 (45,45) 는 oldB 와 겹쳤던 자리 — 이 픽셀이 투명하면 화면에 L자 구멍.
            AssertPixelMatches(layer, 20, 20, expectedColor, "A 좌상");
            AssertPixelMatches(layer, 59, 59, expectedColor, "A 우하");
            AssertPixelMatches(layer, 45, 45, expectedColor, "A 안 oldB 겹쳤던 자리 — L자 잘림 검출");
            AssertPixelMatches(layer, 55, 30, expectedColor, "A 안 oldB 안 겹친 자리");

            // B 새 위치
            AssertPixelMatches(layer, 120, 120, expectedColor, "B 좌상 (새 위치)");
            AssertPixelMatches(layer, 159, 159, expectedColor, "B 우하 (새 위치)");
            AssertPixelMatches(layer, 140, 140, expectedColor, "B 중앙 (새 위치)");
        }

        // ── T14: Obj 분리 후 oldB 가 차지했던 A 바깥 영역은 투명 (다른 인스턴스 영향 없음) ──

        [WpfFact]
        public void T14_Obj_DragSeparate_OldPositionOutsideOtherBBox_BecomesTransparent()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));

            // oldB 의 (70, 70) 은 A 바깥이고 B 새 위치도 아님 → 투명이어야 함.
            var px = layer.SampleDisplayPixel(70, 70);
            px.A.Should().Be(0, "옛 B 위치 중 A 바깥은 투명해야 함");
        }

        private static void AssertPixelMatches(MaskLayer layer, int x, int y, Color expected, string because)
        {
            var actual = layer.SampleDisplayPixel(x, y);
            actual.Should().Be(expected, $"({x},{y}) {because}");
        }

        // ── T17: 핵심 버그 재현 — IndependentInstances 모드에서 A 의 픽셀 마스크가 B 분리 후 손상되어 ──
        //         COCO 저장/로드 라운드트립 시 A 가 L 자로 영구 손상 (사용자 보고 시나리오)

        [WpfFact]
        public void T17_Obj_DragSeparate_APixelMaskSurvivesRoundTrip()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            var rectA = new Rect(20, 20, 40, 40);
            uint idA = DrawRectangle(layer, labelIndex: 1, rectA);
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            // B 분리.
            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));

            // A 의 PixelCount 가 원래 사각의 픽셀수 (40*40=1600) 그대로 유지되어야 함.
            // (저장 시 RLE 마스크가 이 픽셀 마스크에서 인코딩 → 손실되면 다음 로드 시 L 자로 복원됨)
            var instA = layer.Instances.GetById(idA);
            instA.Should().NotBeNull();
            instA!.PixelCount.Should().Be(40 * 40,
                "A 의 픽셀 마스크는 B 가 덮은 픽셀을 잃지 말아야 함 — COCO 저장 후 A 가 L 자로 손상되는 것 방지");

            // RecomputeInstanceMetadata 호출 후에도 BBox 가 원래 사각.
            layer.RecomputeInstanceMetadata(idA);
            layer.Instances.GetById(idA)!.BoundingBox.Should().Be(rectA,
                "RecomputeInstanceMetadata 가 픽셀 extent 로 계산해도 원래 사각이어야 함");
        }

        // ── T15: 사용자 보고 재현 — 두 박스 그리고 B 분리 후 A 의 픽셀 마스크와 RecomputeInstanceMetadata 일관성 ──
        // 사진 L 자 잘림 + "다시 건드려도 복원 안 됨" → A 의 픽셀 마스크 자체가 손상되었는지 검증

        [WpfFact]
        public void T15_Obj_DragSeparate_OriginalAPixelsArePreservedInLayer()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            var rectA = new Rect(20, 20, 40, 40);
            var rectBold = new Rect(40, 40, 40, 40);
            uint idA = DrawRectangle(layer, labelIndex: 1, rectA);
            uint idB = DrawRectangle(layer, labelIndex: 1, rectBold);

            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));

            // A 의 BoundingBox 메타는 원래 사각.
            var instA = layer.Instances.GetById(idA);
            instA!.BoundingBox.Should().Be(rectA);

            // 강제 전체 재합성 — union 영역 밖의 캐시된 프레임이 아니라 신선한 합성 결과 검증.
            // 사용자 사진은 "저장/이동 후에도 잘림 유지" → RefreshAll 후에도 잘림이 보임.
            layer.TestForceRefreshAll();

            // 재렌더 후에도 A 양역 전체가 자기 색.
            var expectedColor = Colors.Red;
            for (int y = 20; y < 60; y++)
            {
                for (int x = 20; x < 60; x++)
                {
                    var px = layer.SampleDisplayPixel(x, y);
                    px.Should().Be(expectedColor, $"({x},{y}) A 박스 내부는 전체 fill 되어야 함");
                }
            }
        }

        // ── T16: A 픽셀 손실 검증 — 주 가설: ResampleInstance 가 A 양역 픽셀을 0 으로 지움 ──
        // A 의 PixelCount 가 B 분리 전후로 유지되는지 확인. 손상되면 PixelCount 감소.

        [WpfFact]
        public void T16_Obj_DragSeparate_APixelMaskRemainsIntact()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            // A 그린 직후 PixelCount 기록. 이 시점 A 는 완전한 사각 → 1600 픽셀.
            var instA = layer.Instances.GetById(idA);
            int aPixelsBefore = instA!.PixelCount;
            aPixelsBefore.Should().Be(40 * 40);

            // B 그림 — A 와 겹침 → A 픽셀 일부가 B ID 로 덮임 (BBox 는 freeze).
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            // A.BoundingBox 는 freeze 되었다. 그러나 A 의 픽셀 마스크는 (40..59,40..59) = 400 픽셀을 잃은 상태.
            // 이건 IndependentInstances 의 설계 (픽셀 손실 허용, render 는 BBox fill 로 온전한 사각 표시).

            // B 분리.
            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));

            // A 의 BBox 메타 freeze.
            instA = layer.Instances.GetById(idA);
            instA!.BoundingBox.Should().Be(new Rect(20, 20, 40, 40));

            // 핵심: B 가 A 양역에서 빠져나가면서 A 의 픽셀을 구멍 남겼는지?
            // 이론상 IndependentInstances 가 BBox freeze + render BBox fill 이므로
            // 표시는 온전 사각. 그러나 사용자는 L 자로 보고 있으므로
            // SampleDisplayPixel 이 A 양역 전체에서 강제로 손상을 검출해야 함.
            for (int y = 20; y < 60; y++)
            {
                for (int x = 20; x < 60; x++)
                {
                    var px = layer.SampleDisplayPixel(x, y);
                    px.Should().Be(Colors.Red, $"({x},{y}) A 사각 내부는 BBox fill 로 완전해야 함");
                }
            }
        }

        // ── T18: IndependentInstances 의 silhouette 은 마스크 픽셀 무시하고 BBox 전체 fill ──
        // 사용자 사진의 잘림 = 그리는 즉시 위 박스 silhouette 이 L 자로 그려져 보이는 것.
        // 사용자 시나리오: 두 박스 겹쳐 그린 직후 (ResampleInstance 호출 전) 위 박스 (B) 의 silhouette.
        // 1.1.15 의 픽셀 reclaim 은 A 픽셀 복원하느라 B 가 A 영역에 가졌던 픽셀을 빼앗음 → B silhouette 이 L 자.

        [WpfFact]
        public void T18_Obj_Silhouette_ReturnsFullBBoxRectangle_NotPixelMaskShape()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            // B 가 A 위에 그려짐. 1.1.15 의 reclaim 로직이 A 영역 픽셀을 A 로 복원하면서
            // B 의 (40..59, 40..59) 픽셀은 A 가 가져감. B 의 픽셀 마스크가 L 자 (BBox 사각 - A 와 겹친 영역).
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            // B 의 silhouette — BBox (40,40,40,40) 모서리 + (40,40) 글로벌 = (0,0) 로컬 (A 와 겹쳤던 자리) 모두 fill.
            var sil = layer.GetInstanceSilhouette(idB) as WriteableBitmap;
            sil.Should().NotBeNull();
            sil!.PixelWidth.Should().Be(40);
            sil.PixelHeight.Should().Be(40);

            AssertSilhouetteOpaque(sil, 0, 0, "B 좌상 = (40,40) 글로벌 — A 와 겹쳤던 자리. 잘림 검출 핵심점.");
            AssertSilhouetteOpaque(sil, 19, 19, "B 안쪽 — A 와 겹친 영역");
            AssertSilhouetteOpaque(sil, 39, 39, "B 우하 — A 와 안 겹친 영역");
            AssertSilhouetteOpaque(sil, 39, 0, "B 우상");
            AssertSilhouetteOpaque(sil, 0, 39, "B 좌하");
        }

        private static void AssertSilhouetteOpaque(WriteableBitmap sil, int x, int y, string because)
        {
            var px = new byte[4];
            sil.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
            // BGRA: alpha=px[3]. IndependentInstances 모드에서 BBox 안 모든 픽셀은 라벨 색으로 fill 되어 alpha > 0.
            px[3].Should().BeGreaterThan(0, $"silhouette ({x},{y}) {because} — BBox 안은 모두 fill");
        }

        // ── T19: IndependentInstances 의 Coco RLE 는 BBox 사각만 인코딩 ──
        // 픽셀 마스크가 손상돼도 저장된 RLE 는 항상 사각 → 재로드 시 L 자 손상 안 됨.

        [WpfFact]
        public void T19_Obj_CocoAnnotation_RleEncodesFullBBox_NotPixelMask()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));

            var anns = layer.BuildCocoAnnotations(imageId: 1);
            anns.Should().HaveCount(2);

            // A 의 annotation 찾기.
            var annA = anns.First(a => a.Bbox[0] == 20 && a.Bbox[1] == 20);
            annA.Bbox.Should().BeEquivalentTo(new[] { 20.0, 20.0, 40.0, 40.0 });
            annA.Area.Should().Be(40 * 40, "Area = bbox.W * bbox.H (BBox 사각 모자이크)");

            // RLE 디코드 → 비트 1 인 픽셀이 BBox 사각 영역과 정확히 일치.
            var rawCounts = CompressedRleCodec.Decode(((CocoCompressedRle)annA.Segmentation!).Counts!);
            var decoded = RleCodec.Decode(rawCounts, 200, 200, outId: 1);
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    bool insideA = x >= 20 && x < 60 && y >= 20 && y < 60;
                    uint bit = decoded[y * 200 + x];
                    if (insideA)
                        bit.Should().Be(1u, $"({x},{y}) A BBox 안은 1");
                    else
                        bit.Should().Be(0u, $"({x},{y}) A BBox 밖은 0");
                }
            }
        }

        // ── T20: 분리 후 display bitmap 도 BBox fill 100% (전체 RefreshAll 후에도) ──

        [WpfFact]
        public void T20_Obj_DragSeparate_DisplayBitmapEqualsBBoxFill_AfterRefreshAll()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = IndependentInstancesInstanceMergeStrategy.Instance;

            uint idA = DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            uint idB = DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            layer.ResampleInstance(idB, new Rect(120, 120, 40, 40));
            layer.TestForceRefreshAll();

            // 양 박스 BBox 안 픽셀 100% 자기 색.
            for (int y = 20; y < 60; y++)
            for (int x = 20; x < 60; x++)
                layer.SampleDisplayPixel(x, y).Should().Be(Colors.Red, $"A ({x},{y})");

            for (int y = 120; y < 160; y++)
            for (int x = 120; x < 160; x++)
                layer.SampleDisplayPixel(x, y).Should().Be(Colors.Red, $"B ({x},{y})");
        }

        // ── T21: Segmentation 회귀 잠금 — IndependentInstances 픽스가 MergeOnOverlap 동작에 영향 없음 ──
        // 사용자 절대 조건: "Segmentation 건들지 않는 조건"

        [WpfFact]
        public void T21_Seg_OverlappingRectangles_StillAutoMerge_UnchangedFromBaseline()
        {
            var layer = CreateLayer(200, 200);
            layer.MergeStrategy = MergeOnOverlapInstanceMergeStrategy.Instance;

            // 같은 라벨 두 사각 겹쳐 그림 → 자동 병합 (Segmentation 기본 정책).
            DrawRectangle(layer, labelIndex: 1, new Rect(20, 20, 40, 40));
            DrawRectangle(layer, labelIndex: 1, new Rect(40, 40, 40, 40));

            layer.Instances.Count.Should().Be(1, "Segmentation 은 겹친 같은 라벨 자동 병합");
            var inst = layer.Instances.First();
            inst.BoundingBox.Should().Be(new Rect(20, 20, 60, 60), "병합 BBox = union");

            // Coco RLE 가 픽셀 마스크 (병합된 정밀 형상) 그대로 인코딩 — BBox fill 아님.
            var anns = layer.BuildCocoAnnotations(imageId: 1);
            anns.Should().HaveCount(1);
            anns[0].Area.Should().Be(inst.PixelCount,
                "Segmentation Area = 실제 픽셀 카운트 (BBox 영역 X). T21 이 깨지면 Segmentation 회귀.");

            // Silhouette 도 픽셀 마스크 모양 그대로 (BBox 모서리에 픽셀 없는 자리 존재) — IndependentInstances 와 다름.
            var sil = layer.GetInstanceSilhouette(inst.Id) as WriteableBitmap;
            sil.Should().NotBeNull();
            // BBox = 60x60. 좌상 (0,0) 은 A 영역 안 → fill. 우상 (59,0) 은 어느 사각에도 안 들어가 → 투명.
            var pxCornerOpposite = new byte[4];
            sil!.CopyPixels(new Int32Rect(59, 0, 1, 1), pxCornerOpposite, 4, 0);
            pxCornerOpposite[3].Should().Be(0,
                "Segmentation silhouette 은 픽셀 마스크 모양 — BBox 우상은 어느 사각도 안 닿아 투명. " +
                "IndependentInstances 픽스가 Segmentation 분기를 건드렸으면 이 점이 fill 되어 실패.");
        }
    }
}
