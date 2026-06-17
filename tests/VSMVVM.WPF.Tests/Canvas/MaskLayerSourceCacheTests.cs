#nullable enable
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using VSMVVM.WPF.Controls;
using Xunit;

namespace VSMVVM.WPF.Tests.Canvas
{
    // MaskLayer 의 SourceImage 픽셀 캐시 (_sourcePixels) 무효화 회귀 테스트.
    //
    // 배경: 라벨링 화면 우측 PixelInfo 패널의 RGB 가 데이터셋(그룹) 전환 후 stale 값을 보여주는 버그.
    // 원인: Cleanup() 이 _sourcePixels/SourceImage 만 null 로 두고 _width/_height 는 그대로 두어,
    //       다음 그룹의 첫 LoadImage 가 SourceImage 를 먼저 set 하면 이전 그룹 차원으로 _sourcePixels 가
    //       잘못된 크기로 빌드되고, 후속 MaskWidth/Height 가 같은 값이면 DP callback 미발화로 stale 영구화.
    // 패치: Cleanup() 에서 _width/_height + MaskWidth/Height DP 까지 0 으로 리셋.
    public class MaskLayerSourceCacheTests
    {
        // 단색으로 채워진 Bgra32 BitmapSource 생성. CopyPixels 비교에 사용.
        private static BitmapSource CreateSolidColorBitmap(int w, int h, byte b, byte g, byte r)
        {
            var pixels = new byte[w * h * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = 255;
            }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
            bmp.Freeze();
            return bmp;
        }

        // ── T1: Cleanup 이 차원을 0 으로 리셋한다 ──────────────────────────
        // 핵심 픽스 자체 (수정 A). Cleanup 후 MaskWidth/MaskHeight 가 0 이어야 다음 그룹의 SourceImage-first
        // 설정이 RebuildSourceCaches 의 early return (`_width==0`) 으로 안전하게 가드된다.
        [WpfFact]
        public void T1_Cleanup_Resets_Dimensions_To_Zero()
        {
            var layer = new MaskLayer();
            // DP 경로 (MaskWidth/Height) 로 차원을 설정해야 후속 Cleanup 의 DP 리셋 효과가 관찰 가능.
            // Resize(...) 는 내부 _width/_height 만 갱신하고 DP 는 동기화하지 않음.
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;
            layer.SourceImage = CreateSolidColorBitmap(200, 100, 50, 100, 150);

            layer.MaskWidth.Should().Be(200);
            layer.MaskHeight.Should().Be(100);
            layer.GetSourcePixels().Should().NotBeNull();

            layer.Cleanup();

            layer.MaskWidth.Should().Be(0, "Cleanup 후 차원 DP 가 0 이어야 다음 SourceImage 가 stale 차원으로 캐시 빌드되지 않음");
            layer.MaskHeight.Should().Be(0);
            layer.GetSourcePixels().Should().BeNull();
            layer.SourceImage.Should().BeNull();
        }

        // ── T2: Cleanup 후 SourceImage 만 먼저 set 되어도 stale 캐시가 만들어지지 않는다 ──────
        // 의심 1 의 정확한 시나리오 재현: 그룹 전환 → Cleanup → 첫 LoadImage 가 BackgroundImageSource
        // (=SourceImage) 를 차원 DP 보다 먼저 세팅. 픽스 전에는 이전 그룹 차원으로 _sourcePixels 가
        // 잘못 채워졌음. 픽스 후에는 _width==0 으로 early return → null 유지.
        [WpfFact]
        public void T2_SourceImage_Before_Dimensions_Does_Not_Build_Stale_Cache()
        {
            var layer = new MaskLayer();
            // 이전 그룹: 큰 이미지로 _width/_height 가 일단 채워졌다고 가정. DP 경로로 설정.
            layer.MaskWidth = 400;
            layer.MaskHeight = 300;
            layer.SourceImage = CreateSolidColorBitmap(400, 300, 10, 20, 30);
            layer.GetSourcePixels().Should().NotBeNull();

            // 그룹 전환 — DeepInsight2 의 OnSelectedGroupChanged 동작.
            layer.Cleanup();

            // 새 그룹의 첫 LoadImage — 픽스 적용 전의 호출 순서 (SourceImage 먼저, 차원 나중)를
            // 의도적으로 흉내내 회귀 시나리오를 재현. 픽스 적용 후라면 _width==0 이라 early return.
            var newImage = CreateSolidColorBitmap(200, 100, 99, 88, 77);
            layer.SourceImage = newImage;

            // RebuildSourceCaches 가 early return 으로 _sourcePixels 를 null 로 유지해야 한다.
            // (실제 패널은 이후 차원 DP set 으로 정상화되지만, 이 시점에 stale 빌드가 없어야 함.)
            layer.GetSourcePixels().Should().BeNull(
                "차원이 0 인 상태에서 SourceImage 가 set 되어도 stale 캐시를 만들면 안 됨");
            layer.GetSourcePixelRgb(10, 10).Should().BeNull();
        }

        // ── T3: 그룹 전환 시나리오 — 두 차원이 다른 이미지를 순차 로드 후 픽셀이 새 이미지와 일치 ──
        // End-to-end 시나리오. Cleanup → 새 이미지의 차원 DP 와 SourceImage 모두 적용 → GetSourcePixelRgb
        // 가 새 이미지의 실제 색상을 반환해야 한다. 이전 그룹의 흔적이 남으면 안 됨.
        [WpfFact]
        public void T3_GroupSwitch_Then_New_Image_Returns_New_Image_Pixel()
        {
            var layer = new MaskLayer();

            // 그룹 A: 400×300 빨간색 이미지. DP 경로로 production 호출 흐름과 동일하게.
            layer.MaskWidth = 400;
            layer.MaskHeight = 300;
            layer.SourceImage = CreateSolidColorBitmap(400, 300, 0, 0, 255);
            var redPx = layer.GetSourcePixelRgb(10, 10);
            redPx.HasValue.Should().BeTrue();
            redPx!.Value.R.Should().Be(255);

            // 그룹 전환.
            layer.Cleanup();

            // 그룹 B: 200×100 파란색 이미지. LoadImage 픽스 (수정 C) 의 순서: 차원 먼저, SourceImage 나중.
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;
            layer.SourceImage = CreateSolidColorBitmap(200, 100, 255, 0, 0);

            var bluePx = layer.GetSourcePixelRgb(10, 10);
            bluePx.HasValue.Should().BeTrue();
            bluePx!.Value.B.Should().Be(255, "새 이미지의 파란색 픽셀이 반환되어야 함 (stale 빨간색 아님)");
            bluePx.Value.R.Should().Be(0);
            bluePx.Value.G.Should().Be(0);
        }

        // ── T4: 동일 차원 그룹 간 전환 — DP callback 미발화 케이스 ─────────────────────
        // 가장 강력한 회귀 케이스. 두 그룹의 이미지가 같은 픽셀 차원이라 MaskWidth/Height DP 가 같은 값으로
        // set 되면 WPF DP change callback 이 발화하지 않음 → Resize/RebuildSourceCaches 가 호출되지 않음.
        // 픽스 전: Cleanup 이 차원을 그대로 두므로, SourceImage 가 먼저 set 될 때 만들어진 stale 캐시가
        //          MaskWidth/Height 재설정으로도 갱신되지 않아 영구 stale.
        // 픽스 후: Cleanup 이 MaskWidth/Height 를 0 으로 떨어뜨려, 새 그룹의 차원 set 이 반드시 0→N 변화로
        //          DP callback 을 발화시켜 Resize → RebuildSourceCaches 정상 호출.
        [WpfFact]
        public void T4_GroupSwitch_SameDimensions_DP_Callback_Fires_And_Rebuilds()
        {
            var layer = new MaskLayer();

            // 그룹 A: 200×100 녹색.
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;
            layer.SourceImage = CreateSolidColorBitmap(200, 100, 0, 255, 0);
            var greenPx = layer.GetSourcePixelRgb(50, 50);
            greenPx!.Value.G.Should().Be(255);

            // 그룹 전환. Cleanup 후 MaskWidth/Height 가 0 으로 떨어져야 한다 (수정 A).
            layer.Cleanup();
            layer.MaskWidth.Should().Be(0);
            layer.MaskHeight.Should().Be(0);

            // 그룹 B: 동일 200×100 차원의 자홍색. 0→200 변화이므로 DP callback 발화 보장.
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;
            layer.SourceImage = CreateSolidColorBitmap(200, 100, 255, 0, 255);

            var magentaPx = layer.GetSourcePixelRgb(50, 50);
            magentaPx.HasValue.Should().BeTrue();
            magentaPx!.Value.R.Should().Be(255);
            magentaPx.Value.B.Should().Be(255);
            magentaPx.Value.G.Should().Be(0, "이전 그룹의 녹색이 stale 로 남으면 안 됨");
        }

        // ── T5: Cleanup() 후에도 SourceImage binding 이 살아있어야 한다 ───────────────────────
        // 회귀: Cleanup() 이 `SourceImage = null` 을 local value 로 set 하면 WPF DP value precedence
        // 규칙에 따라 호출자가 걸어둔 OneWay binding 이 끊긴다. 이후 source 가 새 이미지로 갱신되어도
        // MaskLayer 에 도달하지 못해 _sourcePixels=null 영구화 → GetSourcePixelRgb 항상 null →
        // PixelInfo RGB 가 그룹 전환 후 안 보이는 버그. SetCurrentValue 로 picture 가 흘러야 한다.
        [WpfFact]
        public void T5_Cleanup_PreservesSourceImageBinding()
        {
            var layer = new MaskLayer();
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;

            // OneWay binding 으로 SourceImage 연결 (DeepInsight2 의 CanvasPanel.xaml 패턴과 동일).
            var holder = new BindingHolder();
            var binding = new System.Windows.Data.Binding(nameof(BindingHolder.Source))
            {
                Source = holder,
                Mode = System.Windows.Data.BindingMode.OneWay,
            };
            System.Windows.Data.BindingOperations.SetBinding(layer, MaskLayer.SourceImageProperty, binding);

            // 그룹 A 시뮬: source 에 첫 이미지 push. CreateSolidColorBitmap 인자는 (w, h, B, G, R) 순.
            holder.Source = CreateSolidColorBitmap(200, 100, 255, 0, 0); // 파란색.
            layer.SourceImage.Should().NotBeNull("초기 binding 흐름 검증 — source push 가 MaskLayer 까지 도달");

            // 그룹 전환 시뮬: Cleanup 호출 (실제 DeepInsight2 의 OnSelectedGroupChanged 흐름).
            layer.Cleanup();

            // 그룹 B 시뮬: 새 source push (빨간색). 이게 MaskLayer.SourceImage 까지 도달해야 함.
            // Cleanup 이 binding 을 끊었다면 layer.SourceImage 는 null 로 남아 fail.
            layer.MaskWidth = 200;
            layer.MaskHeight = 100;
            var newBitmap = CreateSolidColorBitmap(200, 100, 0, 0, 255); // 빨간색.
            holder.Source = newBitmap;

            layer.SourceImage.Should().NotBeNull(
                "Cleanup 후에도 binding 이 살아있어 새 source 가 MaskLayer 까지 흘러야 함 — " +
                "Cleanup 의 `SourceImage = null` 이 local value 로 set 되면 binding 이 끊겨 fail.");
            ReferenceEquals(layer.SourceImage, newBitmap).Should().BeTrue(
                "흐른 source 가 정확히 holder 가 push 한 새 비트맵이어야 함");

            // 그리고 그 source 로 _sourcePixels 가 정상 빌드되어 GetSourcePixelRgb 가 의도된 색을 반환해야 함.
            var redPx = layer.GetSourcePixelRgb(50, 50);
            redPx.HasValue.Should().BeTrue("Cleanup → 새 source binding → RebuildSourceCaches 가 정상 실행되어야 함");
            redPx!.Value.R.Should().Be(255);
        }

        // T5 의 binding source 시뮬레이션용 헬퍼 — INotifyPropertyChanged 로 source push 가능.
        private sealed class BindingHolder : System.ComponentModel.INotifyPropertyChanged
        {
            private System.Windows.Media.ImageSource? _source;
            public System.Windows.Media.ImageSource? Source
            {
                get => _source;
                set
                {
                    if (ReferenceEquals(_source, value)) return;
                    _source = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Source)));
                }
            }
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
