using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Xml;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// 단일 <c>&lt;path d="..."/&gt;</c> 중심의 경량 SVG 아이콘 모델.
    /// Material Design 계열 아이콘처럼 viewBox + 단일/소수 path로 구성된 SVG를 대상으로 합니다.
    /// </summary>
    public sealed class SvgIcon
    {
        private static readonly ConcurrentDictionary<string, SvgIcon> _cache =
            new ConcurrentDictionary<string, SvgIcon>(StringComparer.OrdinalIgnoreCase);

        /// <summary>SVG viewBox. 없으면 width/height 기반으로 추정.</summary>
        public RectangleF ViewBox { get; }

        /// <summary>첫 번째 &lt;path&gt;의 d 속성.</summary>
        public string PathData { get; }

        private SvgIcon(RectangleF viewBox, string pathData)
        {
            ViewBox = viewBox;
            PathData = pathData;
        }

        /// <summary>
        /// 파일 경로에서 SVG를 로드합니다. 경로당 한 번만 파싱되며 이후 캐시에서 반환.
        /// </summary>
        public static SvgIcon FromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            return _cache.GetOrAdd(path, p =>
            {
                try { return Parse(File.ReadAllText(p)); }
                catch { return null; }
            });
        }

        /// <summary>
        /// SVG 문자열을 파싱합니다.
        /// </summary>
        public static SvgIcon Parse(string svgXml)
        {
            if (string.IsNullOrWhiteSpace(svgXml)) return null;

            var doc = new XmlDocument();
            doc.LoadXml(svgXml);

            var root = doc.DocumentElement;
            if (root == null) return null;

            var viewBox = ParseViewBox(root);
            string pathData = null;

            // 단일 path 우선 — 여러 path가 있으면 모두 합쳐 하나의 d로 취급
            var paths = root.GetElementsByTagName("path");
            if (paths.Count == 0)
            {
                // xmlns 없이 path를 못 찾는 경우: 모든 자손 순회
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (string.Equals(n.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                    {
                        pathData = AppendPath(pathData, n.Attributes?["d"]?.Value);
                    }
                }
            }
            else
            {
                foreach (XmlNode n in paths)
                {
                    pathData = AppendPath(pathData, n.Attributes?["d"]?.Value);
                }
            }

            if (string.IsNullOrEmpty(pathData)) return null;
            return new SvgIcon(viewBox, pathData);
        }

        /// <summary>
        /// 아이콘을 지정된 bounds에 Uniform 스케일로 채웁니다.
        /// </summary>
        public void Draw(Graphics g, RectangleF bounds, Color color)
        {
            using var path = SvgPathParser.Parse(PathData);
            using var m = SvgPathParser.BuildViewBoxTransform(ViewBox, bounds);
            path.Transform(m);

            var prevSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);

            g.SmoothingMode = prevSmoothing;
        }

        private static string AppendPath(string current, string next)
        {
            if (string.IsNullOrEmpty(next)) return current;
            if (string.IsNullOrEmpty(current)) return next;
            return current + " " + next;
        }

        private static RectangleF ParseViewBox(System.Xml.XmlElement root)
        {
            var vb = root.GetAttribute("viewBox");
            if (!string.IsNullOrWhiteSpace(vb))
            {
                var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                    && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                {
                    return new RectangleF(x, y, w, h);
                }
            }

            // Fallback: width/height만 있는 SVG
            float fw = ParseLen(root.GetAttribute("width"), 24);
            float fh = ParseLen(root.GetAttribute("height"), 24);
            return new RectangleF(0, 0, fw, fh);
        }

        private static float ParseLen(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            int end = 0;
            while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-')) end++;
            if (end == 0) return fallback;
            return float.TryParse(s.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : fallback;
        }
    }
}
