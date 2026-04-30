using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using VSMVVM.WPF.Controls;
using VSMVVM.WPF.Sample.ViewModels;
using VSMVVM.WPF.Sample.Views;

#nullable enable
namespace VSMVVM.WPF.Sample.Behaviors
{
    /// <summary>
    /// 코드비하인드를 대체하여 View 에 부착하면 다음을 수행:
    /// - DataContext 의 <see cref="ImageViewerDemoViewModel"/> 에 <see cref="MaskLayer"/> DP 를 주입
    /// - VM 의 <c>IsNameEditorOpen</c>/<c>IsColorEditorOpen</c> true↔false 전이를 감지해
    ///   <see cref="LabelNameEditDialog"/>/<see cref="LabelColorEditDialog"/> 를 Show/Close
    /// - Dialog 가 X 버튼/Esc 로 닫힐 때 해당 Cancel 커맨드를 호출
    /// - 가능하면 <see cref="DialogPlacementTarget"/> 좌측 바깥에 Dialog 배치
    /// </summary>
    public sealed class LabelEditDialogBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty MaskLayerProperty =
            DependencyProperty.Register(
                nameof(MaskLayer),
                typeof(MaskLayer),
                typeof(LabelEditDialogBehavior),
                new PropertyMetadata(null, (d, _) => ((LabelEditDialogBehavior)d).TryInjectMaskLayer()));

        public static readonly DependencyProperty DialogPlacementTargetProperty =
            DependencyProperty.Register(
                nameof(DialogPlacementTarget),
                typeof(FrameworkElement),
                typeof(LabelEditDialogBehavior),
                new PropertyMetadata(null));

        public MaskLayer? MaskLayer
        {
            get => (MaskLayer?)GetValue(MaskLayerProperty);
            set => SetValue(MaskLayerProperty, value);
        }

        public FrameworkElement? DialogPlacementTarget
        {
            get => (FrameworkElement?)GetValue(DialogPlacementTargetProperty);
            set => SetValue(DialogPlacementTargetProperty, value);
        }

        private ImageViewerDemoViewModel? _subscribedVm;
        private LabelNameEditDialog? _nameDialog;
        private LabelColorEditDialog? _colorDialog;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;
            Subscribe();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;
            Unsubscribe();
            CloseNameDialog();
            CloseColorDialog();
            base.OnDetaching();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Unsubscribe();
            Subscribe();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Subscribe();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unsubscribe();
            CloseNameDialog();
            CloseColorDialog();
        }

        private void Subscribe()
        {
            if (_subscribedVm != null) return;
            if (AssociatedObject.DataContext is not ImageViewerDemoViewModel vm) return;
            _subscribedVm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            TryInjectMaskLayer();
        }

        private void Unsubscribe()
        {
            if (_subscribedVm == null) return;
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm = null;
        }

        private void TryInjectMaskLayer()
        {
            if (_subscribedVm == null) return;
            if (MaskLayer == null) return;
            _subscribedVm.MaskLayer = MaskLayer;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ImageViewerDemoViewModel vm) return;
            if (e.PropertyName == nameof(ImageViewerDemoViewModel.IsNameEditorOpen))
            {
                if (vm.IsNameEditorOpen) ShowNameDialog(vm);
                else CloseNameDialog();
            }
            else if (e.PropertyName == nameof(ImageViewerDemoViewModel.IsColorEditorOpen))
            {
                if (vm.IsColorEditorOpen) ShowColorDialog(vm);
                else CloseColorDialog();
            }
        }

        private void ShowNameDialog(ImageViewerDemoViewModel vm)
        {
            if (_nameDialog != null) return;
            try
            {
                _nameDialog = new LabelNameEditDialog
                {
                    Owner = Window.GetWindow(AssociatedObject),
                    DataContext = vm,
                };
                _nameDialog.Closing += OnNameDialogClosing;
                PlaceDialog(_nameDialog);
                _nameDialog.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LabelNameDialog] Show failed: " + ex);
                _nameDialog = null;
                vm.IsNameEditorOpen = false;
            }
        }

        private void ShowColorDialog(ImageViewerDemoViewModel vm)
        {
            if (_colorDialog != null) return;
            try
            {
                _colorDialog = new LabelColorEditDialog
                {
                    Owner = Window.GetWindow(AssociatedObject),
                    DataContext = vm,
                };
                _colorDialog.Closing += OnColorDialogClosing;
                PlaceDialog(_colorDialog);
                _colorDialog.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LabelColorDialog] Show failed: " + ex);
                _colorDialog = null;
                vm.IsColorEditorOpen = false;
            }
        }

        private void CloseNameDialog()
        {
            if (_nameDialog == null) return;
            var d = _nameDialog;
            _nameDialog = null;
            d.Closing -= OnNameDialogClosing;
            try { d.Close(); } catch { }
        }

        private void CloseColorDialog()
        {
            if (_colorDialog == null) return;
            var d = _colorDialog;
            _colorDialog = null;
            d.Closing -= OnColorDialogClosing;
            try { d.Close(); } catch { }
        }

        private void OnNameDialogClosing(object? sender, CancelEventArgs e)
        {
            if (_subscribedVm?.IsNameEditorOpen == true)
                _subscribedVm.CancelLabelNameCommand.Execute(null);
        }

        private void OnColorDialogClosing(object? sender, CancelEventArgs e)
        {
            if (_subscribedVm?.IsColorEditorOpen == true)
                _subscribedVm.CancelLabelColorCommand.Execute(null);
        }

        private void PlaceDialog(Window dlg)
        {
            var target = DialogPlacementTarget;
            if (target == null || !target.IsLoaded)
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                return;
            }
            try
            {
                var screenPt = target.PointToScreen(new Point(0, 0));
                double dlgWidth = dlg.Width;
                if (double.IsNaN(dlgWidth) || dlgWidth <= 0) dlgWidth = 320;
                double left = screenPt.X - dlgWidth - 12;
                double top = screenPt.Y;
                if (left < SystemParameters.VirtualScreenLeft || top < SystemParameters.VirtualScreenTop)
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    return;
                }
                dlg.Left = left;
                dlg.Top = top;
            }
            catch
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }
    }
}
