using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 수명 주기를 정의합니다.
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// 앱 전체에서 하나의 인스턴스.
        /// </summary>
        Singleton,

        /// <summary>
        /// 매 요청마다 새 인스턴스.
        /// </summary>
        Transient,

        /// <summary>
        /// 스코프 내에서 동일 인스턴스.
        /// </summary>
        Scoped
    }

    /// <summary>
    /// 서비스 등록 정보를 나타냅니다.
    /// </summary>
    public sealed class ServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public ServiceLifetime Lifetime { get; }
        public object Instance { get; }
        public Func<IServiceContainer, object> Factory { get; }

        internal ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime, object instance, Func<IServiceContainer, object> factory)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            Instance = instance;
            Factory = factory;
        }
    }
}
