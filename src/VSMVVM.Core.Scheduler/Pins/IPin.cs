using System;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>
    /// 노드의 핀(연결 가능 지점) 추상화. Exec/Data 두 종류 모두를 표현합니다.
    /// </summary>
    public interface IPin
    {
        /// <summary>노드 내 핀 식별자(멤버 이름과 일치).</summary>
        string Id { get; }

        /// <summary>UI 표시용 이름. 미지정 시 Id와 동일.</summary>
        string DisplayName { get; }

        PinDirection Direction { get; }
        PinKind Kind { get; }

        /// <summary>핀이 운반하는 값의 CLR 타입. Exec 핀은 typeof(void).</summary>
        Type ValueType { get; }

        /// <summary>이 핀을 소유한 노드.</summary>
        INode Owner { get; }
    }
}
