using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VSMVVM.WPF.Imaging;
using VSMVVM.WPF.Imaging.Coco;

#nullable enable
namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 라벨별 독립 마스크 버퍼를 유지하는 픽셀 마스크 레이어.
    /// 한 픽셀이 여러 라벨에 동시 소속 가능 (COCO 스타일 겹침 지원).
    /// 각 라벨 레이어는 uint[] instanceMask 를 갖고, 0 은 "이 라벨에 속하지 않음".
    /// </summary>
    public class MaskLayer : FrameworkElement
    {
        #region Fields

        // 라벨 인덱스 → instanceMask (width*height, 0 = not in this label)
        private readonly Dictionary<int, SparseTileLayer> _layers = new();

        private readonly MaskInstanceCollection _instances = new();

        private WriteableBitmap? _displayBitmap;
        private int _width;
        private int _height;

        // 활성 stroke 상태.
        private uint _activeStrokeId;
        private int _activeStrokeLabel;
        private Dictionary<int, SparseTileLayer>? _strokePreLayers; // stroke 시작 시점 레이어 스냅샷 (sparse — 빈 타일 자동 skip)

        // stroke 에서 실제로 쓰인 픽셀의 bbox.
        private int _strokeMinX, _strokeMinY, _strokeMaxX, _strokeMaxY;
        private bool _strokeAnyPixel;

        // SourceImage 픽셀 캐시 (Bgra32, 4 bytes per pixel).
        private byte[]? _sourcePixels;
        // Gradient magnitude (Sobel) 캐시 — Magnetic Lasso 용.
        private double[]? _sourceGradient;

        // Diff 기록 모드 — Undo/Redo 메모리 절감용. SnapshotFull (200MB × N labels) 대신 변경 픽셀만 추적.
        private List<MaskLayerDiff.PixelEntry>? _diffEntries;
        private List<MaskLayerSnapshot.InstanceRecord>? _diffInstancesBefore;
        private uint _diffNextIdBefore;
        private int _diffMinX, _diffMinY, _diffMaxX, _diffMaxY;

        // UpdateDisplayRect 의 임시 버퍼 — 매 호출마다 새 alloc 하면 8K BBox 시 200MB byte[] + 50MB bool[] 이
        // LOH 에 누적되어 working set 폭증. 멤버 필드로 1회 alloc + 재사용 (size 변경 시만 재alloc).
        private byte[]? _displayPixelsBuffer;
        private bool[]? _displayFilledBuffer;

        // ResampleInstance 의 sourceBits 임시 버퍼 — 큰 인스턴스 (예: 4000×3000=12MB) 시 LOH alloc 회피.
        private bool[]? _resampleSourceBitsBuffer;

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty MaskWidthProperty =
            DependencyProperty.Register(nameof(MaskWidth), typeof(int), typeof(MaskLayer),
                new PropertyMetadata(0, OnSizeChanged));

        public static readonly DependencyProperty MaskHeightProperty =
            DependencyProperty.Register(nameof(MaskHeight), typeof(int), typeof(MaskLayer),
                new PropertyMetadata(0, OnSizeChanged));

        public static readonly DependencyProperty LabelsProperty =
            DependencyProperty.Register(nameof(Labels), typeof(LabelClassCollection), typeof(MaskLayer),
                new PropertyMetadata(null, OnLabelsChanged));

        public static readonly DependencyProperty CurrentLabelIndexProperty =
            DependencyProperty.Register(nameof(CurrentLabelIndex), typeof(int), typeof(MaskLayer),
                new PropertyMetadata(1));

        public static readonly DependencyProperty SourceImageProperty =
            DependencyProperty.Register(nameof(SourceImage), typeof(ImageSource), typeof(MaskLayer),
                new PropertyMetadata(null, (d, _) => ((MaskLayer)d).RebuildSourceCaches()));

        public static readonly DependencyProperty OverlayOpacityProperty =
            DependencyProperty.Register(nameof(OverlayOpacity), typeof(double), typeof(MaskLayer),
                new PropertyMetadata(0.5, (d, _) => ((MaskLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty SelectedInstanceIdProperty =
            DependencyProperty.Register(nameof(SelectedInstanceId), typeof(uint), typeof(MaskLayer),
                // SelectedInstanceId 변경은 픽셀 색에 영향 안 줌 — OnRender 가 다중 선택 BBox 점선만 다시 그리면 됨.
                // RefreshAll() 은 8K 이미지 전체 픽셀 합성 (50M iter) 으로 ~2.7초 lag 원인이었음.
                new PropertyMetadata(MaskInstanceCollection.BackgroundId, (d, _) => ((MaskLayer)d).InvalidateVisual()));

        public static readonly DependencyProperty SelectedInstanceProperty =
            DependencyProperty.Register(nameof(SelectedInstance), typeof(MaskInstance), typeof(MaskLayer),
                new PropertyMetadata(null, OnSelectedInstanceChanged));

        private static void OnSelectedInstanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not MaskLayer layer) return;
            var newId = (e.NewValue as MaskInstance)?.Id ?? 0;
            if (layer.SelectedInstanceId != newId)
                layer.SelectedInstanceId = newId;
            layer.SelectedInstanceChanged?.Invoke(layer, EventArgs.Empty);
        }

        public event EventHandler? SelectedInstanceChanged;

        public static readonly DependencyProperty IsInstanceHitTestEnabledProperty =
            DependencyProperty.Register(nameof(IsInstanceHitTestEnabled), typeof(bool), typeof(MaskLayer),
                new PropertyMetadata(true));

        /// <summary>
        /// false 이면 OnMouseLeftButtonDown 에서 인스턴스 hit / rubber band 처리를 skip 하고 클릭이 부모로 bubble.
        /// 마스크 mutating 도구 (Brush, Eraser, Fill, MagicWand, PointPromptFill 등) 활성 시 false 로 set 해서
        /// 도구가 클릭을 받게 함. SelectTool 등 인스턴스 선택 도구일 때만 true.
        /// </summary>
        public bool IsInstanceHitTestEnabled
        {
            get => (bool)GetValue(IsInstanceHitTestEnabledProperty);
            set => SetValue(IsInstanceHitTestEnabledProperty, value);
        }

        public static readonly DependencyProperty IsVertexEditModeProperty =
            DependencyProperty.Register(nameof(IsVertexEditMode), typeof(bool), typeof(MaskLayer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (d, _) => ((MaskLayer)d).VertexEditModeChanged?.Invoke(d, EventArgs.Empty)));

        /// <summary>true 이면 SelectedInstance 에 대해 꼭짓점 편집 UI 를 표시해야 함. Behavior 가 Adorner 교체 트리거로 사용.</summary>
        public bool IsVertexEditMode
        {
            get => (bool)GetValue(IsVertexEditModeProperty);
            set => SetValue(IsVertexEditModeProperty, value);
        }

        /// <summary>IsVertexEditMode 변경 알림. MaskBehavior 가 구독해 Adorner 교체.</summary>
        public event EventHandler? VertexEditModeChanged;

        private static readonly DependencyPropertyKey DisplayImagePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(DisplayImage), typeof(ImageSource), typeof(MaskLayer),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DisplayImageProperty = DisplayImagePropertyKey.DependencyProperty;

        public int MaskWidth { get => (int)GetValue(MaskWidthProperty); set => SetValue(MaskWidthProperty, value); }
        public int MaskHeight { get => (int)GetValue(MaskHeightProperty); set => SetValue(MaskHeightProperty, value); }
        public LabelClassCollection? Labels { get => (LabelClassCollection?)GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
        public int CurrentLabelIndex { get => (int)GetValue(CurrentLabelIndexProperty); set => SetValue(CurrentLabelIndexProperty, value); }
        public ImageSource? SourceImage { get => (ImageSource?)GetValue(SourceImageProperty); set => SetValue(SourceImageProperty, value); }
        public double OverlayOpacity { get => (double)GetValue(OverlayOpacityProperty); set => SetValue(OverlayOpacityProperty, value); }
        public uint SelectedInstanceId { get => (uint)GetValue(SelectedInstanceIdProperty); set => SetValue(SelectedInstanceIdProperty, value); }
        public MaskInstance? SelectedInstance { get => (MaskInstance?)GetValue(SelectedInstanceProperty); set => SetValue(SelectedInstanceProperty, value); }
        public MaskInstanceCollection Instances => _instances;
        public ImageSource? DisplayImage { get => (ImageSource?)GetValue(DisplayImageProperty); private set => SetValue(DisplayImagePropertyKey, value); }

        public event EventHandler? DisplayChanged;
        private void RaiseDisplayChanged() => DisplayChanged?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Constructor

        public MaskLayer()
        {
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
            _instances.CollectionChanged += OnInstancesCollectionChanged;
        }

        private void OnInstancesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (MaskInstance inst in e.NewItems) inst.PropertyChanged += OnInstancePropertyChanged;
            if (e.OldItems != null)
                foreach (MaskInstance inst in e.OldItems) inst.PropertyChanged -= OnInstancePropertyChanged;
        }

        private void OnInstancePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // IsVisible 은 픽셀 색이 바뀌므로 BBox 영역 재합성 필요.
            if (e.PropertyName == nameof(MaskInstance.IsVisible))
            {
                if (sender is MaskInstance inst) RefreshInstanceRegion(inst);
                InvalidateVisual();
                return;
            }
            // IsSelected 는 픽셀 색에 영향 안 줌. 다중 선택 BBox 점선만 OnRender 에서 다시 그림.
            if (e.PropertyName == nameof(MaskInstance.IsSelected))
            {
                InvalidateVisual();
            }
        }

        private void RefreshInstanceRegion(MaskInstance inst)
        {
            var b = inst.BoundingBox;
            if (b.IsEmpty) { RefreshAll(); return; }
            UpdateDisplayRect((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height);
        }

        private void AssignLabelRef(MaskInstance inst) => inst.Label = Labels?.GetByIndex(inst.LabelIndex);

        public void RefreshInstanceLabels()
        {
            foreach (var inst in _instances) AssignLabelRef(inst);
        }

        #endregion

        #region Mouse (pixel click → instance select)

        // Rubber band 상태.
        private Point? _rbStartPixel;
        private Point? _rbCurrentPixel;
        private bool _rbAdditive; // Ctrl 누른 상태에서 시작된 rubber band 는 기존 선택 유지.

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_width == 0 || _height == 0) return;
            // mutating 도구가 활성 중이면 인스턴스 hit / rubber band 를 가로채지 않고 클릭을 부모로 bubble.
            if (!IsInstanceHitTestEnabled) return;

            var pos = e.GetPosition(this);
            var displayW = ActualWidth > 0 ? ActualWidth : _width;
            var displayH = ActualHeight > 0 ? ActualHeight : _height;
            int px = (int)(pos.X * _width / displayW);
            int py = (int)(pos.Y * _height / displayH);
            if ((uint)px >= (uint)_width || (uint)py >= (uint)_height) return;
            int idx = py * _width + px;

            bool ctrl = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl)
                     || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

            // Z-order 역순 → 최상단 라벨의 인스턴스 hit.
            foreach (var label in _layers.Keys.OrderByDescending(k => k))
            {
                var mask = _layers[label];
                uint id = mask[idx];
                if (id == 0) continue;
                var inst = _instances.GetById(id);
                if (inst == null) continue;

                // 더블클릭 → vertex 편집 모드 진입. PolygonPoints 가 없으면 마스크 픽셀에서 외곽을 lazy 추출.
                if (e.ClickCount == 2)
                {
                    EnsurePolygonPoints(inst.Id);
                    if (inst.PolygonPoints != null && inst.PolygonPoints.Count >= 3)
                    {
                        ClearAllSelected();
                        inst.IsSelected = true;
                        SelectedInstance = inst;
                        IsVertexEditMode = true;
                        e.Handled = true;
                        return;
                    }
                }

                if (ctrl)
                {
                    // 토글: 기존 다중 선택 유지.
                    inst.IsSelected = !inst.IsSelected;
                    SelectedInstance = inst.IsSelected ? inst : null;
                }
                else
                {
                    // 단일 선택: 기존 전부 clear 후 이 인스턴스만.
                    ClearAllSelected();
                    inst.IsSelected = true;
                    SelectedInstance = inst;
                    // 다른 인스턴스로 선택이 바뀌면 vertex edit 모드 해제.
                    IsVertexEditMode = false;
                }
                e.Handled = true;
                return;
            }

            // 배경 픽셀: Rubber band 시작. Ctrl 없으면 기존 선택 clear, Ctrl 이면 유지.
            if (!ctrl) ClearAllSelected();
            _rbStartPixel = new Point(px, py);
            _rbCurrentPixel = _rbStartPixel;
            _rbAdditive = ctrl;
            if (!ctrl) { SelectedInstance = null; IsVertexEditMode = false; }
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_rbStartPixel == null) return;
            var pos = e.GetPosition(this);
            var displayW = ActualWidth > 0 ? ActualWidth : _width;
            var displayH = ActualHeight > 0 ? ActualHeight : _height;
            _rbCurrentPixel = new Point(pos.X * _width / displayW, pos.Y * _height / displayH);
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_rbStartPixel is Point s && _rbCurrentPixel is Point c)
            {
                var rect = new Rect(
                    Math.Min(s.X, c.X), Math.Min(s.Y, c.Y),
                    Math.Abs(c.X - s.X), Math.Abs(c.Y - s.Y));
                if (rect.Width > 2 || rect.Height > 2)
                {
                    // BBox 완전 포함 인스턴스들 선택. Ctrl(additive) 이면 기존 IsSelected 유지, 추가만.
                    MaskInstance? lastSelected = null;
                    foreach (var inst in _instances)
                    {
                        if (inst.BoundingBox.IsEmpty) continue;
                        if (rect.Contains(inst.BoundingBox))
                        {
                            inst.IsSelected = true;
                            lastSelected = inst;
                        }
                    }
                    if (lastSelected != null) SelectedInstance = lastSelected;
                }
                _rbStartPixel = null;
                _rbCurrentPixel = null;
                _rbAdditive = false;
                ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        private void ClearAllSelected()
        {
            foreach (var i in _instances) if (i.IsSelected) i.IsSelected = false;
        }

        #endregion

        #region Public API — 크기/초기화

        public void Resize(int width, int height)
        {
            if (width < 0 || height < 0) throw new ArgumentOutOfRangeException();
            _width = width;
            _height = height;
            _layers.Clear();

            if (width == 0 || height == 0)
            {
                _displayBitmap = null;
            }
            else
            {
                _displayBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                Width = width;
                Height = height;
            }
            DisplayImage = _displayBitmap;
            _instances.Clear();
            RebuildSourceCaches(); // 크기 변경 후 이미지 캐시 재구성.
            RefreshAll();
        }

        public void Clear()
        {
            if (_width == 0 || _height == 0) return;
            _layers.Clear();
            _instances.Clear();
            // 선택/편집 상태 DP 도 함께 초기화 — 새 이미지 로드 시 옛 인스턴스 ID 가 남으면 Adorner/렌더링 혼란.
            SelectedInstance = null;
            SelectedInstanceId = MaskInstanceCollection.BackgroundId;
            IsVertexEditMode = false;
            RefreshAll();
        }

        private SparseTileLayer GetOrCreateLayer(int labelIndex)
        {
            if (!_layers.TryGetValue(labelIndex, out var mask))
            {
                mask = new SparseTileLayer(_width, _height);
                _layers[labelIndex] = mask;
            }
            return mask;
        }

        #endregion

        #region Public API — Stroke lifecycle

        public uint BeginStroke(int labelIndex)
        {
            _activeStrokeId = _instances.NextId();
            _activeStrokeLabel = labelIndex;
            _strokePreLayers = CloneLayers();
            ResetStrokeBounds();
            return _activeStrokeId;
        }

        public void BeginErase()
        {
            _activeStrokeId = MaskInstanceCollection.BackgroundId;
            _activeStrokeLabel = LabelClassCollection.BackgroundIndex;
            _strokePreLayers = CloneLayers();
            ResetStrokeBounds();
        }

        private Dictionary<int, SparseTileLayer> CloneLayers()
        {
            var clone = new Dictionary<int, SparseTileLayer>(_layers.Count);
            foreach (var kv in _layers)
            {
                clone[kv.Key] = kv.Value.Clone(); // 빈 타일 자동 skip — 면적 비례 alloc.
            }
            return clone;
        }

        private void ResetStrokeBounds()
        {
            _strokeMinX = int.MaxValue; _strokeMinY = int.MaxValue;
            _strokeMaxX = int.MinValue; _strokeMaxY = int.MinValue;
            _strokeAnyPixel = false;
        }

        public void EndStroke(int labelIndex)
        {
            if (_activeStrokeId == 0 || _strokePreLayers == null)
            {
                _strokePreLayers = null;
                return;
            }
            var tentativeId = _activeStrokeId;
            if (!_strokeAnyPixel)
            {
                _activeStrokeId = 0;
                _strokePreLayers = null;
                return;
            }

            // 해당 라벨 레이어에서 stroke 영역 내 이전 ID 수집 (같은 라벨 내 merge 대상).
            // (x, y) 직접 호출 — Get(idx) 의 modulo/division 회피. tile cache 가 같은 타일 연속 access 시 효과.
            var layer = GetOrCreateLayer(labelIndex);
            _strokePreLayers.TryGetValue(labelIndex, out var pre);
            var sameLabelReplaced = new HashSet<uint>();
            for (int y = _strokeMinY; y <= _strokeMaxY; y++)
            {
                for (int x = _strokeMinX; x <= _strokeMaxX; x++)
                {
                    uint newId = layer.Get(x, y);
                    if (newId != tentativeId) continue;
                    uint oldId = pre != null ? pre.Get(x, y) : 0;
                    if (oldId != 0 && oldId != tentativeId)
                        sameLabelReplaced.Add(oldId);
                }
            }

            uint finalId = tentativeId;
            if (sameLabelReplaced.Count > 0)
            {
                finalId = sameLabelReplaced.Min();
                var remap = new HashSet<uint>(sameLabelReplaced) { tentativeId };
                remap.Remove(finalId);
                RemapInstanceIdsInLayer(layer, remap, finalId);
                foreach (var id in sameLabelReplaced)
                {
                    if (id == finalId) continue;
                    var victim = _instances.GetById(id);
                    if (victim != null) _instances.Remove(victim);
                }
            }

            // finalId 인스턴스 보장 + 메타 재계산.
            var finalInst = _instances.GetById(finalId);
            if (finalInst == null)
            {
                finalInst = new MaskInstance { Id = finalId, LabelIndex = labelIndex };
                AssignLabelRef(finalInst);
                _instances.Add(finalInst);
            }
            else
            {
                finalInst.LabelIndex = labelIndex;
                AssignLabelRef(finalInst);
                // 기존 인스턴스에 stroke 픽셀 추가/병합 — PolygonContours 가 마스크와 어긋나므로 무효화.
                // 다음 더블클릭 시 EnsurePolygonPoints 가 새 contour 추출.
                finalInst.PolygonContours = null;
            }
            RecomputeInstanceMetadata(finalId);

            _activeStrokeId = 0;
            _strokePreLayers = null;
            LastCreatedInstanceId = finalId;
        }

        /// <summary>가장 최근 <see cref="EndStroke"/> 가 생성/병합해 확정한 인스턴스 ID. Tool 이 직후 참조해 메타 부여용.</summary>
        public uint LastCreatedInstanceId { get; private set; }

        public void EndErase()
        {
            if (_strokePreLayers == null) return;
            if (!_strokeAnyPixel)
            {
                _strokePreLayers = null;
                return;
            }

            // 지우개는 모든 레이어에 영향 가능. 변경된 인스턴스 수집 → CCL 재분석.
            // (x, y) 직접 호출 — Get(idx) 의 modulo/division 회피. tile cache 효과.
            var touched = new HashSet<uint>();
            foreach (var (lblIdx, mask) in _layers)
            {
                _strokePreLayers.TryGetValue(lblIdx, out var pre);
                if (pre == null) continue;
                for (int y = _strokeMinY; y <= _strokeMaxY; y++)
                {
                    for (int x = _strokeMinX; x <= _strokeMaxX; x++)
                    {
                        uint oldId = pre.Get(x, y);
                        if (oldId == 0) continue;
                        if (mask.Get(x, y) != oldId) touched.Add(oldId);
                    }
                }
            }

            foreach (var id in touched)
            {
                RecomputeInstanceMetadata(id);
                var inst = _instances.GetById(id);
                if (inst == null) continue;
                if (inst.PixelCount == 0) { _instances.Remove(inst); continue; }
                // 지우개로 픽셀 변경 — PolygonContours 가 마스크와 어긋나므로 무효화.
                inst.PolygonContours = null;
                SplitIfDisconnected(id);
            }

            _strokePreLayers = null;
        }

        public void SplitInstance(uint id)
        {
            if (_instances.GetById(id) == null) return;
            SplitIfDisconnected(id);
        }

        public void DeleteInstance(uint id)
        {
            var inst = _instances.GetById(id);
            if (inst == null) return;
            if (!_layers.TryGetValue(inst.LabelIndex, out var mask))
            {
                _instances.Remove(inst);
                return;
            }

            int labelIdx = inst.LabelIndex;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            // Sparse 타일 enumerate — 빈 타일 skip, inner loop dense.
            foreach (var (tileX, tileY, tile) in mask.EnumerateAllocatedTiles())
            {
                int tileBaseX = tileX * SparseTileLayer.TileSize;
                int tileBaseY = tileY * SparseTileLayer.TileSize;
                int yEnd = Math.Min(SparseTileLayer.TileSize, _height - tileBaseY);
                int xEnd = Math.Min(SparseTileLayer.TileSize, _width - tileBaseX);
                for (int ly = 0; ly < yEnd; ly++)
                {
                    int tileRowBase = ly * SparseTileLayer.TileSize;
                    int gy = tileBaseY + ly;
                    int gRow = gy * _width;
                    for (int lx = 0; lx < xEnd; lx++)
                    {
                        if (tile[tileRowBase + lx] != id) continue;
                        int gx = tileBaseX + lx;
                        RecordPixelChange(labelIdx, gRow + gx, id, 0);
                        tile[tileRowBase + lx] = 0;
                        if (gx < minX) minX = gx;
                        if (gx > maxX) maxX = gx;
                        if (gy < minY) minY = gy;
                        if (gy > maxY) maxY = gy;
                    }
                }
            }
            _instances.Remove(inst);
            if (minX <= maxX) UpdateDisplayRect(minX, minY, maxX - minX + 1, maxY - minY + 1);

            // 큰 인스턴스 삭제 후 LOH 회수.
            MemoryHelper.CompactAndCollect();
        }

        /// <summary>여러 인스턴스를 한 번에 삭제. RefreshAll 단일 호출로 최적화.</summary>
        public void DeleteInstances(System.Collections.Generic.IEnumerable<uint> ids)
        {
            var idSet = new System.Collections.Generic.HashSet<uint>(ids);
            if (idSet.Count == 0) return;

            foreach (var kv in _layers)
            {
                int labelIdx = kv.Key;
                var m = kv.Value;
                // Sparse 타일 enumerate — 빈 타일 skip, inner loop dense.
                foreach (var (tileX, tileY, tile) in m.EnumerateAllocatedTiles())
                {
                    int tileBaseX = tileX * SparseTileLayer.TileSize;
                    int tileBaseY = tileY * SparseTileLayer.TileSize;
                    int yEnd = Math.Min(SparseTileLayer.TileSize, _height - tileBaseY);
                    int xEnd = Math.Min(SparseTileLayer.TileSize, _width - tileBaseX);
                    for (int ly = 0; ly < yEnd; ly++)
                    {
                        int tileRowBase = ly * SparseTileLayer.TileSize;
                        int gy = tileBaseY + ly;
                        int gRow = gy * _width;
                        for (int lx = 0; lx < xEnd; lx++)
                        {
                            uint old = tile[tileRowBase + lx];
                            if (!idSet.Contains(old)) continue;
                            int gx = tileBaseX + lx;
                            RecordPixelChange(labelIdx, gRow + gx, old, 0);
                            tile[tileRowBase + lx] = 0;
                        }
                    }
                }
            }

            var toRemove = new System.Collections.Generic.List<MaskInstance>();
            foreach (var inst in _instances)
                if (idSet.Contains(inst.Id)) toRemove.Add(inst);
            foreach (var r in toRemove) _instances.Remove(r);

            RefreshAll();

            // 다중 인스턴스 삭제 후 LOH 회수.
            MemoryHelper.CompactAndCollect();
        }

        /// <summary>
        /// 여러 인스턴스를 하나로 병합. 서로 다른 라벨은 각자 독립 그룹으로 처리(물리적으로 다른 layer).
        /// 각 그룹에서 min ID 를 finalId 로 삼아 나머지를 remap 하고 컬렉션에서 제거.
        /// 사용자 명시적 merge 이므로 SplitIfDisconnected 는 호출하지 않음(disconnected 라도 한 ID 유지).
        /// </summary>
        public void MergeInstances(System.Collections.Generic.IReadOnlyList<uint> ids)
        {
            if (ids == null || ids.Count < 2) return;

            // 1. 실제 존재하는 인스턴스만 labelIndex 기준으로 그룹화.
            var groups = new Dictionary<int, List<uint>>();
            foreach (var id in ids)
            {
                var inst = _instances.GetById(id);
                if (inst == null) continue;
                if (!groups.TryGetValue(inst.LabelIndex, out var list))
                {
                    list = new List<uint>();
                    groups[inst.LabelIndex] = list;
                }
                if (!list.Contains(id)) list.Add(id);
            }

            bool anyMerged = false;
            foreach (var kv in groups)
            {
                var labelIndex = kv.Key;
                var groupIds = kv.Value;
                if (groupIds.Count < 2) continue; // 그룹 내 1개면 merge 대상 아님
                if (!_layers.TryGetValue(labelIndex, out var mask)) continue;

                uint finalId = groupIds.Min();
                var remapFrom = new HashSet<uint>(groupIds);
                remapFrom.Remove(finalId);
                if (remapFrom.Count == 0) continue;

                RemapInstanceIdsInLayer(mask, remapFrom, finalId);
                foreach (var victimId in remapFrom)
                {
                    var victim = _instances.GetById(victimId);
                    if (victim != null) _instances.Remove(victim);
                }
                RecomputeInstanceMetadata(finalId);
                // 병합 — finalId 의 PolygonContours 는 부분만 표현. 무효화 → 다음 더블클릭 시 재추출.
                var finalInst = _instances.GetById(finalId);
                if (finalInst != null) finalInst.PolygonContours = null;
                anyMerged = true;
            }

            if (anyMerged)
            {
                RefreshAll();
                MemoryHelper.CompactAndCollect(); // 병합 후 LOH 회수.
            }
        }

        /// <summary>기존 인스턴스 픽셀을 새 BBox 로 nearest-neighbor 리샘플링. 라벨 레이어 안에서만 작동.
        /// 새 BBox 가 같은 라벨의 다른 인스턴스와 겹치면 자동으로 하나의 인스턴스로 병합한다
        /// (EndStroke 와 동일한 정책). 병합 결과가 disconnected 면 SplitIfDisconnected 로 재분리.</summary>
        public void ResampleInstance(uint id, Rect newBBox)
        {
            var inst = _instances.GetById(id);
            if (inst == null) return;
            var oldBBox = inst.BoundingBox;
            if (oldBBox.IsEmpty || newBBox.IsEmpty) return;
            if (newBBox.Width < 1 || newBBox.Height < 1) return;
            if (!_layers.TryGetValue(inst.LabelIndex, out var mask)) return;

            // 이미지 경계 내로 clamp (방어적).
            var bounds = new Rect(0, 0, _width, _height);
            newBBox.Intersect(bounds);
            if (newBBox.IsEmpty || newBBox.Width < 1 || newBBox.Height < 1) return;

            // 같은 라벨 다른 인스턴스 merge 대상 ID 수집은 **재기록 단계의 prev 검사**에서 자동으로 모음.
            // newBBox 사각형 사전 스캔은 회귀 원인 — BBox 만 겹치고 픽셀이 안 닿은 다른 인스턴스도 흡수해버림.
            // 새 마스크 픽셀이 실제 닿은 자리에서만 (prev != 0 && prev != id) 그 ID 가 진짜 흡수 대상.
            var sameLabelReplaced = new HashSet<uint>();

            int oldX = (int)oldBBox.X, oldY = (int)oldBBox.Y;
            int oldW = (int)oldBBox.Width, oldH = (int)oldBBox.Height;

            // sourceBits 캐시 재사용 — 큰 인스턴스 (예: 4000×3000=12MB) 매번 alloc 시 LOH 누적.
            int srcBitsLen = oldW * oldH;
            if (_resampleSourceBitsBuffer == null || _resampleSourceBitsBuffer.Length < srcBitsLen)
                _resampleSourceBitsBuffer = new bool[srcBitsLen];
            else
                Array.Clear(_resampleSourceBitsBuffer, 0, srcBitsLen);
            var sourceBits = _resampleSourceBitsBuffer;

            // sourceBits 추출 — (x, y) 직접 호출 + tile cache 효과.
            for (int y = 0; y < oldH; y++)
            {
                int gy = oldY + y;
                if ((uint)gy >= (uint)_height) continue;
                for (int x = 0; x < oldW; x++)
                {
                    int gx = oldX + x;
                    if ((uint)gx >= (uint)_width) continue;
                    if (mask.Get(gx, gy) == id) sourceBits[y * oldW + x] = true;
                }
            }

            int newXi = (int)Math.Round(newBBox.X);
            int newYi = (int)Math.Round(newBBox.Y);
            int newWi = Math.Max(1, (int)Math.Round(newBBox.Width));
            int newHi = Math.Max(1, (int)Math.Round(newBBox.Height));

            int labelIdx = inst.LabelIndex;

            // 1. 기존 인스턴스 픽셀을 해당 라벨 레이어에서 제거 — sourceBits 가 어느 픽셀이 id 인지 알므로
            //    매 픽셀 `mask.Get(gx, gy) == id` 검사 대신 sourceBits true 만 처리. 더 빠름.
            for (int y = 0; y < oldH; y++)
            {
                int gy = oldY + y;
                if ((uint)gy >= (uint)_height) continue;
                int sourceRowBase = y * oldW;
                int gRow = gy * _width;
                for (int x = 0; x < oldW; x++)
                {
                    if (!sourceBits[sourceRowBase + x]) continue;
                    int gx = oldX + x;
                    if ((uint)gx >= (uint)_width) continue;
                    RecordPixelChange(labelIdx, gRow + gx, id, 0);
                    mask.Set(gx, gy, 0);
                }
            }

            // 2. 새 BBox 로 픽셀 재기록.
            //    pureTranslate(크기 변동 ≤ 1px) 인 경우 nearest-neighbor 대신 1:1 shift 로 복사 —
            //    Merge 로 묶인 disconnected 덩어리의 공백 구조가 정확히 보존됨.
            //    리사이즈(크기 변동) 인 경우만 nearest-neighbor 스케일 수행.
            bool pureTranslate = Math.Abs(newWi - oldW) <= 1 && Math.Abs(newHi - oldH) <= 1;
            if (pureTranslate)
            {
                int dxShift = newXi - oldX;
                int dyShift = newYi - oldY;
                for (int y = 0; y < oldH; y++)
                {
                    int gy = oldY + y + dyShift;
                    if ((uint)gy >= (uint)_height) continue;
                    int sourceRowBase = y * oldW;
                    int gRow = gy * _width;
                    for (int x = 0; x < oldW; x++)
                    {
                        if (!sourceBits[sourceRowBase + x]) continue;
                        int gx = oldX + x + dxShift;
                        if ((uint)gx >= (uint)_width) continue;
                        uint prev = mask.Get(gx, gy);
                        if (prev != id)
                        {
                            // 이동된 마스크의 실제 픽셀이 닿은 자리에서 다른 인스턴스 ID 만났으면 흡수 대상.
                            if (prev != 0) sameLabelReplaced.Add(prev);
                            RecordPixelChange(labelIdx, gRow + gx, prev, id);
                            mask.Set(gx, gy, id);
                        }
                    }
                }
            }
            else
            {
                for (int y = 0; y < newHi; y++)
                {
                    int srcY = (int)((double)y * oldH / newHi);
                    if (srcY >= oldH) srcY = oldH - 1;
                    int gy = newYi + y;
                    if ((uint)gy >= (uint)_height) continue;
                    int sourceRowBase = srcY * oldW;
                    int gRow = gy * _width;
                    for (int x = 0; x < newWi; x++)
                    {
                        int srcX = (int)((double)x * oldW / newWi);
                        if (srcX >= oldW) srcX = oldW - 1;
                        if (!sourceBits[sourceRowBase + srcX]) continue;
                        int gx = newXi + x;
                        if ((uint)gx >= (uint)_width) continue;
                        uint prev = mask.Get(gx, gy);
                        if (prev != id)
                        {
                            // 새 마스크 픽셀이 실제 닿은 자리에서 다른 인스턴스 만났으면 흡수 대상.
                            if (prev != 0) sameLabelReplaced.Add(prev);
                            RecordPixelChange(labelIdx, gRow + gx, prev, id);
                            mask.Set(gx, gy, id);
                        }
                    }
                }
            }

            // 3. PolygonContours (외곽 + hole) 도 동일 아핀 변환으로 동기화 — vertex 편집 진입 시 일치 유지.
            if (inst.PolygonContours != null && inst.PolygonContours.Count > 0
                && oldBBox.Width > 0 && oldBBox.Height > 0)
            {
                double sx = newBBox.Width / oldBBox.Width;
                double sy = newBBox.Height / oldBBox.Height;
                var newContours = new List<IList<Point>>(inst.PolygonContours.Count);
                foreach (var c in inst.PolygonContours)
                {
                    var xformed = new List<Point>(c.Count);
                    foreach (var p in c)
                    {
                        double nx = newBBox.X + (p.X - oldBBox.X) * sx;
                        double ny = newBBox.Y + (p.Y - oldBBox.Y) * sy;
                        xformed.Add(new Point(nx, ny));
                    }
                    newContours.Add(xformed);
                }
                inst.PolygonContours = newContours;
            }

            // 4. 겹쳐 덮인 같은 라벨의 다른 인스턴스들을 id 로 통합 (EndStroke 와 동일 정책).
            if (sameLabelReplaced.Count > 0)
            {
                RemapInstanceIdsInLayer(mask, sameLabelReplaced, id);
                foreach (var victimId in sameLabelReplaced)
                {
                    var victim = _instances.GetById(victimId);
                    if (victim != null) _instances.Remove(victim);
                }
                // 흡수 일어남 — affine 변환된 PolygonContours 가 부분만 표현. 무효화 → 다음 더블클릭 시 재추출.
                inst.PolygonContours = null;
            }

            RecomputeInstanceMetadata(id);

            // 5. 리사이즈로 병합이 일어난 경우 disconnected 면 자동 재분리.
            //    pureTranslate(이동) 에서는 Merge 로 묶인 사용자 의도를 존중해 자동 재분리 생략 —
            //    disconnected 덩어리가 그대로 같은 ID 를 유지.
            if (!pureTranslate)
                SplitIfDisconnected(id);

            // 6. 변경 영역만 합성 — oldBBox ∪ newBBox. 8K 풀 합성 (RefreshAll) 대비 100~1000배 빠름.
            //    union 안 픽셀만 변경됐으므로 그 영역만 재합성하면 정확. union 밖 다른 인스턴스는 그대로.
            var union = Rect.Union(oldBBox, newBBox);
            int rx = Math.Max(0, (int)Math.Floor(union.X));
            int ry = Math.Max(0, (int)Math.Floor(union.Y));
            int rxEnd = Math.Min(_width, (int)Math.Ceiling(union.X + union.Width));
            int ryEnd = Math.Min(_height, (int)Math.Ceiling(union.Y + union.Height));
            int rw = rxEnd - rx;
            int rh = ryEnd - ry;
            if (rw > 0 && rh > 0) UpdateDisplayRect(rx, ry, rw, rh);

            // 큰 작업 후 LOH 단편화 회수 — 자주 호출 안 됨 (drag commit 1회).
            MemoryHelper.CompactAndCollect();
        }

        #endregion

        #region Public API — 드로잉 프리미티브

        public void PaintPixel(int x, int y, int labelIndex)
        {
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return;
            int idx = y * _width + x;
            WritePixelToLayer(idx, labelIndex);
            ExpandStrokeBounds(x, y);
            UpdateDisplayPixel(x, y);
        }

        public void PaintCircle(Point center, int radius, int labelIndex)
        {
            if (_width == 0 || _height == 0 || radius < 0) return;
            int cx = (int)center.X, cy = (int)center.Y;
            int r2 = radius * radius;
            int xMin = Math.Max(0, cx - radius);
            int xMax = Math.Min(_width - 1, cx + radius);
            int yMin = Math.Max(0, cy - radius);
            int yMax = Math.Min(_height - 1, cy + radius);
            if (xMax < xMin || yMax < yMin) return;

            for (int y = yMin; y <= yMax; y++)
            {
                int dy = y - cy;
                int rowStart = y * _width;
                for (int x = xMin; x <= xMax; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy * dy <= r2)
                        WritePixelToLayer(rowStart + x, labelIndex);
                }
            }

            ExpandStrokeBounds(xMin, yMin);
            ExpandStrokeBounds(xMax, yMax);
            UpdateDisplayRect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        public void Erase(Point center, int radius)
            => PaintCircle(center, radius, LabelClassCollection.BackgroundIndex);

        /// <summary>축에 정렬된 사각형 영역을 해당 라벨로 채운다. 활성 stroke 필요.</summary>
        public void PaintRectangle(Rect bounds, int labelIndex)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            int xMin = Math.Max(0, (int)Math.Round(bounds.X));
            int yMin = Math.Max(0, (int)Math.Round(bounds.Y));
            int xMax = Math.Min(_width - 1, (int)Math.Round(bounds.X + bounds.Width) - 1);
            int yMax = Math.Min(_height - 1, (int)Math.Round(bounds.Y + bounds.Height) - 1);
            if (xMax < xMin || yMax < yMin) return;

            for (int y = yMin; y <= yMax; y++)
            {
                int rowStart = y * _width;
                for (int x = xMin; x <= xMax; x++)
                    WritePixelToLayer(rowStart + x, labelIndex);
            }
            ExpandStrokeBounds(xMin, yMin);
            ExpandStrokeBounds(xMax, yMax);
            UpdateDisplayRect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        /// <summary>축에 정렬된 타원(사각형에 내접)을 해당 라벨로 채운다.</summary>
        public void PaintEllipse(Rect bounds, int labelIndex)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            double cx = bounds.X + bounds.Width / 2.0;
            double cy = bounds.Y + bounds.Height / 2.0;
            double rx = bounds.Width / 2.0;
            double ry = bounds.Height / 2.0;
            if (rx <= 0 || ry <= 0) return;

            int xMin = Math.Max(0, (int)Math.Floor(bounds.X));
            int yMin = Math.Max(0, (int)Math.Floor(bounds.Y));
            int xMax = Math.Min(_width - 1, (int)Math.Ceiling(bounds.X + bounds.Width) - 1);
            int yMax = Math.Min(_height - 1, (int)Math.Ceiling(bounds.Y + bounds.Height) - 1);
            if (xMax < xMin || yMax < yMin) return;

            double rx2 = rx * rx, ry2 = ry * ry;
            for (int y = yMin; y <= yMax; y++)
            {
                double dy = (y + 0.5) - cy;
                double term = 1.0 - (dy * dy) / ry2;
                if (term < 0) continue;
                int rowStart = y * _width;
                for (int x = xMin; x <= xMax; x++)
                {
                    double dx = (x + 0.5) - cx;
                    if ((dx * dx) / rx2 + (dy * dy) / ry2 <= 1.0)
                        WritePixelToLayer(rowStart + x, labelIndex);
                }
            }
            ExpandStrokeBounds(xMin, yMin);
            ExpandStrokeBounds(xMax, yMax);
            UpdateDisplayRect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        /// <summary>사용자 지정 폴리곤을 scanline-fill. 활성 stroke 필요.</summary>
        public void PaintPolygon(System.Collections.Generic.IReadOnlyList<Point> points, int labelIndex)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            if (points == null || points.Count < 3) return;

            var flat = new double[points.Count * 2];
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                flat[i * 2] = points[i].X;
                flat[i * 2 + 1] = points[i].Y;
                if (points[i].X < minX) minX = points[i].X;
                if (points[i].X > maxX) maxX = points[i].X;
                if (points[i].Y < minY) minY = points[i].Y;
                if (points[i].Y > maxY) maxY = points[i].Y;
            }

            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(_height - 1, (int)Math.Ceiling(maxY));
            int n = points.Count;
            var xs = new System.Collections.Generic.List<double>();
            for (int y = y0; y <= y1; y++)
            {
                xs.Clear();
                double cy = y + 0.5;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    double ay = flat[i * 2 + 1], by = flat[j * 2 + 1];
                    if ((ay <= cy && by > cy) || (by <= cy && ay > cy))
                    {
                        double ax = flat[i * 2], bx = flat[j * 2];
                        double t = (cy - ay) / (by - ay);
                        xs.Add(ax + t * (bx - ax));
                    }
                }
                xs.Sort();
                int row = y * _width;
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Math.Max(0, (int)Math.Floor(xs[k]));
                    int xb = Math.Min(_width - 1, (int)Math.Ceiling(xs[k + 1]));
                    for (int x = xa; x <= xb; x++) WritePixelToLayer(row + x, labelIndex);
                }
            }
            ExpandStrokeBounds(Math.Max(0, (int)Math.Floor(minX)), y0);
            ExpandStrokeBounds(Math.Min(_width - 1, (int)Math.Ceiling(maxX)), y1);
            int bw = Math.Max(0, (int)Math.Ceiling(maxX) - (int)Math.Floor(minX) + 1);
            int bh = y1 - y0 + 1;
            UpdateDisplayRect((int)Math.Floor(minX), y0, bw, bh);
        }

        /// <summary>
        /// Magic Wand: 시드 픽셀 RGB 와 tolerance 이내인 4-연결 contiguous 영역을 해당 라벨로 채운다.
        /// SourceImage 가 설정되어 있어야 함. 활성 stroke 필요(BeginStroke 이미 호출).
        /// </summary>
        public void PaintRgbFloodFill(int seedX, int seedY, int labelIndex, int tolerance)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            if ((uint)seedX >= (uint)_width || (uint)seedY >= (uint)_height) return;
            var src = _sourcePixels;
            if (src == null) return;

            int seedIdx = seedY * _width + seedX;
            int seedP = seedIdx * 4;
            byte sB = src[seedP + 0], sG = src[seedP + 1], sR = src[seedP + 2];

            var visited = new bool[_width * _height];
            var stack = new Stack<(int x, int y)>();
            stack.Push((seedX, seedY));
            visited[seedIdx] = true;
            int minX = seedX, maxX = seedX, minY = seedY, maxY = seedY;

            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                int idx = y * _width + x;
                int p = idx * 4;
                int db = src[p + 0] - sB;
                int dg = src[p + 1] - sG;
                int dr = src[p + 2] - sR;
                // Chebyshev 거리 (포토샵 근사). tolerance 이내면 포함.
                int maxDiff = Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db)));
                if (maxDiff > tolerance) continue;

                WritePixelToLayer(idx, labelIndex);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                void TryPush(int nx, int ny)
                {
                    if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) return;
                    int ni = ny * _width + nx;
                    if (visited[ni]) return;
                    visited[ni] = true;
                    stack.Push((nx, ny));
                }
                TryPush(x + 1, y);
                TryPush(x - 1, y);
                TryPush(x, y + 1);
                TryPush(x, y - 1);
            }
            ExpandStrokeBounds(minX, minY);
            ExpandStrokeBounds(maxX, maxY);
            UpdateDisplayRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// SAM-style point-prompt fill. 2-pass:
        /// (1) negative 시드들에서 RGB tolerance flood — "blocked" 마킹된 connected component 만 차단.
        /// (2) positive 시드들에서 RGB tolerance flood — blocked 안 됐으면서 positive 색 안인 픽셀만 칠함.
        /// 같은 색이라도 negative 시드에서 끊어진 다른 connected component 는 영향 없음 (spatial 분리).
        /// SourceImage 필요. 활성 stroke 필요(BeginStroke 이미 호출).
        /// </summary>
        public void PaintPointPromptFill(
            IReadOnlyList<(int x, int y)> positiveSeeds,
            IReadOnlyList<(int x, int y)> negativeSeeds,
            int labelIndex,
            int tolerance)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            if (positiveSeeds == null || positiveSeeds.Count == 0) return;
            var src = _sourcePixels;
            if (src == null) return;

            // positive / negative 시드 RGB 수집.
            var posRgb = new List<(byte r, byte g, byte b)>(positiveSeeds.Count);
            foreach (var (x, y) in positiveSeeds)
            {
                if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) continue;
                int p = (y * _width + x) * 4;
                posRgb.Add((src[p + 2], src[p + 1], src[p + 0]));
            }
            if (posRgb.Count == 0) return;

            // Positive 시드 위치를 stop pixel 로 마킹 — negative flood 가 여기 닿으면 멈춤.
            // 같은 색 connected component 안에 positive 와 negative 가 둘 다 있을 때, negative 가
            // positive 영역까지 침범해서 다 막아버리는 문제 방지.
            var positiveStop = new bool[_width * _height];
            foreach (var (x, y) in positiveSeeds)
            {
                if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) continue;
                positiveStop[y * _width + x] = true;
            }

            // Pass 1: negative 시드에서 4-conn flood — 각 시드 색 tolerance 안 인접 픽셀에 blocked 마킹.
            // 같은 색이라도 다른 connected component 면 마킹 안 됨 → spatial 분리 보장.
            // positiveStop 픽셀은 negative flood 가 통과/마킹 못 함 → positive 시드는 항상 살아남음.
            var blocked = new bool[_width * _height];
            if (negativeSeeds != null && negativeSeeds.Count > 0)
            {
                var nStack = new Stack<(int x, int y)>();
                foreach (var (sx, sy) in negativeSeeds)
                {
                    if ((uint)sx >= (uint)_width || (uint)sy >= (uint)_height) continue;
                    int seedIdx = sy * _width + sx;
                    if (blocked[seedIdx] || positiveStop[seedIdx]) continue;
                    int sp = seedIdx * 4;
                    byte sB = src[sp + 0], sG = src[sp + 1], sR = src[sp + 2];

                    blocked[seedIdx] = true;
                    nStack.Push((sx, sy));
                    while (nStack.Count > 0)
                    {
                        var (x, y) = nStack.Pop();
                        int idx = y * _width + x;
                        int p = idx * 4;
                        int diff = Math.Max(Math.Abs(src[p + 2] - sR),
                                   Math.Max(Math.Abs(src[p + 1] - sG), Math.Abs(src[p + 0] - sB)));
                        if (diff > tolerance) continue;
                        // 이웃 push — positiveStop 은 마킹/통과 금지.
                        if (x + 1 < _width && !blocked[idx + 1] && !positiveStop[idx + 1])
                        { blocked[idx + 1] = true; nStack.Push((x + 1, y)); }
                        if (x - 1 >= 0 && !blocked[idx - 1] && !positiveStop[idx - 1])
                        { blocked[idx - 1] = true; nStack.Push((x - 1, y)); }
                        if (y + 1 < _height && !blocked[idx + _width] && !positiveStop[idx + _width])
                        { blocked[idx + _width] = true; nStack.Push((x, y + 1)); }
                        if (y - 1 >= 0 && !blocked[idx - _width] && !positiveStop[idx - _width])
                        { blocked[idx - _width] = true; nStack.Push((x, y - 1)); }
                    }
                }
            }

            // Pass 2: positive 시드에서 4-conn flood. blocked 픽셀은 통과 금지.
            var visited = new bool[_width * _height];
            var stack = new Stack<(int x, int y)>();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            foreach (var (x, y) in positiveSeeds)
            {
                if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) continue;
                int seedIdx = y * _width + x;
                if (visited[seedIdx] || blocked[seedIdx]) continue;
                visited[seedIdx] = true;
                stack.Push((x, y));
            }

            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                int idx = y * _width + x;
                int p = idx * 4;
                byte b = src[p + 0], g = src[p + 1], r = src[p + 2];

                // positive 색 매칭 — 하나라도 tolerance 안이면 통과.
                bool allowed = false;
                for (int i = 0; i < posRgb.Count; i++)
                {
                    var pp = posRgb[i];
                    int diff = Math.Max(Math.Abs(r - pp.r), Math.Max(Math.Abs(g - pp.g), Math.Abs(b - pp.b)));
                    if (diff <= tolerance) { allowed = true; break; }
                }
                if (!allowed) continue;

                WritePixelToLayer(idx, labelIndex);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                void TryPush(int nx, int ny)
                {
                    if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) return;
                    int ni = ny * _width + nx;
                    if (visited[ni] || blocked[ni]) return;
                    visited[ni] = true;
                    stack.Push((nx, ny));
                }
                TryPush(x + 1, y);
                TryPush(x - 1, y);
                TryPush(x, y + 1);
                TryPush(x, y - 1);
            }

            if (maxX >= minX)
            {
                ExpandStrokeBounds(minX, minY);
                ExpandStrokeBounds(maxX, maxY);
                UpdateDisplayRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
        }

        /// <summary>활성 stroke 를 취소하고 시작 전 상태로 롤백.</summary>
        public void CancelStroke()
        {
            if (_strokePreLayers == null) return;
            _layers.Clear();
            foreach (var kv in _strokePreLayers) _layers[kv.Key] = kv.Value;
            _activeStrokeId = 0;
            _strokePreLayers = null;
            ResetStrokeBounds();
            RefreshAll();
        }

        /// <summary>4연결 flood fill — 특정 라벨 레이어 안에서만 작동.</summary>
        public void FloodFill(Point seed, int labelIndex)
        {
            if (_width == 0 || _height == 0) return;
            if (labelIndex == LabelClassCollection.BackgroundIndex) return;
            int sx = (int)seed.X, sy = (int)seed.Y;
            if ((uint)sx >= (uint)_width || (uint)sy >= (uint)_height) return;

            var targetLayer = GetOrCreateLayer(labelIndex);
            int seedIdx = sy * _width + sx;
            uint targetId = targetLayer[seedIdx];

            int minX = sx, maxX = sx, minY = sy, maxY = sy;
            var stack = new Stack<(int x, int y)>();
            stack.Push((sx, sy));
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) continue;
                int idx = y * _width + x;
                if (targetLayer[idx] != targetId) continue;
                WritePixelToLayer(idx, labelIndex);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                stack.Push((x + 1, y));
                stack.Push((x - 1, y));
                stack.Push((x, y + 1));
                stack.Push((x, y - 1));
            }

            ExpandStrokeBounds(minX, minY);
            ExpandStrokeBounds(maxX, maxY);
            UpdateDisplayRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void WritePixelToLayer(int idx, int labelIndex)
        {
            // idx → x, y 한 번 분해 후 (x, y) Set 호출 — 인덱서의 매번 modulo/division 회피, tile cache 활용.
            int x = idx % _width;
            int y = idx / _width;
            if (labelIndex == LabelClassCollection.BackgroundIndex)
            {
                // 지우개: 모든 레이어에서 해당 픽셀을 0 으로.
                foreach (var kv in _layers)
                {
                    uint prev = kv.Value.Get(x, y);
                    if (prev != 0)
                    {
                        RecordPixelChange(kv.Key, idx, prev, 0);
                        kv.Value.Set(x, y, 0);
                    }
                }
                return;
            }
            if (_activeStrokeId == 0) return;
            var layer = GetOrCreateLayer(labelIndex);
            uint prevId = layer.Get(x, y);
            if (prevId != _activeStrokeId)
            {
                RecordPixelChange(labelIndex, idx, prevId, _activeStrokeId);
                layer.Set(x, y, _activeStrokeId);
            }
        }

        private void ExpandStrokeBounds(int x, int y)
        {
            if (x < _strokeMinX) _strokeMinX = x;
            if (x > _strokeMaxX) _strokeMaxX = x;
            if (y < _strokeMinY) _strokeMinY = y;
            if (y > _strokeMaxY) _strokeMaxY = y;
            _strokeAnyPixel = true;
        }

        private void RemapInstanceIdsInLayer(SparseTileLayer layer, HashSet<uint> from, uint toId)
        {
            if (from.Count == 0) return;
            int labelIdx = LabelIndexForLayer(layer);
            // 모든 alloc 된 타일 enumerate — 빈 타일은 자동 skip (sparse 효율).
            foreach (var (tileX, tileY, tile) in layer.EnumerateAllocatedTiles())
            {
                int tileBaseX = tileX * SparseTileLayer.TileSize;
                int tileBaseY = tileY * SparseTileLayer.TileSize;
                for (int ly = 0; ly < SparseTileLayer.TileSize; ly++)
                {
                    int gy = tileBaseY + ly;
                    if (gy >= layer.Height) break;
                    for (int lx = 0; lx < SparseTileLayer.TileSize; lx++)
                    {
                        int gx = tileBaseX + lx;
                        if (gx >= layer.Width) break;
                        int localIdx = ly * SparseTileLayer.TileSize + lx;
                        uint old = tile[localIdx];
                        if (!from.Contains(old)) continue;
                        int globalIdx = gy * layer.Width + gx;
                        RecordPixelChange(labelIdx, globalIdx, old, toId);
                        tile[localIdx] = toId;
                    }
                }
            }
        }

        /// <summary>주어진 layer 가 어느 라벨에 속하는지 _layers 에서 역검색. RecordPixelChange 의 LabelIndex 인자용.</summary>
        private int LabelIndexForLayer(SparseTileLayer layer)
        {
            foreach (var kv in _layers)
                if (ReferenceEquals(kv.Value, layer)) return kv.Key;
            return -1;
        }

        #region Diff 기록 인프라 — Undo/Redo 메모리 절감 (SnapshotFull 200MB → 변경 픽셀 수십 KB)

        /// <summary>diff 기록 시작. 이전 인스턴스 메타와 NextId 를 캡처. 픽셀은 건드리지 않음.</summary>
        public void BeginDiffRecording()
        {
            _diffEntries = new List<MaskLayerDiff.PixelEntry>();
            _diffInstancesBefore = SnapshotInstanceRecords();
            _diffNextIdBefore = _instances.PeekNextId();
            _diffMinX = int.MaxValue; _diffMinY = int.MaxValue;
            _diffMaxX = int.MinValue; _diffMaxY = int.MinValue;
        }

        /// <summary>diff 기록 종료 — 모인 픽셀 변경 + 인스턴스 메타 변화로 MaskLayerDiff 빌드. recording 안 켜져 있으면 null.</summary>
        public MaskLayerDiff? EndDiffRecording()
        {
            if (_diffEntries == null || _diffInstancesBefore == null) return null;

            var entries = _diffEntries;
            var before = _diffInstancesBefore;
            var nextIdBefore = _diffNextIdBefore;

            // before/after 비교는 단순화해서 둘 다 InstanceDelta 에 담아 둠 — 호출 측에서 Restore 시 사용.
            var after = SnapshotInstanceRecords();
            var nextIdAfter = _instances.PeekNextId();
            var delta = new MaskLayerDiff.InstanceDelta(before, after, nextIdBefore, nextIdAfter);

            int minX = _diffMinX == int.MaxValue ? 0 : _diffMinX;
            int minY = _diffMinY == int.MaxValue ? 0 : _diffMinY;
            int maxX = _diffMaxX == int.MinValue ? -1 : _diffMaxX;
            int maxY = _diffMaxY == int.MinValue ? -1 : _diffMaxY;

            // recording 종료.
            _diffEntries = null;
            _diffInstancesBefore = null;
            _diffNextIdBefore = 0;

            // RLE 압축 — 같은 (label, oldId, newId) + pixelIndex 연속 entries 를 한 run 으로 묶음.
            // brush stroke 1.5M entries (24 MB) → 보통 수백 runs (수 KB) 로 1000× 절감.
            var runs = CompressEntriesToRuns(entries);
            return new MaskLayerDiff(runs, delta, minX, minY, maxX, maxY);
        }

        /// <summary>PixelEntry[] → RLE PixelRun[] 압축. (label, oldId, newId) 별로 그룹화 후 pixelIndex 정렬, 연속 묶음.
        /// 같은 (label, idx) 가 여러 번 변경되면 (예: brush 가 같은 자리 재방문, 또는 stroke 후 RemapInstanceIdsInLayer 가 재변경)
        /// 순 효과 (firstOldId → lastNewId) 만 남기는 dedup 단계 선행.</summary>
        private static List<MaskLayerDiff.PixelRun> CompressEntriesToRuns(List<MaskLayerDiff.PixelEntry> entries)
        {
            var runs = new List<MaskLayerDiff.PixelRun>();
            if (entries.Count == 0) return runs;

            // Dedup: (label, idx) → (firstOldId, lastNewId). 순 효과만 남김.
            // 같은 픽셀에 (A→B) 후 (B→C) 가 들어오면 (A→C) 하나로 합침. RLE 정렬 후 순서 의존성 회피 + 메모리 추가 절감.
            var dedup = new Dictionary<(int label, int idx), (uint old, uint cur)>(entries.Count);
            foreach (var e in entries)
            {
                var key = (e.LabelIndex, e.PixelIndex);
                if (dedup.TryGetValue(key, out var v))
                    dedup[key] = (v.old, e.NewId);
                else
                    dedup[key] = (e.OldId, e.NewId);
            }

            // dedup 결과를 일시적인 PixelEntry 리스트로 재구성. oldId == newId (no-op) 는 제거.
            var deduped = new List<MaskLayerDiff.PixelEntry>(dedup.Count);
            foreach (var kv in dedup)
            {
                if (kv.Value.old == kv.Value.cur) continue;
                deduped.Add(new MaskLayerDiff.PixelEntry(kv.Key.label, kv.Key.idx, kv.Value.old, kv.Value.cur));
            }
            if (deduped.Count == 0) return runs;

            // 정렬 키: (label, oldId, newId, pixelIndex). 같은 (label,old,new) 안에서 pixelIndex 순.
            deduped.Sort((a, b) =>
            {
                int c = a.LabelIndex.CompareTo(b.LabelIndex);
                if (c != 0) return c;
                c = a.OldId.CompareTo(b.OldId);
                if (c != 0) return c;
                c = a.NewId.CompareTo(b.NewId);
                if (c != 0) return c;
                return a.PixelIndex.CompareTo(b.PixelIndex);
            });

            int i = 0;
            while (i < deduped.Count)
            {
                var first = deduped[i];
                int runStart = first.PixelIndex;
                int runEnd = first.PixelIndex; // inclusive
                int j = i + 1;
                while (j < deduped.Count)
                {
                    var e = deduped[j];
                    if (e.LabelIndex != first.LabelIndex) break;
                    if (e.OldId != first.OldId) break;
                    if (e.NewId != first.NewId) break;
                    if (e.PixelIndex != runEnd + 1) break; // 연속 끊김.
                    runEnd = e.PixelIndex;
                    j++;
                }
                runs.Add(new MaskLayerDiff.PixelRun(
                    first.LabelIndex, runStart, runEnd - runStart + 1, first.OldId, first.NewId));
                i = j;
            }
            return runs;
        }

        /// <summary>recording 모드일 때 픽셀 변경을 기록. 비-recording 모드면 즉시 return — 비용 거의 0.</summary>
        private void RecordPixelChange(int labelIndex, int pixelIndex, uint oldId, uint newId)
        {
            if (_diffEntries == null) return;
            if (oldId == newId) return;
            _diffEntries.Add(new MaskLayerDiff.PixelEntry(labelIndex, pixelIndex, oldId, newId));
            int x = pixelIndex % _width;
            int y = pixelIndex / _width;
            if (x < _diffMinX) _diffMinX = x;
            if (x > _diffMaxX) _diffMaxX = x;
            if (y < _diffMinY) _diffMinY = y;
            if (y > _diffMaxY) _diffMaxY = y;
        }

        /// <summary>현재 인스턴스 컬렉션을 InstanceRecord 리스트로 캡처 (메타데이터만, 깊은 복사).</summary>
        private List<MaskLayerSnapshot.InstanceRecord> SnapshotInstanceRecords()
        {
            var list = new List<MaskLayerSnapshot.InstanceRecord>(_instances.Count);
            foreach (var i in _instances)
            {
                list.Add(new MaskLayerSnapshot.InstanceRecord(
                    i.Id, i.LabelIndex, i.BoundingBox, i.PixelCount, i.IsVisible,
                    CloneContours(i.PolygonContours)));
            }
            return list;
        }

        /// <summary>diff 의 NewId 로 forward 적용 (Redo). 픽셀 + 인스턴스 메타 모두.</summary>
        public void ApplyDiffForward(MaskLayerDiff diff)
        {
            if (diff == null) return;
            ApplyPixelEntries(diff, forward: true);
            RestoreInstanceRecords(diff.Instances.After, diff.Instances.NextIdAfter);
            RefreshDisplayForDiff(diff);
        }

        /// <summary>diff 의 OldId 로 reverse 적용 (Undo). 픽셀 + 인스턴스 메타 모두.</summary>
        public void ApplyDiffReverse(MaskLayerDiff diff)
        {
            if (diff == null) return;
            ApplyPixelEntries(diff, forward: false);
            RestoreInstanceRecords(diff.Instances.Before, diff.Instances.NextIdBefore);
            RefreshDisplayForDiff(diff);
        }

        private void ApplyPixelEntries(MaskLayerDiff diff, bool forward)
        {
            // Entries (legacy 호환) 적용.
            foreach (var e in diff.Entries)
            {
                if (e.LabelIndex < 0) continue;
                var layer = GetOrCreateLayer(e.LabelIndex);
                if ((uint)e.PixelIndex >= (uint)layer.Length) continue;
                layer[e.PixelIndex] = forward ? e.NewId : e.OldId;
            }
            // Runs (RLE 압축) 적용 — 한 run 의 pixelIndex 연속 구간을 한 번에 채움.
            foreach (var r in diff.Runs)
            {
                if (r.LabelIndex < 0 || r.Length <= 0) continue;
                var layer = GetOrCreateLayer(r.LabelIndex);
                int end = r.StartIndex + r.Length;
                if (end > layer.Length) end = layer.Length;
                int start = r.StartIndex < 0 ? 0 : r.StartIndex;
                uint val = forward ? r.NewId : r.OldId;
                for (int i = start; i < end; i++) layer[i] = val;
            }
        }

        private void RestoreInstanceRecords(IReadOnlyList<MaskLayerSnapshot.InstanceRecord> records, uint nextId)
        {
            // 단순 정책: 컬렉션 비우고 records 로 재구성. INPC 폭증을 피하려면 diff 기반 add/remove 가 더 좋지만 본 작업 범위 외.
            _instances.Clear();
            foreach (var r in records)
            {
                var inst = new MaskInstance
                {
                    Id = r.Id,
                    LabelIndex = r.LabelIndex,
                    BoundingBox = r.BoundingBox,
                    PixelCount = r.PixelCount,
                    IsVisible = r.IsVisible,
                    PolygonContours = CloneContoursMutable(r.PolygonContours),
                };
                AssignLabelRef(inst);
                _instances.Add(inst);
            }
            _instances.EnsureNextIdAtLeast(nextId);
        }

        private void RefreshDisplayForDiff(MaskLayerDiff diff)
        {
            if (!diff.HasPixelChanges) { InvalidateVisual(); return; }
            int x = Math.Max(0, diff.StrokeMinX);
            int y = Math.Max(0, diff.StrokeMinY);
            int w = Math.Min(_width - x, diff.StrokeMaxX - diff.StrokeMinX + 1);
            int h = Math.Min(_height - y, diff.StrokeMaxY - diff.StrokeMinY + 1);
            if (w > 0 && h > 0) UpdateDisplayRect(x, y, w, h); else RefreshAll();
        }

        // CloneContours / CloneContoursMutable 은 Snapshot 영역 (이 파일 하단) 에 이미 정의되어 있으므로 여기선 재사용.

        #endregion

        public void RecomputeInstanceMetadata(uint id)
        {
            var inst = _instances.GetById(id);
            if (inst == null) return;
            if (!_layers.TryGetValue(inst.LabelIndex, out var mask))
            {
                inst.PixelCount = 0;
                inst.BoundingBox = Rect.Empty;
                return;
            }

            // Sparse 친화적 — 모든 alloc 된 타일만 enumerate (빈 타일 skip), inner loop 는 dense.
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int count = 0;
            foreach (var (tileX, tileY, tile) in mask.EnumerateAllocatedTiles())
            {
                int tileBaseX = tileX * SparseTileLayer.TileSize;
                int tileBaseY = tileY * SparseTileLayer.TileSize;
                int yEnd = Math.Min(SparseTileLayer.TileSize, _height - tileBaseY);
                int xEnd = Math.Min(SparseTileLayer.TileSize, _width - tileBaseX);
                for (int ly = 0; ly < yEnd; ly++)
                {
                    int tileRowBase = ly * SparseTileLayer.TileSize;
                    int gy = tileBaseY + ly;
                    for (int lx = 0; lx < xEnd; lx++)
                    {
                        if (tile[tileRowBase + lx] != id) continue;
                        int gx = tileBaseX + lx;
                        if (gx < minX) minX = gx;
                        if (gx > maxX) maxX = gx;
                        if (gy < minY) minY = gy;
                        if (gy > maxY) maxY = gy;
                        count++;
                    }
                }
            }
            inst.PixelCount = count;
            if (count == 0)
            {
                inst.BoundingBox = Rect.Empty;
            }
            else
            {
                var bbox = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                var imgBounds = new Rect(0, 0, _width, _height);
                bbox.Intersect(imgBounds);
                inst.BoundingBox = bbox.IsEmpty ? Rect.Empty : bbox;
            }
        }

        private void SplitIfDisconnected(uint id)
        {
            var inst = _instances.GetById(id);
            if (inst == null || inst.PixelCount == 0) return;
            if (!_layers.TryGetValue(inst.LabelIndex, out var mask)) return;

            var bbox = inst.BoundingBox;
            if (bbox.IsEmpty) return;
            int bx = (int)bbox.X, by = (int)bbox.Y;
            int bw = (int)bbox.Width, bh = (int)bbox.Height;

            var visited = new bool[bw * bh];
            var components = new List<List<int>>();
            var queue = new Queue<(int x, int y)>();

            for (int y = by; y < by + bh; y++)
            {
                for (int x = bx; x < bx + bw; x++)
                {
                    int localIdx = (y - by) * bw + (x - bx);
                    if (visited[localIdx]) continue;
                    if (mask.Get(x, y) != id) continue;

                    var comp = new List<int>();
                    queue.Clear();
                    queue.Enqueue((x, y));
                    visited[localIdx] = true;

                    void TryPush(int nx, int ny)
                    {
                        if (nx < bx || nx >= bx + bw || ny < by || ny >= by + bh) return;
                        int nLocal = (ny - by) * bw + (nx - bx);
                        if (visited[nLocal]) return;
                        if (mask.Get(nx, ny) != id) return;
                        visited[nLocal] = true;
                        queue.Enqueue((nx, ny));
                    }

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        comp.Add(cy * _width + cx);
                        TryPush(cx + 1, cy);
                        TryPush(cx - 1, cy);
                        TryPush(cx, cy + 1);
                        TryPush(cx, cy - 1);
                    }
                    components.Add(comp);
                }
            }

            if (components.Count <= 1) return;

            for (int c = 1; c < components.Count; c++)
            {
                var newId = _instances.NextId();
                foreach (var gi in components[c])
                {
                    int gx = gi % _width;
                    int gy = gi / _width;
                    RecordPixelChange(inst.LabelIndex, gi, id, newId);
                    mask.Set(gx, gy, newId);
                }
                var newInst = new MaskInstance { Id = newId, LabelIndex = inst.LabelIndex };
                AssignLabelRef(newInst);
                _instances.Add(newInst);
                RecomputeInstanceMetadata(newId);
            }
            RecomputeInstanceMetadata(id);
            // 분리 발생 — 원래 id 의 PolygonContours 가 부분만 표현. 무효화 → 다음 더블클릭 시 재추출.
            inst.PolygonContours = null;
        }

        #endregion

        #region Public API — Memory management

        /// <summary>가벼운 메모리 회수 — LOH compact + GC. 큰 작업 후 자동 호출되지만 사용자가 명시 호출도 가능.</summary>
        public void TrimMemory() => MemoryHelper.CompactAndCollect();

        /// <summary>Aggressive 메모리 회수 — LOH compact + Gen2 + Working Set 트림. 이미지 unload / 프로젝트 전환 시 사용.</summary>
        public void ForceMemoryCleanup() => MemoryHelper.ForceFullCleanup();

        #endregion

        #region Public API — Snapshot / Restore

        public byte[] Snapshot()
        {
            // 하위 호환 (deprecated): 가장 큰 라벨 인덱스 픽셀을 byte 로 근사.
            var arr = new byte[_width * _height];
            foreach (var (lbl, mask) in _layers.OrderBy(k => k.Key))
            {
                for (int i = 0; i < arr.Length; i++)
                    if (mask[i] != 0) arr[i] = (byte)lbl;
            }
            return arr;
        }

        public void Restore(byte[] snapshot)
        {
            // 하위 호환: 단일 라벨 이미지를 복원. 인스턴스는 CCL 로 재구성.
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length != _width * _height)
                throw new ArgumentException("스냅샷 크기가 현재 마스크와 일치하지 않습니다.");
            _layers.Clear();
            _instances.Clear();
            var byLabel = new Dictionary<int, SparseTileLayer>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                byte lbl = snapshot[i];
                if (lbl == 0) continue;
                if (!byLabel.TryGetValue(lbl, out var mask))
                {
                    mask = new SparseTileLayer(_width, _height);
                    byLabel[lbl] = mask;
                }
            }
            foreach (var kv in byLabel) _layers[kv.Key] = kv.Value;
            // 각 라벨마다 CCL 로 인스턴스 재구성.
            foreach (var (lbl, _) in byLabel) RebuildInstancesForLayer(lbl, snapshot);
            RefreshAll();
        }

        private void RebuildInstancesForLayer(int labelIndex, byte[] labelIndexMap)
        {
            var mask = _layers[labelIndex];
            var visited = new bool[_width * _height];
            var queue = new Queue<(int x, int y)>();
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    if (visited[idx]) continue;
                    if (labelIndexMap[idx] != labelIndex) { visited[idx] = true; continue; }

                    var newId = _instances.NextId();
                    queue.Clear();
                    queue.Enqueue((x, y));
                    visited[idx] = true;
                    int minX = x, minY = y, maxX = x, maxY = y, count = 0;
                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        int ci = cy * _width + cx;
                        mask[ci] = newId;
                        count++;
                        if (cx < minX) minX = cx;
                        if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy;
                        if (cy > maxY) maxY = cy;
                        void TryPush(int nx, int ny)
                        {
                            if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) return;
                            int ni = ny * _width + nx;
                            if (visited[ni]) return;
                            if (labelIndexMap[ni] != labelIndex) return;
                            visited[ni] = true;
                            queue.Enqueue((nx, ny));
                        }
                        TryPush(cx + 1, cy);
                        TryPush(cx - 1, cy);
                        TryPush(cx, cy + 1);
                        TryPush(cx, cy - 1);
                    }
                    var inst = new MaskInstance
                    {
                        Id = newId, LabelIndex = labelIndex, PixelCount = count,
                        BoundingBox = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    };
                    AssignLabelRef(inst);
                    _instances.Add(inst);
                }
            }
        }

        public MaskLayerSnapshot SnapshotFull()
        {
            var layers = new Dictionary<int, SparseTileLayer>(_layers.Count);
            foreach (var kv in _layers)
            {
                layers[kv.Key] = kv.Value.Clone(); // 빈 타일 자동 skip — 면적 비례 alloc.
            }
            var records = _instances.Select(i =>
                new MaskLayerSnapshot.InstanceRecord(i.Id, i.LabelIndex, i.BoundingBox, i.PixelCount, i.IsVisible,
                    CloneContours(i.PolygonContours)))
                .ToList();
            uint maxId = 0;
            foreach (var i in _instances) if (i.Id > maxId) maxId = i.Id;
            return new MaskLayerSnapshot(layers, records, maxId);
        }

        public void RestoreFull(MaskLayerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _layers.Clear();
            foreach (var kv in snapshot.Layers)
            {
                if (kv.Value.Width != _width || kv.Value.Height != _height)
                    throw new ArgumentException("스냅샷 레이어 크기가 현재 마스크와 일치하지 않습니다.");
                _layers[kv.Key] = kv.Value.Clone(); // 깊은 복사 — 외부에서 snapshot 변경해도 영향 없게.
            }
            _instances.Clear();
            foreach (var r in snapshot.Instances)
            {
                var inst = new MaskInstance
                {
                    Id = r.Id, LabelIndex = r.LabelIndex,
                    BoundingBox = r.BoundingBox, PixelCount = r.PixelCount, IsVisible = r.IsVisible,
                    PolygonContours = CloneContoursMutable(r.PolygonContours),
                };
                AssignLabelRef(inst);
                _instances.Add(inst);
            }
            _instances.EnsureNextIdAtLeast(snapshot.NextId);
            RefreshAll();

            // 큰 snapshot 복원 (Undo/Redo) 후 LOH 회수.
            MemoryHelper.CompactAndCollect();
        }

        /// <summary>Snapshot 용 read-only 깊은 복사. null/empty 는 null 반환.</summary>
        private static IReadOnlyList<IReadOnlyList<Point>>? CloneContours(IList<IList<Point>>? src)
        {
            if (src == null || src.Count == 0) return null;
            var outer = new List<IReadOnlyList<Point>>(src.Count);
            foreach (var c in src) outer.Add(new List<Point>(c));
            return outer;
        }

        /// <summary>Restore 용 mutable 깊은 복사. null/empty 는 null 반환.</summary>
        private static IList<IList<Point>>? CloneContoursMutable(IReadOnlyList<IReadOnlyList<Point>>? src)
        {
            if (src == null || src.Count == 0) return null;
            var outer = new List<IList<Point>>(src.Count);
            foreach (var c in src) outer.Add(new List<Point>(c));
            return outer;
        }

        /// <summary>표준 ray-casting point-in-polygon. 폐곡선 가정 (마지막 점 → 첫 점 implicit close).</summary>
        private static bool IsPointInPolygon(Point p, IReadOnlyList<Point> polygon)
        {
            int n = polygon.Count;
            if (n < 3) return false;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.Y > p.Y) != (b.Y > p.Y))
                {
                    double xCross = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
                    if (p.X < xCross) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 더블클릭 시 호출. 첫 호출 시 마스크 픽셀에서 외곽 contour 를 추출 (촘촘하게).
        /// **이미 PolygonContours 가 있으면 보존** — 사용자가 vertex 끌어 commit 한 미세 편집이 다음 더블클릭에 사라지지 않게.
        /// 마스크 픽셀이 외부에서 변경되면 (브러시/지우개/resample/merge 등) RepaintPolygon 등 commit 경로에서 PolygonContours
        /// 도 동기화되거나 무효화. 이 EnsurePolygonPoints 는 vertex 편집 진입 트리거이지 마스크 변경 후 강제 동기화 트리거가 아님.
        /// </summary>
        public bool EnsurePolygonPoints(uint id)
        {
            var inst = _instances.GetById(id);
            if (inst == null) return false;

            // 이미 PolygonContours 가 있고 외곽이 ≥3 점이면 그대로 사용 (사용자 편집 보존).
            if (inst.PolygonContours != null && inst.PolygonContours.Count > 0
                && inst.PolygonContours[0] != null && inst.PolygonContours[0].Count >= 3)
            {
                return true;
            }

            if (!_layers.TryGetValue(inst.LabelIndex, out var mask)) return false;
            var bb = inst.BoundingBox;
            if (bb.IsEmpty) return false;

            int bx = Math.Max(0, (int)Math.Floor(bb.X));
            int by = Math.Max(0, (int)Math.Floor(bb.Y));
            int bw = Math.Min(_width - bx, (int)Math.Ceiling(bb.X + bb.Width) - bx);
            int bh = Math.Min(_height - by, (int)Math.Ceiling(bb.Y + bb.Height) - by);
            if (bw <= 0 || bh <= 0) return false;

            var contours = ContourTracing.ExtractContoursAll(mask, _width, _height, id, bx, by, bw, bh);
            if (contours.Count == 0) return false;

            // 가장 긴 폐곡선 = 외곽. 나머지는 외곽 안에 들어있으면 hole, 아니면 무시(분리 컴포넌트).
            contours.Sort((a, b) => b.Count.CompareTo(a.Count));
            var outer = contours[0];
            if (outer.Count < 3) return false;

            var result = new List<IList<Point>>();
            var simplifiedOuter = SimplifyOne(outer);
            if (simplifiedOuter.Count < 3) return false;
            result.Add(simplifiedOuter);

            // 외곽 단순화는 끝났으나 hole 판정은 raw outer (촘촘한 점) 으로 해야 정확.
            for (int i = 1; i < contours.Count; i++)
            {
                var c = contours[i];
                if (c.Count < 3) continue;
                if (!IsPointInPolygon(c[0], outer)) continue; // 외곽 안 hole 만 채택.
                var simplifiedHole = SimplifyOne(c);
                if (simplifiedHole.Count < 3) continue;
                result.Add(simplifiedHole);
            }

            inst.PolygonContours = result;
            return true;
        }

        /// <summary>외곽/hole 한 contour 를 DP 로 약하게 단순화 — 사용자 미세 편집 여지를 위해 점을 촘촘히 유지.
        /// 코너만 제거하지 않고 직선 위 점도 일부 보존. 너무 많아지면 점진 단순화.</summary>
        private static List<Point> SimplifyOne(List<Point> contour)
        {
            // 약한 단순화 — corner-aligned 0.5 offset 의 계단만 흡수, 직선 위 점은 그대로.
            const double Epsilon = 0.6;
            var simplified = PolygonSimplifier.SimplifyClosed(contour, Epsilon);
            if (simplified.Count < 3)
            {
                int n = Math.Min(8, contour.Count);
                var pts = new List<Point>(n);
                double step = (double)contour.Count / n;
                for (int k = 0; k < n; k++) pts.Add(contour[(int)(k * step)]);
                return pts;
            }
            // 너무 많은 점은 시각 혼잡 — Adorner 핸들이 겹침. 1000 초과면 점진 단순화.
            if (simplified.Count > 1000)
            {
                return PolygonSimplifier.SimplifyClosed(contour, Epsilon * 2.0);
            }
            return simplified;
        }

        /// <summary>
        /// 지정 인스턴스의 픽셀을 모두 지우고 새 polygon 으로 재그린다. ID 는 유지.
        /// 다른 라벨의 영역은 건드리지 않으며, 같은 라벨 내 다른 인스턴스와 겹쳐도 이번 라운드는 덮어씀.
        /// Vertex 편집 commit 경로.
        /// </summary>
        public void RepaintPolygon(uint instanceId, IReadOnlyList<System.Windows.Point> newPoints)
        {
            if (newPoints == null || newPoints.Count < 3) return;
            RepaintPolygon(instanceId, new[] { newPoints });
        }

        /// <summary>
        /// 다중 contour 버전 — 외곽 + hole 들을 even-odd rasterize 로 한 번에 그림.
        /// contours[0] = 외곽, ≥1 = hole. 각 contour 는 ≥3 점 필요.
        /// </summary>
        public void RepaintPolygon(uint instanceId, IReadOnlyList<IReadOnlyList<System.Windows.Point>> newContours)
        {
            var inst = _instances.GetById(instanceId);
            if (inst == null || newContours == null || newContours.Count == 0) return;
            // 외곽은 필수, hole 은 선택. 외곽 < 3 이면 무시.
            if (newContours[0] == null || newContours[0].Count < 3) return;
            int label = inst.LabelIndex;
            var layer = GetOrCreateLayer(label);

            // 1) 기존 픽셀 clear (해당 라벨 레이어 내 id==instanceId 만).
            var oldBB = inst.BoundingBox;
            int x0 = 0, y0 = 0, x1 = _width - 1, y1 = _height - 1;
            if (!oldBB.IsEmpty)
            {
                x0 = Math.Max(0, (int)oldBB.X);
                y0 = Math.Max(0, (int)oldBB.Y);
                x1 = Math.Min(_width - 1, (int)(oldBB.X + oldBB.Width));
                y1 = Math.Min(_height - 1, (int)(oldBB.Y + oldBB.Height));
            }
            // BBox 와 겹치는 alloc 된 타일만 enumerate (빈 타일 skip), inner loop dense.
            foreach (var (tileX, tileY, tile) in layer.EnumerateTilesInBox(x0, y0, x1, y1))
            {
                int tileBaseX = tileX * SparseTileLayer.TileSize;
                int tileBaseY = tileY * SparseTileLayer.TileSize;
                int sxLo = Math.Max(x0, tileBaseX);
                int syLo = Math.Max(y0, tileBaseY);
                int sxHi = Math.Min(x1, tileBaseX + SparseTileLayer.TileSize - 1);
                int syHi = Math.Min(y1, tileBaseY + SparseTileLayer.TileSize - 1);
                for (int gy = syLo; gy <= syHi; gy++)
                {
                    int ly = gy - tileBaseY;
                    int tileRowBase = ly * SparseTileLayer.TileSize;
                    int gRow = gy * _width;
                    for (int gx = sxLo; gx <= sxHi; gx++)
                    {
                        int lx = gx - tileBaseX;
                        if (tile[tileRowBase + lx] == instanceId)
                        {
                            RecordPixelChange(label, gRow + gx, instanceId, 0);
                            tile[tileRowBase + lx] = 0;
                        }
                    }
                }
            }


            // 2) 모든 contour 를 flat 배열로 변환해 even-odd rasterize 한 번에 호출.
            var flatList = new List<IReadOnlyList<double>>(newContours.Count);
            foreach (var pts in newContours)
            {
                if (pts == null || pts.Count < 3) continue;
                var flat = new double[pts.Count * 2];
                for (int i = 0; i < pts.Count; i++)
                {
                    flat[i * 2] = pts[i].X;
                    flat[i * 2 + 1] = pts[i].Y;
                }
                flatList.Add(flat);
            }
            RasterizePolygonIntoLayer(layer, flatList, instanceId);

            // 3) PolygonContours 업데이트 + 메타 재계산 + 렌더 갱신.
            var stored = new List<IList<System.Windows.Point>>(newContours.Count);
            foreach (var pts in newContours)
            {
                if (pts == null || pts.Count < 3) continue;
                stored.Add(new List<System.Windows.Point>(pts));
            }
            inst.PolygonContours = stored;
            RecomputeInstanceMetadata(instanceId);
            RefreshAll();
        }

        #endregion

        #region Public API — PNG (deprecated, 하위 호환용 stub)

        public void ExportPng(string path)
        {
            if (_width == 0 || _height == 0) return;
            var arr = Snapshot();
            var labelBitmap = BitmapSource.Create(_width, _height, 96, 96, PixelFormats.Gray8, null, arr, _width);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(labelBitmap));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }

        public void ImportPng(string path)
        {
            using var fs = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Gray8, null, 0);
            if (converted.PixelWidth != _width || converted.PixelHeight != _height)
                throw new InvalidOperationException("PNG 크기가 현재 마스크와 일치하지 않습니다.");
            var arr = new byte[_width * _height];
            converted.CopyPixels(arr, _width, 0);
            Restore(arr);
        }

        /// <summary>
        /// 지정 인스턴스의 픽셀 실루엣만 라벨 색상으로 그린 WriteableBitmap 을 반환한다.
        /// 크기는 인스턴스의 BoundingBox(픽셀). 인스턴스 외부 픽셀은 완전 투명.
        /// Adorner 의 리사이즈/이동 프리뷰용.
        /// </summary>
        public ImageSource? GetInstanceSilhouette(uint id)
        {
            var inst = _instances.GetById(id);
            if (inst == null) return null;
            if (!_layers.TryGetValue(inst.LabelIndex, out var mask)) return null;
            var bbox = inst.BoundingBox;
            if (bbox.IsEmpty) return null;

            int bx = (int)bbox.X, by = (int)bbox.Y;
            int bw = (int)bbox.Width, bh = (int)bbox.Height;
            if (bw <= 0 || bh <= 0) return null;

            // 큰 BBox 는 다운샘플 — DrawImage 가 NearestNeighbor 로 dest rect 에 자동 stretch 하므로 시각 품질 동일.
            // 8K 전면 BBox (8192×6144) 도 출력 ~1024×768 → 3 MB alloc, 100ms 이하.
            const int MaxDim = 1024;
            int scale = Math.Max(1, (Math.Max(bw, bh) + MaxDim - 1) / MaxDim);
            int outW = Math.Max(1, bw / scale);
            int outH = Math.Max(1, bh / scale);

            var color = inst.Label?.Color ?? Labels?.GetByIndex(inst.LabelIndex)?.Color ?? Colors.Yellow;
            var bmp = new WriteableBitmap(outW, outH, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[outW * outH * 4];
            for (int oy = 0; oy < outH; oy++)
            {
                int gy = by + oy * scale;
                if ((uint)gy >= (uint)_height) continue;
                int rowStart = gy * _width;
                int dstRow = oy * outW * 4;
                for (int ox = 0; ox < outW; ox++)
                {
                    int gx = bx + ox * scale;
                    if ((uint)gx >= (uint)_width) continue;
                    if (mask[rowStart + gx] != id) continue;
                    int di = dstRow + ox * 4;
                    pixels[di + 0] = color.B;
                    pixels[di + 1] = color.G;
                    pixels[di + 2] = color.R;
                    pixels[di + 3] = color.A;
                }
            }
            bmp.WritePixels(new Int32Rect(0, 0, outW, outH), pixels, outW * 4, 0);
            bmp.Freeze();
            return bmp;
        }

        #endregion

        #region Public API — COCO JSON I/O (layer-aware)

        public void ExportCocoJson(string path, string imageFileName = "")
        {
            if (_width == 0 || _height == 0) return;
            var doc = new CocoDocument
            {
                Images = { new CocoImage { Id = 1, Width = _width, Height = _height, FileName = imageFileName ?? "" } }
            };
            if (Labels != null)
            {
                foreach (var lbl in Labels)
                {
                    if (lbl.Index == LabelClassCollection.BackgroundIndex) continue;
                    doc.Categories.Add(new CocoCategory
                    {
                        Id = lbl.Index, Name = lbl.Name,
                        Color = $"#{lbl.Color.R:X2}{lbl.Color.G:X2}{lbl.Color.B:X2}",
                    });
                }
            }
            foreach (var inst in _instances)
            {
                if (!_layers.TryGetValue(inst.LabelIndex, out var mask)) continue;
                // pycocotools 호환 compressed RLE 로 segmentation 기록.
                // 1) bitmap → column-major run-lengths (uncompressed).
                // 2) run-lengths → LEB128-variant ASCII 문자열.
                var rawCounts = RleCodec.Encode(mask, _width, _height, inst.Id);
                var countsStr = CompressedRleCodec.Encode(rawCounts);
                var b = inst.BoundingBox;
                doc.Annotations.Add(new CocoAnnotation
                {
                    Id = inst.Id, ImageId = 1, CategoryId = inst.LabelIndex,
                    Segmentation = new CocoCompressedRle
                    {
                        Size = new List<int> { _height, _width },
                        Counts = countsStr,
                    },
                    // Rle/polygon 보조 필드는 기록하지 않음 (pycocotools 표준 부합, 파일 크기 감소).
                    Bbox = new List<double> { b.X, b.Y, b.Width, b.Height },
                    Area = inst.PixelCount,
                });
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                // null 속성(legacy Rle, null Color 등)을 JSON 에 출력하지 않음. pycocotools 표준 스키마와 정확히 일치.
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(doc, options));
        }

        public void ImportCocoJson(string path)
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<CocoDocument>(json);
            if (doc == null) return;
            var img = doc.Images.FirstOrDefault();
            if (img != null && (img.Width != _width || img.Height != _height))
                throw new InvalidOperationException("COCO image 크기가 현재 마스크와 일치하지 않습니다.");

            _layers.Clear();
            _instances.Clear();

            if (Labels != null)
            {
                foreach (var cat in doc.Categories)
                {
                    if (Labels.GetByIndex(cat.Id) != null) continue;
                    var color = ParseHexColor(cat.Color);
                    Labels.Add(cat.Name, color);
                }
            }

            uint maxId = 0;
            foreach (var ann in doc.Annotations)
            {
                int label = ann.CategoryId;
                uint id = ann.Id;
                if (id > maxId) maxId = id;
                var layer = GetOrCreateLayer(label);

                // 우선순위: compressed RLE(표준) → legacy rle(int[]) → polygon.
                if (ann.Segmentation is CocoCompressedRle cRle && !string.IsNullOrEmpty(cRle.Counts))
                {
                    var ints = CompressedRleCodec.Decode(cRle.Counts);
                    var decoded = RleCodec.Decode(ints, _width, _height, id);
                    for (int i = 0; i < decoded.Length; i++)
                        if (decoded[i] != 0) layer[i] = id;
                }
                else if (ann.Rle != null && ann.Rle.Counts.Count > 0)
                {
                    var decoded = RleCodec.Decode(ann.Rle.Counts, _width, _height, id);
                    for (int i = 0; i < decoded.Length; i++)
                        if (decoded[i] != 0) layer[i] = id;
                }
                else if (ann.Segmentation is List<List<double>> polys)
                {
                    foreach (var poly in polys) RasterizePolygonIntoLayer(layer, poly, id);
                }

                var inst = new MaskInstance { Id = id, LabelIndex = label };
                AssignLabelRef(inst);
                _instances.Add(inst);
                RecomputeInstanceMetadata(id);
            }
            _instances.EnsureNextIdAtLeast(maxId);
            RefreshAll();
        }

        private static Color ParseHexColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Gray;
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return Colors.Gray; }
        }

        /// <summary>단일 contour 오버로드 — 다중 오버로드로 위임.</summary>
        private void RasterizePolygonIntoLayer(SparseTileLayer layer, IReadOnlyList<double> flat, uint id)
        {
            RasterizePolygonIntoLayer(layer, new[] { flat }, id);
        }

        /// <summary>
        /// 다중 contour rasterize. even-odd scanline fill — 외곽 + hole 자동 처리.
        /// 각 contour 는 평면 [x0,y0,x1,y1,...] 형태. contour 별로 j=(i+1)%n_c 로 폐쇄, 모든 contour 의 edge 교차점을
        /// 한 xs 리스트에 모아 정렬 → 짝수 진입/홀수 종료 = WPF FillRule.EvenOdd 와 동치.
        /// </summary>
        private void RasterizePolygonIntoLayer(SparseTileLayer layer, IReadOnlyList<IReadOnlyList<double>> contours, uint id)
        {
            // y 범위 = 모든 contour 합집합.
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            int totalPoints = 0;
            foreach (var c in contours)
            {
                if (c.Count < 6) continue;
                totalPoints += c.Count / 2;
                for (int i = 0; i < c.Count; i += 2)
                {
                    double y = c[i + 1];
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (totalPoints < 3) return;
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(_height - 1, (int)Math.Ceiling(maxY));
            for (int y = y0; y <= y1; y++)
            {
                var xs = new List<double>();
                double cy = y + 0.5;
                foreach (var flat in contours)
                {
                    if (flat.Count < 6) continue;
                    int n = flat.Count / 2;
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n; // contour 내부에서만 wrap — 외곽-hole 사이 가짜 edge 방지.
                        double ay = flat[i * 2 + 1], by = flat[j * 2 + 1];
                        if ((ay <= cy && by > cy) || (by <= cy && ay > cy))
                        {
                            double ax = flat[i * 2], bx = flat[j * 2];
                            double t = (cy - ay) / (by - ay);
                            xs.Add(ax + t * (bx - ax));
                        }
                    }
                }
                xs.Sort();
                int labelIdx = LabelIndexForLayer(layer);
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Math.Max(0, (int)Math.Floor(xs[k]));
                    int xb = Math.Min(_width - 1, (int)Math.Ceiling(xs[k + 1]));
                    int row = y * _width;
                    for (int x = xa; x <= xb; x++)
                    {
                        // (x, y) 직접 호출 — modulo/division 2번 회피. tile cache 도 같은 타일 연속 access 시 효과.
                        uint prev = layer.Get(x, y);
                        if (prev != id) RecordPixelChange(labelIdx, row + x, prev, id);
                        layer.Set(x, y, id);
                    }
                }
            }
        }

        #endregion

        #region Source image pixel/gradient cache (Magic Wand / Magnetic Lasso)

        /// <summary>SourceImage 의 Bgra32 픽셀 배열 접근 (읽기 전용, null 가능).</summary>
        public byte[]? GetSourcePixels() => _sourcePixels;

        /// <summary>(x, y) 픽셀의 RGB 를 반환. 범위 밖이거나 SourceImage 미설정이면 null.</summary>
        public (byte R, byte G, byte B)? GetSourcePixelRgb(int x, int y)
        {
            if (_sourcePixels == null) return null;
            if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return null;
            int i = (y * _width + x) * 4;
            return (_sourcePixels[i + 2], _sourcePixels[i + 1], _sourcePixels[i + 0]); // Bgra32
        }

        /// <summary>Sobel gradient magnitude 배열 (width*height, null 가능). Magnetic Lasso 첫 사용 시 lazy 빌드.</summary>
        public double[]? GetSourceGradient()
        {
            if (_sourceGradient != null) return _sourceGradient;
            if (_sourcePixels == null) return null;
            BuildSourceGradient();
            return _sourceGradient;
        }

        /// <summary>SourceImage 가 바뀌면 픽셀 캐시만 재구성. Sobel gradient 는 lazy (8K 면 400MB × 2 = 800MB LOH 절감).</summary>
        private void RebuildSourceCaches()
        {
            var src = SourceImage as BitmapSource;
            if (src == null || _width == 0 || _height == 0)
            {
                _sourcePixels = null;
                _sourceGradient = null;
                return;
            }
            // 이미지 크기가 마스크와 다르면 Bgra32 로 변환+리스케일 (nearest).
            BitmapSource toRead = src;
            if (src.PixelWidth != _width || src.PixelHeight != _height || src.Format != PixelFormats.Bgra32)
            {
                // 스케일 + 포맷 변환.
                var scaleX = (double)_width / src.PixelWidth;
                var scaleY = (double)_height / src.PixelHeight;
                var scaled = new TransformedBitmap(src, new ScaleTransform(scaleX, scaleY));
                toRead = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            }
            var buf = new byte[_width * _height * 4];
            toRead.CopyPixels(buf, _width * 4, 0);
            _sourcePixels = buf;

            // gradient cache 는 GetSourceGradient() 가 처음 호출될 때 lazy 빌드.
            _sourceGradient = null;
        }

        /// <summary>Sobel gradient magnitude 빌드. Magnetic Lasso 도구 첫 사용 시에만 호출 — 8K 면 800MB temp + 400MB cache.</summary>
        private void BuildSourceGradient()
        {
            if (_sourcePixels == null || _width == 0 || _height == 0) return;
            var buf = _sourcePixels;

            // Sobel gradient magnitude: gray = 0.299R + 0.587G + 0.114B.
            var gray = new double[_width * _height];
            for (int i = 0; i < gray.Length; i++)
            {
                int p = i * 4;
                gray[i] = 0.114 * buf[p + 0] + 0.587 * buf[p + 1] + 0.299 * buf[p + 2];
            }
            var grad = new double[_width * _height];
            for (int y = 1; y < _height - 1; y++)
            {
                for (int x = 1; x < _width - 1; x++)
                {
                    int idx = y * _width + x;
                    double gx = -gray[idx - _width - 1] - 2 * gray[idx - 1] - gray[idx + _width - 1]
                              + gray[idx - _width + 1] + 2 * gray[idx + 1] + gray[idx + _width + 1];
                    double gy = -gray[idx - _width - 1] - 2 * gray[idx - _width] - gray[idx - _width + 1]
                              + gray[idx + _width - 1] + 2 * gray[idx + _width] + gray[idx + _width + 1];
                    grad[idx] = Math.Sqrt(gx * gx + gy * gy);
                }
            }
            _sourceGradient = grad;
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_displayBitmap == null || _width == 0 || _height == 0) return;
            var rect = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : _width, ActualHeight > 0 ? ActualHeight : _height);
            dc.PushOpacity(OverlayOpacity);
            dc.DrawImage(_displayBitmap, rect);
            dc.Pop();

            // MaskLayer 자신이 zoom 스케일을 상속 → 점선 두께가 zoom 배로 두꺼워짐. 1/zoom 보정.
            double z = 1.0;
            if (RenderTransform is MatrixTransform zmt && zmt.Matrix.M11 > 0.0001) z = zmt.Matrix.M11;

            // 다중 선택(IsSelected=true) 인스턴스들의 BBox 점선 (핸들 없음).
            // SelectedInstance 하나는 Adorner 가 full(실루엣+핸들) 을 그리므로 중복되지만
            // Adorner 도 노란 점선이라 시각적으로 자연스러움.
            double sxRatioSel = (ActualWidth > 0 ? ActualWidth : _width) / (double)_width;
            double syRatioSel = (ActualHeight > 0 ? ActualHeight : _height) / (double)_height;
            Pen? selPen = null;
            foreach (var inst in _instances)
            {
                if (!inst.IsSelected) continue;
                var b = inst.BoundingBox;
                if (b.IsEmpty) continue;
                if (selPen == null)
                {
                    selPen = new Pen(Brushes.Yellow, 1.2 / z) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
                    selPen.Freeze();
                }
                var localRect = new Rect(
                    b.X * sxRatioSel, b.Y * syRatioSel,
                    b.Width * sxRatioSel, b.Height * syRatioSel);
                dc.DrawRectangle(null, selPen, localRect);
            }

            // Rubber band 사각형 (픽셀 좌표 → 로컬 DIU).
            if (_rbStartPixel is Point s && _rbCurrentPixel is Point c)
            {
                double sxRatio = (ActualWidth > 0 ? ActualWidth : _width) / _width;
                double syRatio = (ActualHeight > 0 ? ActualHeight : _height) / _height;
                var localRect = new Rect(
                    Math.Min(s.X, c.X) * sxRatio,
                    Math.Min(s.Y, c.Y) * syRatio,
                    Math.Abs(c.X - s.X) * sxRatio,
                    Math.Abs(c.Y - s.Y) * syRatio);
                var rbPen = new Pen(Brushes.Yellow, 1.0 / z) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };
                rbPen.Freeze();
                var rbFill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)); rbFill.Freeze();
                dc.DrawRectangle(rbFill, rbPen, localRect);
            }
        }

        private void RefreshAll()
        {
            if (_displayBitmap == null || _width == 0 || _height == 0) { InvalidateVisual(); return; }
            UpdateDisplayRect(0, 0, _width, _height);
            InvalidateVisual();
        }

        /// <summary>
        /// 레이어별 Z-order 역순(가장 큰 라벨 인덱스가 최상단)으로 픽셀을 렌더.
        /// 각 픽셀은 최상단 보이는 라벨의 색상으로 그려짐 (간단 overwrite).
        /// </summary>
        private void UpdateDisplayRect(int x, int y, int w, int h)
        {
            if (_displayBitmap == null) return;
            if (w <= 0 || h <= 0) return;
            x = Math.Max(0, x); y = Math.Max(0, y);
            w = Math.Min(w, _width - x); h = Math.Min(h, _height - y);
            if (w <= 0 || h <= 0) return;

            var labels = Labels;

            // 라벨 메타 캐시 — 매 픽셀 GetByIndex / IsVisible 호출 제거.
            // 정렬은 라벨 인덱스 내림차순 (위에서 아래로). 보이지 않는 라벨은 제외.
            var sortedLabelKeys = _layers.Keys.OrderByDescending(k => k).ToList();
            int labelCount = sortedLabelKeys.Count;
            var labelLayers = new SparseTileLayer[labelCount];
            var labelColors = new Color[labelCount];
            int activeLabelCount = 0;
            for (int i = 0; i < labelCount; i++)
            {
                int lblIdx = sortedLabelKeys[i];
                var lbl = labels?.GetByIndex(lblIdx);
                if (lbl == null || !lbl.IsVisible) continue;
                labelLayers[activeLabelCount] = _layers[lblIdx];
                labelColors[activeLabelCount] = lbl.Color;
                activeLabelCount++;
            }

            // 인스턴스 IsVisible=false ID 만 캐시. 매 픽셀 _instances.GetById 회피.
            HashSet<uint>? hiddenInstances = null;
            foreach (var inst in _instances)
            {
                if (!inst.IsVisible)
                {
                    hiddenInstances ??= new HashSet<uint>();
                    hiddenInstances.Add(inst.Id);
                }
            }

            // 임시 버퍼 재사용 — 매 호출마다 alloc 하면 8K BBox 시 200MB byte[] + 50MB bool[] 이 LOH 누적.
            // 버퍼 size 가 부족할 때만 재alloc. UpdateDisplayRect 의 최대 BBox 가 _width × _height 라 최대 alloc 1회.
            int bufferLen = w * h;
            int pixelsLen = bufferLen * 4;
            if (_displayPixelsBuffer == null || _displayPixelsBuffer.Length < pixelsLen)
                _displayPixelsBuffer = new byte[pixelsLen];
            else
                Array.Clear(_displayPixelsBuffer, 0, pixelsLen);
            if (_displayFilledBuffer == null || _displayFilledBuffer.Length < bufferLen)
                _displayFilledBuffer = new bool[bufferLen];
            else
                Array.Clear(_displayFilledBuffer, 0, bufferLen);
            var pixels = _displayPixelsBuffer;
            var filled = _displayFilledBuffer;

            // 픽셀 합성 — 라벨별 z-order 우선 픽셀 합성. 빈 영역은 0 (Array.Clear).
            // 라벨 layer 의 BBox 와 겹치는 타일을 enumerate 해서 inner loop 는 dense tile[localIdx].
            // 가장 높은 z 부터 그리되 이미 채운 픽셀은 skip — bool[] filled 로 추적.

            for (int li = 0; li < activeLabelCount; li++) // sortedLabelKeys 가 내림차순 = li 0 이 가장 높은 z
            {
                var layer = labelLayers[li];
                var color = labelColors[li];
                byte cb = color.B, cg = color.G, cr = color.R, ca = color.A;

                int x1 = x + w - 1;
                int y1 = y + h - 1;
                foreach (var (tileX, tileY, tile) in layer.EnumerateTilesInBox(x, y, x1, y1))
                {
                    int tileBaseX = tileX * SparseTileLayer.TileSize;
                    int tileBaseY = tileY * SparseTileLayer.TileSize;

                    // 타일과 BBox 의 교집합 영역만.
                    int sxLo = Math.Max(x, tileBaseX);
                    int syLo = Math.Max(y, tileBaseY);
                    int sxHi = Math.Min(x1, tileBaseX + SparseTileLayer.TileSize - 1);
                    int syHi = Math.Min(y1, tileBaseY + SparseTileLayer.TileSize - 1);

                    for (int gy = syLo; gy <= syHi; gy++)
                    {
                        int ly = gy - tileBaseY;
                        int dstRowBase = (gy - y) * w;
                        int tileRowBase = ly * SparseTileLayer.TileSize;
                        for (int gx = sxLo; gx <= sxHi; gx++)
                        {
                            int dstIdx = dstRowBase + (gx - x);
                            if (filled[dstIdx]) continue;
                            int lx = gx - tileBaseX;
                            uint instId = tile[tileRowBase + lx];
                            if (instId == 0) continue;
                            if (hiddenInstances != null && hiddenInstances.Contains(instId)) continue;
                            int di = dstIdx * 4;
                            pixels[di + 0] = cb;
                            pixels[di + 1] = cg;
                            pixels[di + 2] = cr;
                            pixels[di + 3] = ca;
                            filled[dstIdx] = true;
                        }
                    }
                }
            }

            _displayBitmap.WritePixels(new Int32Rect(x, y, w, h), pixels, w * 4, 0);
            RaiseDisplayChanged();
            // OnRender 가 다시 실행되어 다중 선택 BBox 점선 (이전 위치 잔상) 도 새 위치로 갱신.
            InvalidateVisual();
        }

        private void UpdateDisplayPixel(int x, int y) => UpdateDisplayRect(x, y, 1, 1);

        #endregion

        #region DP callbacks

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskLayer layer) layer.Resize(layer.MaskWidth, layer.MaskHeight);
        }

        private static void OnLabelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskLayer layer)
            {
                if (e.OldValue is LabelClassCollection oldCol)
                {
                    oldCol.CollectionChanged -= layer.OnLabelsCollectionChanged;
                    foreach (var lbl in oldCol) lbl.PropertyChanged -= layer.OnLabelPropertyChanged;
                }
                if (e.NewValue is LabelClassCollection newCol)
                {
                    newCol.CollectionChanged += layer.OnLabelsCollectionChanged;
                    foreach (var lbl in newCol) lbl.PropertyChanged += layer.OnLabelPropertyChanged;
                }
                layer.RefreshInstanceLabels();
                layer.RefreshAll();
            }
        }

        private void OnLabelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (LabelClass lbl in e.NewItems) lbl.PropertyChanged += OnLabelPropertyChanged;
            if (e.OldItems != null)
                foreach (LabelClass lbl in e.OldItems) lbl.PropertyChanged -= OnLabelPropertyChanged;
            RefreshInstanceLabels();
            RefreshAll();
        }

        private void OnLabelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LabelClass.Color) || e.PropertyName == nameof(LabelClass.IsVisible))
                RefreshAll();
        }

        #endregion
    }
}