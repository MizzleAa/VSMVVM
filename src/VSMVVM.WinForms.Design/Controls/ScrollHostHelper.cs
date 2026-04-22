using System.Windows.Forms;

namespace VSMVVM.WinForms.Design.Controls
{
    /// <summary>
    /// Designer로 생성된 Dock=Fill + AutoScroll 컨테이너(주로 FlowLayoutPanel)를
    /// 런타임에 <see cref="VSScrollHost"/>로 감싸 커스텀 스크롤바 스타일을 적용하는 헬퍼.
    /// </summary>
    public static class ScrollHostHelper
    {
        /// <summary>
        /// 지정된 컨테이너의 부모를 VSScrollHost로 교체한다. 이미 VSScrollHost 자식인 경우 no-op.
        /// <paramref name="panel"/>은 AutoScroll=false, AutoSize=true(GrowAndShrink), Dock=None 으로 재구성되어
        /// 호스트의 뷰포트 내부에서 세로 스크롤 대상이 된다.
        /// </summary>
        public static VSScrollHost WrapWithScrollHost(FlowLayoutPanel panel)
        {
            if (panel == null || panel.Parent is VSScrollHost existing) return panel?.Parent as VSScrollHost;

            var parent = panel.Parent;
            if (parent == null) return null;

            var host = new VSScrollHost
            {
                Dock = DockStyle.Fill,
                BackColor = panel.BackColor
            };

            int index = parent.Controls.GetChildIndex(panel);
            parent.Controls.Remove(panel);

            panel.AutoScroll = false;
            panel.Dock = DockStyle.None;
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.WrapContents = false;

            host.Content = panel;
            parent.Controls.Add(host);
            parent.Controls.SetChildIndex(host, index);

            return host;
        }
    }
}
