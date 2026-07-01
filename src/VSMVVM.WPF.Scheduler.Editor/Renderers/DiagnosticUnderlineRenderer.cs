using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using VSMVVM.Core.Scheduler.Compilation;

namespace VSMVVM.WPF.Scheduler.Editor.Renderers
{
    /// <summary>
    /// AvalonEdit IBackgroundRenderer 구현. CodeNodeEditor의 Diagnostics 컬렉션을 받아
    /// 각 진단 범위에 sine-wave 물결 밑줄을 그린다.
    /// Severity별 색상은 VSMVVM.WPF.Design 시맨틱 토큰을 우선 사용:
    ///   Error   → Error  (Red500)
    ///   Warning → Warning(Amber500)
    ///   Info    → Info   (Sky500)
    ///   Hidden  → 그리지 않음
    /// 토큰을 찾지 못하면 폴백 RGB 색.
    /// </summary>
    public sealed class DiagnosticUnderlineRenderer : IBackgroundRenderer
    {
        private static readonly Brush FallbackErrorBrush   = MakeFrozen("#EF4444"); // Red500
        private static readonly Brush FallbackWarningBrush = MakeFrozen("#F59E0B"); // Amber500
        private static readonly Brush FallbackInfoBrush    = MakeFrozen("#0EA5E9"); // Sky500

        private readonly Func<IReadOnlyList<CompilationDiagnostic>> _diagnosticsProvider;
        private readonly Func<Brush> _errorBrushProvider;
        private readonly Func<Brush> _warningBrushProvider;
        private readonly Func<Brush> _infoBrushProvider;

        public DiagnosticUnderlineRenderer(Func<IReadOnlyList<CompilationDiagnostic>> diagnosticsProvider,
                                           Func<Brush> errorBrushProvider = null,
                                           Func<Brush> warningBrushProvider = null,
                                           Func<Brush> infoBrushProvider = null)
        {
            _diagnosticsProvider = diagnosticsProvider ?? throw new ArgumentNullException(nameof(diagnosticsProvider));
            _errorBrushProvider = errorBrushProvider ?? (() => FallbackErrorBrush);
            _warningBrushProvider = warningBrushProvider ?? (() => FallbackWarningBrush);
            _infoBrushProvider = infoBrushProvider ?? (() => FallbackInfoBrush);
        }

        // Selection 위에 그리도록 Selection 레이어 사용 (텍스트 가독성 유지)
        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;
            if (!textView.VisualLinesValid) return;
            var diagnostics = _diagnosticsProvider();
            if (diagnostics == null || diagnostics.Count == 0) return;

            var doc = textView.Document;
            if (doc == null) return;

            textView.EnsureVisualLines();
            var pixelsPerDip = VisualTreeHelper.GetDpi(textView).PixelsPerDip;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                var d = diagnostics[i];
                if (d.Severity == CompilationDiagnosticSeverity.Hidden) continue;

                var brush = SelectBrush(d.Severity);
                var pen = new Pen(brush, 1) { LineJoin = PenLineJoin.Round };
                pen.Freeze();

                if (!TryConvertToOffsets(doc, d, out var startOffset, out var endOffset)) continue;
                if (endOffset <= startOffset) endOffset = startOffset + 1;

                var segment = new TextSegment { StartOffset = startOffset, EndOffset = endOffset };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    var waveGeometry = BuildWaveGeometry(rect);
                    drawingContext.DrawGeometry(null, pen, waveGeometry);
                }
            }
        }

        private Brush SelectBrush(CompilationDiagnosticSeverity sev) => sev switch
        {
            CompilationDiagnosticSeverity.Error   => _errorBrushProvider() ?? FallbackErrorBrush,
            CompilationDiagnosticSeverity.Warning => _warningBrushProvider() ?? FallbackWarningBrush,
            CompilationDiagnosticSeverity.Info    => _infoBrushProvider() ?? FallbackInfoBrush,
            _ => FallbackInfoBrush,
        };

        private static bool TryConvertToOffsets(TextDocument doc, CompilationDiagnostic d, out int start, out int end)
        {
            start = 0; end = 0;
            try
            {
                var startLine = Clamp(d.StartLine, 1, doc.LineCount);
                var endLine = Clamp(d.EndLine, 1, doc.LineCount);
                var startLineObj = doc.GetLineByNumber(startLine);
                var endLineObj = doc.GetLineByNumber(endLine);

                start = startLineObj.Offset + Math.Max(0, d.StartColumn - 1);
                start = Math.Min(start, startLineObj.EndOffset);

                end = endLineObj.Offset + Math.Max(0, d.EndColumn - 1);
                end = Math.Min(end, endLineObj.EndOffset);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Geometry BuildWaveGeometry(Rect rect)
        {
            // 텍스트 아래쪽에서 시작해 sine-wave 형태로 우측 끝까지 한 차례 진동.
            const double amplitude = 1.5;
            const double waveLength = 4.0;
            var baseY = rect.Bottom - 1;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(rect.Left, baseY), isFilled: false, isClosed: false);
                double x = rect.Left;
                bool up = true;
                while (x < rect.Right)
                {
                    var nextX = Math.Min(x + waveLength, rect.Right);
                    var midX = (x + nextX) / 2;
                    var peakY = baseY + (up ? -amplitude : amplitude);
                    ctx.QuadraticBezierTo(new Point(midX, peakY), new Point(nextX, baseY),
                                          isStroked: true, isSmoothJoin: true);
                    x = nextX;
                    up = !up;
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private static Brush MakeFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
