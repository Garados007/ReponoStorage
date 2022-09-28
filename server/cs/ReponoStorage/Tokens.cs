using System.Text.Json;
using ReponoStorage.Data;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace ReponoStorage;

public static class Tokens
{
    public const int TokenKeyLength = 30;

    private static string GetTokenPath(string id)
    {
        return Path.Combine(
            Environment.CurrentDirectory,
            "token",
            $"{id}.json"
        );
    }

    public static async Task<Token> GenerateNewTokenAsync()
    {
        string id;
        do
        {
            id = Tools.GetRandomKey(TokenKeyLength);
        }
        while (File.Exists(GetTokenPath(id)));
        var token = new Token
        {
            Id = id,
            Created = DateTime.UtcNow,
            Used = DateTime.UtcNow,
        };
        cachedTokens.AddOrUpdate(id,
            _ => new WeakReference<Token>(token),
            (_, _) => new WeakReference<Token>(token)
        );
        await SaveTokenAsync(token);
        return token;
    }

    private static async Task<Token> GenerateRootTokenAsync(string infoPath)
    {
        var token = await GenerateNewTokenAsync();
        await File.WriteAllTextAsync(infoPath, token.Id);
        return token;
    }

    public static async Task<Token> GetRootTokenAsync()
    {
        var infoPath = Path.Combine(
            Environment.CurrentDirectory,
            "root-token.txt"
        );
        if (!File.Exists(infoPath))
            return await GenerateRootTokenAsync(infoPath);
        var id = await File.ReadAllTextAsync(infoPath);
        var token = await GetTokenAsync(id);
        if (token is null || token.Expired)
            return await GenerateRootTokenAsync(infoPath);
        return token;
    }

    private static readonly ConcurrentDictionary<string, WeakReference<Token>> cachedTokens = new();

    public static async Task<Token?> GetTokenAsync(string id)
    {
        if (cachedTokens.TryGetValue(id, out WeakReference<Token>? weakToken)
            && weakToken.TryGetTarget(out Token? token)
        )
            return token;

        if (!Tools.ValidKey(id))
            return null;
        var path = GetTokenPath(id);
        if (!File.Exists(path))
            return null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            token = await JsonSerializer.DeserializeAsync<Token>(fs);
            if (token is null)
                return null;
            cachedTokens.AddOrUpdate(id,
                _ => new WeakReference<Token>(token),
                (_, weakToken) => 
                {
                    weakToken.SetTarget(token);
                    return weakToken;
                }
            );
            return token;
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            return await GetTokenAsync(id);
        }
    }

    public static async Task SaveTokenAsync(Token token)
    {
        var path = GetTokenPath(token.Id);
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);
        try
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, token, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            await fs.FlushAsync();
            fs.SetLength(fs.Position);
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            await SaveTokenAsync(token);
        }
    }
}