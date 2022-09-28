using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using ReponoStorage.Data;

namespace ReponoStorage;

public sealed class ContainerService : Service
{
    [Path("/v1/container/{container_id}/")]
    [return: JsonDataConverter]
    public async Task<ContainerBase?> GetContainerInfoAsync(
        [Var("container_id")] string containerId,
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        if (!location.GetParameter.TryGetValue("password", out string? password))
            password = null;
        if (password == "")
            password = null;

        var container = await Containers.GetContainerAsync(containerId);
        if (container is null)
        {
            response.StatusCode = HttpStateCode.NotFound;
            return null;
        }

        if (container.Encryption is not null)
        {
            if (password is null)
                return container.BaseData;

            var checkKey = Encryption.GetKey(password, container.Encryption.HashSalt.Span);
            if (!checkKey.Span.SequenceEqual(container.Encryption.HashResult.Span))
            {
                response.StatusCode = HttpStateCode.Forbidden;
                return null;
            }
        }
        else if (password is not null)
        {
            response.StatusCode = HttpStateCode.Forbidden;
            return null;
        }

        return container;
    }

    [Path("/v1/container/")]
    [return: JsonDataConverter]
    public async Task<Container?> CreateContainerAsync(
        [Get("token")] string tokenKey,
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        if (!location.GetParameter.TryGetValue("password", out string? password))
            password = null;
        if (password == "")
            password = null;
        
        var token = await Tokens.GetTokenAsync(tokenKey);
        if (token is null
            || token.Expired
            || token.StorageLimit is null 
            || (token.TokenLimit is not null && token.TokenLimit <= 0)
        )
        {
            response.StatusCode = HttpStateCode.Forbidden;
            return null;
        }

        var container = await Containers.GenerateNewContainerAsync();
        container.StorageLimit = token.StorageLimit.Value;
        if (password is not null)
        {
            var encryption = Encryption.Create(password);
            container.Encryption = encryption;
        }
        await Containers.SaveContainerAsync(container);

        token.ChildContainer.Add(container.Id);
        if (token.TokenLimit is not null)
            token.TokenLimit--;
        await Tokens.SaveTokenAsync(token);

        return container;
    }
}