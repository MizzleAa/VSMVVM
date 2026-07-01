using System.Collections.Generic;
using OpenCvSharp;

namespace VSMVVM.WPF.Sample.Scheduler
{
    /// <summary>
    /// ImageViewNode 의 실행 스냅샷을 ViewName 별로 누적 보관.
    /// Run 시작 시(SchedulerDemoViewModel 이 NodeEnteringMessage 의 RunId 변경 감지) 자동 클리어.
    /// 다이얼로그가 더블클릭으로 열릴 때 해당 ViewName 의 모든 스냅샷을 슬라이더로 탐색 가능.
    /// </summary>
    public sealed class ImageSnapshotStore
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, List<Mat>> _byView = new();

        /// <summary>스냅샷 추가 — Mat 은 호출자가 Clone 해서 전달해야 한다 (소유권 이전).</summary>
        public void Push(string viewName, Mat snapshot)
        {
            if (string.IsNullOrEmpty(viewName) || snapshot == null) return;
            lock (_sync)
            {
                if (!_byView.TryGetValue(viewName, out var list))
                {
                    list = new List<Mat>();
                    _byView[viewName] = list;
                }
                list.Add(snapshot);
            }
        }

        /// <summary>현재 ViewName 의 스냅샷 스냅 (얕은 복사 — Mat 자체는 공유, 인덱스 안정성용).</summary>
        public IReadOnlyList<Mat> GetAll(string viewName)
        {
            lock (_sync)
            {
                if (!_byView.TryGetValue(viewName, out var list)) return System.Array.Empty<Mat>();
                return list.ToArray();
            }
        }

        /// <summary>모든 ViewName 의 모든 스냅샷을 해제하고 보관소를 비운다. Run 재시작 시 호출.</summary>
        public void Clear()
        {
            lock (_sync)
            {
                foreach (var list in _byView.Values)
                {
                    foreach (var m in list) m?.Dispose();
                }
                _byView.Clear();
            }
        }

        /// <summary>스냅샷 추가/클리어 시 발화 — UI 가 슬라이더 max 갱신용으로 구독.</summary>
        public event System.EventHandler<string> Changed;

        internal void RaiseChanged(string viewName)
        {
            Changed?.Invoke(this, viewName);
        }
    }
}
