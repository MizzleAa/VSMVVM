using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;
using VSMVVM.WPF.Sample.Scheduler;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Mat 미리보기 다이얼로그 ViewModel.
    /// 부모(SchedulerDemoViewModel) 가 ShowWindow 시 DialogParameter 로 (ViewName, SnapshotStore) 를 전달.
    /// 본 VM 은 store 에서 ViewName 의 스냅샷을 가져와 슬라이더로 탐색 가능하게 한다.
    /// store.Changed 를 구독해 Run 종료/추가 시 자동 갱신.
    /// </summary>
    public partial class ImageViewWindowViewModel : ViewModelBase
    {
        private readonly Dispatcher _dispatcher;
        private ImageSnapshotStore _store;
        private IReadOnlyList<Mat> _snapshots = Array.Empty<Mat>();

        [Property] private string _viewName = "view";
        [Property] private BitmapSource _imageSource;
        [Property] private string _status = "(no snapshots — run the graph first)";

        /// <summary>현재 선택된 스냅샷 인덱스 (0-based).</summary>
        [Property]
        [PropertyChangedFor(nameof(IndexLabel))]
        private int _currentIndex;

        /// <summary>슬라이더 최대값 — Count-1. 항상 0 이상.</summary>
        [Property] private int _maxIndex;

        public string IndexLabel => _snapshots.Count > 0
            ? $"{CurrentIndex + 1} / {_snapshots.Count}"
            : "0 / 0";

        public event EventHandler RequestClose;

        public ImageViewWindowViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>WindowService 가 ShowWindow 시 자동 주입 — (ViewName, SnapshotStore) 튜플.</summary>
        public (string ViewName, ImageSnapshotStore Store) DialogParameter
        {
            get => (ViewName, _store);
            set
            {
                ViewName = value.ViewName ?? "view";
                if (_store != null) _store.Changed -= OnStoreChanged;
                _store = value.Store;
                if (_store != null) _store.Changed += OnStoreChanged;
                RefreshFromStore(selectLast: true);
            }
        }

        public object DialogResultData => null;

        partial void OnCurrentIndexChanged(int value)
        {
            UpdateImage();
        }

        private void OnStoreChanged(object sender, string changedView)
        {
            if (changedView != ViewName) return;
            if (_dispatcher.CheckAccess()) RefreshFromStore(selectLast: true);
            else _dispatcher.BeginInvoke(new Action(() => RefreshFromStore(selectLast: true)));
        }

        private void RefreshFromStore(bool selectLast)
        {
            _snapshots = _store?.GetAll(ViewName) ?? Array.Empty<Mat>();
            MaxIndex = Math.Max(0, _snapshots.Count - 1);
            if (_snapshots.Count == 0)
            {
                ImageSource = null;
                Status = $"(no snapshots for '{ViewName}' — run the graph)";
                CurrentIndex = 0;
                OnPropertyChanged(nameof(IndexLabel));
                return;
            }
            int target = selectLast || CurrentIndex >= _snapshots.Count
                ? _snapshots.Count - 1
                : CurrentIndex;
            // 인덱스 setter 가 같은 값이면 OnCurrentIndexChanged 가 발화 안 되므로 UpdateImage 를 직접 호출해야 갱신됨.
            // (예: CurrentIndex=0 이고 첫 번째 스냅샷이 막 도착한 경우)
            if (target == CurrentIndex)
            {
                UpdateImage();
            }
            else
            {
                CurrentIndex = target; // setter 가 UpdateImage 트리거
            }
            OnPropertyChanged(nameof(IndexLabel));
        }

        private void UpdateImage()
        {
            if (_snapshots.Count == 0) { ImageSource = null; return; }
            int idx = Math.Max(0, Math.Min(CurrentIndex, _snapshots.Count - 1));
            var mat = _snapshots[idx];
            ImageSource = TryConvert(mat, out var reason);
            Status = ImageSource != null
                ? $"'{ViewName}' #{idx + 1}/{_snapshots.Count}  ({mat?.Width}×{mat?.Height}, {mat?.Type()})"
                : $"'{ViewName}' #{idx + 1}/{_snapshots.Count}  FAILED: {reason}";
            OnPropertyChanged(nameof(IndexLabel));
        }

        /// <summary>Mat → BitmapSource. 실패 시 reason 에 사유 기록.</summary>
        private static BitmapSource TryConvert(Mat mat, out string reason)
        {
            reason = null;
            if (mat == null) { reason = "mat is null"; return null; }
            if (mat.Empty()) { reason = "mat is Empty()"; return null; }

            try
            {
                // 정규화 — 항상 8UC3(BGR) 로 변환 후 BitmapSource 생성.
                using var u8bgr = new Mat();
                using var step1 = new Mat();
                if (mat.Channels() == 1)
                    Cv2.CvtColor(mat, step1, ColorConversionCodes.GRAY2BGR);
                else if (mat.Channels() == 4)
                    Cv2.CvtColor(mat, step1, ColorConversionCodes.BGRA2BGR);
                else
                    mat.CopyTo(step1);

                if (step1.Depth() != MatType.CV_8U)
                    step1.ConvertTo(u8bgr, MatType.CV_8U);
                else
                    step1.CopyTo(u8bgr);

                if (u8bgr.Empty()) { reason = "u8bgr Empty after normalize"; return null; }

                // 직접 픽셀 복사 — Bgr24 픽셀 포맷. unsafe 없이 IntPtr + Marshal.Copy.
                int w = u8bgr.Width;
                int h = u8bgr.Height;
                int stride = ((w * 3 + 3) / 4) * 4; // 4-byte 정렬
                var bytes = new byte[stride * h];
                long matStep = (long)u8bgr.Step();
                System.IntPtr dataPtr = u8bgr.Data;
                for (int y = 0; y < h; y++)
                {
                    var rowPtr = System.IntPtr.Add(dataPtr, (int)(y * matStep));
                    System.Runtime.InteropServices.Marshal.Copy(rowPtr, bytes, y * stride, w * 3);
                }
                var bmp = BitmapSource.Create(w, h, 96, 96,
                    System.Windows.Media.PixelFormats.Bgr24, null, bytes, stride);
                if (bmp.CanFreeze) bmp.Freeze();
                return bmp;
            }
            catch (System.Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        /// <summary>레거시 호출 — reason 무시.</summary>
        private static BitmapSource TryConvert(Mat mat) => TryConvert(mat, out _);

        [RelayCommand]
        private void Prev()
        {
            if (CurrentIndex > 0) CurrentIndex--;
        }

        [RelayCommand]
        private void Next()
        {
            if (CurrentIndex < MaxIndex) CurrentIndex++;
        }

        [RelayCommand]
        private void First()
        {
            if (_snapshots.Count > 0) CurrentIndex = 0;
        }

        [RelayCommand]
        private void Last()
        {
            if (_snapshots.Count > 0) CurrentIndex = _snapshots.Count - 1;
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

        protected override void Dispose(bool disposing)
        {
            if (disposing && _store != null) _store.Changed -= OnStoreChanged;
            base.Dispose(disposing);
        }
    }
}
