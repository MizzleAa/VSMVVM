using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// pycocotools (maskApi.c rleToString/rleFrString) 호환 compressed RLE 인코더/디코더.
    /// int[] (column-major run-lengths) ↔ ASCII 문자열 변환.
    /// 외부 라이브러리 없이 순수 C# 구현.
    /// </summary>
    public static class CompressedRleCodec
    {
        /// <summary>
        /// uncompressed run-length 배열을 pycocotools 형식 문자열로 인코딩.
        /// i>=3 부터 counts[i-2] 기준 delta, 5비트 가변 청크 + sign/continuation bit.
        /// </summary>
        public static string Encode(IReadOnlyList<int> counts)
        {
            if (counts == null) return string.Empty;
            var sb = new StringBuilder(counts.Count * 2);
            for (int i = 0; i < counts.Count; i++)
            {
                long x = counts[i];
                if (i > 2) x -= counts[i - 2];
                bool more = true;
                while (more)
                {
                    long c = x & 0x1f;
                    x >>= 5;                                   // signed arithmetic shift
                    bool sign = (c & 0x10) != 0;
                    more = sign ? (x != -1L) : (x != 0L);
                    if (more) c |= 0x20;
                    c += 48;                                   // ASCII '0'
                    sb.Append((char)c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// pycocotools 형식 compressed RLE 문자열을 uncompressed run-length 배열로 디코딩.
        /// </summary>
        public static List<int> Decode(string s)
        {
            var counts = new List<int>();
            if (string.IsNullOrEmpty(s)) return counts;
            int p = 0, len = s.Length;
            while (p < len)
            {
                long x = 0;
                int k = 0;
                bool more = true;
                while (more)
                {
                    if (p >= len)
                        throw new InvalidDataException("Unexpected end of RLE stream.");
                    long c = (long)(s[p] - 48);
                    if (c < 0 || c > 63)
                        throw new InvalidDataException($"Invalid RLE char at index {p}.");
                    x |= (c & 0x1fL) << (5 * k);
                    more = (c & 0x20L) != 0;
                    p++;
                    k++;
                    if (!more && (c & 0x10L) != 0)
                        x |= -1L << (5 * k);                    // sign-extend
                }
                if (counts.Count > 2) x += counts[counts.Count - 2];
                if (x < 0 || x > int.MaxValue)
                    throw new InvalidDataException("RLE run out of int32 range.");
                counts.Add((int)x);
            }
            return counts;
        }
    }
}
