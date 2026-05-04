using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// pycocotools 호환 column-major RLE 인코딩/디코딩.
    /// 각 인스턴스당 binary mask (0 = not-this-instance, 1 = this-instance) 를 RLE 로.
    /// </summary>
    public static class RleCodec
    {
        /// <summary>instanceMask 전체 버퍼에서 targetId 에 해당하는 binary mask 를 column-major 로 RLE 인코딩.</summary>
        public static List<int> Encode(uint[] instanceMask, int width, int height, uint targetId)
        {
            var counts = new List<int>();
            int run = 0;
            bool prevBit = false; // 첫 런은 "0(false)" 런부터 시작 (pycocotools 관례)

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool bit = instanceMask[y * width + x] == targetId;
                    if (bit == prevBit)
                    {
                        run++;
                    }
                    else
                    {
                        counts.Add(run);
                        prevBit = bit;
                        run = 1;
                    }
                }
            }
            counts.Add(run);
            return counts;
        }

        /// <summary>SparseTileLayer 오버로드 — 동일 알고리즘, Get(x, y) 사용. COCO export 경로.</summary>
        public static List<int> Encode(SparseTileLayer instanceMask, int width, int height, uint targetId)
        {
            var counts = new List<int>();
            int run = 0;
            bool prevBit = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool bit = instanceMask.Get(x, y) == targetId;
                    if (bit == prevBit)
                    {
                        run++;
                    }
                    else
                    {
                        counts.Add(run);
                        prevBit = bit;
                        run = 1;
                    }
                }
            }
            counts.Add(run);
            return counts;
        }

        /// <summary>
        /// column-major RLE 를 row-major binary mask 로 디코딩. 1 인 픽셀은 outId, 아니면 0.
        /// 반환 배열 크기는 width*height. instanceMask 에 OR 로 합치거나 직접 씀.
        /// </summary>
        public static uint[] Decode(IReadOnlyList<int> counts, int width, int height, uint outId)
        {
            var mask = new uint[width * height];
            int total = width * height;
            int pos = 0;
            bool bit = false;
            foreach (var run in counts)
            {
                for (int i = 0; i < run && pos < total; i++)
                {
                    if (bit)
                    {
                        // column-major: pos 를 (x*h + y) 로 해석
                        int x = pos / height;
                        int y = pos % height;
                        mask[y * width + x] = outId;
                    }
                    pos++;
                }
                bit = !bit;
            }
            return mask;
        }
    }
}
