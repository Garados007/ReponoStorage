using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public sealed class FileMeta
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }

    [JsonPropertyName("size")]
    public ulong Size { get; set; }

    [JsonPropertyName("mime")]
    public string Mime { get; set; } = "";
}