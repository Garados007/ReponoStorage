using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public sealed class Container : ContainerBase
{
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }

    [JsonPropertyName("storage_limit")]
    public ulong StorageLimit { get; set; }

    [JsonPropertyName("files")]
    public List<FileMeta> Files { get; set; } = new();

    [JsonIgnore]
    public ContainerBaseData BaseData
    => new ContainerBaseData { Id = Id, Encryption = Encryption };
}