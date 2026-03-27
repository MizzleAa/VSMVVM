using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VSMVVM.WPF.Design.Components
{
    public partial class DateTimePicker : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        #region DependencyProperty

        public static readonly DependencyProperty SelectedDateTimeProperty =
            DependencyProperty.Register(
                nameof(SelectedDateTime), typeof(DateTime), typeof(DateTimePicker),
                new FrameworkPropertyMetadata(DateTime.Now,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedDateTimeChanged));

        public DateTime SelectedDateTime
        {
            get => (DateTime)GetValue(SelectedDateTimeProperty);
            set => SetValue(SelectedDateTimeProperty, value);
        }

        private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DateTimePicker picker)
            {
                picker.SyncFieldsFromDateTime();
            }
        }

        #endregion

        #region Private Fields

        private bool _isSyncing;

        #endregion

        #region Notify Properties

        private int _year;
        public int Year
        {
            get => _year;
            set { _year = value; OnFieldChanged(); RaisePropertyChanged(nameof(Year)); RaisePropertyChanged(nameof(YearText)); }
        }

        private int _month;
        public int Month
        {
            get => _month;
            set { _month = Math.Clamp(value, 1, 12); OnFieldChanged(); RaisePropertyChanged(nameof(Month)); RaisePropertyChanged(nameof(MonthText)); }
        }

        private int _day;
        public int Day
        {
            get => _day;
            set
            {
                int maxDay = DateTime.DaysInMonth(Year, Month);
                _day = Math.Clamp(value, 1, maxDay);
                OnFieldChanged();
                RaisePropertyChanged(nameof(Day));
                RaisePropertyChanged(nameof(DayText));
            }
        }

        private int _hour;
        public int Hour
        {
            get => _hour;
            set { _hour = Math.Clamp(value, 0, 23); OnFieldChanged(); RaisePropertyChanged(nameof(Hour)); RaisePropertyChanged(nameof(HourText)); }
        }

        private int _minute;
        public int Minute
        {
            get => _minute;
            set { _minute = Math.Clamp(value, 0, 59); OnFieldChanged(); RaisePropertyChanged(nameof(Minute)); RaisePropertyChanged(nameof(MinuteText)); }
        }

        private int _second;
        public int Second
        {
            get => _second;
            set { _second = Math.Clamp(value, 0, 59); OnFieldChanged(); RaisePropertyChanged(nameof(Second)); RaisePropertyChanged(nameof(SecondText)); }
        }

        public string YearText
        {
            get => Year.ToString("D4");
            set { if (int.TryParse(value, out int v)) Year = v; }
        }
        public string MonthText
        {
            get => Month.ToString("D2");
            set { if (int.TryParse(value, out int v)) Month = v; }
        }
        public string DayText
        {
            get => Day.ToString("D2");
            set { if (int.TryParse(value, out int v)) Day = v; }
        }
        public string HourText
        {
            get => Hour.ToString("D2");
            set { if (int.TryParse(value, out int v)) Hour = v; }
        }
        public string MinuteText
        {
            get => Minute.ToString("D2");
            set { if (int.TryParse(value, out int v)) Minute = v; }
        }
        public string SecondText
        {
            get => Second.ToString("D2");
            set { if (int.TryParse(value, out int v)) Second = v; }
        }

        public string DisplayText => SelectedDateTime.ToString("yyyy/MM/dd  HH:mm:ss");

        private bool _isPopupOpen;
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set { _isPopupOpen = value; RaisePropertyChanged(nameof(IsPopupOpen)); }
        }

        #endregion

        #region Constructor

        public DateTimePicker()
        {
            InitializeComponent();
            SyncFieldsFromDateTime();
        }

        #endregion

        #region Sync Methods

        private void SyncFieldsFromDateTime()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                var dt = SelectedDateTime;
                _year = dt.Year;
                _month = dt.Month;
                _day = dt.Day;
                _hour = dt.Hour;
                _minute = dt.Minute;
                _second = dt.Second;

                RaisePropertyChanged(nameof(Year));
                RaisePropertyChanged(nameof(Month));
                RaisePropertyChanged(nameof(Day));
                RaisePropertyChanged(nameof(Hour));
                RaisePropertyChanged(nameof(Minute));
                RaisePropertyChanged(nameof(Second));
                RaisePropertyChanged(nameof(YearText));
                RaisePropertyChanged(nameof(MonthText));
                RaisePropertyChanged(nameof(DayText));
                RaisePropertyChanged(nameof(HourText));
                RaisePropertyChanged(nameof(MinuteText));
                RaisePropertyChanged(nameof(SecondText));
                RaisePropertyChanged(nameof(DisplayText));
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void OnFieldChanged()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                int maxDay = DateTime.DaysInMonth(_year, _month);
                if (_day > maxDay) _day = maxDay;

                SelectedDateTime = new DateTime(_year, _month, _day, _hour, _minute, _second);
                RaisePropertyChanged(nameof(DisplayText));
            }
            catch { }
            finally
            {
                _isSyncing = false;
            }
        }

        #endregion

        #region Button Handlers

        private void IncrementYear(object s, RoutedEventArgs e) => Year++;
        private void DecrementYear(object s, RoutedEventArgs e) => Year--;
        private void IncrementMonth(object s, RoutedEventArgs e) => Month = Month >= 12 ? 1 : Month + 1;
        private void DecrementMonth(object s, RoutedEventArgs e) => Month = Month <= 1 ? 12 : Month - 1;
        private void IncrementDay(object s, RoutedEventArgs e)
        {
            int max = DateTime.DaysInMonth(Year, Month);
            Day = Day >= max ? 1 : Day + 1;
        }
        private void DecrementDay(object s, RoutedEventArgs e)
        {
            int max = DateTime.DaysInMonth(Year, Month);
            Day = Day <= 1 ? max : Day - 1;
        }
        private void IncrementHour(object s, RoutedEventArgs e) => Hour = Hour >= 23 ? 0 : Hour + 1;
        private void DecrementHour(object s, RoutedEventArgs e) => Hour = Hour <= 0 ? 23 : Hour - 1;
        private void IncrementMinute(object s, RoutedEventArgs e) => Minute = Minute >= 59 ? 0 : Minute + 1;
        private void DecrementMinute(object s, RoutedEventArgs e) => Minute = Minute <= 0 ? 59 : Minute - 1;
        private void IncrementSecond(object s, RoutedEventArgs e) => Second = Second >= 59 ? 0 : Second + 1;
        private void DecrementSecond(object s, RoutedEventArgs e) => Second = Second <= 0 ? 59 : Second - 1;

        private void SetNow(object s, RoutedEventArgs e)
        {
            SelectedDateTime = DateTime.Now;
        }

        private void Confirm(object s, RoutedEventArgs e)
        {
            IsPopupOpen = false;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
