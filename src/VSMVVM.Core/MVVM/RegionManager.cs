using System;
using System.Collections.Generic;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Region 매니저 구현체. 네비게이션 히스토리(Back/Forward) 스택을 관리합니다.
    /// </summary>
    internal sealed class RegionManager : IRegionManager
    {
        #region Inner Types

        /// <summary>
        /// Region별 히스토리 정보를 관리하는 내부 클래스.
        /// </summary>
        private sealed class RegionState
        {
            public IRegion Region { get; set; }
            public readonly Stack<Type> BackStack = new Stack<Type>();
            public readonly Stack<Type> ForwardStack = new Stack<Type>();
            public Type CurrentViewType { get; set; }
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, RegionState> _regionStates = new Dictionary<string, RegionState>();
        private readonly Dictionary<string, Type> _initialMappings = new Dictionary<string, Type>();

        #endregion

        #region Registration

        public void Register(string regionName, IRegion region)
        {
            if (!_regionStates.ContainsKey(regionName))
            {
                _regionStates[regionName] = new RegionState { Region = region };
            }

            if (_initialMappings.TryGetValue(regionName, out var viewType))
            {
                Navigate(regionName, viewType);
            }
        }

        public void Mapping<TView>(string regionName) where TView : class
        {
            Mapping(regionName, typeof(TView));
        }

        public void Mapping(string regionName, Type viewType)
        {
            _initialMappings[regionName] = viewType;
        }

        public void Cleanup(string regionName)
        {
            _regionStates.Remove(regionName);
        }

        #endregion

        #region Navigation

        public void Navigate<TView>(string regionName) where TView : class
        {
            Navigate(regionName, typeof(TView));
        }

        public void Navigate<TView>(string regionName, NavigationContext context) where TView : class
        {
            Navigate(regionName, typeof(TView), context);
        }

        public void Navigate(string regionName, string viewName)
        {
            var viewType = ServiceLocator.GetServiceProvider().KeyType(viewName);
            if (viewType == null)
            {
                throw new InvalidOperationException($"View not found: {viewName}");
            }

            Navigate(regionName, viewType);
        }

        public void Navigate(string regionName, string viewName, NavigationContext context)
        {
            var viewType = ServiceLocator.GetServiceProvider().KeyType(viewName);
            if (viewType == null)
            {
                throw new InvalidOperationException($"View not found: {viewName}");
            }

            Navigate(regionName, viewType, context);
        }

        public void Navigate(string regionName, Type viewType, NavigationContext navigationContext = null)
        {
            if (!_regionStates.TryGetValue(regionName, out var state))
            {
                throw new InvalidOperationException($"Region not registered: {regionName}");
            }

            if (viewType == null)
            {
                state.Region.Content = null;
                return;
            }

            if (navigationContext == null)
            {
                navigationContext = new NavigationContext();
            }

            var serviceProvider = ServiceLocator.GetServiceProvider();
            var nextView = serviceProvider.GetService(viewType);
            if (nextView == null)
            {
                return;
            }

            // 새 View가 navigation을 받아들이는지 먼저 확인.
            // 거부될 경우 prevView를 정리/히스토리를 변경하지 않아야 region이 좀비 상태가 되지 않는다.
            if (!CanNavigateTo(nextView, navigationContext))
            {
                return;
            }

            // 현재 View의 DataContext에서 INavigateAware 통지
            var prevView = state.Region.Content;
            NotifyNavigatedFrom(prevView, navigationContext);

            // ICleanup 호출
            InvokeCleanup(prevView);

            // 히스토리 관리: 현재 View를 BackStack에 추가
            if (state.CurrentViewType != null)
            {
                state.BackStack.Push(state.CurrentViewType);
                state.ForwardStack.Clear();
            }

            state.Region.Content = nextView;
            state.CurrentViewType = viewType;

            NotifyNavigatedTo(nextView, navigationContext);

            // IAsyncInitializable 호출
            InvokeAsyncInitializable(nextView);
        }

        public void Hide(string regionName)
        {
            if (_regionStates.TryGetValue(regionName, out var state))
            {
                InvokeCleanup(state.Region.Content);
                state.Region.Content = null;
            }
        }

        #endregion

        #region Navigation History

        public void GoBack(string regionName)
        {
            if (!_regionStates.TryGetValue(regionName, out var state))
            {
                return;
            }

            if (state.BackStack.Count == 0)
            {
                return;
            }

            // 현재 View를 ForwardStack에 추가
            if (state.CurrentViewType != null)
            {
                state.ForwardStack.Push(state.CurrentViewType);
            }

            var previousViewType = state.BackStack.Pop();

            var serviceProvider = ServiceLocator.GetServiceProvider();
            var previousView = serviceProvider.GetService(previousViewType);

            var context = new NavigationContext();

            // Navigate()와 동일하게 OnNavigatedFrom → Cleanup 순서. 거꾸로 호출하면
            // ICleanup으로 dispose된 ViewModel에 OnNavigatedFrom이 들어가 ObjectDisposedException 발생.
            NotifyNavigatedFrom(state.Region.Content, context);
            InvokeCleanup(state.Region.Content);

            state.Region.Content = previousView;
            state.CurrentViewType = previousViewType;

            NotifyNavigatedTo(previousView, context);
            InvokeAsyncInitializable(previousView);
        }

        public void GoForward(string regionName)
        {
            if (!_regionStates.TryGetValue(regionName, out var state))
            {
                return;
            }

            if (state.ForwardStack.Count == 0)
            {
                return;
            }

            // 현재 View를 BackStack에 추가
            if (state.CurrentViewType != null)
            {
                state.BackStack.Push(state.CurrentViewType);
            }

            var forwardViewType = state.ForwardStack.Pop();

            var serviceProvider = ServiceLocator.GetServiceProvider();
            var forwardView = serviceProvider.GetService(forwardViewType);

            var context = new NavigationContext();

            // Navigate()와 동일하게 OnNavigatedFrom → Cleanup 순서.
            NotifyNavigatedFrom(state.Region.Content, context);
            InvokeCleanup(state.Region.Content);

            state.Region.Content = forwardView;
            state.CurrentViewType = forwardViewType;

            NotifyNavigatedTo(forwardView, context);
            InvokeAsyncInitializable(forwardView);
        }

        public bool CanGoBack(string regionName)
        {
            if (_regionStates.TryGetValue(regionName, out var state))
            {
                return state.BackStack.Count > 0;
            }

            return false;
        }

        public bool CanGoForward(string regionName)
        {
            if (_regionStates.TryGetValue(regionName, out var state))
            {
                return state.ForwardStack.Count > 0;
            }

            return false;
        }

        public string GetCurrentViewTypeName(string regionName)
        {
            if (_regionStates.TryGetValue(regionName, out var state) && state.CurrentViewType != null)
            {
                return state.CurrentViewType.Name;
            }

            return string.Empty;
        }

        public string GetCurrentViewDisplayName(string regionName)
        {
            var typeName = GetCurrentViewTypeName(regionName);
            return PascalCaseToDisplayName(typeName);
        }

        /// <summary>
        /// View Type 이름을 사용자 친화적 표시 이름으로 변환합니다.
        /// "View" 접미사 제거 후 PascalCase를 공백 분리합니다.
        /// </summary>
        internal static string PascalCaseToDisplayName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            // "View" 접미사 제거
            if (typeName.EndsWith("View"))
                typeName = typeName.Substring(0, typeName.Length - 4);

            // PascalCase → 공백 분리
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]))
                    sb.Append(' ');
                sb.Append(typeName[i]);
            }
            return sb.ToString();
        }

        #endregion

        #region Private Methods

        private static object GetDataContext(object view)
        {
            if (view == null)
            {
                return null;
            }

            var dataContextProperty = view.GetType().GetProperty("DataContext");
            return dataContextProperty?.GetValue(view);
        }

        private static bool CanNavigateTo(object view, NavigationContext context)
        {
            var dataContext = GetDataContext(view);
            if (dataContext is INavigateAware navigateAware)
            {
                return navigateAware.CanNavigate(context);
            }

            return true;
        }

        private static void NotifyNavigatedFrom(object view, NavigationContext context)
        {
            var dataContext = GetDataContext(view);
            if (dataContext is INavigateAware navigateAware)
            {
                if (navigateAware.CanNavigate(context))
                {
                    navigateAware.OnNavigatedFrom(context);
                }
            }
        }

        private static void NotifyNavigatedTo(object view, NavigationContext context)
        {
            var dataContext = GetDataContext(view);
            if (dataContext is INavigateAware navigateAware)
            {
                navigateAware.OnNavigatedTo(context);
            }
        }

        private static void InvokeCleanup(object view)
        {
            var dataContext = GetDataContext(view);
            if (dataContext is ICleanup cleanup)
            {
                cleanup.Cleanup();
            }
        }

        private static async void InvokeAsyncInitializable(object view)
        {
            // async void: 호출부에서 try/catch가 불가능하므로 여기서 예외를 swallow.
            // 미처리 예외가 SynchronizationContext로 전파되어 앱이 크래시되는 것을 방지한다.
            try
            {
                var dataContext = GetDataContext(view);
                if (dataContext is IAsyncInitializable initializable)
                {
                    await initializable.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RegionManager] InitializeAsync failed: {ex}");
            }
        }

        #endregion
    }
}
