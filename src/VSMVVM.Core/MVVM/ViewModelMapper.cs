using System;
using System.Collections.Generic;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// View-ViewModel 타입 매핑 구현체.
    /// </summary>
    internal sealed class ViewModelMapper : IViewModelMapper
    {
        #region Fields

        private readonly Dictionary<Type, Type> _mappings = new Dictionary<Type, Type>();

        #endregion

        #region IViewModelMapper

        public void Register<TView, TViewModel>() where TView : class where TViewModel : class
        {
            _mappings[typeof(TView)] = typeof(TViewModel);
        }

        public Type GetViewModelType(Type viewType)
        {
            if (viewType == null)
            {
                throw new ArgumentNullException(nameof(viewType));
            }

            if (_mappings.TryGetValue(viewType, out var vmType))
            {
                return vmType;
            }

            return null;
        }

        public bool HasMapping(Type viewType)
        {
            if (viewType == null)
            {
                return false;
            }

            return _mappings.ContainsKey(viewType);
        }

        #endregion
    }
}
