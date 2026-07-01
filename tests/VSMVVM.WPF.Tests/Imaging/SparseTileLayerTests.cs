using System.Linq;
using FluentAssertions;
using VSMVVM.WPF.Imaging;
using Xunit;

namespace VSMVVM.WPF.Tests.Imaging
{
    public class SparseTileLayerTests
    {
        private static SparseTileLayer Create(int w = 256, int h = 256) => new SparseTileLayer(w, h);

        // ── 기본 크기 정보 ────────────────────────────────────────────

        [Fact]
        public void Constructor_ShouldSetWidthAndHeight()
        {
            // Arrange / Act
            var layer = Create(300, 200);

            // Assert
            layer.Width.Should().Be(300);
            layer.Height.Should().Be(200);
            layer.Length.Should().Be(300 * 200);
        }

        [Fact]
        public void Constructor_ShouldComputeTileCounts()
        {
            // Arrange / Act
            var layer = Create(256, 256); // 정확히 2×2 타일

            // Assert
            layer.TilesX.Should().Be(2); // 256/128 = 2
            layer.TilesY.Should().Be(2);
        }

        [Fact]
        public void Constructor_WhenSizeNotMultipleOfTile_ShouldRoundUpTileCount()
        {
            // Arrange / Act
            var layer = Create(129, 1); // 1 타일 + 1픽셀 → 타일 2개

            // Assert
            layer.TilesX.Should().Be(2);
        }

        // ── Get / Set ─────────────────────────────────────────────────

        [Fact]
        public void Get_WhenPixelNotSet_ShouldReturnZero()
        {
            // Arrange
            var layer = Create();

            // Act
            var value = layer.Get(10, 10);

            // Assert
            value.Should().Be(0u);
        }

        [Fact]
        public void Set_ThenGet_ShouldReturnSameValue()
        {
            // Arrange
            var layer = Create();

            // Act
            layer.Set(50, 60, 42u);
            var result = layer.Get(50, 60);

            // Assert
            result.Should().Be(42u);
        }

        [Fact]
        public void Set_WhenValueIsZeroAndTileNotAllocated_ShouldNotAllocateTile()
        {
            // Arrange
            var layer = Create();

            // Act
            layer.Set(10, 10, 0u);

            // Assert
            layer.AllocatedTileCount.Should().Be(0, "값이 0 이고 타일이 없으면 alloc 하지 않아야 한다");
        }

        [Fact]
        public void Set_WhenValueIsNonZero_ShouldAllocateTile()
        {
            // Arrange
            var layer = Create();

            // Act
            layer.Set(10, 10, 1u);

            // Assert
            layer.AllocatedTileCount.Should().Be(1);
        }

        [Fact]
        public void Set_WhenValueZeroAfterNonZero_ShouldStorZeroButKeepTile()
        {
            // Arrange
            var layer = Create();
            layer.Set(10, 10, 5u);

            // Act — 이미 alloc 된 타일에 0 저장
            layer.Set(10, 10, 0u);

            // Assert
            layer.Get(10, 10).Should().Be(0u, "0 으로 덮어쓴 후 읽으면 0");
            layer.AllocatedTileCount.Should().Be(1, "타일 자체는 해제되지 않음");
        }

        [Fact]
        public void Set_MultipleTilesBounds_ShouldTrackEachTile()
        {
            // Arrange
            var layer = Create(256, 256);

            // Act — 4개의 서로 다른 타일에 값 설정
            layer.Set(0, 0, 1u);     // tile (0,0)
            layer.Set(128, 0, 2u);   // tile (1,0)
            layer.Set(0, 128, 3u);   // tile (0,1)
            layer.Set(128, 128, 4u); // tile (1,1)

            // Assert
            layer.AllocatedTileCount.Should().Be(4);
            layer.Get(0, 0).Should().Be(1u);
            layer.Get(128, 0).Should().Be(2u);
            layer.Get(0, 128).Should().Be(3u);
            layer.Get(128, 128).Should().Be(4u);
        }

        // ── 범위 밖 접근 ──────────────────────────────────────────────

        [Fact]
        public void Get_WhenOutOfBoundsX_ShouldReturnZero()
        {
            // Arrange
            var layer = Create(100, 100);

            // Act / Assert
            layer.Get(100, 50).Should().Be(0u);
            layer.Get(-1, 50).Should().Be(0u);
        }

        [Fact]
        public void Get_WhenOutOfBoundsY_ShouldReturnZero()
        {
            // Arrange
            var layer = Create(100, 100);

            // Act / Assert
            layer.Get(50, 100).Should().Be(0u);
            layer.Get(50, -1).Should().Be(0u);
        }

        [Fact]
        public void Set_WhenOutOfBounds_ShouldNotThrowOrAllocate()
        {
            // Arrange
            var layer = Create(100, 100);

            // Act
            layer.Set(100, 50, 99u);
            layer.Set(50, 100, 99u);
            layer.Set(-1, 50, 99u);

            // Assert
            layer.AllocatedTileCount.Should().Be(0, "범위 밖 Set 은 아무것도 하지 않아야 한다");
        }

        // ── 인덱서 및 픽셀 인덱스 ─────────────────────────────────────

        [Fact]
        public void PixelIndexGet_ShouldMatchXYGet()
        {
            // Arrange
            var layer = Create(100, 100);
            layer.Set(5, 3, 77u);

            // Act — pixelIndex = y * Width + x = 3 * 100 + 5 = 305
            var byIndex = layer.Get(305);
            var byXY = layer.Get(5, 3);

            // Assert
            byIndex.Should().Be(byXY);
        }

        [Fact]
        public void Indexer_GetAndSet_ShouldMatchDirectMethods()
        {
            // Arrange
            var layer = Create(100, 100);

            // Act
            layer[250] = 123u; // x = 250%100 = 50, y = 250/100 = 2
            var result = layer[250];

            // Assert
            result.Should().Be(123u);
            layer.Get(50, 2).Should().Be(123u);
        }

        // ── Clone ─────────────────────────────────────────────────────

        [Fact]
        public void Clone_ShouldCreateIndependentCopyWithSameValues()
        {
            // Arrange
            var layer = Create(256, 256);
            layer.Set(10, 10, 42u);
            layer.Set(130, 130, 99u);

            // Act
            var clone = layer.Clone();

            // Assert
            clone.Get(10, 10).Should().Be(42u);
            clone.Get(130, 130).Should().Be(99u);
            clone.Width.Should().Be(layer.Width);
            clone.Height.Should().Be(layer.Height);
        }

        [Fact]
        public void Clone_WhenOriginalModifiedAfterClone_ShouldNotAffectClone()
        {
            // Arrange
            var layer = Create(256, 256);
            layer.Set(10, 10, 42u);
            var clone = layer.Clone();

            // Act — 원본 수정
            layer.Set(10, 10, 999u);

            // Assert — 클론은 영향 없음
            clone.Get(10, 10).Should().Be(42u, "클론은 원본과 독립적이어야 한다");
        }

        // ── ClearAllTiles ─────────────────────────────────────────────

        [Fact]
        public void ClearAllTiles_ShouldRemoveAllData()
        {
            // Arrange
            var layer = Create(256, 256);
            layer.Set(10, 10, 1u);
            layer.Set(130, 130, 2u);

            // Act
            layer.ClearAllTiles();

            // Assert
            layer.AllocatedTileCount.Should().Be(0);
            layer.Get(10, 10).Should().Be(0u);
            layer.Get(130, 130).Should().Be(0u);
        }

        [Fact]
        public void ClearAllTiles_ThenSetAgain_ShouldWork()
        {
            // Arrange
            var layer = Create();
            layer.Set(10, 10, 1u);
            layer.ClearAllTiles();

            // Act
            layer.Set(10, 10, 5u);

            // Assert
            layer.Get(10, 10).Should().Be(5u);
        }

        // ── EnumerateAllocatedTiles ───────────────────────────────────

        [Fact]
        public void EnumerateAllocatedTiles_WhenNoTiles_ShouldReturnEmpty()
        {
            // Arrange
            var layer = Create();

            // Act / Assert
            layer.EnumerateAllocatedTiles().Should().BeEmpty();
        }

        [Fact]
        public void EnumerateAllocatedTiles_ShouldReturnAllAllocatedTiles()
        {
            // Arrange
            var layer = Create(256, 256);
            layer.Set(0, 0, 1u);    // tile (0,0)
            layer.Set(128, 0, 2u);  // tile (1,0)

            // Act
            var tiles = layer.EnumerateAllocatedTiles().ToList();

            // Assert
            tiles.Should().HaveCount(2);
        }

        // ── EnumerateTilesInBox ───────────────────────────────────────

        [Fact]
        public void EnumerateTilesInBox_WhenBoxContainsTiles_ShouldReturnMatchingTiles()
        {
            // Arrange
            var layer = Create(256, 256);
            layer.Set(10, 10, 1u);    // tile (0,0)
            layer.Set(130, 130, 2u);  // tile (1,1)

            // Act — BBox [0,0,127,127]: tile (0,0)만 포함
            var tiles = layer.EnumerateTilesInBox(0, 0, 127, 127).ToList();

            // Assert
            tiles.Should().HaveCount(1);
            tiles[0].tileX.Should().Be(0);
            tiles[0].tileY.Should().Be(0);
        }

        // ── GetTileOrNull ─────────────────────────────────────────────

        [Fact]
        public void GetTileOrNull_WhenTileNotAllocated_ShouldReturnNull()
        {
            // Arrange
            var layer = Create();

            // Act / Assert
            layer.GetTileOrNull(0, 0).Should().BeNull();
        }

        [Fact]
        public void GetTileOrNull_WhenTileAllocated_ShouldReturnTileArray()
        {
            // Arrange
            var layer = Create();
            layer.Set(10, 10, 7u);

            // Act
            var tile = layer.GetTileOrNull(0, 0);

            // Assert
            tile.Should().NotBeNull();
            tile!.Length.Should().Be(SparseTileLayer.TilePixelCount);
        }

        // ── 캐시 최적화 경로 (연속 같은 타일 접근) ────────────────────

        [Fact]
        public void Get_WhenAccessingSameTileRepeatedly_ShouldReturnCorrectValues()
        {
            // Arrange — 캐시 히트 경로를 타도록 같은 타일(0,0) 안에서 연속 접근
            var layer = Create(256, 256);
            layer.Set(0, 0, 10u);
            layer.Set(1, 0, 20u);
            layer.Set(2, 0, 30u);

            // Act — 같은 타일 내 연속 읽기 (cache hit 경로)
            var v0 = layer.Get(0, 0);
            var v1 = layer.Get(1, 0);
            var v2 = layer.Get(2, 0);

            // Assert
            v0.Should().Be(10u);
            v1.Should().Be(20u);
            v2.Should().Be(30u);
        }
    }
}
