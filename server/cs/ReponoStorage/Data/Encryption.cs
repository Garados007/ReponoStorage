using System.Text.Json.Serialization;
using System.Text;
using System.Security.Cryptography;

namespace ReponoStorage.Data;

public class Encryption
{
    public Memory<byte> HashSalt { get; set; }

    public Memory<byte> HashResult { get; set; }

    public Memory<byte> EncryptionSalt { get; set; }

    public Dictionary<string, Memory<byte>> FileIV { get; set; } = new();

    public static Memory<byte> GetKey(string password, ReadOnlySpan<byte> salt)
    {
        using var pcb = new System.Security.Cryptography.Rfc2898DeriveBytes(
            password,
            salt.ToArray(),
            10_000,
            HashAlgorithmName.SHA512
        );
        return pcb.GetBytes(32);
    }

    public static Encryption Create(string password)
    {
        var encryption = new Encryption
        {
            HashSalt = new byte[32],
            EncryptionSalt = new byte[32]
        };
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(encryption.HashSalt.Span);
        rng.GetBytes(encryption.EncryptionSalt.Span);
        encryption.HashResult = GetKey(password, encryption.HashSalt.Span);
        return encryption;
    }
}