using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.WPF.Controls.Behaviors;
using VSMVVM.WPF.Controls.Tools;
using VSMVVM.WPF.Imaging;
using VSMVVM.WPF.Imaging.Measurements;
using VSMVVM.WPF.Services;
using VSMVVM.WPF.Controls;

#nullable enable
namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 이미지 뷰어 데모 ViewModel. 모든 View 상호작용은 바인딩/Behavior로 처리한다:
    /// - 이미지 로드 → <see cref="BackgroundImageSource"/>/<see cref="ImagePixelWidth"/>/<see cref="ImagePixelHeight"/> 변경이
    ///   View의 Image/MaskLayer에 반영.
    /// - Fit 요청 → <see cref="FitTrigger"/> 프로퍼티 변경으로 FitToContentBehavior가 처리.
    /// - 마스크 I/O → <see cref="SaveMaskRequest"/>/<see cref="LoadMaskRequest"/>를 MaskBehavior가 처리.
    /// - Stroke/Load 결과 → <see cref="StrokeCompletedCommand"/>로 돌아와 Undo/Redo에 Push.
    /// </summary>
    public partial class ImageViewerDemoViewModel : ViewModelBase
    {
        private readonly IUndoRedoService _undoRedo;
        private readonly IFileDialogService _fileDialog;

        // ── Tool 인스턴스 ──
        public ArrowTool ArrowTool { get; } = new();
        public SelectTool SelectTool { get; } = new();
        public BrushTool BrushTool { get; } = new();
        public EraserTool EraserTool { get; } = new();
        public FillTool FillTool { get; } = new();
        public RectangleMaskTool RectangleMaskTool { get; } = new();
        public EllipseMaskTool EllipseMaskTool { get; } = new();
        public PolygonMaskTool PolygonMaskTool { get; } = new();
        public LengthMeasurementTool LengthMeasurementTool { get; } = new();
        public AngleMeasurementTool AngleMeasurementTool { get; } = new();
        public MagicWandTool MagicWandTool { get; } = new();
        public MagneticLassoTool MagneticLassoTool { get; } = new();

        // ── 측정 컬렉션 ──
        public MeasurementCollection Measurements { get; } = new();

        // ── 라벨 시스템 ──
        public LabelClassCollection Labels { get; } = new();

        [Property] private LabelClass? _selectedLabel;
        [Property] private int _currentLabelIndex = 1;

        // ── 라벨 편집 상태 (이름/색상 Popup) ──
        [Property] private LabelClass? _editingLabel;
        [Property] private string _editingName = string.Empty;
        [Property] private Color _editingColor;
        [Property] private bool _isNameEditorOpen;
        [Property] private bool _isColorEditorOpen;

        private string _editOriginalName = string.Empty;
        private Color _editOriginalColor;

        // ── 이미지 (바인딩 원천) ──
        [Property] private ImageSource? _backgroundImageSource;
        [Property] private int _imagePixelWidth;
        [Property] private int _imagePixelHeight;
        [Property] private bool _hasImage;

        // ── 뷰포트/툴 상태 ──
        [Property] private double _zoomLevel = 1.0;
        [Property] private bool _isPanLocked;
        [Property] private string _statusText = "Ready";
        [Property] private int _brushRadius = 8;
        [Property] private ICanvasTool _currentTool = null!;
        [Property] private bool _showRuler = true;

        // ── Vertex 편집 모드 ──
        [Property] private bool _isVertexEditMode;

        // ── Behavior 트리거/요청 토큰 ──
        [Property] private object? _fitTrigger;
        [Property] private Rect? _zoomToBoundsTrigger;
        [Property] private MaskIoRequest? _saveMaskRequest;
        [Property] private MaskIoRequest? _loadMaskRequest;
        [Property] private MaskInstanceRequest? _splitInstanceRequest;
        [Property] private MaskInstanceRequest? _deleteInstanceRequest;
        [Property] private MaskInstancesRequest? _deleteInstancesRequest;
        [Property] private MaskInstancesMergeRequest? _mergeInstancesRequest;

        // ── 인스턴스 다중 선택 카운트 (Merge 버튼 IsEnabled 바인딩용) ──
        [Property] private int _selectedInstanceCount;

        // ── 인스턴스 선택 ──
        [Property] private MaskInstance? _selectedInstance;

        // ── 마우스 픽셀 정보 (PixelInfoBehavior 가 set) ──
        [Property] private int _mouseImageX;
        [Property] private int _mouseImageY;
        [Property] private Color? _mouseRgb;
        [Property] private bool _isMouseInImage;

        // ── 표시용 string (Popup 바인딩) ──
        [Property] private string _mousePositionText = "-";
        [Property] private string _imageSizeText = "-";
        [Property] private string _mouseRgbText = "-";

        /// <summary>View 의 MaskLayer 가 로드되면 여기로 주입된다(코드비하인드). Instances 바인딩 원천.</summary>
        [Property] private MaskLayer? _maskLayer;

        public bool CanUndo => _undoRedo.CanUndo;
        public bool CanRedo => _undoRedo.CanRedo;

        public ImageViewerDemoViewModel(IUndoRedoService undoRedo, IFileDialogService fileDialog)
        {
            _undoRedo = undoRedo;
            _fileDialog = fileDialog;
            _undoRedo.StateChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            };

            _currentTool = ArrowTool;

            Labels.Add("Tissue", Colors.DeepSkyBlue);
            Labels.Add("Lesion", Colors.OrangeRed);
            SelectedLabel = Labels.Count > 1 ? Labels[1] : null;

            // 측정 도구에 결과 저장 컬렉션 주입.
            LengthMeasurementTool.Target = Measurements;
            AngleMeasurementTool.Target = Measurements;

            // 측정 확정 시 Undo 스택에 push. preview 단계 (MouseDown ~ MouseUp 중 갱신) 는 push 하지 않는다.
            LengthMeasurementTool.MeasurementCommitted += (_, m) =>
                _undoRedo.Push(
                    undo: () => { Measurements.Remove(m); if (SelectedMeasurement == m) SelectedMeasurement = null; },
                    redo: () => Measurements.Add(m));

            AngleMeasurementTool.MeasurementCommitted += (_, m) =>
                _undoRedo.Push(
                    undo: () => { Measurements.Remove(m); if (SelectedMeasurement == m) SelectedMeasurement = null; },
                    redo: () => Measurements.Add(m));
        }

        [Property] private MeasurementBase? _selectedMeasurement;

        partial void OnSelectedLabelChanged(LabelClass? value)
        {
            if (value != null) CurrentLabelIndex = value.Index;
        }

        partial void OnBrushRadiusChanged(int value)
        {
            BrushTool.Radius = value;
            EraserTool.Radius = value;
        }

        partial void OnCurrentToolChanged(ICanvasTool value)
        {
            UpdateStatus();
        }

        partial void OnZoomLevelChanged(double value)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var name = CurrentTool?.Mode.ToString() ?? "Select";
            StatusText = $"Tool: {name} | Zoom: {ZoomLevel:F2}x";
        }

        // ── 툴 전환 ──
        [RelayCommand] private void SetToolArrow() => CurrentTool = ArrowTool;
        [RelayCommand] private void SetToolSelect() => CurrentTool = SelectTool;
        [RelayCommand] private void SetToolBrush() => CurrentTool = BrushTool;
        [RelayCommand] private void SetToolEraser() => CurrentTool = EraserTool;
        [RelayCommand] private void SetToolFill() => CurrentTool = FillTool;
        [RelayCommand] private void SetToolRectangleMask() => CurrentTool = RectangleMaskTool;
        [RelayCommand] private void SetToolEllipseMask() => CurrentTool = EllipseMaskTool;
        [RelayCommand] private void SetToolPolygonMask() => CurrentTool = PolygonMaskTool;
        [RelayCommand] private void SetToolLengthMeasurement() => CurrentTool = LengthMeasurementTool;
        [RelayCommand] private void SetToolAngleMeasurement() => CurrentTool = AngleMeasurementTool;
        [RelayCommand] private void SetToolMagicWand() => CurrentTool = MagicWandTool;
        [RelayCommand] private void SetToolMagneticLasso() => CurrentTool = MagneticLassoTool;

        [RelayCommand]
        private void ClearMeasurements()
        {
            if (Measurements.Count == 0) return;
            var snapshot = new System.Collections.Generic.List<MeasurementBase>(Measurements);
            var prevSelected = SelectedMeasurement;
            Measurements.Clear();
            SelectedMeasurement = null;
            _undoRedo.Push(
                undo: () =>
                {
                    foreach (var m in snapshot) Measurements.Add(m);
                    SelectedMeasurement = prevSelected;
                },
                redo: () => { Measurements.Clear(); SelectedMeasurement = null; });
        }

        [RelayCommand]
        private void DeleteSelectedMeasurement()
        {
            if (SelectedMeasurement == null) return;
            var removed = SelectedMeasurement;
            Measurements.Remove(removed);
            SelectedMeasurement = null;
            _undoRedo.Push(
                undo: () => { Measurements.Add(removed); SelectedMeasurement = removed; },
                redo: () => { Measurements.Remove(removed); SelectedMeasurement = null; });
        }

        // ── 라벨 관리 ──
        [RelayCommand]
        private void AddLabel()
        {
            var idx = Labels.Count;
            var presets = new[] { Colors.DeepSkyBlue, Colors.OrangeRed, Colors.LimeGreen, Colors.Gold, Colors.MediumPurple };
            var color = presets[(idx - 1) % presets.Length];
            var prevSelected = SelectedLabel;
            var lbl = Labels.Add($"Label {idx}", color);
            SelectedLabel = lbl;
            _undoRedo.Push(
                undo: () => { Labels.Remove(lbl); SelectedLabel = prevSelected; },
                redo: () => { Labels.Add(lbl); SelectedLabel = lbl; });
        }

        [RelayCommand]
        private void RemoveSelectedLabel()
        {
            if (SelectedLabel == null) return;
            if (SelectedLabel.Index == LabelClassCollection.BackgroundIndex) return;
            var removed = SelectedLabel;
            var removedAt = Labels.IndexOf(removed);
            var prevSelected = SelectedLabel;
            Labels.Remove(removed);
            SelectedLabel = Labels.Count > 0 ? Labels[^1] : null;
            _undoRedo.Push(
                undo: () => { Labels.Insert(removedAt, removed); SelectedLabel = prevSelected; },
                redo: () => { Labels.Remove(removed); SelectedLabel = Labels.Count > 0 ? Labels[^1] : null; });
        }

        // ── 라벨 이름 편집 ──
        [RelayCommand]
        private void BeginEditLabelName(LabelClass? label)
        {
            if (label == null) return;
            if (label.Index == LabelClassCollection.BackgroundIndex) return;
            EditingLabel = label;
            _editOriginalName = label.Name;
            EditingName = label.Name;
            IsNameEditorOpen = true;
        }

        [RelayCommand]
        private void CommitLabelName()
        {
            var label = EditingLabel;
            if (label == null) { IsNameEditorOpen = false; return; }
            var newName = EditingName ?? string.Empty;
            var originalName = _editOriginalName;
            IsNameEditorOpen = false;
            EditingLabel = null;
            if (newName == originalName) return;
            label.Name = newName;
            _undoRedo.Push(
                undo: () => label.Name = originalName,
                redo: () => label.Name = newName);
        }

        [RelayCommand]
        private void CancelLabelName()
        {
            // 이름은 라이브 반영 아님 → 그냥 닫기만.
            IsNameEditorOpen = false;
            EditingLabel = null;
        }

        // ── 라벨 색상 편집 (라이브 프리뷰 + 취소 시 원복) ──
        [RelayCommand]
        private void BeginEditLabelColor(LabelClass? label)
        {
            if (label == null) return;
            if (label.Index == LabelClassCollection.BackgroundIndex) return;
            EditingLabel = label;
            _editOriginalColor = label.Color;
            EditingColor = label.Color;
            IsColorEditorOpen = true;
        }

        [RelayCommand]
        private void CommitLabelColor()
        {
            var label = EditingLabel;
            if (label == null) { IsColorEditorOpen = false; return; }
            var newColor = EditingColor;
            var originalColor = _editOriginalColor;
            // OnIsColorEditorOpenChanged 의 자동 취소 로직이 실행되지 않도록 먼저 플래그 클리어.
            _committingColor = true;
            IsColorEditorOpen = false;
            _committingColor = false;
            EditingLabel = null;
            if (newColor == originalColor) return;
            label.Color = newColor;
            _undoRedo.Push(
                undo: () => label.Color = originalColor,
                redo: () => label.Color = newColor);
        }

        [RelayCommand]
        private void CancelLabelColor()
        {
            var label = EditingLabel;
            if (label != null)
            {
                // 라이브 프리뷰로 이미 변경된 Color 를 원본으로 복구.
                label.Color = _editOriginalColor;
            }
            IsColorEditorOpen = false;
            EditingLabel = null;
        }

        // Popup StaysOpen=False 로 바깥 클릭 시 IsColorEditorOpen=false 가 외부에서 들어오는 경우 자동 취소.
        private bool _committingColor;

        partial void OnIsColorEditorOpenChanged(bool value)
        {
            if (value) return;
            if (_committingColor) return;
            // 외부에 의해 닫힘 → 취소와 동일하게 원색 복구.
            var label = EditingLabel;
            if (label != null)
            {
                label.Color = _editOriginalColor;
                EditingLabel = null;
            }
        }

        partial void OnIsNameEditorOpenChanged(bool value)
        {
            if (value) return;
            // 외부에 의해 닫힘 → EditingLabel 정리만(이름은 라이브 반영 아님).
            EditingLabel = null;
        }

        // ColorPicker 의 SelectedColor 는 이 EditingColor 와 TwoWay 바인딩.
        // 값이 바뀔 때마다 라이브 프리뷰로 실제 LabelClass.Color 에 밀어넣어 마스크 렌더를 갱신.
        partial void OnEditingColorChanged(Color value)
        {
            if (!IsColorEditorOpen) return;
            var label = EditingLabel;
            if (label == null) return;
            if (label.Color != value) label.Color = value;
        }

        // ── 이미지 I/O: 바인딩으로 View에 반영 ──
        [RelayCommand]
        private void OpenImage()
        {
            var path = _fileDialog.OpenFile("Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff");
            if (string.IsNullOrEmpty(path)) return;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();

            BackgroundImageSource = bmp;
            ImagePixelWidth = bmp.PixelWidth;
            ImagePixelHeight = bmp.PixelHeight;
            HasImage = true;

            RequestFit();
        }

        // ── Behavior 트리거 헬퍼 ──
        private void RequestFit() => FitTrigger = new object();

        [RelayCommand] private void FitToContent() => RequestFit();

        [RelayCommand] private void ToggleRuler() => ShowRuler = !ShowRuler;

        /// <summary>Esc 로 vertex 편집 모드 종료. MaskLayer 의 IsVertexEditMode 바인딩이 TwoWay 이므로 false 설정만으로 해제.</summary>
        [RelayCommand] private void ExitVertexEdit() => IsVertexEditMode = false;

        // ── 마스크 I/O: COCO JSON 경로 ──
        [RelayCommand]
        private void SaveMask()
        {
            var path = _fileDialog.SaveFile("COCO JSON|*.json", "mask.json");
            if (string.IsNullOrEmpty(path)) return;
            SaveMaskRequest = new MaskIoRequest(path);
        }

        [RelayCommand]
        private void LoadMask()
        {
            var path = _fileDialog.OpenFile("COCO JSON|*.json");
            if (string.IsNullOrEmpty(path)) return;
            LoadMaskRequest = new MaskIoRequest(path);
        }

        // ── Undo/Redo ──
        [RelayCommand] private void Undo() => _undoRedo.Undo();
        [RelayCommand] private void Redo() => _undoRedo.Redo();

        /// <summary>MaskBehavior가 stroke/Load 완료 시 호출. Full 스냅샷을 Undo/Redo에 Push.</summary>
        [RelayCommand]
        private void StrokeCompleted(MaskStrokeCompletedArgs? args)
        {
            if (args == null) return;
            var before = args.Before;
            var after = args.After;
            var restore = args.Restore;
            _undoRedo.Push(
                undo: () => restore(before),
                redo: () => restore(after));
        }

        // ── 인스턴스 관리 커맨드 ──

        [RelayCommand]
        private void SplitSelectedInstance()
        {
            if (SelectedInstance == null) return;
            SplitInstanceRequest = new MaskInstanceRequest(SelectedInstance.Id);
        }

        [RelayCommand]
        private void MergeSelectedInstances()
        {
            if (MaskLayer == null) return;
            var ids = new System.Collections.Generic.List<uint>();
            foreach (var inst in MaskLayer.Instances)
                if (inst.IsSelected) ids.Add(inst.Id);
            if (ids.Count < 2) return;
            MergeInstancesRequest = new MaskInstancesMergeRequest(ids);
            // 병합 후 어느 ID 가 앵커로 남을지는 MaskLayer 가 결정하므로 일단 null 로 두고
            // IsSelected 이벤트 경로에서 남은 선택 중 하나가 자동으로 앵커로 승격된다.
            SelectedInstance = null;
        }

        [RelayCommand]
        private void DeleteSelectedInstance()
        {
            // 다중 선택(IsSelected=true) 우선, 없으면 SelectedInstance 만.
            if (MaskLayer == null) return;
            var multi = new System.Collections.Generic.List<uint>();
            foreach (var inst in MaskLayer.Instances)
                if (inst.IsSelected) multi.Add(inst.Id);

            if (multi.Count > 1)
            {
                DeleteInstancesRequest = new MaskInstancesRequest(multi);
            }
            else if (SelectedInstance != null)
            {
                DeleteInstanceRequest = new MaskInstanceRequest(SelectedInstance.Id);
            }
            SelectedInstance = null;
        }

        partial void OnSelectedInstanceChanged(MaskInstance? value)
        {
            // MaskLayer 에 선택 ID 를 알려 렌더 하이라이트를 유도.
            if (MaskLayer != null)
                MaskLayer.SelectedInstanceId = value?.Id ?? 0;
        }

        partial void OnMaskLayerChanged(MaskLayer? value)
        {
            // 이전 MaskLayer 에 걸어둔 구독 해제.
            if (_subscribedMaskLayer != null)
            {
                _subscribedMaskLayer.Instances.CollectionChanged -= OnInstancesCollectionChanged;
                foreach (var inst in _subscribedMaskLayer.Instances)
                    inst.PropertyChanged -= OnInstancePropertyChanged;
                _subscribedMaskLayer = null;
            }

            if (value == null) return;

            // 새 MaskLayer 구독 + 현재 선택 상태 즉시 반영.
            value.SelectedInstanceId = SelectedInstance?.Id ?? 0;
            value.Instances.CollectionChanged += OnInstancesCollectionChanged;
            foreach (var inst in value.Instances)
                inst.PropertyChanged += OnInstancePropertyChanged;
            _subscribedMaskLayer = value;
            UpdateSelectedInstanceCount();
        }

        private MaskLayer? _subscribedMaskLayer;

        private void OnInstancesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (MaskInstance inst in e.OldItems)
                    inst.PropertyChanged -= OnInstancePropertyChanged;
            if (e.NewItems != null)
                foreach (MaskInstance inst in e.NewItems)
                    inst.PropertyChanged += OnInstancePropertyChanged;
            UpdateSelectedInstanceCount();
        }

        private void OnInstancePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MaskInstance.IsSelected)) return;
            UpdateSelectedInstanceCount();
            if (sender is MaskInstance inst)
            {
                if (inst.IsSelected)
                {
                    // 최근 IsSelected=true 가 된 인스턴스를 앵커로.
                    SelectedInstance = inst;
                }
                else if (ReferenceEquals(SelectedInstance, inst))
                {
                    // 현재 앵커가 해제됨 → 남아 있는 선택 중 하나를 새 앵커로.
                    SelectedInstance = FindAnySelected();
                }
            }
            // 선택 변경 시 자동 zoom 은 하지 않음. 명시적 "해당 영역 보기" 커맨드(ZoomToSelectedInstancesCommand)로만 발화.
        }

        private MaskInstance? FindAnySelected()
        {
            if (MaskLayer == null) return null;
            foreach (var i in MaskLayer.Instances)
                if (i.IsSelected) return i;
            return null;
        }

        private void UpdateSelectedInstanceCount()
        {
            if (MaskLayer == null) { SelectedInstanceCount = 0; return; }
            int n = 0;
            foreach (var i in MaskLayer.Instances)
                if (i.IsSelected) n++;
            SelectedInstanceCount = n;
        }

        /// <summary>
        /// IsSelected=true 인 인스턴스들의 BoundingBox 합집합을 계산해 뷰포트에 zoom+center.
        /// 사용자가 Instances 헤더의 "해당 영역 보기" 버튼을 눌렀을 때만 발화.
        /// 선택 0개면 동작 없음. 단일/다중 모두 동일 경로.
        /// </summary>
        [RelayCommand]
        private void ZoomToSelectedInstances()
        {
            if (MaskLayer == null) return;
            Rect union = Rect.Empty;
            foreach (var inst in MaskLayer.Instances)
            {
                if (!inst.IsSelected) continue;
                if (inst.BoundingBox.IsEmpty) continue;
                union = union.IsEmpty ? inst.BoundingBox : Rect.Union(union, inst.BoundingBox);
            }
            if (union.IsEmpty || union.Width <= 0 || union.Height <= 0) return;
            // 같은 Rect 가 연속 들어올 때도 재발화되도록 null 로 한 번 리셋 후 세팅.
            ZoomToBoundsTrigger = null;
            ZoomToBoundsTrigger = union;
        }

        // ── 픽셀 정보 표시용 string 갱신 ──
        partial void OnMouseImageXChanged(int value) => UpdateMousePositionText();
        partial void OnMouseImageYChanged(int value) => UpdateMousePositionText();
        partial void OnIsMouseInImageChanged(bool value) => UpdateMousePositionText();
        partial void OnMouseRgbChanged(Color? value) => UpdateMouseRgbText();
        partial void OnImagePixelWidthChanged(int value) => UpdateImageSizeText();
        partial void OnImagePixelHeightChanged(int value) => UpdateImageSizeText();
        partial void OnHasImageChanged(bool value) => UpdateImageSizeText();

        private void UpdateMousePositionText() =>
            MousePositionText = IsMouseInImage ? $"X: {MouseImageX}, Y: {MouseImageY}" : "-";

        private void UpdateImageSizeText() =>
            ImageSizeText = HasImage ? $"W: {ImagePixelWidth}, H: {ImagePixelHeight}" : "-";

        private void UpdateMouseRgbText() =>
            MouseRgbText = MouseRgb is { } c ? $"{c.R:D3}, {c.G:D3}, {c.B:D3}" : "-";
    }
}
