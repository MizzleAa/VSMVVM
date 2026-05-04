using System;
using System.Collections.Generic;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 등록 구현체. Singleton/Transient/Scoped 라이프사이클 지원.
    /// </summary>
    public sealed class ServiceCollection : IServiceCollection
    {
        #region Fields

        private readonly Dictionary<Type, ServiceDescriptor> _descriptors = new Dictionary<Type, ServiceDescriptor>();
        private readonly Dictionary<string, Type> _keyTypes = new Dictionary<string, Type>();

        #endregion

        #region Constructor

        public ServiceCollection()
        {
        }

        #endregion

        #region Static Factory

        public static IServiceCollection Create()
        {
            return new ServiceCollection();
        }

        #endregion

        #region Private Methods

        private void Register(Type serviceType, Type implementationType, ServiceLifetime lifetime, object instance, Func<IServiceContainer, object> factory)
        {
            // 단순명(Type.Name) 키는 GetService(string) / Navigate(string viewName) 등에서 사용된다.
            // 서로 다른 네임스페이스의 동일 이름 타입이 등록되면 조용히 덮어써져 잘못된 타입이 해석되는
            // 사일런트 버그가 발생하므로, 충돌 시 명확하게 throw한다.
            if (_keyTypes.TryGetValue(serviceType.Name, out var existing) && existing != serviceType)
            {
                throw new InvalidOperationException(
                    $"Service name conflict: '{serviceType.Name}' is already registered as '{existing.FullName}', " +
                    $"cannot register '{serviceType.FullName}' with the same simple name. " +
                    $"Rename one of the types or use a distinct namespace.");
            }

            _keyTypes[serviceType.Name] = serviceType;
            _descriptors[serviceType] = new ServiceDescriptor(serviceType, implementationType, lifetime, instance, factory);
        }

        #endregion

        #region Singleton

        public void AddSingleton<TInterface, TImplementation>() where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Singleton, null, null);
        }

        public void AddSingleton<TImplementation>() where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Singleton, null, null);
        }

        public void AddSingleton<TImplementation>(TImplementation implementation) where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Singleton, implementation, null);
        }

        public void AddSingleton<TInterface, TImplementation>(TImplementation implementation) where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Singleton, implementation, null);
        }

        public void AddSingleton<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Singleton, null, c => factory(c));
        }

        public void AddSingleton<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Singleton, null, c => factory(c));
        }

        #endregion

        #region Transient

        public void AddTransient<TInterface, TImplementation>() where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Transient, null, null);
        }

        public void AddTransient<TImplementation>() where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Transient, null, null);
        }

        public void AddTransient<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Transient, null, c => factory(c));
        }

        public void AddTransient<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Transient, null, c => factory(c));
        }

        #endregion

        #region Scoped

        public void AddScoped<TInterface, TImplementation>() where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Scoped, null, null);
        }

        public void AddScoped<TImplementation>() where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Scoped, null, null);
        }

        public void AddScoped<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), ServiceLifetime.Scoped, null, c => factory(c));
        }

        public void AddScoped<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class
        {
            Register(typeof(TImplementation), typeof(TImplementation), ServiceLifetime.Scoped, null, c => factory(c));
        }

        #endregion

        #region Query

        public bool ContainsService(Type type)
        {
            return _descriptors.ContainsKey(type);
        }

        public Type GetKeyType(string name)
        {
            if (_keyTypes.TryGetValue(name, out var type))
                return type;

            return null;
        }

        public ServiceDescriptor GetDescriptor(Type type)
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
            {
                throw new InvalidOperationException($"Service not registered: {type.FullName}");
            }

            return descriptor;
        }

        #endregion

        #region Container

        public IServiceContainer CreateContainer()
        {
            return new ServiceContainer(this);
        }

        #endregion
    }
}
