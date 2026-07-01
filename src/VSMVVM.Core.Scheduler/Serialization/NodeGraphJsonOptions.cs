using System.Text.Json;
using System.Text.Json.Serialization;

namespace VSMVVM.Core.Scheduler.Serialization
{
    /// <summary>NodeGraphSerializer가 사용하는 공유 JsonSerializerOptions.</summary>
    public static class NodeGraphJsonOptions
    {
        public static readonly JsonSerializerOptions Default = CreateDefault();

        public static JsonSerializerOptions CreateDefault()
        {
            var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            // PinKind 등 enum을 문자열로 직렬화하면 사람이 읽기 쉽고 마이그레이션도 안전.
            opts.Converters.Add(new JsonStringEnumConverter());
            return opts;
        }
    }
}
