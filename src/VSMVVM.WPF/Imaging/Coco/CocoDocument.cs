using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// COCO instance segmentation JSON 스키마의 축약 POCO.
    /// System.Text.Json 으로 직렬화된다.
    /// </summary>
    public sealed class CocoDocument
    {
        [JsonPropertyName("info")]
        public CocoInfo Info { get; set; } = new();

        [JsonPropertyName("images")]
        public List<CocoImage> Images { get; set; } = new();

        [JsonPropertyName("categories")]
        public List<CocoCategory> Categories { get; set; } = new();

        [JsonPropertyName("annotations")]
        public List<CocoAnnotation> Annotations { get; set; } = new();
    }

    public sealed class CocoInfo
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = "VSMVVM mask export";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";
    }

    public sealed class CocoImage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;
    }

    public sealed class CocoCategory
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>비표준 필드: "#RRGGBB" 형식의 색상 저장.</summary>
        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    public sealed class CocoAnnotation
    {
        [JsonPropertyName("id")]
        public uint Id { get; set; }

        [JsonPropertyName("image_id")]
        public int ImageId { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        /// <summary>
        /// polygon (<see cref="List{T}"/> of List&lt;double&gt;) 또는
        /// compressed RLE (<see cref="CocoCompressedRle"/>). pycocotools 와 동일 스키마.
        /// JSON 토큰이 배열이면 polygon, 객체면 RLE 로 파싱.
        /// </summary>
        [JsonPropertyName("segmentation")]
        [JsonConverter(typeof(SegmentationJsonConverter))]
        public object? Segmentation { get; set; }

        /// <summary>
        /// 비표준 보조 필드 (legacy). 기존 파일 호환 로드 용.
        /// 신규 export 는 <see cref="Segmentation"/> 에 <see cref="CocoCompressedRle"/> 를 쓰며 이 필드는 기록하지 않는다.
        /// </summary>
        [JsonPropertyName("rle")]
        public CocoRle? Rle { get; set; }

        /// <summary>bbox = [x, y, w, h].</summary>
        [JsonPropertyName("bbox")]
        public List<double> Bbox { get; set; } = new();

        [JsonPropertyName("area")]
        public int Area { get; set; }

        [JsonPropertyName("iscrowd")]
        public int IsCrowd { get; set; } = 0;
    }

    public sealed class CocoRle
    {
        /// <summary>[height, width] (pycocotools 와 동일 순서).</summary>
        [JsonPropertyName("size")]
        public List<int> Size { get; set; } = new();

        /// <summary>column-major run lengths.</summary>
        [JsonPropertyName("counts")]
        public List<int> Counts { get; set; } = new();
    }

    /// <summary>
    /// pycocotools 호환 compressed RLE. segmentation 필드가 객체일 때 사용.
    /// { "size": [h, w], "counts": "<ascii-string>" }.
    /// </summary>
    public sealed class CocoCompressedRle
    {
        /// <summary>[height, width].</summary>
        [JsonPropertyName("size")]
        public List<int> Size { get; set; } = new();

        /// <summary>LEB128-variant ASCII 인코딩된 compressed counts.</summary>
        [JsonPropertyName("counts")]
        public string Counts { get; set; } = string.Empty;
    }
}
