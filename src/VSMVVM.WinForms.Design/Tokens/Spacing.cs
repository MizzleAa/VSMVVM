using System.Windows.Forms;

namespace VSMVVM.WinForms.Design.Tokens
{
    /// <summary>
    /// Spacing 토큰. WPF.Design Spacing.xaml의 Thickness를 Padding으로 포팅합니다.
    /// M = Margin, P = Padding, x/y = Horizontal/Vertical, t/r/b/l = 방향.
    /// </summary>
    public static class Spacing
    {
        // Scale 0 (0px)
        public static readonly Padding P0 = new Padding(0);

        // Scale 0.5 (2px)
        public static readonly Padding P0_5 = new Padding(2);
        public static readonly Padding Px0_5 = new Padding(2, 0, 2, 0);
        public static readonly Padding Py0_5 = new Padding(0, 2, 0, 2);

        // Scale 1 (4px)
        public static readonly Padding P1 = new Padding(4);
        public static readonly Padding Px1 = new Padding(4, 0, 4, 0);
        public static readonly Padding Py1 = new Padding(0, 4, 0, 4);
        public static readonly Padding Pt1 = new Padding(0, 4, 0, 0);
        public static readonly Padding Pr1 = new Padding(0, 0, 4, 0);
        public static readonly Padding Pb1 = new Padding(0, 0, 0, 4);
        public static readonly Padding Pl1 = new Padding(4, 0, 0, 0);

        // Compound
        public static readonly Padding Px2Py1 = new Padding(8, 4, 8, 4);
        public static readonly Padding Px3Py2 = new Padding(12, 8, 12, 8);

        // Scale 1.5 (6px)
        public static readonly Padding P1_5 = new Padding(6);
        public static readonly Padding Px1_5 = new Padding(6, 0, 6, 0);
        public static readonly Padding Py1_5 = new Padding(0, 6, 0, 6);

        // Scale 2 (8px)
        public static readonly Padding P2 = new Padding(8);
        public static readonly Padding Px2 = new Padding(8, 0, 8, 0);
        public static readonly Padding Py2 = new Padding(0, 8, 0, 8);
        public static readonly Padding Pt2 = new Padding(0, 8, 0, 0);
        public static readonly Padding Pr2 = new Padding(0, 0, 8, 0);
        public static readonly Padding Pb2 = new Padding(0, 0, 0, 8);
        public static readonly Padding Pl2 = new Padding(8, 0, 0, 0);

        // Scale 3 (12px)
        public static readonly Padding P3 = new Padding(12);
        public static readonly Padding Px3 = new Padding(12, 0, 12, 0);
        public static readonly Padding Py3 = new Padding(0, 12, 0, 12);
        public static readonly Padding Pt3 = new Padding(0, 12, 0, 0);
        public static readonly Padding Pb3 = new Padding(0, 0, 0, 12);

        // Scale 4 (16px)
        public static readonly Padding P4 = new Padding(16);
        public static readonly Padding Px4 = new Padding(16, 0, 16, 0);
        public static readonly Padding Py4 = new Padding(0, 16, 0, 16);
        public static readonly Padding Pt4 = new Padding(0, 16, 0, 0);
        public static readonly Padding Pb4 = new Padding(0, 0, 0, 16);

        // Scale 5 (20px)
        public static readonly Padding P5 = new Padding(20);
        public static readonly Padding Pb5 = new Padding(0, 0, 0, 20);

        // Scale 6 (24px)
        public static readonly Padding P6 = new Padding(24);
        public static readonly Padding Pb6 = new Padding(0, 0, 0, 24);

        // Scale 8 (32px)
        public static readonly Padding P8 = new Padding(32);

        // Scale 10 (40px)
        public static readonly Padding P10 = new Padding(40);

        // Scale 12 (48px)
        public static readonly Padding P12 = new Padding(48);

        // Gap Values (for FlowLayoutPanel, TableLayoutPanel spacing)
        public const int Gap0 = 0;
        public const int Gap1 = 4;
        public const int Gap2 = 8;
        public const int Gap3 = 12;
        public const int Gap4 = 16;
        public const int Gap5 = 20;
        public const int Gap6 = 24;
        public const int Gap8 = 32;
    }
}
