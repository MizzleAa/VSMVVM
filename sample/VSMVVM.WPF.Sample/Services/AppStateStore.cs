using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Sample.Services
{
    /// <summary>
    /// Sample application state.
    /// </summary>
    public class AppState
    {
        public int Counter { get; set; }
        public string LastAction { get; set; } = "";
        public string Theme { get; set; } = "Dark";
    }

    /// <summary>
    /// Concrete StateStore using StateStoreBase.
    /// Redux-style: UpdateState creates a new state, notifies all subscribers.
    /// </summary>
    public class AppStateStore : StateStoreBase<AppState>
    {
        public AppStateStore() : base(new AppState()) { }

        public void Increment()
        {
            UpdateState(new AppState
            {
                Counter = State.Counter + 1,
                LastAction = "Increment",
                Theme = State.Theme
            });
        }

        public void Decrement()
        {
            UpdateState(new AppState
            {
                Counter = State.Counter - 1,
                LastAction = "Decrement",
                Theme = State.Theme
            });
        }

        public void SetTheme(string theme)
        {
            UpdateState(new AppState
            {
                Counter = State.Counter,
                LastAction = $"Theme -> {theme}",
                Theme = theme
            });
        }

        public void Reset()
        {
            UpdateState(new AppState
            {
                Counter = 0,
                LastAction = "Reset",
                Theme = State.Theme
            });
        }
    }
}
