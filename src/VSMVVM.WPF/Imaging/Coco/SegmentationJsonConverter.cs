using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace VSMVVM.WPF.Imaging.Coco
{
    /// <summary>
    /// COCO annotation.segmentation 필드의 polymorphic 직렬화기.
    /// - JSON 배열 → <see cref="List{T}"/> of List&lt;double&gt; (polygon)
    /// - JSON 객체 → <see cref="CocoCompressedRle"/>
    /// - null → null
    /// </summary>
    public sealed class SegmentationJsonConverter : JsonConverter<object?>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.StartArray:
                    return JsonSerializer.Deserialize<List<List<double>>>(ref reader, options);
                case JsonTokenType.StartObject:
                    return JsonSerializer.Deserialize<CocoCompressedRle>(ref reader, options);
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} for segmentation.");
            }
        }

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case CocoCompressedRle rle:
                    JsonSerializer.Serialize(writer, rle, options);
                    break;
                case List<List<double>> polys:
                    JsonSerializer.Serialize(writer, polys, options);
                    break;
                default:
                    throw new JsonException($"Unsupported segmentation runtime type: {value.GetType()}");
            }
        }
    }
}
