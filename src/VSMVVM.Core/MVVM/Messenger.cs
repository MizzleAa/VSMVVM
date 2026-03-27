using System;
using System.Collections.Generic;
using System.Linq;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// WeakReference 기반 메시지 시스템 구현체.
    /// GC 수집 시 자동으로 수신자가 해제되어 메모리 누수를 방지합니다.
    /// </summary>
    public sealed class Messenger : IMessenger
    {
        #region Inner Types

        /// <summary>
        /// 메시지 등록 키: 메시지 타입 + 토큰 조합.
        /// </summary>
        private struct RegistrationKey : IEquatable<RegistrationKey>
        {
            public Type MessageType;
            public string Token;

            public RegistrationKey(Type messageType, string token)
            {
                MessageType = messageType;
                Token = token;
            }

            public bool Equals(RegistrationKey other)
            {
                return MessageType == other.MessageType && Token == other.Token;
            }

            public override bool Equals(object obj)
            {
                return obj is RegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = MessageType?.GetHashCode() ?? 0;
                    hash = (hash * 397) ^ (Token?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }

        /// <summary>
        /// 수신자와 핸들러를 약한 참조로 유지하는 구독 정보.
        /// </summary>
        private sealed class Subscription
        {
            public WeakReference RecipientRef { get; }
            public Delegate Handler { get; }

            public Subscription(object recipient, Delegate handler)
            {
                RecipientRef = new WeakReference(recipient);
                Handler = handler;
            }

            public bool IsAlive => RecipientRef.IsAlive;
            public object Target => RecipientRef.Target;
        }

        #endregion

        #region Fields

        private readonly Dictionary<RegistrationKey, List<Subscription>> _subscriptions
            = new Dictionary<RegistrationKey, List<Subscription>>();

        private readonly object _lock = new object();

        #endregion

        #region IMessenger

        public void Register<TMessage>(object recipient, Action<object, TMessage> handler) where TMessage : MessageBase
        {
            Register(recipient, null, handler);
        }

        public void Register<TMessage>(object recipient, string token, Action<object, TMessage> handler) where TMessage : MessageBase
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var key = new RegistrationKey(typeof(TMessage), token);

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(key, out var list))
                {
                    list = new List<Subscription>();
                    _subscriptions[key] = list;
                }

                list.Add(new Subscription(recipient, handler));
            }
        }

        public TMessage Send<TMessage>(TMessage message) where TMessage : MessageBase
        {
            return SendInternal(message, null);
        }

        public TMessage Send<TMessage>(TMessage message, string token) where TMessage : MessageBase
        {
            return SendInternal(message, token);
        }

        public TMessage Send<TMessage>() where TMessage : MessageBase, new()
        {
            var message = new TMessage();
            return SendInternal(message, null);
        }

        public void Unregister<TMessage>(object recipient) where TMessage : MessageBase
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));

            lock (_lock)
            {
                var keysToProcess = _subscriptions.Keys
                    .Where(k => k.MessageType == typeof(TMessage))
                    .ToList();

                foreach (var key in keysToProcess)
                {
                    var list = _subscriptions[key];
                    list.RemoveAll(s => !s.IsAlive || ReferenceEquals(s.Target, recipient));

                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(key);
                    }
                }
            }
        }

        public void UnregisterAll(object recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));

            lock (_lock)
            {
                var keysToProcess = _subscriptions.Keys.ToList();

                foreach (var key in keysToProcess)
                {
                    var list = _subscriptions[key];
                    list.RemoveAll(s => !s.IsAlive || ReferenceEquals(s.Target, recipient));

                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(key);
                    }
                }
            }
        }

        public void Cleanup()
        {
            lock (_lock)
            {
                var keysToProcess = _subscriptions.Keys.ToList();

                foreach (var key in keysToProcess)
                {
                    var list = _subscriptions[key];
                    list.RemoveAll(s => !s.IsAlive);

                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(key);
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private TMessage SendInternal<TMessage>(TMessage message, string token) where TMessage : MessageBase
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            List<Subscription> snapshot;
            var key = new RegistrationKey(typeof(TMessage), token);

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(key, out var list))
                    return message;

                // 스냅샷으로 복사하여 lock 밖에서 실행
                snapshot = new List<Subscription>(list);
            }

            var deadSubscriptions = new List<Subscription>();

            foreach (var subscription in snapshot)
            {
                if (!subscription.IsAlive)
                {
                    deadSubscriptions.Add(subscription);
                    continue;
                }

                var recipient = subscription.Target;
                var handler = (Action<object, TMessage>)subscription.Handler;
                handler(recipient, message);
            }

            // 죽은 참조 정리
            if (deadSubscriptions.Count > 0)
            {
                lock (_lock)
                {
                    if (_subscriptions.TryGetValue(key, out var list))
                    {
                        foreach (var dead in deadSubscriptions)
                        {
                            list.Remove(dead);
                        }

                        if (list.Count == 0)
                        {
                            _subscriptions.Remove(key);
                        }
                    }
                }
            }

            return message;
        }

        #endregion
    }
}
