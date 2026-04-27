using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Controls.Tools;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// <see cref="LayeredCanvas"/>에 부착해 마스크 stroke의 before/after 스냅샷과
    /// PNG 저장/로드 요청을 MVVM 친화적으로 VM에 노출한다.
    /// </summary>
    /// <remarks>
    /// <para>역할 분리:</para>
    /// <list type="bullet">
    /// <item>Behavior: MaskLayer 인스턴스 메서드(Snapshot/Restore/ExportPng/ImportPng) 호출 담당.</item>
    /// <item>ViewModel: 결과를 Command로 수신, Undo/Redo 서비스에 Push.</item>
    /// </list>
    /// <para>Stroke 생명주기: <see cref="LayeredCanvas.PreviewMouseLeftButtonDown"/>에서 before 스냅샷을 뜨고,
    /// <see cref="LayeredCanvas.DrawingCompleted"/>에서 after를 떠 <see cref="StrokeCompletedCommand"/>로 발행.</para>
    /// <para>Load는 before/after를 직접 뜨고 동일 커맨드로 발행하므로 VM은 stroke/load를 구분할 필요 없이 Push만 한다.</para>
    /// </remarks>
    public sealed class MaskBehavior : Behavior<LayeredCanvas>
    {
        #region DependencyProperties

        public static readonly DependencyProperty MaskLayerProperty =
            DependencyProperty.Register(
                nameof(MaskLayer),
                typeof(MaskLayer),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnMaskLayerChanged));

        private static void OnMaskLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not MaskBehavior self) return;
            if (e.OldValue is MaskLayer oldM)
            {
                oldM.SelectedInstanceChanged -= self.OnMaskSelectedInstanceChanged;
                oldM.VertexEditModeChanged -= self.OnVertexEditModeChanged;
            }
            if (e.NewValue is MaskLayer newM)
            {
                newM.SelectedInstanceChanged += self.OnMaskSelectedInstanceChanged;
                newM.VertexEditModeChanged += self.OnVertexEditModeChanged;
            }
        }

        private MaskInstanceAdorner? _currentAdorner;
        private PolygonVertexAdorner? _vertexAdorner;

        private void OnMaskSelectedInstanceChanged(object? sender, EventArgs e)
        {
            RefreshAdorner();
        }

        private void OnVertexEditModeChanged(object? sender, EventArgs e)
        {
            RefreshAdorner();
        }

        private void RefreshAdorner()
        {
            var mask = MaskLayer;
            if (mask == null) return;
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(mask);
            if (layer == null) return;

            if (_currentAdorner != null)
            {
                _currentAdorner.CommitRequested -= OnAdornerCommitRequested;
                layer.Remove(_currentAdorner);
                _currentAdorner = null;
            }
            if (_vertexAdorner != null)
            {
                _vertexAdorner.CommitRequested -= OnVertexAdornerCommitRequested;
                layer.Remove(_vertexAdorner);
                _vertexAdorner = null;
            }

            var inst = mask.SelectedInstance;
            if (inst == null) return;

            if (mask.IsVertexEditMode && inst.PolygonPoints != null && inst.PolygonPoints.Count >= 3)
            {
                _vertexAdorner = new PolygonVertexAdorner(mask, inst);
                _vertexAdorner.CommitRequested += OnVertexAdornerCommitRequested;
                layer.Add(_vertexAdorner);
                _vertexAdorner.Focus();
            }
            else
            {
                _currentAdorner = new MaskInstanceAdorner(mask, inst);
                _currentAdorner.CommitRequested += OnAdornerCommitRequested;
                layer.Add(_currentAdorner);
            }
        }

        private void OnVertexAdornerCommitRequested(object? sender, PolygonVertexCommitEventArgs e)
        {
            var mask = MaskLayer;
            if (mask == null) return;
            mask.BeginDiffRecording();
            mask.RepaintPolygon(e.InstanceId, e.Contours);
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);
        }

        private void OnAdornerCommitRequested(object? sender, MaskInstanceResizeEventArgs e)
        {
            var mask = MaskLayer;
            if (mask == null) return;
            mask.BeginDiffRecording();
            mask.ResampleInstance(e.InstanceId, e.NewBoundingBox);
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);
        }

        public static readonly DependencyProperty StrokeCompletedCommandProperty =
            DependencyProperty.Register(
                nameof(StrokeCompletedCommand),
                typeof(ICommand),
                typeof(MaskBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SavePngRequestProperty =
            DependencyProperty.Register(
                nameof(SavePngRequest),
                typeof(MaskIoRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnSavePngRequestChanged));

        public static readonly DependencyProperty LoadPngRequestProperty =
            DependencyProperty.Register(
                nameof(LoadPngRequest),
                typeof(MaskIoRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnLoadPngRequestChanged));

        public static readonly DependencyProperty SplitInstanceRequestProperty =
            DependencyProperty.Register(
                nameof(SplitInstanceRequest),
                typeof(MaskInstanceRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnSplitInstanceRequestChanged));

        public static readonly DependencyProperty DeleteInstanceRequestProperty =
            DependencyProperty.Register(
                nameof(DeleteInstanceRequest),
                typeof(MaskInstanceRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnDeleteInstanceRequestChanged));

        public static readonly DependencyProperty DeleteInstancesRequestProperty =
            DependencyProperty.Register(
                nameof(DeleteInstancesRequest),
                typeof(MaskInstancesRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnDeleteInstancesRequestChanged));

        public static readonly DependencyProperty MergeInstancesRequestProperty =
            DependencyProperty.Register(
                nameof(MergeInstancesRequest),
                typeof(MaskInstancesMergeRequest),
                typeof(MaskBehavior),
                new PropertyMetadata(null, OnMergeInstancesRequestChanged));

        /// <summary>대상 <see cref="MaskLayer"/> 인스턴스. XAML에서 ElementName 또는 리소스로 바인딩한다.</summary>
        public MaskLayer? MaskLayer
        {
            get => (MaskLayer?)GetValue(MaskLayerProperty);
            set => SetValue(MaskLayerProperty, value);
        }

        /// <summary>stroke 혹은 Load 완료 시 <see cref="MaskStrokeCompletedArgs"/>를 파라미터로 실행되는 Command.</summary>
        public ICommand? StrokeCompletedCommand
        {
            get => (ICommand?)GetValue(StrokeCompletedCommandProperty);
            set => SetValue(StrokeCompletedCommandProperty, value);
        }

        /// <summary>VM이 PNG 저장을 요청하기 위해 새 <see cref="MaskIoRequest"/>를 set 한다.</summary>
        public MaskIoRequest? SavePngRequest
        {
            get => (MaskIoRequest?)GetValue(SavePngRequestProperty);
            set => SetValue(SavePngRequestProperty, value);
        }

        /// <summary>VM이 PNG 로드를 요청하기 위해 새 <see cref="MaskIoRequest"/>를 set 한다.</summary>
        public MaskIoRequest? LoadPngRequest
        {
            get => (MaskIoRequest?)GetValue(LoadPngRequestProperty);
            set => SetValue(LoadPngRequestProperty, value);
        }

        /// <summary>VM이 특정 인스턴스 분리(CCL)를 요청하기 위해 새 요청 토큰을 set 한다.</summary>
        public MaskInstanceRequest? SplitInstanceRequest
        {
            get => (MaskInstanceRequest?)GetValue(SplitInstanceRequestProperty);
            set => SetValue(SplitInstanceRequestProperty, value);
        }

        /// <summary>VM이 특정 인스턴스 삭제를 요청하기 위해 새 요청 토큰을 set 한다.</summary>
        public MaskInstanceRequest? DeleteInstanceRequest
        {
            get => (MaskInstanceRequest?)GetValue(DeleteInstanceRequestProperty);
            set => SetValue(DeleteInstanceRequestProperty, value);
        }

        /// <summary>VM이 다중 인스턴스 삭제를 요청하기 위해 새 요청 토큰을 set 한다.</summary>
        public MaskInstancesRequest? DeleteInstancesRequest
        {
            get => (MaskInstancesRequest?)GetValue(DeleteInstancesRequestProperty);
            set => SetValue(DeleteInstancesRequestProperty, value);
        }

        /// <summary>VM이 다중 인스턴스 병합을 요청하기 위해 새 요청 토큰을 set 한다.</summary>
        public MaskInstancesMergeRequest? MergeInstancesRequest
        {
            get => (MaskInstancesMergeRequest?)GetValue(MergeInstancesRequestProperty);
            set => SetValue(MergeInstancesRequestProperty, value);
        }

        #endregion

        private bool _diffRecordingActive;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.DrawingCompleted += OnDrawingCompleted;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.DrawingCompleted -= OnDrawingCompleted;
            base.OnDetaching();
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;

            var tool = AssociatedObject.CurrentTool;
            if (tool is IMaskMutatingTool)
            {
                mask.BeginDiffRecording();
                _diffRecordingActive = true;
            }
        }

        private void OnDrawingCompleted(object? sender, EventArgs e)
        {
            var mask = MaskLayer;
            System.Diagnostics.Debug.WriteLine($"[OnDrawingCompleted] recording={_diffRecordingActive}, mask={mask != null}");
            if (mask == null || !_diffRecordingActive) { _diffRecordingActive = false; return; }
            _diffRecordingActive = false;
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);

            // 메모리 진단 — 모든 stroke 완료 후 GC 강제 + Working Set 측정.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            System.Diagnostics.Debug.WriteLine($"[Memory] After full GC: managed={System.GC.GetTotalMemory(true) / 1024 / 1024} MB, WorkingSet={System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
        }

        private static void OnSavePngRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskIoRequest req)
                self.HandleSave(req);
        }

        private static void OnLoadPngRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskIoRequest req)
                self.HandleLoad(req);
        }

        private void HandleSave(MaskIoRequest req)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;
            // COCO JSON 으로 저장. 이미지 파일명은 경로의 파일명만 취함(필요 시 VM 에서 더 정교한 값 주입).
            mask.ExportCocoJson(req.Path, System.IO.Path.GetFileNameWithoutExtension(req.Path));
        }

        private void HandleLoad(MaskIoRequest req)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;

            // Load 는 전체 교체라 diff 가 비현실적으로 큼 → Snapshot 기반 유지.
            var before = mask.SnapshotFull();
            mask.ImportCocoJson(req.Path);
            var after = mask.SnapshotFull();
            InvokeStrokeCommand(before, after, mask);
        }

        private void InvokeStrokeCommand(MaskLayerSnapshot before, MaskLayerSnapshot after, MaskLayer mask)
        {
            System.Diagnostics.Debug.WriteLine($"[StrokeCmd-SNAPSHOT] (legacy Snapshot path called! before={before != null}, after={after != null})");
            var cmd = StrokeCompletedCommand;
            if (cmd == null) return;

            var args = new MaskStrokeCompletedArgs(before, after, snapshot => mask.RestoreFull(snapshot));
            if (cmd.CanExecute(args))
                cmd.Execute(args);
        }

        /// <summary>diff 기반 args 발화 — Snapshot 200MB × N 대신 변경 픽셀 비례 메모리만 사용.</summary>
        private void InvokeStrokeCommandFromDiff(VSMVVM.WPF.Imaging.MaskLayerDiff? diff, MaskLayer mask)
        {
            System.Diagnostics.Debug.WriteLine($"[StrokeCmdFromDiff] diff={diff != null}, entries={diff?.Entries.Count ?? -1}, runs={diff?.Runs.Count ?? -1}, hasPixel={diff?.HasPixelChanges ?? false}");
            var cmd = StrokeCompletedCommand;
            if (cmd == null || diff == null) return;
            // 변경 없으면 push 안 함.
            if (!diff.HasPixelChanges
                && diff.Instances.Before.Count == diff.Instances.After.Count
                && diff.Instances.NextIdBefore == diff.Instances.NextIdAfter)
            {
                return;
            }
            var args = new MaskStrokeCompletedArgs(
                undo: () => mask.ApplyDiffReverse(diff),
                redo: () => mask.ApplyDiffForward(diff));
            if (cmd.CanExecute(args))
                cmd.Execute(args);
        }

        private static void OnSplitInstanceRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskInstanceRequest req)
                self.HandleInstanceOp(req.InstanceId, split: true);
        }

        private static void OnDeleteInstanceRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskInstanceRequest req)
                self.HandleInstanceOp(req.InstanceId, split: false);
        }

        private static void OnDeleteInstancesRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskInstancesRequest req)
                self.HandleInstancesDelete(req.InstanceIds);
        }

        private static void OnMergeInstancesRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MaskBehavior self && e.NewValue is MaskInstancesMergeRequest req)
                self.HandleInstancesMerge(req.InstanceIds);
        }

        private void HandleInstanceOp(uint instanceId, bool split)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;
            if (instanceId == 0) return;

            mask.BeginDiffRecording();
            if (split) mask.SplitInstance(instanceId);
            else mask.DeleteInstance(instanceId);
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);
        }

        private void HandleInstancesDelete(System.Collections.Generic.IReadOnlyList<uint> ids)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;
            if (ids.Count == 0) return;

            mask.BeginDiffRecording();
            mask.DeleteInstances(ids);
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);
        }

        private void HandleInstancesMerge(System.Collections.Generic.IReadOnlyList<uint> ids)
        {
            var mask = MaskLayer;
            if (mask == null || mask.MaskWidth <= 0 || mask.MaskHeight <= 0) return;
            if (ids.Count < 2) return;

            mask.BeginDiffRecording();
            mask.MergeInstances(ids);
            var diff = mask.EndDiffRecording();
            InvokeStrokeCommandFromDiff(diff, mask);
        }
    }
}
