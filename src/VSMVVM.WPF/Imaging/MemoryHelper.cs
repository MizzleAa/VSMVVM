using System;
using System.Runtime;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// 큰 마스크 작업 (Resample / Delete / Merge / Repaint / Restore) 후 LOH 단편화 + Working Set 누적을
    /// 명시적으로 회수하는 helper. 기본 GC 정책은 자동이지만, 200MB+ 일시 alloc 직후엔 즉시 회수가 유용.
    /// </summary>
    public static class MemoryHelper
    {
        /// <summary>
        /// LOH 압축 + GC 강제 실행. 마스크 작업 끝에 가볍게 호출.
        /// </summary>
        public static void CompactAndCollect()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// LOH 압축 + Gen2 Aggressive GC + Finalizer 대기 + 2차 GC + Working Set 트리밍.
        /// 대형 리소스 해제 후 즉각적인 메모리 반환이 필요할 때 사용 (예: 이미지 unload, 프로젝트 전환).
        /// </summary>
        public static void ForceFullCleanup()
        {
            // LOH 압축 활성화 (다음 GC 에서 1회 적용).
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // 1차: 모든 세대 강제 수집 (Aggressive + blocking + compacting).
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();

            // 2차: Finalizer 에 의해 해제된 객체 회수.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

            // Working Set 트리밍 (OS 에 페이지 반환).
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                process.MinWorkingSet = (IntPtr)(-1);
            }
            catch { }
        }
    }
}