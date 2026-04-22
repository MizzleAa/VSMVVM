using System.Drawing;

namespace VSMVVM.WinForms.Design.Colors
{
    /// <summary>
    /// Tailwind CSS Full Color Palette.
    /// 22 Colors × 11 Shades = 242 Colors + White/Black/Transparent.
    /// WPF.Design Palette.xaml과 동일한 색상값을 제공합니다.
    /// </summary>
    public static class Palette
    {
        // White/Black
        public static readonly Color White = Color.FromArgb(255, 255, 255);
        public static readonly Color Black = Color.FromArgb(0, 0, 0);
        public static readonly Color TransparentColor = Color.Transparent;

        // Slate
        public static readonly Color Slate50 = ColorTranslator.FromHtml("#F8FAFC");
        public static readonly Color Slate100 = ColorTranslator.FromHtml("#F1F5F9");
        public static readonly Color Slate200 = ColorTranslator.FromHtml("#E2E8F0");
        public static readonly Color Slate300 = ColorTranslator.FromHtml("#CBD5E1");
        public static readonly Color Slate400 = ColorTranslator.FromHtml("#94A3B8");
        public static readonly Color Slate500 = ColorTranslator.FromHtml("#64748B");
        public static readonly Color Slate600 = ColorTranslator.FromHtml("#475569");
        public static readonly Color Slate700 = ColorTranslator.FromHtml("#334155");
        public static readonly Color Slate800 = ColorTranslator.FromHtml("#1E293B");
        public static readonly Color Slate900 = ColorTranslator.FromHtml("#0F172A");
        public static readonly Color Slate950 = ColorTranslator.FromHtml("#020617");

        // Gray
        public static readonly Color Gray50 = ColorTranslator.FromHtml("#F9FAFB");
        public static readonly Color Gray100 = ColorTranslator.FromHtml("#F3F4F6");
        public static readonly Color Gray200 = ColorTranslator.FromHtml("#E5E7EB");
        public static readonly Color Gray300 = ColorTranslator.FromHtml("#D1D5DB");
        public static readonly Color Gray400 = ColorTranslator.FromHtml("#9CA3AF");
        public static readonly Color Gray500 = ColorTranslator.FromHtml("#6B7280");
        public static readonly Color Gray600 = ColorTranslator.FromHtml("#4B5563");
        public static readonly Color Gray700 = ColorTranslator.FromHtml("#374151");
        public static readonly Color Gray800 = ColorTranslator.FromHtml("#1F2937");
        public static readonly Color Gray900 = ColorTranslator.FromHtml("#111827");
        public static readonly Color Gray950 = ColorTranslator.FromHtml("#030712");

        // Zinc
        public static readonly Color Zinc50 = ColorTranslator.FromHtml("#FAFAFA");
        public static readonly Color Zinc100 = ColorTranslator.FromHtml("#F4F4F5");
        public static readonly Color Zinc200 = ColorTranslator.FromHtml("#E4E4E7");
        public static readonly Color Zinc300 = ColorTranslator.FromHtml("#D4D4D8");
        public static readonly Color Zinc400 = ColorTranslator.FromHtml("#A1A1AA");
        public static readonly Color Zinc500 = ColorTranslator.FromHtml("#71717A");
        public static readonly Color Zinc600 = ColorTranslator.FromHtml("#52525B");
        public static readonly Color Zinc700 = ColorTranslator.FromHtml("#3F3F46");
        public static readonly Color Zinc800 = ColorTranslator.FromHtml("#27272A");
        public static readonly Color Zinc900 = ColorTranslator.FromHtml("#18181B");
        public static readonly Color Zinc950 = ColorTranslator.FromHtml("#09090B");

        // Neutral
        public static readonly Color Neutral50 = ColorTranslator.FromHtml("#FAFAFA");
        public static readonly Color Neutral100 = ColorTranslator.FromHtml("#F5F5F5");
        public static readonly Color Neutral200 = ColorTranslator.FromHtml("#E5E5E5");
        public static readonly Color Neutral300 = ColorTranslator.FromHtml("#D4D4D4");
        public static readonly Color Neutral400 = ColorTranslator.FromHtml("#A3A3A3");
        public static readonly Color Neutral500 = ColorTranslator.FromHtml("#737373");
        public static readonly Color Neutral600 = ColorTranslator.FromHtml("#525252");
        public static readonly Color Neutral700 = ColorTranslator.FromHtml("#404040");
        public static readonly Color Neutral800 = ColorTranslator.FromHtml("#262626");
        public static readonly Color Neutral900 = ColorTranslator.FromHtml("#171717");
        public static readonly Color Neutral950 = ColorTranslator.FromHtml("#0A0A0A");

        // Stone
        public static readonly Color Stone50 = ColorTranslator.FromHtml("#FAFAF9");
        public static readonly Color Stone100 = ColorTranslator.FromHtml("#F5F5F4");
        public static readonly Color Stone200 = ColorTranslator.FromHtml("#E7E5E4");
        public static readonly Color Stone300 = ColorTranslator.FromHtml("#D6D3D1");
        public static readonly Color Stone400 = ColorTranslator.FromHtml("#A8A29E");
        public static readonly Color Stone500 = ColorTranslator.FromHtml("#78716C");
        public static readonly Color Stone600 = ColorTranslator.FromHtml("#57534E");
        public static readonly Color Stone700 = ColorTranslator.FromHtml("#44403C");
        public static readonly Color Stone800 = ColorTranslator.FromHtml("#292524");
        public static readonly Color Stone900 = ColorTranslator.FromHtml("#1C1917");
        public static readonly Color Stone950 = ColorTranslator.FromHtml("#0C0A09");

        // Red
        public static readonly Color Red50 = ColorTranslator.FromHtml("#FEF2F2");
        public static readonly Color Red100 = ColorTranslator.FromHtml("#FEE2E2");
        public static readonly Color Red200 = ColorTranslator.FromHtml("#FECACA");
        public static readonly Color Red300 = ColorTranslator.FromHtml("#FCA5A5");
        public static readonly Color Red400 = ColorTranslator.FromHtml("#F87171");
        public static readonly Color Red500 = ColorTranslator.FromHtml("#EF4444");
        public static readonly Color Red600 = ColorTranslator.FromHtml("#DC2626");
        public static readonly Color Red700 = ColorTranslator.FromHtml("#B91C1C");
        public static readonly Color Red800 = ColorTranslator.FromHtml("#991B1B");
        public static readonly Color Red900 = ColorTranslator.FromHtml("#7F1D1D");
        public static readonly Color Red950 = ColorTranslator.FromHtml("#450A0A");

        // Orange
        public static readonly Color Orange50 = ColorTranslator.FromHtml("#FFF7ED");
        public static readonly Color Orange100 = ColorTranslator.FromHtml("#FFEDD5");
        public static readonly Color Orange200 = ColorTranslator.FromHtml("#FED7AA");
        public static readonly Color Orange300 = ColorTranslator.FromHtml("#FDBA74");
        public static readonly Color Orange400 = ColorTranslator.FromHtml("#FB923C");
        public static readonly Color Orange500 = ColorTranslator.FromHtml("#F97316");
        public static readonly Color Orange600 = ColorTranslator.FromHtml("#EA580C");
        public static readonly Color Orange700 = ColorTranslator.FromHtml("#C2410C");
        public static readonly Color Orange800 = ColorTranslator.FromHtml("#9A3412");
        public static readonly Color Orange900 = ColorTranslator.FromHtml("#7C2D12");
        public static readonly Color Orange950 = ColorTranslator.FromHtml("#431407");

        // Amber
        public static readonly Color Amber50 = ColorTranslator.FromHtml("#FFFBEB");
        public static readonly Color Amber100 = ColorTranslator.FromHtml("#FEF3C7");
        public static readonly Color Amber200 = ColorTranslator.FromHtml("#FDE68A");
        public static readonly Color Amber300 = ColorTranslator.FromHtml("#FCD34D");
        public static readonly Color Amber400 = ColorTranslator.FromHtml("#FBBF24");
        public static readonly Color Amber500 = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color Amber600 = ColorTranslator.FromHtml("#D97706");
        public static readonly Color Amber700 = ColorTranslator.FromHtml("#B45309");
        public static readonly Color Amber800 = ColorTranslator.FromHtml("#92400E");
        public static readonly Color Amber900 = ColorTranslator.FromHtml("#78350F");
        public static readonly Color Amber950 = ColorTranslator.FromHtml("#451A03");

        // Yellow
        public static readonly Color Yellow50 = ColorTranslator.FromHtml("#FEFCE8");
        public static readonly Color Yellow100 = ColorTranslator.FromHtml("#FEF9C3");
        public static readonly Color Yellow200 = ColorTranslator.FromHtml("#FEF08A");
        public static readonly Color Yellow300 = ColorTranslator.FromHtml("#FDE047");
        public static readonly Color Yellow400 = ColorTranslator.FromHtml("#FACC15");
        public static readonly Color Yellow500 = ColorTranslator.FromHtml("#EAB308");
        public static readonly Color Yellow600 = ColorTranslator.FromHtml("#CA8A04");
        public static readonly Color Yellow700 = ColorTranslator.FromHtml("#A16207");
        public static readonly Color Yellow800 = ColorTranslator.FromHtml("#854D0E");
        public static readonly Color Yellow900 = ColorTranslator.FromHtml("#713F12");
        public static readonly Color Yellow950 = ColorTranslator.FromHtml("#422006");

        // Lime
        public static readonly Color Lime50 = ColorTranslator.FromHtml("#F7FEE7");
        public static readonly Color Lime100 = ColorTranslator.FromHtml("#ECFCCB");
        public static readonly Color Lime200 = ColorTranslator.FromHtml("#D9F99D");
        public static readonly Color Lime300 = ColorTranslator.FromHtml("#BEF264");
        public static readonly Color Lime400 = ColorTranslator.FromHtml("#A3E635");
        public static readonly Color Lime500 = ColorTranslator.FromHtml("#84CC16");
        public static readonly Color Lime600 = ColorTranslator.FromHtml("#65A30D");
        public static readonly Color Lime700 = ColorTranslator.FromHtml("#4D7C0F");
        public static readonly Color Lime800 = ColorTranslator.FromHtml("#3F6212");
        public static readonly Color Lime900 = ColorTranslator.FromHtml("#365314");
        public static readonly Color Lime950 = ColorTranslator.FromHtml("#1A2E05");

        // Green
        public static readonly Color Green50 = ColorTranslator.FromHtml("#F0FDF4");
        public static readonly Color Green100 = ColorTranslator.FromHtml("#DCFCE7");
        public static readonly Color Green200 = ColorTranslator.FromHtml("#BBF7D0");
        public static readonly Color Green300 = ColorTranslator.FromHtml("#86EFAC");
        public static readonly Color Green400 = ColorTranslator.FromHtml("#4ADE80");
        public static readonly Color Green500 = ColorTranslator.FromHtml("#22C55E");
        public static readonly Color Green600 = ColorTranslator.FromHtml("#16A34A");
        public static readonly Color Green700 = ColorTranslator.FromHtml("#15803D");
        public static readonly Color Green800 = ColorTranslator.FromHtml("#166534");
        public static readonly Color Green900 = ColorTranslator.FromHtml("#14532D");
        public static readonly Color Green950 = ColorTranslator.FromHtml("#052E16");

        // Emerald
        public static readonly Color Emerald50 = ColorTranslator.FromHtml("#ECFDF5");
        public static readonly Color Emerald100 = ColorTranslator.FromHtml("#D1FAE5");
        public static readonly Color Emerald200 = ColorTranslator.FromHtml("#A7F3D0");
        public static readonly Color Emerald300 = ColorTranslator.FromHtml("#6EE7B7");
        public static readonly Color Emerald400 = ColorTranslator.FromHtml("#34D399");
        public static readonly Color Emerald500 = ColorTranslator.FromHtml("#10B981");
        public static readonly Color Emerald600 = ColorTranslator.FromHtml("#059669");
        public static readonly Color Emerald700 = ColorTranslator.FromHtml("#047857");
        public static readonly Color Emerald800 = ColorTranslator.FromHtml("#065F46");
        public static readonly Color Emerald900 = ColorTranslator.FromHtml("#064E3B");
        public static readonly Color Emerald950 = ColorTranslator.FromHtml("#022C22");

        // Teal
        public static readonly Color Teal50 = ColorTranslator.FromHtml("#F0FDFA");
        public static readonly Color Teal100 = ColorTranslator.FromHtml("#CCFBF1");
        public static readonly Color Teal200 = ColorTranslator.FromHtml("#99F6E4");
        public static readonly Color Teal300 = ColorTranslator.FromHtml("#5EEAD4");
        public static readonly Color Teal400 = ColorTranslator.FromHtml("#2DD4BF");
        public static readonly Color Teal500 = ColorTranslator.FromHtml("#14B8A6");
        public static readonly Color Teal600 = ColorTranslator.FromHtml("#0D9488");
        public static readonly Color Teal700 = ColorTranslator.FromHtml("#0F766E");
        public static readonly Color Teal800 = ColorTranslator.FromHtml("#115E59");
        public static readonly Color Teal900 = ColorTranslator.FromHtml("#134E4A");
        public static readonly Color Teal950 = ColorTranslator.FromHtml("#042F2E");

        // Cyan
        public static readonly Color Cyan50 = ColorTranslator.FromHtml("#ECFEFF");
        public static readonly Color Cyan100 = ColorTranslator.FromHtml("#CFFAFE");
        public static readonly Color Cyan200 = ColorTranslator.FromHtml("#A5F3FC");
        public static readonly Color Cyan300 = ColorTranslator.FromHtml("#67E8F9");
        public static readonly Color Cyan400 = ColorTranslator.FromHtml("#22D3EE");
        public static readonly Color Cyan500 = ColorTranslator.FromHtml("#06B6D4");
        public static readonly Color Cyan600 = ColorTranslator.FromHtml("#0891B2");
        public static readonly Color Cyan700 = ColorTranslator.FromHtml("#0E7490");
        public static readonly Color Cyan800 = ColorTranslator.FromHtml("#155E75");
        public static readonly Color Cyan900 = ColorTranslator.FromHtml("#164E63");
        public static readonly Color Cyan950 = ColorTranslator.FromHtml("#083344");

        // Sky
        public static readonly Color Sky50 = ColorTranslator.FromHtml("#F0F9FF");
        public static readonly Color Sky100 = ColorTranslator.FromHtml("#E0F2FE");
        public static readonly Color Sky200 = ColorTranslator.FromHtml("#BAE6FD");
        public static readonly Color Sky300 = ColorTranslator.FromHtml("#7DD3FC");
        public static readonly Color Sky400 = ColorTranslator.FromHtml("#38BDF8");
        public static readonly Color Sky500 = ColorTranslator.FromHtml("#0EA5E9");
        public static readonly Color Sky600 = ColorTranslator.FromHtml("#0284C7");
        public static readonly Color Sky700 = ColorTranslator.FromHtml("#0369A1");
        public static readonly Color Sky800 = ColorTranslator.FromHtml("#075985");
        public static readonly Color Sky900 = ColorTranslator.FromHtml("#0C4A6E");
        public static readonly Color Sky950 = ColorTranslator.FromHtml("#082F49");

        // Blue
        public static readonly Color Blue50 = ColorTranslator.FromHtml("#EFF6FF");
        public static readonly Color Blue100 = ColorTranslator.FromHtml("#DBEAFE");
        public static readonly Color Blue200 = ColorTranslator.FromHtml("#BFDBFE");
        public static readonly Color Blue300 = ColorTranslator.FromHtml("#93C5FD");
        public static readonly Color Blue400 = ColorTranslator.FromHtml("#60A5FA");
        public static readonly Color Blue500 = ColorTranslator.FromHtml("#3B82F6");
        public static readonly Color Blue600 = ColorTranslator.FromHtml("#2563EB");
        public static readonly Color Blue700 = ColorTranslator.FromHtml("#1D4ED8");
        public static readonly Color Blue800 = ColorTranslator.FromHtml("#1E40AF");
        public static readonly Color Blue900 = ColorTranslator.FromHtml("#1E3A8A");
        public static readonly Color Blue950 = ColorTranslator.FromHtml("#172554");

        // Indigo
        public static readonly Color Indigo50 = ColorTranslator.FromHtml("#EEF2FF");
        public static readonly Color Indigo100 = ColorTranslator.FromHtml("#E0E7FF");
        public static readonly Color Indigo200 = ColorTranslator.FromHtml("#C7D2FE");
        public static readonly Color Indigo300 = ColorTranslator.FromHtml("#A5B4FC");
        public static readonly Color Indigo400 = ColorTranslator.FromHtml("#818CF8");
        public static readonly Color Indigo500 = ColorTranslator.FromHtml("#6366F1");
        public static readonly Color Indigo600 = ColorTranslator.FromHtml("#4F46E5");
        public static readonly Color Indigo700 = ColorTranslator.FromHtml("#4338CA");
        public static readonly Color Indigo800 = ColorTranslator.FromHtml("#3730A3");
        public static readonly Color Indigo900 = ColorTranslator.FromHtml("#312E81");
        public static readonly Color Indigo950 = ColorTranslator.FromHtml("#1E1B4B");

        // Violet
        public static readonly Color Violet50 = ColorTranslator.FromHtml("#F5F3FF");
        public static readonly Color Violet100 = ColorTranslator.FromHtml("#EDE9FE");
        public static readonly Color Violet200 = ColorTranslator.FromHtml("#DDD6FE");
        public static readonly Color Violet300 = ColorTranslator.FromHtml("#C4B5FD");
        public static readonly Color Violet400 = ColorTranslator.FromHtml("#A78BFA");
        public static readonly Color Violet500 = ColorTranslator.FromHtml("#8B5CF6");
        public static readonly Color Violet600 = ColorTranslator.FromHtml("#7C3AED");
        public static readonly Color Violet700 = ColorTranslator.FromHtml("#6D28D9");
        public static readonly Color Violet800 = ColorTranslator.FromHtml("#5B21B6");
        public static readonly Color Violet900 = ColorTranslator.FromHtml("#4C1D95");
        public static readonly Color Violet950 = ColorTranslator.FromHtml("#2E1065");

        // Purple
        public static readonly Color Purple50 = ColorTranslator.FromHtml("#FAF5FF");
        public static readonly Color Purple100 = ColorTranslator.FromHtml("#F3E8FF");
        public static readonly Color Purple200 = ColorTranslator.FromHtml("#E9D5FF");
        public static readonly Color Purple300 = ColorTranslator.FromHtml("#D8B4FE");
        public static readonly Color Purple400 = ColorTranslator.FromHtml("#C084FC");
        public static readonly Color Purple500 = ColorTranslator.FromHtml("#A855F7");
        public static readonly Color Purple600 = ColorTranslator.FromHtml("#9333EA");
        public static readonly Color Purple700 = ColorTranslator.FromHtml("#7E22CE");
        public static readonly Color Purple800 = ColorTranslator.FromHtml("#6B21A8");
        public static readonly Color Purple900 = ColorTranslator.FromHtml("#581C87");
        public static readonly Color Purple950 = ColorTranslator.FromHtml("#3B0764");

        // Fuchsia
        public static readonly Color Fuchsia50 = ColorTranslator.FromHtml("#FDF4FF");
        public static readonly Color Fuchsia100 = ColorTranslator.FromHtml("#FAE8FF");
        public static readonly Color Fuchsia200 = ColorTranslator.FromHtml("#F5D0FE");
        public static readonly Color Fuchsia300 = ColorTranslator.FromHtml("#F0ABFC");
        public static readonly Color Fuchsia400 = ColorTranslator.FromHtml("#E879F9");
        public static readonly Color Fuchsia500 = ColorTranslator.FromHtml("#D946EF");
        public static readonly Color Fuchsia600 = ColorTranslator.FromHtml("#C026D3");
        public static readonly Color Fuchsia700 = ColorTranslator.FromHtml("#A21CAF");
        public static readonly Color Fuchsia800 = ColorTranslator.FromHtml("#86198F");
        public static readonly Color Fuchsia900 = ColorTranslator.FromHtml("#701A75");
        public static readonly Color Fuchsia950 = ColorTranslator.FromHtml("#4A044E");

        // Pink
        public static readonly Color Pink50 = ColorTranslator.FromHtml("#FDF2F8");
        public static readonly Color Pink100 = ColorTranslator.FromHtml("#FCE7F3");
        public static readonly Color Pink200 = ColorTranslator.FromHtml("#FBCFE8");
        public static readonly Color Pink300 = ColorTranslator.FromHtml("#F9A8D4");
        public static readonly Color Pink400 = ColorTranslator.FromHtml("#F472B6");
        public static readonly Color Pink500 = ColorTranslator.FromHtml("#EC4899");
        public static readonly Color Pink600 = ColorTranslator.FromHtml("#DB2777");
        public static readonly Color Pink700 = ColorTranslator.FromHtml("#BE185D");
        public static readonly Color Pink800 = ColorTranslator.FromHtml("#9D174D");
        public static readonly Color Pink900 = ColorTranslator.FromHtml("#831843");
        public static readonly Color Pink950 = ColorTranslator.FromHtml("#500724");

        // Rose
        public static readonly Color Rose50 = ColorTranslator.FromHtml("#FFF1F2");
        public static readonly Color Rose100 = ColorTranslator.FromHtml("#FFE4E6");
        public static readonly Color Rose200 = ColorTranslator.FromHtml("#FECDD3");
        public static readonly Color Rose300 = ColorTranslator.FromHtml("#FDA4AF");
        public static readonly Color Rose400 = ColorTranslator.FromHtml("#FB7185");
        public static readonly Color Rose500 = ColorTranslator.FromHtml("#F43F5E");
        public static readonly Color Rose600 = ColorTranslator.FromHtml("#E11D48");
        public static readonly Color Rose700 = ColorTranslator.FromHtml("#BE123C");
        public static readonly Color Rose800 = ColorTranslator.FromHtml("#9F1239");
        public static readonly Color Rose900 = ColorTranslator.FromHtml("#881337");
        public static readonly Color Rose950 = ColorTranslator.FromHtml("#4C0519");
    }
}
