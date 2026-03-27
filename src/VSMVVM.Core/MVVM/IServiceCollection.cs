using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 등록 인터페이스. Singleton, Transient, Scoped 라이프사이클을 지원합니다.
    /// </summary>
    public interface IServiceCollection
    {
        #region Singleton

        void AddSingleton<TInterface, TImplementation>() where TImplementation : TInterface;
        void AddSingleton<TImplementation>() where TImplementation : class;
        void AddSingleton<TImplementation>(TImplementation implementation) where TImplementation : class;
        void AddSingleton<TInterface, TImplementation>(TImplementation implementation) where TImplementation : TInterface;
        void AddSingleton<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface;
        void AddSingleton<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class;

        #endregion

        #region Transient

        void AddTransient<TInterface, TImplementation>() where TImplementation : TInterface;
        void AddTransient<TImplementation>() where TImplementation : class;
        void AddTransient<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface;
        void AddTransient<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class;

        #endregion

        #region Scoped

        void AddScoped<TInterface, TImplementation>() where TImplementation : TInterface;
        void AddScoped<TImplementation>() where TImplementation : class;
        void AddScoped<TInterface, TImplementation>(Func<IServiceContainer, TInterface> factory) where TImplementation : TInterface;
        void AddScoped<TImplementation>(Func<IServiceContainer, TImplementation> factory) where TImplementation : class;

        #endregion

        #region Query

        bool ContainsService(Type type);
        Type GetKeyType(string name);
        ServiceDescriptor GetDescriptor(Type type);

        #endregion

        #region Container

        IServiceContainer CreateContainer();

        #endregion
    }
}
