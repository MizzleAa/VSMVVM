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
        // _response/_hasResponse를 cross-thread에서 접근할 때 memory reorder로 인해
        // _hasResponse=true이지만 _response는 아직 기본값인 상태가 보일 수 있다. lock으로 직렬화한다.
        private readonly object _lock = new object();
        private TResponse _response;
        private bool _hasResponse;

        /// <summary>
        /// 응답이 설정되었는지 여부.
        /// </summary>
        public bool HasResponse
        {
            get
            {
                lock (_lock) { return _hasResponse; }
            }
        }

        /// <summary>
        /// 응답 값. Reply가 호출되지 않았으면 예외를 발생시킵니다.
        /// </summary>
        public TResponse Response
        {
            get
            {
                lock (_lock)
                {
                    if (!_hasResponse)
                        throw new InvalidOperationException("No response has been provided for this request message.");

                    return _response;
                }
            }
        }

        /// <summary>
        /// 요청에 대한 응답을 설정합니다.
        /// </summary>
        public void Reply(TResponse response)
        {
            lock (_lock)
            {
                if (_hasResponse)
                    throw new InvalidOperationException("A response has already been provided for this request message.");

                _response = response;
                _hasResponse = true;
            }
        }
    }
}
