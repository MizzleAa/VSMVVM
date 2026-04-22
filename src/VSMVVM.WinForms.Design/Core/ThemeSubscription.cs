using System;
using System.Windows.Forms;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// ThemeManager.ThemeChanged 구독 + UI 스레드 marshalling + Dispose 해제를 묶은 IDisposable 헬퍼.
    /// 모든 VS* 컨트롤이 중복 구현하던 3블록을 1곳으로 통합합니다.
    /// </summary>
    public sealed class ThemeSubscription : IDisposable
    {
        private readonly Control _owner;
        private readonly Action _applyTheme;
        private bool _disposed;

        public ThemeSubscription(Control owner, Action applyTheme)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (_disposed) return;
            if (_owner.IsDisposed) return;

            if (_owner.InvokeRequired)
            {
                try { _owner.BeginInvoke(_applyTheme); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
            else
            {
                _applyTheme();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ThemeManager.ThemeChanged -= OnThemeChanged;
        }
    }
}
