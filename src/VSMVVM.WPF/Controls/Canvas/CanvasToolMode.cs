namespace VSMVVM.WPF.Controls
{
    /// <summary>
    /// 캔버스 도구 모드 열거형.
    /// </summary>
    public enum CanvasToolMode
    {
        Select,
        Arrow,
        Pen,
        Rectangle,
        RoundedRectangle,
        Ellipse,
        Image,
        // 마스크 기반
        Brush,
        Eraser,
        Fill,
        RectangleMask,
        EllipseMask,
        PolygonMask,
        // 측정 도구
        LengthMeasurement,
        AngleMeasurement,
        // 자동 선택 도구
        MagicWand,
        MagneticLasso,
    }
}
