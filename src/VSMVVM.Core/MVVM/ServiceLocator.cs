using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 전역 서비스 프로바이더 접근을 위한 정적 로케이터.
    /// </summary>
    public static class ServiceLocator
    {
        #region Fields

        private static IServiceContainer _serviceProvider;

        #endregion

        #region Public Methods

        /// <summary>
        /// 서비스 프로바이더를 설정합니다.
        /// </summary>
        public static void SetServiceProvider(IServiceContainer provider)
        {
            _serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// 등록된 서비스 프로바이더를 반환합니다.
        /// </summary>
        public static IServiceContainer GetServiceProvider()
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceProvider has not been initialized. Call SetServiceProvider first.");

            return _serviceProvider;
        }

        #endregion
    }
}
