using System.ComponentModel;
using System.Runtime.CompilerServices;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// SplashWindow ViewModel. ISplashService uses reflection to find Message/Progress properties.
    /// </summary>
    public class SplashViewModel : INotifyPropertyChanged
    {
        private string _message = "Loading...";
        private double _progress;

        /// <summary>
        /// Loading message displayed on splash screen.
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Loading progress (0.0 to 1.0).
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
