using System.Drawing;

namespace VSMVVM.WinForms.Design.Tokens
{
    /// <summary>
    /// Typography 토큰. WPF.Design Typography.xaml의 Font 정의를 포팅합니다.
    /// </summary>
    public static class Typography
    {
        // Font Families
        public const string FontSansName = "Segoe UI";
        public const string FontSerifName = "Georgia";
        public const string FontMonoName = "Cascadia Code";

        public static readonly FontFamily FontSans = new FontFamily(FontSansName);
        public static readonly FontFamily FontSerif = new FontFamily(FontSerifName);
        public static readonly FontFamily FontMono = new FontFamily(FontMonoName);

        // Font Sizes
        public const float TextXxs = 10f;
        public const float TextXs = 12f;
        public const float TextSm = 14f;
        public const float TextBase = 16f;
        public const float TextLg = 18f;
        public const float TextXl = 20f;
        public const float Text2xl = 24f;
        public const float Text3xl = 30f;
        public const float Text4xl = 36f;
        public const float Text5xl = 48f;
        public const float Text6xl = 60f;

        // Pre-built Fonts (Sans, Regular, common sizes)
        public static readonly Font DefaultFont = new Font(FontSansName, TextSm, FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font SmallFont = new Font(FontSansName, TextXs, FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font BaseFont = new Font(FontSansName, TextBase, FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font MediumFont = new Font(FontSansName, TextSm, FontStyle.Bold, GraphicsUnit.Pixel);

        // Heading Fonts
        public static readonly Font H1 = new Font(FontSansName, Text4xl, FontStyle.Bold, GraphicsUnit.Pixel);
        public static readonly Font H2 = new Font(FontSansName, Text3xl, FontStyle.Bold, GraphicsUnit.Pixel);
        public static readonly Font H3 = new Font(FontSansName, Text2xl, FontStyle.Bold, GraphicsUnit.Pixel);
        public static readonly Font H4 = new Font(FontSansName, TextXl, FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font H5 = new Font(FontSansName, TextLg, FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font H6 = new Font(FontSansName, TextBase, FontStyle.Regular, GraphicsUnit.Pixel);

        // Code Font
        public static readonly Font CodeFont = new Font(FontMonoName, TextSm, FontStyle.Regular, GraphicsUnit.Pixel);

        // Caption Font
        public static readonly Font CaptionFont = new Font(FontSansName, TextXs, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
