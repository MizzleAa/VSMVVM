using System;
using OpenCvSharp;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>
    /// ImageViewNode 가 실행될 때마다 발화. ImageViewWindow 가 ViewName 기준으로 구독.
    /// Sample 전용 — OpenCvSharp 는 Sample 어셈블리에만 의존.
    /// </summary>
    public sealed class ImageViewMessage : MessageBase
    {
        /// <summary>다이얼로그 식별자 — 동일 ViewName 에 해당하는 다이얼로그가 받음.</summary>
        public string ViewName { get; }
        public Mat Image { get; }
        public DateTimeOffset Timestamp { get; }

        public ImageViewMessage(string viewName, Mat image)
        {
            ViewName = viewName ?? string.Empty;
            Image = image;
            Timestamp = DateTimeOffset.Now;
        }
    }
}
