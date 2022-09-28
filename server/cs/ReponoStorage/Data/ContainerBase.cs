using System.Text.Json.Serialization;

namespace ReponoStorage.Data;

public abstract class ContainerBase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("encrypted")]
    public bool Encrypted => Encryption is not null;

    [JsonIgnore]
    public Encryption? Encryption { get; set;}
}

public sealed class ContainerBaseData : ContainerBase
{

}
