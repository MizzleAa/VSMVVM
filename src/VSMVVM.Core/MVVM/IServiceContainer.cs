using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 해석(resolve) 인터페이스.
    /// </summary>
    public interface IServiceContainer
    {
        /// <summary>
        /// 제네릭 타입으로 서비스를 해석합니다.
        /// </summary>
        TInterface GetService<TInterface>() where TInterface : class;

        /// <summary>
        /// Type으로 서비스를 해석합니다.
        /// </summary>
        object GetService(Type serviceType);

        /// <summary>
        /// 등록된 이름(Type.Name)으로 서비스를 해석합니다.
        /// </summary>
        object GetService(string typeName);

        /// <summary>
        /// 등록된 이름으로 Type을 조회합니다.
        /// </summary>
        Type KeyType(string key);
    }
}
