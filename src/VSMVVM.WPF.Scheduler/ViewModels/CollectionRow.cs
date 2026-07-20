using System;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>인스펙터 "자세히" 창의 테이블 행. IEnumerable은 Key=인덱스, IDictionary는 Key=키.
    /// <see cref="RawValue"/> 는 드릴다운(중첩 컬렉션 진입) 판정용 원본 참조.</summary>
    public sealed class CollectionRow
    {
        public string Key { get; }
        public string Value { get; }
        public object RawValue { get; }
        public bool HasChildren { get; }

        public CollectionRow(string key, string value, object rawValue, bool hasChildren)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value ?? "null";
            RawValue = rawValue;
            HasChildren = hasChildren;
        }
    }
}
