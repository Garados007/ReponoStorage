using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public sealed class Token
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("child_tokens")]
    public List<string> ChildTokens { get; set; } = new();

    [JsonPropertyName("child_container")]
    public List<string> ChildContainer { get; set; } = new();

    [JsonPropertyName("storage_limit")]
    public ulong? StorageLimit { get; set; }

    [JsonPropertyName("token_limit")]
    public ulong? TokenLimit { get; set; }

    [JsonPropertyName("expired")]
    public bool Expired { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("used")]
    public DateTime Used { get; set; }

    [JsonPropertyName("hint")]
    public string? Hint { get; set; }
}