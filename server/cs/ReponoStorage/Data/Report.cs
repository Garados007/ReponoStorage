using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public sealed class Report
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}
