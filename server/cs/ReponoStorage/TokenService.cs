using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using ReponoStorage.Data;

namespace ReponoStorage;

public class TokenService : Service
{
    [Path("/v1/token/{token_id}/")]
    [return: JsonDataConverter]
    public async Task<Token?> GetTokenInfoAsync(
        [Var("token_id")] string tokenId,
        HttpResponseHeader response
    )
    {
        var token = await Tokens.GetTokenAsync(tokenId);
        if (token is null)
        {
            response.StatusCode = HttpStateCode.NotFound;
            return null;
        }
        return token;
    }

    [Path("/v1/token/{token_id}/new")]
    [return: JsonDataConverter]
    public async Task<Token?> GetNewTokenAsync(
        [Var("token_id")] string tokenId,
        [Get("token_limit")] ulong tokenLimit,
        [Get("storage_limit")] ulong storageLimit,
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        if (!location.GetParameter.TryGetValue("hint", out string? hint))
            hint = null;
        if (hint == "")
            hint = null;
        var parent = await Tokens.GetTokenAsync(tokenId);
        if (parent is null)
        {
            response.StatusCode = HttpStateCode.NotFound;
            return null;
        }

        if ((parent.TokenLimit is not null && 
                tokenLimit + 1 > parent.TokenLimit.Value
            )
            || (parent.StorageLimit is not null && storageLimit > parent.StorageLimit.Value)
            || parent.Expired
            || tokenLimit + 1 == 0
        )
        {
            response.StatusCode = HttpStateCode.InsufficientStorage;
            return null;
        }

        var child = await Tokens.GenerateNewTokenAsync();
        child.Parent = parent.Id;
        child.TokenLimit = tokenLimit;
        child.StorageLimit = storageLimit;
        child.Hint = hint;
        await Tokens.SaveTokenAsync(child);

        parent.ChildTokens.Add(child.Id);
        if (parent.TokenLimit is not null)
        {
            parent.TokenLimit -= 1 + tokenLimit;
        }
        parent.Used = child.Used;
        await Tokens.SaveTokenAsync(parent);

        return child;
    }
}