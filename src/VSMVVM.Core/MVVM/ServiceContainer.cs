using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
        // Lazy<object>로 감싸서 ConcurrentDictionary.GetOrAdd factory가 race로 두 번 실행되더라도
        // 실제 인스턴스 생성은 단 한 번만 일어나도록 보장한다.
        private readonly ConcurrentDictionary<Type, Lazy<object>> _singletonCache = new ConcurrentDictionary<Type, Lazy<object>>();
        private readonly ConcurrentDictionary<Type, Lazy<object>> _scopedCache = new ConcurrentDictionary<Type, Lazy<object>>();

        // 순환 의존 감지를 위한 thread-local 해석 체인.
        // 같은 스레드에서 같은 타입을 두 번 Resolve하려 하면 cycle로 판정한다.
        private readonly ThreadLocal<HashSet<Type>> _resolutionChain =
            new ThreadLocal<HashSet<Type>>(() => new HashSet<Type>());

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

            // Singleton/Scoped 캐시 hit는 cycle이 아니므로 chain 등록 없이 즉시 반환.
            // (이미 만들어진 객체를 단순 조회하는 행위는 의존성 빌드 그래프와 무관)
            if (descriptor.Lifetime == ServiceLifetime.Singleton
                && _singletonCache.TryGetValue(serviceType, out var cachedSingleton)
                && cachedSingleton.IsValueCreated)
            {
                return cachedSingleton.Value;
            }

            if (descriptor.Lifetime == ServiceLifetime.Scoped
                && _scopedCache.TryGetValue(serviceType, out var cachedScoped)
                && cachedScoped.IsValueCreated)
            {
                return cachedScoped.Value;
            }

            // 빌드가 필요한 경로. 진입 시점에 cycle 감지.
            // (.NET Lazy는 self-reference를 'ValueFactory attempted to access...' 메시지로 throw하지만
            //  의미 불명이므로 여기서 더 명확한 메시지로 선제적으로 throw한다.)
            var chain = _resolutionChain.Value;
            if (!chain.Add(serviceType))
            {
                var path = string.Join(" -> ", chain.Select(t => t.FullName)) + " -> " + serviceType.FullName;
                // 정상 stack 정리를 위해 add 실패 시점엔 chain에 변동 없으므로 그대로 throw.
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving services: {path}");
            }

            try
            {
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
            finally
            {
                chain.Remove(serviceType);
            }
        }

        private object ResolveSingleton(Type serviceType, ServiceDescriptor descriptor)
        {
            var lazy = _singletonCache.GetOrAdd(serviceType, _ => new Lazy<object>(() =>
            {
                if (descriptor.Instance != null)
                {
                    InvokeServiceInitialized(descriptor.Instance);
                    return descriptor.Instance;
                }

                return CreateInstance(descriptor);
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazy.Value;
        }

        private object ResolveScoped(Type serviceType, ServiceDescriptor descriptor)
        {
            var lazy = _scopedCache.GetOrAdd(serviceType, _ => new Lazy<object>(
                () => CreateInstance(descriptor),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            return lazy.Value;
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

            // .NET DI 표준과 동일한 greedy 전략: 등록된 서비스로 모든 파라미터를 만족할 수 있는
            // 가장 큰 ctor를 선택한다. 단순히 가장 큰 ctor를 잡으면 (예: 5-param + 0-param 두 개일 때
            // 5-param 중 미등록이 있으면) 0-param fallback이 가능한데도 throw하게 된다.
            var orderedCtors = constructors.OrderByDescending(c => c.GetParameters().Length).ToList();

            foreach (var ctor in orderedCtors)
            {
                var parameters = ctor.GetParameters();
                if (parameters.All(p => _serviceCollection.ContainsService(p.ParameterType)))
                {
                    var args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = Resolve(parameters[i].ParameterType);
                    }
                    return ctor.Invoke(args);
                }
            }

            // 어떤 ctor도 만족 못함. 가장 큰 ctor 기준으로 어느 파라미터가 미등록인지 알리는 메시지로 throw.
            var biggest = orderedCtors[0];
            var unresolved = biggest.GetParameters()
                .Where(p => !_serviceCollection.ContainsService(p.ParameterType))
                .Select(p => p.ParameterType.FullName);
            throw new InvalidOperationException(
                $"No suitable constructor found for {type.FullName}. Unresolved parameter types in largest constructor: {string.Join(", ", unresolved)}");
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
