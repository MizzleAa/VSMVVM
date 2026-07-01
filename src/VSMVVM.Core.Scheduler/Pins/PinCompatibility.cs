namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>
    /// 두 핀의 연결 가능 여부를 판정합니다.
    /// 규칙:
    ///   1. 방향이 반대(Output → Input)여야 한다.
    ///   2. Kind가 동일해야 한다 (Exec↔Exec 또는 Data↔Data).
    ///   3. Data 핀은 타겟 타입이 소스 타입을 받아들일 수 있어야 한다 (IsAssignableFrom).
    /// 추가 변환 규칙(IConvertible 위닝 등)은 Phase 3a 이후 converter 레지스트리에서 처리.
    /// </summary>
    public static class PinCompatibility
    {
        public static bool CanConnect(IPin source, IPin target, out string reason)
        {
            if (source == null)
            {
                reason = "Source pin is null.";
                return false;
            }
            if (target == null)
            {
                reason = "Target pin is null.";
                return false;
            }

            if (source.Direction != PinDirection.Output)
            {
                reason = "Source pin must be an output pin.";
                return false;
            }
            if (target.Direction != PinDirection.Input)
            {
                reason = "Target pin must be an input pin.";
                return false;
            }

            if (source.Kind != target.Kind)
            {
                reason = "Pin kinds do not match (Exec vs Data).";
                return false;
            }

            if (source.Kind == PinKind.Exec)
            {
                reason = null;
                return true;
            }

            // Data 핀: 타겟 타입이 소스 타입을 수용할 수 있어야 한다.
            if (!target.ValueType.IsAssignableFrom(source.ValueType))
            {
                reason = $"Type '{source.ValueType.Name}' is not assignable to '{target.ValueType.Name}'.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// registry 와 연동되는 강화 오버로드. 기본 규칙 + 양쪽 타입이 registry 에 등록(또는 자동 생성 가능)되어 있어야 함.
        /// 사용자 정의 타입은 호스트가 명시적으로 Register 해야 데이터 핀으로 연결 가능.
        /// </summary>
        public static bool CanConnect(IPin source, IPin target, IPinTypeRegistry registry, out string reason)
        {
            if (!CanConnect(source, target, out reason)) return false;
            if (source.Kind == PinKind.Exec) return true; // exec 은 타입 검사 X
            if (registry == null) return true; // registry 없으면 기본 동작.

            if (registry.GetOrCreate(source.ValueType) == null)
            {
                reason = $"Source pin type '{source.ValueType.FullName}' is not registered in PinTypeRegistry.";
                return false;
            }
            if (registry.GetOrCreate(target.ValueType) == null)
            {
                reason = $"Target pin type '{target.ValueType.FullName}' is not registered in PinTypeRegistry.";
                return false;
            }
            return true;
        }
    }
}
