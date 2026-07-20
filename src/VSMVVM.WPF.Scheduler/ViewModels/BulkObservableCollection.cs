using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// ObservableCollection 확장 — 여러 아이템을 한 번의 <see cref="NotifyCollectionChangedAction.Reset"/>
    /// 알림으로 추가/제거할 수 있게 해준다. WPF ItemsControl 은 Reset 을 받으면 items host 를 한 번만
    /// 재구성하므로, 폭주 시 개별 Add 대비 UI 부하가 훨씬 낮다.
    ///
    /// 부하 시나리오 전용 — 소규모 사용은 기존 <see cref="ObservableCollection{T}"/> 그대로 써도 무방.
    /// </summary>
    public sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public BulkObservableCollection() { }

        public BulkObservableCollection(IEnumerable<T> items) : base(items) { }

        /// <summary>여러 아이템을 한 번의 Reset 알림으로 추가.</summary>
        public void AddRange(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) return;
            var list = Items;
            for (int i = 0; i < items.Count; i++) list.Add(items[i]);
            RaiseResetNotifications();
        }

        /// <summary>앞에서 <paramref name="count"/> 개를 한 번의 Reset 알림으로 제거.</summary>
        public void RemoveRangeFromStart(int count)
        {
            if (count <= 0) return;
            var list = Items;
            if (count > list.Count) count = list.Count;
            for (int i = 0; i < count; i++) list.RemoveAt(0);
            RaiseResetNotifications();
        }

        private void RaiseResetNotifications()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
