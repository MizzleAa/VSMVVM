using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// WeakReference 기반 메시지 시스템 인터페이스.
    /// Pub/Sub + 요청/응답 + Token 채널 분리 지원.
    /// </summary>
    public interface IMessenger
    {
        /// <summary>
        /// 메시지를 수신 등록합니다.
        /// </summary>
        void Register<TMessage>(object recipient, Action<object, TMessage> handler) where TMessage : MessageBase;

        /// <summary>
        /// 토큰 기반 채널에 메시지를 수신 등록합니다.
        /// </summary>
        void Register<TMessage>(object recipient, string token, Action<object, TMessage> handler) where TMessage : MessageBase;

        /// <summary>
        /// 등록된 모든 수신자에게 메시지를 전송합니다.
        /// </summary>
        TMessage Send<TMessage>(TMessage message) where TMessage : MessageBase;

        /// <summary>
        /// 토큰 기반 채널로 메시지를 전송합니다.
        /// </summary>
        TMessage Send<TMessage>(TMessage message, string token) where TMessage : MessageBase;

        /// <summary>
        /// 파라미터 없는 RequestMessage를 생성하여 전송하고 응답을 반환합니다.
        /// </summary>
        TMessage Send<TMessage>() where TMessage : MessageBase, new();

        /// <summary>
        /// 특정 수신자의 특정 메시지 타입 등록을 해제합니다.
        /// </summary>
        void Unregister<TMessage>(object recipient) where TMessage : MessageBase;

        /// <summary>
        /// 특정 수신자의 모든 메시지 등록을 해제합니다.
        /// </summary>
        void UnregisterAll(object recipient);

        /// <summary>
        /// GC 수집된 약한 참조를 정리합니다.
        /// </summary>
        void Cleanup();
    }
}
