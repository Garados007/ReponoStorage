using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using ReponoStorage.Data;
using System.Security.Cryptography;

namespace ReponoStorage;

public sealed class FileService : Service
{

    [Ignore]
    public static Task<Container?> GetContainer(
        string containerId, out string? password, 
        HttpLocation location, HttpResponseHeader response
    )
    {
        if (!location.GetParameter.TryGetValue("password", out password))
            password = null;
        if (password == "")
            password = null;
        var _password = password;
        
        return Task.Run(async () => 
        {
            var container = await Containers.GetContainerAsync(containerId);
            if (container is null)
            {
                response.StatusCode = HttpStateCode.NotFound;
                return null;
            }

            if (container.Encryption is not null)
            {
                if (_password is null)
                    return null;

                var checkKey = Encryption.GetKey(_password, container.Encryption.HashSalt.Span);
                if (!checkKey.Span.SequenceEqual(container.Encryption.HashResult.Span))
                {
                    response.StatusCode = HttpStateCode.Forbidden;
                    return null;
                }
            }
            else if (_password is not null)
            {
                response.StatusCode = HttpStateCode.Forbidden;
                return null;
            }

            return container;
        });
    }

    [Method(HttpProtocolMethod.Get)]
    [Path("/v1/file/{container_id}/")]
    public async Task<HttpDataSource?> GetFileAsync(
        [Var("container_id")] string containerId,
        [Get] string path,
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        var container = await GetContainer(containerId, out string? password, location, response);
        if (container is null)
            return null;

        var file = container.Files
            .Where(x => x.Path == path)
            .FirstOrDefault();

        if (file is null)
        {
            response.StatusCode = HttpStateCode.NotFound;
            return null;
        }

        var targetFile = Containers.GetContainerFilePath(container, file);
        if (!File.Exists(targetFile))
            return null;
        return await LoadData(container, password, file, targetFile);
    }

    [Ignore]
    private static async Task<HttpDataSource?> LoadData(
        Container container, string? password, FileMeta file, string targetFile
    )
    {
        try
        {
            var fileStream = new FileStream(
                targetFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None
            );
            if (container.Encryption is null)
                return new HttpStreamDataSource(fileStream)
                {
                    MimeType = file.Mime,
                };
            if (!container.Encryption.FileIV.TryGetValue(file.Id, out Memory<byte> iv))
            {
                fileStream.Dispose();
                return null;
            }
            var key = Encryption.GetKey(password!, container.Encryption.EncryptionSalt.Span);

            using var aes = Aes.Create();
            aes.Key = key.ToArray();
            aes.IV = iv.ToArray();

            var cryptoStream = new CryptoStream(
                fileStream,
                aes.CreateDecryptor(key.ToArray(), iv.ToArray()),
                CryptoStreamMode.Read
            );
            return new MaxLib.WebServer.Chunked.HttpChunkedStream(cryptoStream)
            {
                MimeType = file.Mime,
            };
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(10);
            return await LoadData(container, password, file, targetFile);
        }
    }

    [Ignore]
    private string FixMimeType(string path, string mime)
    {
        if (mime.Contains('*') || mime == MimeType.ApplicationOctetStream)
        {
            var ext = Path.GetExtension(path);
            if (ext is null || ext == "")
                return MimeType.ApplicationOctetStream;
            return MimeType.GetMimeTypeForExtension(ext)
                ?? MimeType.ApplicationOctetStream;
        }
        else return mime;
    }

    [Method(HttpProtocolMethod.Put)]
    [Path("/v1/file/{container_id}/")]
    public async Task PutFileAsync(
        [Var("container_id")] string containerId,
        [Get] string path,
        HttpLocation location,
        HttpRequestHeader request,
        HttpPost post,
        HttpResponseHeader response
    )
    {
        var container = await GetContainer(containerId, out string? password, location, response);
        if (container is null)
            return;

        if (post.DataAsync is null
            || await post.DataAsync is not MaxLib.WebServer.Post.UnknownPostData postData
        )
        {
            response.StatusCode = HttpStateCode.UnsupportedMediaType;
            return;
        }

        if (!request.HeaderParameter.TryGetValue("Content-Length", out string? contentLengthString)
            || !ulong.TryParse(contentLengthString, out ulong contentLength)
        )
        {
            response.StatusCode = HttpStateCode.LengthRequired;
            return;
        }

        var file = container.Files
            .Where(x => x.Path == path)
            .FirstOrDefault();

        if (file is null && container.Files.Count >= 1024)
        {
            response.StatusCode = HttpStateCode.InsufficientStorage;
            return;
        }

        var totalUsage = container.Files
            .Where(x => x.Path != path)
            .Select(x => x.Size < 1024 ? 1024 : x.Size)
            .Aggregate(0UL, (a, b) => a + b);
        totalUsage += contentLength;

        if (totalUsage > container.StorageLimit)
        {
            response.StatusCode = HttpStateCode.InsufficientStorage;
            return;
        }

        if (file is null)
        {
            string id;
            do { id = Tools.GetRandomKey(3); }
            while (container.Files.Any(x => x.Id == id));
            file = new FileMeta
            {
                Id = id,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Path = path,
                Mime = FixMimeType(path, postData.MimeType),
                Size = contentLength
            };
            container.Files.Add(file);
        }
        else
        {
            file.Modified = DateTime.UtcNow;
            file.Mime = FixMimeType(path, postData.MimeType);
            file.Size = contentLength;
        }
        await Containers.SaveContainerAsync(container);

        using var sourceStream = new PartialStream(postData.Data, (long)contentLength); // limit the size to its reported value
        var targetFile = Containers.GetContainerFilePath(container, file);
        var dir = Path.GetDirectoryName(targetFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await StoreData(container, password, file, sourceStream, targetFile);

        response.StatusCode = HttpStateCode.Created;
    }

    [Ignore]
    private static async Task StoreData(
        Container container, string? password, FileMeta file, Stream sourceStream,
        string targetFile
    )
    {
        try
        {
            using var fileStream = new FileStream(
                targetFile,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.None
            );
            if (container.Encryption is not null)
            {
                if (!container.Encryption.FileIV.TryGetValue(file.Id, out Memory<byte> iv))
                {
                    using var rng = RandomNumberGenerator.Create();
                    iv = new byte[16];
                    rng.GetBytes(iv.Span);
                    container.Encryption.FileIV[file.Id] = iv;
                    await Containers.SaveContainerAsync(container);
                }
                var key = Encryption.GetKey(password!, container.Encryption.EncryptionSalt.Span);
    
                using var aes = Aes.Create();
                aes.Key = key.ToArray();
                aes.IV = iv.ToArray();
    
                using var cryptoStream = new CryptoStream(
                    fileStream,
                    aes.CreateEncryptor(key.ToArray(), iv.ToArray()),
                    CryptoStreamMode.Write
                );
                await sourceStream.CopyToAsync(cryptoStream);
            }
            else await sourceStream.CopyToAsync(fileStream);
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(10);
            await StoreData(container, password, file, sourceStream, targetFile);
        }
    }

    [Method(HttpProtocolMethod.Delete)]
    [Path("/v1/file/{container_id}/")]
    public async Task DeleteFileAsync(
        [Var("container_id")] string containerId,
        [Get] string path,
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        var container = await GetContainer(containerId, out string? password, location, response);
        if (container is null)
            return;

        var file = container.Files
            .Where(x => x.Path == path)
            .FirstOrDefault();

        if (file is null)
        {
            response.StatusCode = HttpStateCode.NotFound;
            return;
        }

        var targetFile = Containers.GetContainerFilePath(container, file);
        if (File.Exists(targetFile))
            File.Delete(targetFile);
        container.Files.Remove(file);
        container.Encryption?.FileIV.Remove(file.Id);

        response.StatusCode = HttpStateCode.NoContent;
    }
}