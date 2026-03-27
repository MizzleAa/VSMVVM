using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 타입 기반 메시지의 기본 클래스.
    /// </summary>
    public abstract class MessageBase
    {
    }

    /// <summary>
    /// 값을 전달하는 메시지.
    /// </summary>
    public class Message<T> : MessageBase
    {
        public T Value { get; }

        public Message(T value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 요청/응답 패턴의 메시지. Send 후 Response를 통해 결과를 받습니다.
    /// </summary>
    public class RequestMessage<TResponse> : MessageBase
    {
        private TResponse _response;
        private bool _hasResponse;

        /// <summary>
        /// 응답이 설정되었는지 여부.
        /// </summary>
        public bool HasResponse => _hasResponse;

        /// <summary>
        /// 응답 값. Reply가 호출되지 않았으면 예외를 발생시킵니다.
        /// </summary>
        public TResponse Response
        {
            get
            {
                if (!_hasResponse)
                    throw new InvalidOperationException("No response has been provided for this request message.");

                return _response;
            }
        }

        /// <summary>
        /// 요청에 대한 응답을 설정합니다.
        /// </summary>
        public void Reply(TResponse response)
        {
            if (_hasResponse)
                throw new InvalidOperationException("A response has already been provided for this request message.");

            _response = response;
            _hasResponse = true;
        }
    }
}
