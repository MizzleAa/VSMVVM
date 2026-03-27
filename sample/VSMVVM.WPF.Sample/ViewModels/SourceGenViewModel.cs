using System.Threading.Tasks;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>

    /// ViewModel for source generator demo (Property, RelayCommand, AsyncRelayCommand).

    /// </summary>

    public partial class SourceGenViewModel : ViewModelBase
    {
        #region [Property] + [PropertyChangedFor]

        [Property]
        [PropertyChangedFor(nameof(Greeting))]
        private string _userName = "";

        public string Greeting => string.IsNullOrWhiteSpace(UserName)
            ? "Enter a name above..."
            : $"Hello, {UserName}!";

        #endregion

        #region [RelayCommand] + CanExecute

        [Property]
        [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private int _counter;

        [RelayCommand]
        private void Increment() => Counter++;

        [RelayCommand(CanExecute = nameof(CanDecrement))]
        private void Decrement() => Counter--;

        private bool CanDecrement() => Counter > 0;

        [RelayCommand(CanExecute = nameof(CanReset))]
        private void Reset() => Counter = 0;

        private bool CanReset() => Counter != 0;

        #endregion

        #region [AsyncRelayCommand] + CanExecute

        [Property]
        private bool _isBusy;

        [Property]
        private string _asyncStatus = "Ready";

        [Property]
        private int _progress;

        [AsyncRelayCommand(CanExecute = nameof(CanLoadData))]
        private async Task LoadData()
        {
            IsBusy = true;
            Progress = 0;
            AsyncStatus = "Loading...";

            for (int i = 1; i <= 5; i++)
            {
                await Task.Delay(500);
                Progress = i * 20;
                AsyncStatus = $"Step {i}/5 ({Progress}%)";
            }

            AsyncStatus = "Complete!";
            IsBusy = false;
        }

        private bool CanLoadData() => !IsBusy;

        #endregion
    }
}
