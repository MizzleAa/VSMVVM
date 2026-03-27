using System;
using System.Threading.Tasks;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.Guard;
using VSMVVM.WPF.Sample.Services;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for Guard, DispatcherService, and StateStore demo.

    /// </summary>

    public partial class ServicesViewModel : ViewModelBase
    {
        private readonly IDispatcherService _dispatcher;
        private readonly AppStateStore _store;

        /// <summary>
        /// WeakReference 기반 Subscribe에서 GC 방지를 위한 강한 참조 보관.
        /// </summary>
        private readonly Action<AppState> _onStateChanged;

        [Property]
        private string _guardInput = "";

        [Property]
        private string _guardResult = "";

        [Property]
        private string _dispatcherStatus = "Ready";

        [Property]
        private int _storeCounter;

        [Property]
        private string _storeLastAction = "";

        [Property]
        private string _storeTheme = "";

        [Property]
        private string _storeLog = "";

        public ServicesViewModel(IDispatcherService dispatcher, AppStateStore store)
        {
            _dispatcher = dispatcher;
            _store = store;

            _onStateChanged = OnStateChanged;
            _store.Subscribe(_onStateChanged);
        }

        private void OnStateChanged(AppState state)
        {
            StoreCounter = state.Counter;
            StoreLastAction = state.LastAction;
            StoreTheme = state.Theme;
            StoreLog += $"[{DateTime.Now:HH:mm:ss}] Counter={state.Counter} Action={state.LastAction}\n";
        }

        #region Guard Demo

        [RelayCommand]
        private void TestNotNull()
        {
            try
            {
                Guard.IsNotNull(GuardInput, nameof(GuardInput));
                GuardResult = $"IsNotNull passed: '{GuardInput}'";
            }
            catch (ArgumentNullException ex)
            {
                GuardResult = $"IsNotNull threw: {ex.Message}";
            }
        }

        [RelayCommand]
        private void TestNotNullOrEmpty()
        {
            try
            {
                Guard.IsNotNullOrEmpty(GuardInput, nameof(GuardInput));
                GuardResult = $"IsNotNullOrEmpty passed: '{GuardInput}'";
            }
            catch (ArgumentException ex)
            {
                GuardResult = $"IsNotNullOrEmpty threw: {ex.Message}";
            }
        }

        [RelayCommand]
        private void TestInRange()
        {
            try
            {
                if (int.TryParse(GuardInput, out int val))
                {
                    Guard.IsInRange(val, 1, 100, nameof(GuardInput));
                    GuardResult = $"IsInRange(1-100) passed: {val}";
                }
                else
                {
                    GuardResult = "Enter a number to test IsInRange";
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                GuardResult = $"IsInRange threw: {ex.Message}";
            }
        }

        #endregion

        #region Dispatcher Demo

        [AsyncRelayCommand]
        private async Task DispatcherTest()
        {
            DispatcherStatus = "Starting background task...";
            await Task.Run(async () =>
            {
                await Task.Delay(1000);
                _dispatcher.Invoke(() =>
                {
                    DispatcherStatus = $"Updated from background thread at {DateTime.Now:HH:mm:ss}";
                });
            });
        }

        #endregion

        #region StateStore Demo

        [RelayCommand]
        private void StoreIncrement() => _store.Increment();

        [RelayCommand]
        private void StoreDecrement() => _store.Decrement();

        [RelayCommand]
        private void StoreReset() => _store.Reset();

        #endregion
    }
}
