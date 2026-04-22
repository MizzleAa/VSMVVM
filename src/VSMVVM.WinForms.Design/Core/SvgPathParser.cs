using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace VSMVVM.WinForms.Design.Core
{
    /// <summary>
    /// SVG path data(<c>d</c> attribute)를 GDI+ GraphicsPath로 변환합니다.
    /// 지원 명령: M/m, L/l, H/h, V/v, C/c, S/s, Q/q, T/t, Z/z.
    /// A/a(호)는 Material Design 아이콘 패턴에서 거의 사용되지 않아 제외.
    /// </summary>
    public static class SvgPathParser
    {
        /// <summary>
        /// SVG path 문자열을 파싱하여 GraphicsPath로 반환합니다.
        /// </summary>
        public static GraphicsPath Parse(string d)
        {
            var path = new GraphicsPath(FillMode.Winding);
            if (string.IsNullOrWhiteSpace(d)) return path;

            var tokens = Tokenize(d);
            int i = 0;
            float cx = 0, cy = 0;           // current point
            float sx = 0, sy = 0;           // subpath start
            float lastCtrlX = 0, lastCtrlY = 0;
            char lastCmd = '\0';

            while (i < tokens.Count)
            {
                char cmd = tokens[i].IsCommand ? tokens[i++].Command : ImplicitCmd(lastCmd);
                bool rel = char.IsLower(cmd);
                char uc = char.ToUpperInvariant(cmd);

                switch (uc)
                {
                    case 'M':
                    {
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x += cx; y += cy; }
                        path.StartFigure();
                        cx = sx = x; cy = sy = y;
                        // 이후 좌표 쌍은 암묵적 L
                        while (i < tokens.Count && !tokens[i].IsCommand)
                        {
                            float lx = Num(tokens, ref i), ly = Num(tokens, ref i);
                            if (rel) { lx += cx; ly += cy; }
                            path.AddLine(cx, cy, lx, ly);
                            cx = lx; cy = ly;
                        }
                        lastCmd = rel ? 'l' : 'L';
                        break;
                    }
                    case 'L':
                    {
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x += cx; y += cy; }
                        path.AddLine(cx, cy, x, y);
                        cx = x; cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'H':
                    {
                        float x = Num(tokens, ref i);
                        if (rel) x += cx;
                        path.AddLine(cx, cy, x, cy);
                        cx = x;
                        lastCmd = cmd;
                        break;
                    }
                    case 'V':
                    {
                        float y = Num(tokens, ref i);
                        if (rel) y += cy;
                        path.AddLine(cx, cy, cx, y);
                        cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'C':
                    {
                        float x1 = Num(tokens, ref i), y1 = Num(tokens, ref i);
                        float x2 = Num(tokens, ref i), y2 = Num(tokens, ref i);
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x1 += cx; y1 += cy; x2 += cx; y2 += cy; x += cx; y += cy; }
                        path.AddBezier(cx, cy, x1, y1, x2, y2, x, y);
                        lastCtrlX = x2; lastCtrlY = y2;
                        cx = x; cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'S':
                    {
                        float x1, y1;
                        if (lastCmd == 'C' || lastCmd == 'c' || lastCmd == 'S' || lastCmd == 's')
                        { x1 = 2 * cx - lastCtrlX; y1 = 2 * cy - lastCtrlY; }
                        else { x1 = cx; y1 = cy; }
                        float x2 = Num(tokens, ref i), y2 = Num(tokens, ref i);
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x2 += cx; y2 += cy; x += cx; y += cy; }
                        path.AddBezier(cx, cy, x1, y1, x2, y2, x, y);
                        lastCtrlX = x2; lastCtrlY = y2;
                        cx = x; cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'Q':
                    {
                        float x1 = Num(tokens, ref i), y1 = Num(tokens, ref i);
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x1 += cx; y1 += cy; x += cx; y += cy; }
                        AddQuadratic(path, cx, cy, x1, y1, x, y);
                        lastCtrlX = x1; lastCtrlY = y1;
                        cx = x; cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'T':
                    {
                        float x1, y1;
                        if (lastCmd == 'Q' || lastCmd == 'q' || lastCmd == 'T' || lastCmd == 't')
                        { x1 = 2 * cx - lastCtrlX; y1 = 2 * cy - lastCtrlY; }
                        else { x1 = cx; y1 = cy; }
                        float x = Num(tokens, ref i), y = Num(tokens, ref i);
                        if (rel) { x += cx; y += cy; }
                        AddQuadratic(path, cx, cy, x1, y1, x, y);
                        lastCtrlX = x1; lastCtrlY = y1;
                        cx = x; cy = y;
                        lastCmd = cmd;
                        break;
                    }
                    case 'Z':
                    {
                        path.CloseFigure();
                        cx = sx; cy = sy;
                        lastCmd = cmd;
                        break;
                    }
                    default:
                        // Unsupported — skip remaining tokens until next command
                        while (i < tokens.Count && !tokens[i].IsCommand) i++;
                        break;
                }
            }

            return path;
        }

        /// <summary>
        /// SVG path를 지정된 bounds에 맞게 스케일/이동 변환합니다.
        /// viewBox("minX minY w h")를 bounds로 매핑하며 Uniform 유지.
        /// </summary>
        public static Matrix BuildViewBoxTransform(RectangleF viewBox, RectangleF bounds)
        {
            float sx = bounds.Width / viewBox.Width;
            float sy = bounds.Height / viewBox.Height;
            float s = Math.Min(sx, sy);
            float offX = bounds.X + (bounds.Width - viewBox.Width * s) / 2f - viewBox.X * s;
            float offY = bounds.Y + (bounds.Height - viewBox.Height * s) / 2f - viewBox.Y * s;

            var m = new Matrix();
            m.Translate(offX, offY);
            m.Scale(s, s);
            return m;
        }

        private static void AddQuadratic(GraphicsPath path, float x0, float y0, float x1, float y1, float x, float y)
        {
            // Quadratic → Cubic 변환 (GDI+는 3차만 지원)
            float cx1 = x0 + 2f / 3f * (x1 - x0);
            float cy1 = y0 + 2f / 3f * (y1 - y0);
            float cx2 = x + 2f / 3f * (x1 - x);
            float cy2 = y + 2f / 3f * (y1 - y);
            path.AddBezier(x0, y0, cx1, cy1, cx2, cy2, x, y);
        }

        private static char ImplicitCmd(char last)
        {
            // M/m 이후 생략된 좌표는 L/l로 처리
            if (last == 'M') return 'L';
            if (last == 'm') return 'l';
            return last;
        }

        private struct Token
        {
            public bool IsCommand;
            public char Command;
            public float Number;
        }

        private static List<Token> Tokenize(string d)
        {
            var list = new List<Token>(d.Length);
            int i = 0;
            while (i < d.Length)
            {
                char c = d[i];
                if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }

                if (IsCommandChar(c))
                {
                    list.Add(new Token { IsCommand = true, Command = c });
                    i++;
                    continue;
                }

                // Number: optional sign, digits, optional '.', digits, optional e[+-]digits
                int start = i;
                if (c == '+' || c == '-') i++;
                bool sawDot = false;
                while (i < d.Length)
                {
                    char ch = d[i];
                    if (char.IsDigit(ch)) { i++; }
                    else if (ch == '.' && !sawDot) { sawDot = true; i++; }
                    else if (ch == 'e' || ch == 'E')
                    {
                        i++;
                        if (i < d.Length && (d[i] == '+' || d[i] == '-')) i++;
                    }
                    else if ((ch == '-' || ch == '+') && i > start && d[i - 1] != 'e' && d[i - 1] != 'E')
                    {
                        // Next number starts without separator (SVG allows "1-2" = "1 -2")
                        break;
                    }
                    else break;
                }

                if (i == start)
                {
                    // 알 수 없는 문자 — skip
                    i++;
                    continue;
                }

                float n = float.Parse(d.Substring(start, i - start), CultureInfo.InvariantCulture);
                list.Add(new Token { IsCommand = false, Number = n });
            }
            return list;
        }

        private static bool IsCommandChar(char c)
        {
            return c == 'M' || c == 'm' || c == 'L' || c == 'l'
                || c == 'H' || c == 'h' || c == 'V' || c == 'v'
                || c == 'C' || c == 'c' || c == 'S' || c == 's'
                || c == 'Q' || c == 'q' || c == 'T' || c == 't'
                || c == 'A' || c == 'a' || c == 'Z' || c == 'z';
        }

        private static float Num(List<Token> tokens, ref int i)
        {
            if (i >= tokens.Count || tokens[i].IsCommand) return 0f;
            return tokens[i++].Number;
        }
    }
}
