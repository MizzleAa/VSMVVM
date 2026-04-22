using System;
using System.Windows.Forms;

namespace VSMVVM.WinForms.Binding
{
    /// <summary>
    /// Bind* 확장 메서드가 반환하는 구독 핸들.
    /// Control.Disposed 이벤트에 자동 hook되어 해제 누락을 방지합니다.
    /// </summary>
    public sealed class BindingHandle : IDisposable
    {
        private Action _detach;
        private bool _disposed;

        internal BindingHandle(Control owner, Action detach)
        {
            _detach = detach ?? throw new ArgumentNullException(nameof(detach));
            if (owner != null)
            {
                owner.Disposed += OnOwnerDisposed;
            }
        }

        private void OnOwnerDisposed(object sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var detach = _detach;
            _detach = null;
            detach?.Invoke();
        }
    }
}
