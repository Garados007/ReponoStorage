using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public class MemoryConverter : JsonConverter<Memory<byte>>
{
    public override Memory<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetBytesFromBase64();
    }

    public override void Write(Utf8JsonWriter writer, Memory<byte> value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.Span);
    }
}