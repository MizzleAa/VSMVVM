using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 노드 실행 직후 캡처된 한 핀의 값 스냅샷. 인스펙터 패널의 ItemsControl 항목 모델.
    /// 불변 — 다음 실행에서는 새 인스턴스로 교체된다.
    /// </summary>
    public sealed class PinValueSnapshot
    {
        private const int SummaryPreviewCount = 5;
        private const int SummaryMaxLength = 100;

        private IReadOnlyList<CollectionRow> _expandedRows;

        public string PinId { get; }
        public string DisplayName { get; }
        public object Value { get; }
        public PinDirection Direction { get; }

        public PinValueSnapshot(string pinId, string displayName, object value, PinDirection direction)
        {
            PinId = pinId ?? throw new ArgumentNullException(nameof(pinId));
            DisplayName = string.IsNullOrEmpty(displayName) ? pinId : displayName;
            Value = value;
            Direction = direction;
        }

        /// <summary>UI 바인딩용 — null/숫자/문자열을 안전한 문자열로 변환.
        /// 컬렉션은 앞 5개 요소를 요약 (예: "[1, 2.5, 3.5, 4] (4)"). 100자 초과 시 뒤를 자른다.</summary>
        public string DisplayValue
        {
            get
            {
                if (Value == null) return "(null)";
                string s;
                switch (Value)
                {
                    case string str: s = "\"" + str + "\""; break;
                    case IDictionary dict: s = FormatDictionary(dict); break;
                    case IEnumerable en: s = FormatEnumerable(en); break;
                    case IFormattable f: s = f.ToString(null, CultureInfo.InvariantCulture); break;
                    default: s = Value.ToString(); break;
                }
                if (s.Length > SummaryMaxLength) s = s.Substring(0, SummaryMaxLength - 3) + "...";
                return s;
            }
        }

        /// <summary>UI 보조 — 값의 CLR 타입 짧은 이름.</summary>
        public string TypeName => Value?.GetType().Name ?? "null";

        /// <summary>string 제외 IEnumerable/IDictionary 여부. 인스펙터의 "자세히" 버튼 노출 조건.</summary>
        public bool IsCollection => Value is IEnumerable && !(Value is string);

        /// <summary>자세히 팝업 바인딩 대상. IDictionary는 (Key, Value), 그 외 IEnumerable은 (Index, Value).
        /// 컬렉션이 아니면 빈 리스트. 최초 접근 시 한 번 열거해서 캐시.</summary>
        public IReadOnlyList<CollectionRow> ExpandedRows
        {
            get
            {
                if (_expandedRows != null) return _expandedRows;
                _expandedRows = BuildExpandedRows();
                return _expandedRows;
            }
        }

        private IReadOnlyList<CollectionRow> BuildExpandedRows()
            => BuildRowsFor(Value);

        /// <summary>주어진 컬렉션 값에 대해 (Key, Value요약, RawValue, HasChildren) 행을 생성.
        /// 드릴다운 창이 각 레벨마다 재호출한다.</summary>
        internal static IReadOnlyList<CollectionRow> BuildRowsFor(object value)
        {
            if (value == null || (value is string) || !(value is IEnumerable)) return Array.Empty<CollectionRow>();

            var rows = new List<CollectionRow>();
            if (value is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                    rows.Add(new CollectionRow(FormatElement(entry.Key), FormatElement(entry.Value), entry.Value, IsDrillable(entry.Value)));
            }
            else
            {
                int i = 0;
                foreach (var item in (IEnumerable)value)
                {
                    rows.Add(new CollectionRow(i.ToString(CultureInfo.InvariantCulture), FormatElement(item), item, IsDrillable(item)));
                    i++;
                }
            }
            return rows;
        }

        private static bool IsDrillable(object item) => item is IEnumerable && !(item is string);

        private static string FormatEnumerable(IEnumerable en)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            int shown = 0, total = 0;
            foreach (var item in en)
            {
                if (shown < SummaryPreviewCount)
                {
                    if (shown > 0) sb.Append(", ");
                    sb.Append(FormatElement(item));
                    shown++;
                }
                total++;
            }
            if (total > shown) sb.Append(", ...");
            sb.Append(']');
            sb.Append(" (").Append(total.ToString(CultureInfo.InvariantCulture)).Append(')');
            return sb.ToString();
        }

        private static string FormatDictionary(IDictionary dict)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            int shown = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (shown < SummaryPreviewCount)
                {
                    if (shown > 0) sb.Append(", ");
                    sb.Append(FormatElement(entry.Key)).Append(": ").Append(FormatElement(entry.Value));
                    shown++;
                }
            }
            if (dict.Count > shown) sb.Append(", ...");
            sb.Append('}');
            sb.Append(" (").Append(dict.Count.ToString(CultureInfo.InvariantCulture)).Append(')');
            return sb.ToString();
        }

        private static string FormatElement(object item)
        {
            if (item == null) return "null";
            switch (item)
            {
                case string s: return "\"" + s + "\"";
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                case IDictionary d: return FormatDictionary(d);
                case IEnumerable en: return FormatEnumerable(en);
                default: return item.ToString();
            }
        }
    }
}
