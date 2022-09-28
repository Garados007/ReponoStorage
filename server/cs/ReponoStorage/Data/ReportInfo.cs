using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public sealed class ReportInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("container_id")]
    public string ContainerId { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("report")]
    public Report Report { get; set; } = new();
}