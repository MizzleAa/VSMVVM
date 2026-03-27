using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VSMVVM.WPF.Media
{
    /// <summary>
    /// SVG를 WPF ImageSource로 변환하는 자체 구현 컨버터.
    /// 기본 SVG 요소(rect, circle, ellipse, line, polyline, polygon, path, text)를 지원합니다.
    /// </summary>
    public static class SvgImageConverter
    {
        #region Constants

        private static readonly Regex ViewBoxRegex = new Regex(
            @"viewBox\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WidthRegex = new Regex(
            @"<svg[^>]+width\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HeightRegex = new Regex(
            @"<svg[^>]+height\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RectRegex = new Regex(
            @"<rect\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CircleRegex = new Regex(
            @"<circle\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EllipseRegex = new Regex(
            @"<ellipse\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LineRegex = new Regex(
            @"<line\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PathRegex = new Regex(
            @"<path\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PolygonRegex = new Regex(
            @"<polygon\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PolylineRegex = new Regex(
            @"<polyline\s+([^>]+)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SvgElementRegex = new Regex(
            @"<svg\s+([^>]+)>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AttributeRegex = new Regex(
            @"(\w[\w-]*)\s*=\s*""([^""]+)""",
            RegexOptions.Compiled);

        #endregion

        #region Public Methods

        /// <summary>
        /// SVG 파일을 DrawingImage로 변환합니다.
        /// </summary>
        public static DrawingImage ConvertFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var svgContent = File.ReadAllText(filePath);
            return ConvertFromString(svgContent);
        }

        /// <summary>
        /// SVG 문자열을 DrawingImage로 변환합니다.
        /// </summary>
        public static DrawingImage ConvertFromString(string svgContent)
        {
            if (string.IsNullOrEmpty(svgContent))
            {
                return null;
            }

            // <svg> 요소에서 속성 추출
            var svgAttrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var svgMatch = SvgElementRegex.Match(svgContent);
            if (svgMatch.Success)
            {
                svgAttrs = ParseAttributes(svgMatch.Groups[1].Value);
            }

            // <svg fill="..."> 상속용 기본 fill 추출
            string inheritedFill = null;
            if (svgAttrs.TryGetValue("fill", out var svgFill) && svgFill != "none")
            {
                inheritedFill = svgFill;
            }

            var drawingGroup = new DrawingGroup();
            ParseSvg(svgContent, drawingGroup, inheritedFill);

            // viewBox 변환 적용
            ApplyViewBoxTransform(drawingGroup, svgAttrs);

            return new DrawingImage(drawingGroup);
        }

        /// <summary>
        /// 리소스 URI에서 SVG를 로드합니다.
        /// </summary>
        public static DrawingImage ConvertFromResource(Uri resourceUri)
        {
            try
            {
                var streamInfo = Application.GetResourceStream(resourceUri);
                if (streamInfo == null)
                {
                    return null;
                }

                using (var reader = new StreamReader(streamInfo.Stream))
                {
                    var svgContent = reader.ReadToEnd();
                    return ConvertFromString(svgContent);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// DrawingGroup 내 모든 도형의 Fill을 지정된 Brush로 대체합니다.
        /// </summary>
        public static void OverrideFill(DrawingGroup group, Brush fill)
        {
            if (group == null)
            {
                return;
            }

            foreach (var drawing in group.Children)
            {
                if (drawing is GeometryDrawing gd)
                {
                    gd.Brush = fill;
                }
                else if (drawing is DrawingGroup childGroup)
                {
                    OverrideFill(childGroup, fill);
                }
            }
        }

        #endregion

        #region Private Methods

        private static void ParseSvg(string svgContent, DrawingGroup group, string inheritedFill = null)
        {
            // Rect 처리
            foreach (Match match in RectRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreateRectGeometry(attributes);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Circle 처리
            foreach (Match match in CircleRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreateCircleGeometry(attributes);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Ellipse 처리
            foreach (Match match in EllipseRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreateEllipseGeometry(attributes);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Line 처리
            foreach (Match match in LineRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreateLineGeometry(attributes);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Polyline 처리
            foreach (Match match in PolylineRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreatePolyGeometry(attributes, false);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Polygon 처리
            foreach (Match match in PolygonRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreatePolyGeometry(attributes, true);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }

            // Path 처리
            foreach (Match match in PathRegex.Matches(svgContent))
            {
                var attributes = ParseAttributes(match.Groups[1].Value);
                var geometry = CreatePathGeometry(attributes);
                AddGeometryDrawing(group, geometry, attributes, inheritedFill);
            }
        }

        /// <summary>
        /// viewBox 속성을 파싱하여 DrawingGroup에 좌표 변환을 적용합니다.
        /// </summary>
        private static void ApplyViewBoxTransform(DrawingGroup group, Dictionary<string, string> svgAttrs)
        {
            var vbMatch = ViewBoxRegex.Match(
                svgAttrs.TryGetValue("viewBox", out var vb) ? $"viewBox=\"{vb}\"" : "");

            if (!vbMatch.Success)
            {
                return;
            }

            var vbParts = vbMatch.Groups[1].Value.Split(
                new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (vbParts.Length != 4)
            {
                return;
            }

            var vbMinX = ParseDouble(vbParts[0]);
            var vbMinY = ParseDouble(vbParts[1]);
            var vbWidth = ParseDouble(vbParts[2]);
            var vbHeight = ParseDouble(vbParts[3]);

            if (vbWidth <= 0 || vbHeight <= 0)
            {
                return;
            }

            // SVG width/height (기본값: viewBox 크기)
            var svgWidth = GetDouble(svgAttrs, "width", vbWidth);
            var svgHeight = GetDouble(svgAttrs, "height", vbHeight);

            var scaleX = svgWidth / vbWidth;
            var scaleY = svgHeight / vbHeight;

            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform(-vbMinX, -vbMinY));
            transform.Children.Add(new ScaleTransform(scaleX, scaleY));

            group.Transform = transform;
        }

        private static Dictionary<string, string> ParseAttributes(string attributeString)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in AttributeRegex.Matches(attributeString))
            {
                attributes[match.Groups[1].Value] = match.Groups[2].Value;
            }

            return attributes;
        }

        private static Geometry CreateRectGeometry(Dictionary<string, string> attrs)
        {
            var x = GetDouble(attrs, "x");
            var y = GetDouble(attrs, "y");
            var w = GetDouble(attrs, "width");
            var h = GetDouble(attrs, "height");
            var rx = GetDouble(attrs, "rx");
            var ry = GetDouble(attrs, "ry");

            return new RectangleGeometry(new Rect(x, y, w, h), rx, ry);
        }

        private static Geometry CreateCircleGeometry(Dictionary<string, string> attrs)
        {
            var cx = GetDouble(attrs, "cx");
            var cy = GetDouble(attrs, "cy");
            var r = GetDouble(attrs, "r");

            return new EllipseGeometry(new Point(cx, cy), r, r);
        }

        private static Geometry CreateEllipseGeometry(Dictionary<string, string> attrs)
        {
            var cx = GetDouble(attrs, "cx");
            var cy = GetDouble(attrs, "cy");
            var rx = GetDouble(attrs, "rx");
            var ry = GetDouble(attrs, "ry");

            return new EllipseGeometry(new Point(cx, cy), rx, ry);
        }

        private static Geometry CreateLineGeometry(Dictionary<string, string> attrs)
        {
            var x1 = GetDouble(attrs, "x1");
            var y1 = GetDouble(attrs, "y1");
            var x2 = GetDouble(attrs, "x2");
            var y2 = GetDouble(attrs, "y2");

            return new LineGeometry(new Point(x1, y1), new Point(x2, y2));
        }

        private static Geometry CreatePolyGeometry(Dictionary<string, string> attrs, bool isClosed)
        {
            if (!attrs.TryGetValue("points", out var pointsString))
            {
                return Geometry.Empty;
            }

            var parts = pointsString.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return Geometry.Empty;
            }

            var figure = new PathFigure
            {
                StartPoint = new Point(ParseDouble(parts[0]), ParseDouble(parts[1])),
                IsClosed = isClosed
            };

            for (int i = 2; i + 1 < parts.Length; i += 2)
            {
                figure.Segments.Add(new LineSegment(
                    new Point(ParseDouble(parts[i]), ParseDouble(parts[i + 1])), true));
            }

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(figure);
            return pathGeometry;
        }

        private static Geometry CreatePathGeometry(Dictionary<string, string> attrs)
        {
            if (!attrs.TryGetValue("d", out var pathData))
            {
                return Geometry.Empty;
            }

            try
            {
                return Geometry.Parse(pathData);
            }
            catch
            {
                return Geometry.Empty;
            }
        }

        private static void AddGeometryDrawing(DrawingGroup group, Geometry geometry, Dictionary<string, string> attrs, string inheritedFill = null)
        {
            if (geometry == null || geometry == Geometry.Empty)
            {
                return;
            }

            Brush fill = null;
            Pen stroke = null;

            if (attrs.TryGetValue("fill", out var fillColor) && fillColor != "none")
            {
                fill = ParseBrush(fillColor, attrs);
            }
            else if (inheritedFill != null && fillColor != "none")
            {
                // <svg fill="..."> 상속
                fill = ParseBrush(inheritedFill, attrs);
            }

            if (attrs.TryGetValue("stroke", out var strokeColor) && strokeColor != "none")
            {
                var strokeWidth = GetDouble(attrs, "stroke-width", 1.0);
                stroke = new Pen(ParseBrush(strokeColor, attrs), strokeWidth);
            }

            // fill도 stroke도 없으면 기본 검정 fill
            if (fill == null && stroke == null)
            {
                fill = Brushes.Black;
            }

            group.Children.Add(new GeometryDrawing(fill, stroke, geometry));
        }

        private static Brush ParseBrush(string colorValue, Dictionary<string, string> attrs)
        {
            if (string.IsNullOrEmpty(colorValue) || colorValue == "none")
            {
                return null;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorValue);

                if (attrs.TryGetValue("opacity", out var opacityStr))
                {
                    var opacity = ParseDouble(opacityStr);
                    color.A = (byte)(opacity * 255);
                }

                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Black;
            }
        }

        private static double GetDouble(Dictionary<string, string> attrs, string key, double defaultValue = 0.0)
        {
            if (attrs.TryGetValue(key, out var value))
            {
                return ParseDouble(value);
            }

            return defaultValue;
        }

        private static double ParseDouble(string value)
        {
            // "px", "pt" 등 단위 접미사 제거
            value = value.TrimEnd('p', 'x', 't', 'e', 'm');

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return 0.0;
        }

        #endregion
    }
}
