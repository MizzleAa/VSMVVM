using Microsoft.Win32;

#nullable enable
namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// <see cref="IFileDialogService"/>의 WPF(Win32 Common Dialog) 구현.
    /// </summary>
    public sealed class WpfFileDialogService : IFileDialogService
    {
        public string? OpenFile(string filter)
        {
            var dlg = new OpenFileDialog { Filter = filter };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string? SaveFile(string filter, string? suggestedName = null)
        {
            var dlg = new SaveFileDialog { Filter = filter };
            if (!string.IsNullOrEmpty(suggestedName)) dlg.FileName = suggestedName;
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
