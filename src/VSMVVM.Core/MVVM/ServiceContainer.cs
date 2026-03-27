using System;
using System.Collections.Generic;
using System.Linq;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 해석(resolve) 구현체.
    /// Reflection 기반 DI + Singleton 캐싱 + Scoped 스코프 관리.
    /// </summary>
    public sealed class ServiceContainer : IServiceContainer
    {
        #region Fields

        private readonly IServiceCollection _serviceCollection;
        private readonly Dictionary<Type, object> _singletonCache = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _scopedCache = new Dictionary<Type, object>();

        #endregion

        #region Constructor

        public ServiceContainer(IServiceCollection services)
        {
            _serviceCollection = services ?? throw new ArgumentNullException(nameof(services));
        }

        #endregion

        #region Private Methods

        private object Resolve(Type serviceType)
        {
            if (!_serviceCollection.ContainsService(serviceType))
            {
                throw new InvalidOperationException($"Service not registered: {serviceType.FullName}");
            }

            var descriptor = _serviceCollection.GetDescriptor(serviceType);

            switch (descriptor.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    return ResolveSingleton(serviceType, descriptor);

                case ServiceLifetime.Transient:
                    return CreateInstance(descriptor);

                case ServiceLifetime.Scoped:
                    return ResolveScoped(serviceType, descriptor);

                default:
                    throw new InvalidOperationException($"Unknown service lifetime: {descriptor.Lifetime}");
            }
        }

        private object ResolveSingleton(Type serviceType, ServiceDescriptor descriptor)
        {
            if (_singletonCache.TryGetValue(serviceType, out var cached))
                return cached;

            // 이미 등록된 인스턴스가 있으면 사용
            if (descriptor.Instance != null)
            {
                _singletonCache[serviceType] = descriptor.Instance;
                InvokeServiceInitialized(descriptor.Instance);
                return descriptor.Instance;
            }

            var instance = CreateInstance(descriptor);
            _singletonCache[serviceType] = instance;
            return instance;
        }

        private object ResolveScoped(Type serviceType, ServiceDescriptor descriptor)
        {
            if (_scopedCache.TryGetValue(serviceType, out var cached))
                return cached;

            var instance = CreateInstance(descriptor);
            _scopedCache[serviceType] = instance;
            return instance;
        }

        private object CreateInstance(ServiceDescriptor descriptor)
        {
            object instance;

            if (descriptor.Factory != null)
            {
                instance = descriptor.Factory(this);
            }
            else
            {
                instance = ConstructViaReflection(descriptor.ImplementationType);
            }

            InvokeServiceInitialized(instance);
            return instance;
        }

        private object ConstructViaReflection(Type type)
        {
            var constructors = type.GetConstructors();
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructor found: {type.FullName}");
            }

            // 파라미터가 가장 많은 생성자 선택 (greedy resolution)
            var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = Resolve(parameters[i].ParameterType);
            }

            return constructor.Invoke(args);
        }

        private static void InvokeServiceInitialized(object instance)
        {
            if (instance is IServiceInitializable initializable)
            {
                initializable.OnServiceInitialized();
            }
        }

        #endregion

        #region IServiceContainer

        public TInterface GetService<TInterface>() where TInterface : class
        {
            try
            {
                return (TInterface)Resolve(typeof(TInterface));
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to resolve: {typeof(TInterface).FullName}", ex);
            }
        }

        public object GetService(Type serviceType)
        {
            try
            {
                return Resolve(serviceType);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to resolve: {serviceType.FullName}", ex);
            }
        }

        public object GetService(string typeName)
        {
            var serviceType = KeyType(typeName);
            if (serviceType == null)
            {
                throw new InvalidOperationException($"Service not found by name: {typeName}");
            }

            return GetService(serviceType);
        }

        public Type KeyType(string key)
        {
            return _serviceCollection.GetKeyType(key);
        }

        #endregion
    }
}
