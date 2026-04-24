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
        private readonly Dictionary<int, uint[]> _layers = new();

        private readonly MaskInstanceCollection _instances = new();

        private WriteableBitmap? _displayBitmap;
        private int _width;
        private int _height;

        // 활성 stroke 상태.
        private uint _activeStrokeId;
        private int _activeStrokeLabel;
        private Dictionary<int, uint[]>? _strokePreLayers; // stroke 시작 시점 레이어 스냅샷

        // stroke 에서 실제로 쓰인 픽셀의 bbox.
        private int _strokeMinX, _strokeMinY, _strokeMaxX, _strokeMaxY;
        private bool _strokeAnyPixel;

        // SourceImage 픽셀 캐시 (Bgra32, 4 bytes per pixel).
        private byte[]? _sourcePixels;
        // Gradient magnitude (Sobel) 캐시 — Magnetic Lasso 용.
        private double[]? _sourceGradient;

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
                new PropertyMetadata(MaskInstanceCollection.BackgroundId, (d, _) => ((MaskLayer)d).RefreshAll()));

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
            if (e.PropertyName == nameof(MaskInstance.IsVisible)
                || e.PropertyName == nameof(MaskInstance.IsSelected))
            {
                if (sender is MaskInstance inst) RefreshInstanceRegion(inst);
                // 다중 선택 BBox 점선은 OnRender 에서 그리므로 전체 InvalidateVisual.
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

                // 더블클릭 + PolygonPoints 존재 → vertex 편집 모드 진입.
                if (e.ClickCount == 2 && inst.PolygonPoints != null && inst.PolygonPoints.Count >= 3)
                {
                    ClearAllSelected();
                    inst.IsSelected = true;
                    SelectedInstance = inst;
                    IsVertexEditMode = true;
                    e.Handled = true;
                    return;
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
            RefreshAll();
        }

        private uint[] GetOrCreateLayer(int labelIndex)
        {
            if (!_layers.TryGetValue(labelIndex, out var mask))
            {
                mask = new uint[_width * _height];
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

        private void ResetStrokeBounds()
        {
            _strokeMinX = int.MaxValue; _strokeMinY = int.MaxValue;
            _strokeMaxX = int.MinValue; _strokeMaxY = int.MinValue;
            _strokeAnyPixel = false;
        }

        private Dictionary<int, uint[]> CloneLayers()
        {
            var clone = new Dictionary<int, uint[]>(_layers.Count);
            foreach (var kv in _layers)
            {
                var copy = new uint[kv.Value.Length];
                Buffer.BlockCopy(kv.Value, 0, copy, 0, kv.Value.Length * sizeof(uint));
                clone[kv.Key] = copy;
            }
            return clone;
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
            var layer = GetOrCreateLayer(labelIndex);
            _strokePreLayers.TryGetValue(labelIndex, out var pre);
            var sameLabelReplaced = new HashSet<uint>();
            for (int y = _strokeMinY; y <= _strokeMaxY; y++)
            {
                int rowStart = y * _width;
                for (int x = _strokeMinX; x <= _strokeMaxX; x++)
                {
                    int idx = rowStart + x;
                    uint newId = layer[idx];
                    uint oldId = pre != null ? pre[idx] : 0;
                    if (newId == tentativeId && oldId != 0 && oldId != tentativeId)
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
            var touched = new HashSet<uint>();
            foreach (var (lblIdx, mask) in _layers)
            {
                _strokePreLayers.TryGetValue(lblIdx, out var pre);
                if (pre == null) continue;
                for (int y = _strokeMinY; y <= _strokeMaxY; y++)
                {
                    int rowStart = y * _width;
                    for (int x = _strokeMinX; x <= _strokeMaxX; x++)
                    {
                        int idx = rowStart + x;
                        uint oldId = pre[idx];
                        if (oldId != 0 && mask[idx] != oldId) touched.Add(oldId);
                    }
                }
            }

            foreach (var id in touched)
            {
                RecomputeInstanceMetadata(id);
                var inst = _instances.GetById(id);
                if (inst == null) continue;
                if (inst.PixelCount == 0) { _instances.Remove(inst); continue; }
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

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < _height; y++)
            {
                int rowStart = y * _width;
                for (int x = 0; x < _width; x++)
                {
                    int idx = rowStart + x;
                    if (mask[idx] == id)
                    {
                        mask[idx] = 0;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            _instances.Remove(inst);
            if (minX <= maxX) UpdateDisplayRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>여러 인스턴스를 한 번에 삭제. RefreshAll 단일 호출로 최적화.</summary>
        public void DeleteInstances(System.Collections.Generic.IEnumerable<uint> ids)
        {
            var idSet = new System.Collections.Generic.HashSet<uint>(ids);
            if (idSet.Count == 0) return;

            foreach (var kv in _layers)
            {
                var m = kv.Value;
                for (int i = 0; i < m.Length; i++)
                    if (idSet.Contains(m[i])) m[i] = 0;
            }

            var toRemove = new System.Collections.Generic.List<MaskInstance>();
            foreach (var inst in _instances)
                if (idSet.Contains(inst.Id)) toRemove.Add(inst);
            foreach (var r in toRemove) _instances.Remove(r);

            RefreshAll();
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
                anyMerged = true;
            }

            if (anyMerged) RefreshAll();
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

            // 새 BBox 영역에 이미 있는 같은 라벨의 다른 인스턴스 ID 수집 (병합 대상).
            var sameLabelReplaced = new HashSet<uint>();
            int nxStart = Math.Max(0, (int)Math.Round(newBBox.X));
            int nyStart = Math.Max(0, (int)Math.Round(newBBox.Y));
            int nxEnd = Math.Min(_width, (int)Math.Round(newBBox.X + newBBox.Width));
            int nyEnd = Math.Min(_height, (int)Math.Round(newBBox.Y + newBBox.Height));
            for (int y = nyStart; y < nyEnd; y++)
            {
                int rowStart = y * _width;
                for (int x = nxStart; x < nxEnd; x++)
                {
                    uint existing = mask[rowStart + x];
                    if (existing != 0 && existing != id) sameLabelReplaced.Add(existing);
                }
            }

            int oldX = (int)oldBBox.X, oldY = (int)oldBBox.Y;
            int oldW = (int)oldBBox.Width, oldH = (int)oldBBox.Height;

            var sourceBits = new bool[oldW * oldH];
            for (int y = 0; y < oldH; y++)
            {
                int gy = oldY + y;
                if ((uint)gy >= (uint)_height) continue;
                int rowStart = gy * _width;
                for (int x = 0; x < oldW; x++)
                {
                    int gx = oldX + x;
                    if ((uint)gx >= (uint)_width) continue;
                    if (mask[rowStart + gx] == id) sourceBits[y * oldW + x] = true;
                }
            }

            int newXi = (int)Math.Round(newBBox.X);
            int newYi = (int)Math.Round(newBBox.Y);
            int newWi = Math.Max(1, (int)Math.Round(newBBox.Width));
            int newHi = Math.Max(1, (int)Math.Round(newBBox.Height));

            // 1. 기존 인스턴스 픽셀을 해당 라벨 레이어에서 제거.
            for (int y = 0; y < oldH; y++)
            {
                int gy = oldY + y;
                if ((uint)gy >= (uint)_height) continue;
                int rowStart = gy * _width;
                for (int x = 0; x < oldW; x++)
                {
                    int gx = oldX + x;
                    if ((uint)gx >= (uint)_width) continue;
                    int idx = rowStart + gx;
                    if (mask[idx] == id) mask[idx] = 0;
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
                    int rowStart = gy * _width;
                    for (int x = 0; x < oldW; x++)
                    {
                        if (!sourceBits[y * oldW + x]) continue;
                        int gx = oldX + x + dxShift;
                        if ((uint)gx >= (uint)_width) continue;
                        mask[rowStart + gx] = id;
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
                    int rowStart = gy * _width;
                    for (int x = 0; x < newWi; x++)
                    {
                        int srcX = (int)((double)x * oldW / newWi);
                        if (srcX >= oldW) srcX = oldW - 1;
                        if (!sourceBits[srcY * oldW + srcX]) continue;
                        int gx = newXi + x;
                        if ((uint)gx >= (uint)_width) continue;
                        mask[rowStart + gx] = id;
                    }
                }
            }

            // 3. PolygonPoints 도 동일 아핀 변환으로 동기화 (vertex 편집 진입 시 일치 유지).
            if (inst.PolygonPoints != null && oldBBox.Width > 0 && oldBBox.Height > 0)
            {
                double sx = newBBox.Width / oldBBox.Width;
                double sy = newBBox.Height / oldBBox.Height;
                var xformed = new List<Point>(inst.PolygonPoints.Count);
                foreach (var p in inst.PolygonPoints)
                {
                    double nx = newBBox.X + (p.X - oldBBox.X) * sx;
                    double ny = newBBox.Y + (p.Y - oldBBox.Y) * sy;
                    xformed.Add(new Point(nx, ny));
                }
                inst.PolygonPoints = xformed;
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
            }

            RecomputeInstanceMetadata(id);

            // 5. 리사이즈로 병합이 일어난 경우 disconnected 면 자동 재분리.
            //    pureTranslate(이동) 에서는 Merge 로 묶인 사용자 의도를 존중해 자동 재분리 생략 —
            //    disconnected 덩어리가 그대로 같은 ID 를 유지.
            if (!pureTranslate)
                SplitIfDisconnected(id);

            RefreshAll();
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
            if (labelIndex == LabelClassCollection.BackgroundIndex)
            {
                // 지우개: 모든 레이어에서 해당 픽셀을 0 으로.
                foreach (var kv in _layers) kv.Value[idx] = 0;
                return;
            }
            if (_activeStrokeId == 0) return;
            var layer = GetOrCreateLayer(labelIndex);
            layer[idx] = _activeStrokeId;
        }

        private void ExpandStrokeBounds(int x, int y)
        {
            if (x < _strokeMinX) _strokeMinX = x;
            if (x > _strokeMaxX) _strokeMaxX = x;
            if (y < _strokeMinY) _strokeMinY = y;
            if (y > _strokeMaxY) _strokeMaxY = y;
            _strokeAnyPixel = true;
        }

        private void RemapInstanceIdsInLayer(uint[] layer, HashSet<uint> from, uint toId)
        {
            if (from.Count == 0) return;
            for (int i = 0; i < layer.Length; i++)
                if (from.Contains(layer[i])) layer[i] = toId;
        }

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

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int count = 0;
            for (int y = 0; y < _height; y++)
            {
                int rowStart = y * _width;
                for (int x = 0; x < _width; x++)
                {
                    if (mask[rowStart + x] == id)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
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
                    int globalIdx = y * _width + x;
                    if (mask[globalIdx] != id) continue;

                    var comp = new List<int>();
                    queue.Clear();
                    queue.Enqueue((x, y));
                    visited[localIdx] = true;

                    void TryPush(int nx, int ny)
                    {
                        if (nx < bx || nx >= bx + bw || ny < by || ny >= by + bh) return;
                        int nLocal = (ny - by) * bw + (nx - bx);
                        if (visited[nLocal]) return;
                        int nGlobal = ny * _width + nx;
                        if (mask[nGlobal] != id) return;
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
                foreach (var gi in components[c]) mask[gi] = newId;
                var newInst = new MaskInstance { Id = newId, LabelIndex = inst.LabelIndex };
                AssignLabelRef(newInst);
                _instances.Add(newInst);
                RecomputeInstanceMetadata(newId);
            }
            RecomputeInstanceMetadata(id);
        }

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
            var byLabel = new Dictionary<int, uint[]>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                byte lbl = snapshot[i];
                if (lbl == 0) continue;
                if (!byLabel.TryGetValue(lbl, out var mask))
                {
                    mask = new uint[_width * _height];
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
            var layers = new Dictionary<int, uint[]>(_layers.Count);
            foreach (var kv in _layers)
            {
                var copy = new uint[kv.Value.Length];
                Buffer.BlockCopy(kv.Value, 0, copy, 0, kv.Value.Length * sizeof(uint));
                layers[kv.Key] = copy;
            }
            var records = _instances.Select(i =>
                new MaskLayerSnapshot.InstanceRecord(i.Id, i.LabelIndex, i.BoundingBox, i.PixelCount, i.IsVisible,
                    i.PolygonPoints != null ? (IReadOnlyList<System.Windows.Point>)new List<System.Windows.Point>(i.PolygonPoints) : null))
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
                if (kv.Value.Length != _width * _height)
                    throw new ArgumentException("스냅샷 레이어 크기가 현재 마스크와 일치하지 않습니다.");
                var copy = new uint[kv.Value.Length];
                Buffer.BlockCopy(kv.Value, 0, copy, 0, kv.Value.Length * sizeof(uint));
                _layers[kv.Key] = copy;
            }
            _instances.Clear();
            foreach (var r in snapshot.Instances)
            {
                var inst = new MaskInstance
                {
                    Id = r.Id, LabelIndex = r.LabelIndex,
                    BoundingBox = r.BoundingBox, PixelCount = r.PixelCount, IsVisible = r.IsVisible,
                    PolygonPoints = r.PolygonPoints != null ? new List<System.Windows.Point>(r.PolygonPoints) : null,
                };
                AssignLabelRef(inst);
                _instances.Add(inst);
            }
            _instances.EnsureNextIdAtLeast(snapshot.NextId);
            RefreshAll();
        }

        /// <summary>
        /// 지정 인스턴스의 픽셀을 모두 지우고 새 polygon 으로 재그린다. ID 는 유지.
        /// 다른 라벨의 영역은 건드리지 않으며, 같은 라벨 내 다른 인스턴스와 겹쳐도 이번 라운드는 덮어씀.
        /// Vertex 편집 commit 경로.
        /// </summary>
        public void RepaintPolygon(uint instanceId, IReadOnlyList<System.Windows.Point> newPoints)
        {
            var inst = _instances.GetById(instanceId);
            if (inst == null || newPoints == null || newPoints.Count < 3) return;
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
            for (int y = y0; y <= y1; y++)
            {
                int row = y * _width;
                for (int x = x0; x <= x1; x++)
                {
                    if (layer[row + x] == instanceId) layer[row + x] = 0;
                }
            }

            // 2) 새 polygon rasterize — flat 배열로 변환 후 기존 헬퍼 재사용.
            var flat = new double[newPoints.Count * 2];
            for (int i = 0; i < newPoints.Count; i++)
            {
                flat[i * 2] = newPoints[i].X;
                flat[i * 2 + 1] = newPoints[i].Y;
            }
            RasterizePolygonIntoLayer(layer, flat, instanceId);

            // 3) PolygonPoints 업데이트 + 메타 재계산 + 렌더 갱신.
            inst.PolygonPoints = new List<System.Windows.Point>(newPoints);
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

            var color = inst.Label?.Color ?? Labels?.GetByIndex(inst.LabelIndex)?.Color ?? Colors.Yellow;
            var bmp = new WriteableBitmap(bw, bh, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[bw * bh * 4];
            for (int y = 0; y < bh; y++)
            {
                int gy = by + y;
                if ((uint)gy >= (uint)_height) continue;
                int rowStart = gy * _width;
                int dstRow = y * bw * 4;
                for (int x = 0; x < bw; x++)
                {
                    int gx = bx + x;
                    if ((uint)gx >= (uint)_width) continue;
                    if (mask[rowStart + gx] != id) continue;
                    int di = dstRow + x * 4;
                    pixels[di + 0] = color.B;
                    pixels[di + 1] = color.G;
                    pixels[di + 2] = color.R;
                    pixels[di + 3] = color.A;
                }
            }
            bmp.WritePixels(new Int32Rect(0, 0, bw, bh), pixels, bw * 4, 0);
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

        private void RasterizePolygonIntoLayer(uint[] layer, IReadOnlyList<double> flat, uint id)
        {
            if (flat.Count < 6) return;
            int n = flat.Count / 2;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                double y = flat[i * 2 + 1];
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(_height - 1, (int)Math.Ceiling(maxY));
            for (int y = y0; y <= y1; y++)
            {
                var xs = new List<double>();
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
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Math.Max(0, (int)Math.Floor(xs[k]));
                    int xb = Math.Min(_width - 1, (int)Math.Ceiling(xs[k + 1]));
                    int row = y * _width;
                    for (int x = xa; x <= xb; x++) layer[row + x] = id;
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

        /// <summary>Sobel gradient magnitude 배열 (width*height, null 가능).</summary>
        public double[]? GetSourceGradient() => _sourceGradient;

        /// <summary>SourceImage 가 바뀌면 픽셀/gradient 캐시 재구성.</summary>
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
            var sortedLabels = _layers.Keys.OrderByDescending(k => k).ToList(); // 위에서 아래로

            var pixels = new byte[w * h * 4];
            for (int row = 0; row < h; row++)
            {
                int srcRow = (y + row) * _width + x;
                int dstRow = row * w * 4;
                for (int col = 0; col < w; col++)
                {
                    int srcIdx = srcRow + col;
                    Color color = Colors.Transparent;
                    byte a = 0;
                    bool found = false;
                    foreach (var lblIdx in sortedLabels)
                    {
                        var layer = _layers[lblIdx];
                        uint instId = layer[srcIdx];
                        if (instId == 0) continue;
                        var lbl = labels?.GetByIndex(lblIdx);
                        if (lbl == null) continue;
                        if (!lbl.IsVisible) continue;
                        var inst = _instances.GetById(instId);
                        if (inst != null && !inst.IsVisible) continue;
                        color = lbl.Color;
                        a = color.A;
                        // 선택된(IsSelected=true) 인스턴스는 alpha 최대로 boost → 다중 선택 모두 강조.
                        if (inst != null && inst.IsSelected) a = 255;
                        found = true;
                        break;
                    }

                    int di = dstRow + col * 4;
                    if (!found)
                    {
                        pixels[di + 0] = 0; pixels[di + 1] = 0; pixels[di + 2] = 0; pixels[di + 3] = 0;
                    }
                    else
                    {
                        pixels[di + 0] = color.B; pixels[di + 1] = color.G; pixels[di + 2] = color.R; pixels[di + 3] = a;
                    }
                }
            }
            _displayBitmap.WritePixels(new Int32Rect(x, y, w, h), pixels, w * 4, 0);
            RaiseDisplayChanged();
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