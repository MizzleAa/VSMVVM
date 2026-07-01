using System;
using System.Collections.Generic;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>
    /// 데이터 핀 타입의 등록소 — 호스트 앱이 사용자 타입을 추가하는 확장 포인트.
    /// </summary>
    public interface IPinTypeRegistry
    {
        /// <summary>새 타입 등록. 중복 등록 시 InvalidOperationException.</summary>
        void Register(PinTypeInfo info);

        /// <summary>CLR 타입 정확 일치로 조회 — 없으면 null.</summary>
        PinTypeInfo Get(Type clrType);

        /// <summary>
        /// CLR 타입 조회 시도 + 미등록이면 generic 인스턴스 자동 생성 (open generic 이 등록되어 있으면).
        /// 사용자 정의 클래스가 명시적으로 등록되지 않았으면 null.
        /// </summary>
        PinTypeInfo GetOrCreate(Type clrType);

        /// <summary>StableName 으로 조회 — 없으면 null. 직렬화/역직렬화 경로.</summary>
        PinTypeInfo GetByStableName(string stableName);

        /// <summary>등록 순서대로 모든 타입.</summary>
        IReadOnlyList<PinTypeInfo> All { get; }
    }
}
