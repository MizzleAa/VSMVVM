using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// 마스크 레이어의 sparse 타일 storage. dense uint[width*height] 대신 256×256 타일 단위로
    /// 픽셀이 그려진 타일만 alloc — 8K 빈 layer 200MB → 0MB, 마스크 면적 비례 메모리.
    ///
    /// 좌표는 글로벌 픽셀 좌표 (x ∈ [0, Width), y ∈ [0, Height)) 또는 글로벌 픽셀 인덱스 (y * Width + x).
    /// 픽셀별 access (Get/Set) 는 dictionary lookup 1회 + 타일 내 dense access — dense 대비 ~수십 cycle 느림.
    /// Hot loop 는 EnumerateTiles 로 타일 단위 access (inner loop dense) 권장.
    /// </summary>
    public sealed class SparseTileLayer
    {
        // 128 × 128 = 16384 픽셀 × 4 byte = 64 KB 타일. LOH 임계 85 KB 미만이라 SOH 에 alloc 됨.
        // SOH 는 generational GC 가 자동 compact → fragmentation 누적 회피 (256 타일은 LOH 에서 fragment 심함).
        public const int TileSize = 128;
        public const int TilePixelCount = TileSize * TileSize; // 16384

        private readonly Dictionary<int, uint[]> _tiles = new();

        // 마지막으로 access 한 타일 캐시 — 같은 타일 안의 연속 픽셀 access 시 dictionary lookup skip.
        // brush stroke / scanline fill 등 hot loop 의 단일 픽셀 호출 패턴에 효과적.
        private int _cachedTileKey = -1;
        private uint[]? _cachedTile;

        public SparseTileLayer(int width, int height)
        {
            Width = width;
            Height = height;
            TilesX = (width + TileSize - 1) / TileSize;
            TilesY = (height + TileSize - 1) / TileSize;
        }

        /// <summary>전체 픽셀 폭 (글로벌).</summary>
        public int Width { get; }

        /// <summary>전체 픽셀 높이 (글로벌).</summary>
        public int Height { get; }

        /// <summary>타일 grid 의 가로 칸수.</summary>
        public int TilesX { get; }

        /// <summary>타일 grid 의 세로 칸수.</summary>
        public int TilesY { get; }

        /// <summary>전체 픽셀 수 (Width * Height) — dense uint[].Length 호환.</summary>
        public int Length => Width * Height;

        /// <summary>현재 alloc 된 타일 수. 메모리 측정용.</summary>
        public int AllocatedTileCount => _tiles.Count;

        /// <summary>(x, y) 픽셀 값. 타일 미할당이면 0.</summary>
        public uint Get(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return 0;
            int tileX = x / TileSize;
            int tileY = y / TileSize;
            int tileKey = tileY * TilesX + tileX;
            uint[]? tile;
            if (tileKey == _cachedTileKey)
            {
                tile = _cachedTile;
                if (tile == null) return 0;
            }
            else
            {
                if (!_tiles.TryGetValue(tileKey, out tile))
                {
                    _cachedTileKey = tileKey;
                    _cachedTile = null;
                    return 0;
                }
                _cachedTileKey = tileKey;
                _cachedTile = tile;
            }
            int localX = x - tileX * TileSize;
            int localY = y - tileY * TileSize;
            return tile[localY * TileSize + localX];
        }

        /// <summary>(x, y) 픽셀에 value 설정. value=0 + 타일 미할당이면 no-op (불필요한 alloc 회피).</summary>
        public void Set(int x, int y, uint value)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            int tileX = x / TileSize;
            int tileY = y / TileSize;
            int tileKey = tileY * TilesX + tileX;
            uint[]? tile;
            if (tileKey == _cachedTileKey)
            {
                tile = _cachedTile;
                if (tile == null)
                {
                    if (value == 0) return;
                    tile = new uint[TilePixelCount];
                    _tiles[tileKey] = tile;
                    _cachedTile = tile;
                }
            }
            else
            {
                if (!_tiles.TryGetValue(tileKey, out tile))
                {
                    if (value == 0)
                    {
                        _cachedTileKey = tileKey;
                        _cachedTile = null;
                        return;
                    }
                    tile = new uint[TilePixelCount];
                    _tiles[tileKey] = tile;
                }
                _cachedTileKey = tileKey;
                _cachedTile = tile;
            }
            int localX = x - tileX * TileSize;
            int localY = y - tileY * TileSize;
            tile[localY * TileSize + localX] = value;
        }

        /// <summary>글로벌 픽셀 인덱스 (y*Width + x) 로 조회. 호환용.</summary>
        public uint Get(int pixelIndex)
        {
            if ((uint)pixelIndex >= (uint)(Width * Height)) return 0;
            int x = pixelIndex % Width;
            int y = pixelIndex / Width;
            return Get(x, y);
        }

        /// <summary>글로벌 픽셀 인덱스로 설정. 호환용.</summary>
        public void Set(int pixelIndex, uint value)
        {
            if ((uint)pixelIndex >= (uint)(Width * Height)) return;
            int x = pixelIndex % Width;
            int y = pixelIndex / Width;
            Set(x, y, value);
        }

        /// <summary>인덱서 syntax — Get/Set 호출과 동일.</summary>
        public uint this[int pixelIndex]
        {
            get => Get(pixelIndex);
            set => Set(pixelIndex, value);
        }

        /// <summary>타일 (tileX, tileY) 의 dense uint[] 반환. 미할당이면 null.</summary>
        public uint[]? GetTileOrNull(int tileX, int tileY)
        {
            if ((uint)tileX >= (uint)TilesX || (uint)tileY >= (uint)TilesY) return null;
            int tileKey = tileY * TilesX + tileX;
            return _tiles.TryGetValue(tileKey, out var tile) ? tile : null;
        }

        /// <summary>현재 alloc 된 타일을 enumerate. (tileX, tileY, tile) — hot loop 용.</summary>
        public IEnumerable<(int tileX, int tileY, uint[] tile)> EnumerateAllocatedTiles()
        {
            foreach (var kv in _tiles)
            {
                int tileX = kv.Key % TilesX;
                int tileY = kv.Key / TilesX;
                yield return (tileX, tileY, kv.Value);
            }
        }

        /// <summary>BBox 와 겹치는 타일들만 enumerate. BBox-scoped 스캔용.</summary>
        public IEnumerable<(int tileX, int tileY, uint[] tile)> EnumerateTilesInBox(int x0, int y0, int x1, int y1)
        {
            int tx0 = System.Math.Max(0, x0 / TileSize);
            int ty0 = System.Math.Max(0, y0 / TileSize);
            int tx1 = System.Math.Min(TilesX - 1, x1 / TileSize);
            int ty1 = System.Math.Min(TilesY - 1, y1 / TileSize);
            for (int ty = ty0; ty <= ty1; ty++)
            {
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    int tileKey = ty * TilesX + tx;
                    if (_tiles.TryGetValue(tileKey, out var tile))
                        yield return (tx, ty, tile);
                }
            }
        }

        /// <summary>모든 alloc 된 타일을 깊은 복사한 새 SparseTileLayer 반환. CloneLayers 용.</summary>
        public SparseTileLayer Clone()
        {
            var clone = new SparseTileLayer(Width, Height);
            foreach (var kv in _tiles)
            {
                var copy = new uint[TilePixelCount];
                System.Buffer.BlockCopy(kv.Value, 0, copy, 0, TilePixelCount * sizeof(uint));
                clone._tiles[kv.Key] = copy;
            }
            return clone;
        }

        /// <summary>모든 타일 제거. Clear 용.</summary>
        public void ClearAllTiles()
        {
            _tiles.Clear();
            _cachedTileKey = -1;
            _cachedTile = null;
        }
    }
}