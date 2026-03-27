using System.Collections.Generic;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Region 네비게이션 시 전달되는 컨텍스트.
    /// 파라미터 딕셔너리를 통해 View 간 데이터를 전달합니다.
    /// </summary>
    public class NavigationContext
    {
        #region Properties

        /// <summary>
        /// 네비게이션 파라미터 딕셔너리.
        /// </summary>
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        #endregion

        #region Public Methods

        /// <summary>
        /// 파라미터 값을 제네릭으로 조회합니다.
        /// </summary>
        public T GetValue<T>(string key)
        {
            if (Parameters.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return default;
        }

        /// <summary>
        /// 파라미터 존재 여부를 확인합니다.
        /// </summary>
        public bool ContainsKey(string key)
        {
            return Parameters.ContainsKey(key);
        }

        #endregion
    }
}
