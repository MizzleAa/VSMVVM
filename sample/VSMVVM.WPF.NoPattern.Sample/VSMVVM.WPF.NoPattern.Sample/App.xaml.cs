using System;
using System.Windows;

namespace VSMVVM.WPF.NoPattern.Sample
{
    public partial class App : Application
    {
        private static int _sharedCount;

        public static event Action? SharedCountChanged;

        public static int SharedCount
        {
            get => _sharedCount;
            set
            {
                _sharedCount = value;
                SharedCountChanged?.Invoke();
            }
        }
    }
}
