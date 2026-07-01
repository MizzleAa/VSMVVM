using System;
using System.Globalization;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.WPF.Scheduler.ViewModels
{
    /// <summary>
    /// 노드 실행 직후 캡처된 한 핀의 값 스냅샷. 인스펙터 패널의 ItemsControl 항목 모델.
    /// 불변 — 다음 실행에서는 새 인스턴스로 교체된다.
    /// </summary>
    public sealed class PinValueSnapshot
    {
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
        /// 큰 객체는 ToString() 결과를 100자로 자른다 (인스펙터 셀이 폭 제한).</summary>
        public string DisplayValue
        {
            get
            {
                if (Value == null) return "(null)";
                string s;
                switch (Value)
                {
                    case string str: s = "\"" + str + "\""; break;
                    case IFormattable f: s = f.ToString(null, CultureInfo.InvariantCulture); break;
                    default: s = Value.ToString(); break;
                }
                if (s.Length > 100) s = s.Substring(0, 97) + "...";
                return s;
            }
        }

        /// <summary>UI 보조 — 값의 CLR 타입 짧은 이름.</summary>
        public string TypeName => Value?.GetType().Name ?? "null";
    }
}
