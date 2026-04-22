using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// 네이티브 WinForms 스크롤바를 구조적으로 억제하는 공통 WndProc 헬퍼.
    /// WM_NCCALCSIZE 단계에서 클라이언트 영역의 오른쪽을 축소하여
    /// OS가 스크롤바 공간을 할당하지 못하도록 만든다.
    /// VSListBox / VSTextBox(내부) / VSDataGridView 가 공유한다.
    /// </summary>
    internal static class NativeScrollBarSuppressor
    {
        public const int WM_NCCALCSIZE = 0x0083;
        public const int WM_NCPAINT = 0x0085;
        public const int WM_PAINT = 0x000F;
        public const int WM_VSCROLL = 0x0115;

        private const int SB_VERT = 1;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NCCALCSIZE_PARAMS
        {
            public RECT rgrc0, rgrc1, rgrc2;
            public IntPtr lppos;
        }

        /// <summary>
        /// base.WndProc 호출 후 WM_NCCALCSIZE 메시지에 대해 rgrc0.Right 를 축소한다.
        /// wParam이 TRUE(비영) 일 때만 NCCALCSIZE_PARAMS 구조체 형태.
        /// </summary>
        public static void AdjustNcCalcSize(ref Message m, int reserveRight)
        {
            if (m.Msg != WM_NCCALCSIZE || m.WParam == IntPtr.Zero || reserveRight <= 0) return;
            var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
            int newRight = p.rgrc0.Right - reserveRight;
            if (newRight < p.rgrc0.Left) newRight = p.rgrc0.Left;
            p.rgrc0.Right = newRight;
            Marshal.StructureToPtr(p, m.LParam, false);
        }

        /// <summary>네이티브 세로 스크롤바를 숨긴다 (일회성이므로 여러 메시지에서 방어적으로 호출).</summary>
        public static void HideVertical(IntPtr handle)
        {
            if (handle != IntPtr.Zero) ShowScrollBar(handle, SB_VERT, false);
        }

        /// <summary>SWP_FRAMECHANGED 로 WM_NCCALCSIZE 재발생을 유도하여 클라이언트 영역을 즉시 재계산한다.</summary>
        public static void RecalcFrame(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
    }
}
