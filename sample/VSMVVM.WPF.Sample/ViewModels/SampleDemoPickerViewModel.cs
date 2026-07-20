using System;
using System.Collections.Generic;
using VSMVVM.Core.Attributes;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// Scheduler Demo 워크스페이스의 "Demo…" 버튼 다이얼로그 VM.
    /// 워크스페이스에서 <see cref="IDialogService"/>.ShowDialog 로 띄우고,
    /// <see cref="DialogParameter"/> 로 데모 목록을 주입, OK 시 <see cref="DialogResultData"/> 로
    /// 선택된 <see cref="SampleDemoItem"/> 을 반환한다.
    /// </summary>
    public partial class SampleDemoPickerViewModel : ViewModelBase
    {
        /// <summary>DialogService 가 세팅 — 표시할 데모 후보들.</summary>
        public IReadOnlyList<SampleDemoItem> DialogParameter
        {
            get => _items;
            set
            {
                _items = value ?? Array.Empty<SampleDemoItem>();
                OnPropertyChanged(nameof(Items));
                if (_items.Count > 0) SelectedItem = _items[0];
            }
        }

        /// <summary>
        /// DialogService 가 Closed 이벤트에서 회수. 항상 현재 SelectedItem 을 반환 —
        /// Cancel 여부는 DialogService 가 <see cref="DialogResultType"/> 로 별도 판정하므로
        /// 여기서 accept 플래그로 걸러낼 필요 없음. (안 그러면 wrapping OK 버튼 클릭 경로에서 null 이 됨)
        /// </summary>
        public SampleDemoItem DialogResultData => SelectedItem;

        private IReadOnlyList<SampleDemoItem> _items = Array.Empty<SampleDemoItem>();

        public IReadOnlyList<SampleDemoItem> Items => _items;

        [Property] private SampleDemoItem _selectedItem;

        /// <summary>
        /// ListBox 더블클릭 시 다이얼로그 종료 (OK 로 취급). DialogService 가 RequestClose 이벤트를 구독해
        /// window.DialogResult = true 로 설정한 뒤 Close() 하므로, DialogResultType.OK 로 회수됨.
        /// </summary>
        public event EventHandler RequestClose;

        [RelayCommand(CanExecute = nameof(CanAccept))]
        private void Accept() => RequestClose?.Invoke(this, EventArgs.Empty);

        private bool CanAccept() => SelectedItem != null;

        partial void OnSelectedItemChanged(SampleDemoItem value)
        {
            AcceptCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Demo picker 리스트 한 줄. Action = 선택 확정 시 워크스페이스가 호출할 로더.</summary>
    public sealed class SampleDemoItem
    {
        public string DisplayName { get; }
        public string Description { get; }
        public Action Load { get; }

        public SampleDemoItem(string displayName, string description, Action load)
        {
            DisplayName = displayName;
            Description = description;
            Load = load;
        }
    }
}
