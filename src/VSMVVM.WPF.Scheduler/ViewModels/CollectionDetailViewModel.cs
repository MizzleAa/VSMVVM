using System.Collections.Generic;
using System.Collections.ObjectModel;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>브레드크럼 한 세그먼트. <see cref="Value"/> 는 해당 지점의 원본 컬렉션 값.</summary>
    public sealed class CollectionPathSegment
    {
        public string Label { get; }
        public object Value { get; }

        public CollectionPathSegment(string label, object value)
        {
            Label = label ?? string.Empty;
            Value = value;
        }
    }

    /// <summary>
    /// 인스펙터 "자세히" 창의 상태. 루트 컬렉션에서 셀 더블클릭으로 안쪽 컬렉션에 진입/이탈하며 탐색.
    /// Path 는 브레드크럼 표시 및 되돌아가기용, Rows 는 현재 계층의 (Key, Value요약).
    /// </summary>
    public partial class CollectionDetailViewModel : ViewModelBase
    {
        public string RootDisplayName { get; }

        public ObservableCollection<CollectionPathSegment> Path { get; } = new();
        public ObservableCollection<CollectionRow> Rows { get; } = new();

        private string _headerType;
        public string HeaderType
        {
            get => _headerType;
            private set => SetProperty(ref _headerType, value);
        }

        private int _headerCount;
        public int HeaderCount
        {
            get => _headerCount;
            private set => SetProperty(ref _headerCount, value);
        }

        public CollectionDetailViewModel(string rootDisplayName, object rootValue)
        {
            RootDisplayName = string.IsNullOrEmpty(rootDisplayName) ? "Value" : rootDisplayName;
            Path.Add(new CollectionPathSegment(RootDisplayName, rootValue));
            RefreshFromCurrent();
        }

        /// <summary>주어진 행이 컬렉션이면 그 안으로 진입 — Path 에 세그먼트 추가.</summary>
        [RelayCommand]
        private void DrillDown(CollectionRow row)
        {
            if (row == null || !row.HasChildren) return;
            Path.Add(new CollectionPathSegment("[" + row.Key + "]", row.RawValue));
            RefreshFromCurrent();
        }

        /// <summary>브레드크럼에서 세그먼트 클릭 — 그 세그먼트까지만 남기고 뒤를 잘라낸다.</summary>
        [RelayCommand]
        private void NavigateTo(CollectionPathSegment segment)
        {
            if (segment == null) return;
            int idx = Path.IndexOf(segment);
            if (idx < 0 || idx == Path.Count - 1) return;
            for (int i = Path.Count - 1; i > idx; i--) Path.RemoveAt(i);
            RefreshFromCurrent();
        }

        private void RefreshFromCurrent()
        {
            var current = Path.Count > 0 ? Path[Path.Count - 1].Value : null;
            Rows.Clear();
            var built = PinValueSnapshot.BuildRowsFor(current);
            foreach (var r in built) Rows.Add(r);

            HeaderType = current?.GetType().Name ?? "null";
            HeaderCount = built.Count;
        }
    }
}
