namespace VSMVVM.WinForms.Design.Tokens
{
    /// <summary>
    /// Effects 토큰. WPF.Design Effects.xaml의 CornerRadius, Shadow, Opacity, Border 값을 포팅합니다.
    /// </summary>
    public static class Effects
    {
        #region Corner Radius

        public const int RoundedNone = 0;
        public const int RoundedSm = 2;
        public const int Rounded = 4;
        public const int RoundedMd = 6;
        public const int RoundedLg = 8;
        public const int RoundedXl = 12;
        public const int Rounded2xl = 16;
        public const int Rounded3xl = 24;
        public const int RoundedFull = 9999;

        #endregion

        #region Shadow Parameters

        /// <summary>그림자 파라미터 구조체.</summary>
        public struct ShadowParams
        {
            public int BlurRadius;
            public int OffsetY;
            public float Opacity;

            public ShadowParams(int blurRadius, int offsetY, float opacity)
            {
                BlurRadius = blurRadius;
                OffsetY = offsetY;
                Opacity = opacity;
            }
        }

        public static readonly ShadowParams ShadowSm = new ShadowParams(4, 1, 0.05f);
        public static readonly ShadowParams Shadow = new ShadowParams(6, 2, 0.10f);
        public static readonly ShadowParams ShadowMd = new ShadowParams(10, 4, 0.10f);
        public static readonly ShadowParams ShadowLg = new ShadowParams(15, 8, 0.10f);
        public static readonly ShadowParams ShadowXl = new ShadowParams(25, 12, 0.10f);

        #endregion

        #region Opacity

        public const float Opacity0 = 0f;
        public const float Opacity5 = 0.05f;
        public const float Opacity10 = 0.1f;
        public const float Opacity20 = 0.2f;
        public const float Opacity25 = 0.25f;
        public const float Opacity30 = 0.3f;
        public const float Opacity40 = 0.4f;
        public const float Opacity50 = 0.5f;
        public const float Opacity60 = 0.6f;
        public const float Opacity70 = 0.7f;
        public const float Opacity75 = 0.75f;
        public const float Opacity80 = 0.8f;
        public const float Opacity90 = 0.9f;
        public const float Opacity100 = 1.0f;

        #endregion

        #region Border Width

        public const int Border0 = 0;
        public const int Border = 1;
        public const int Border2 = 2;
        public const int Border4 = 4;

        #endregion
    }
}
