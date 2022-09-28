using System.Text.Json;
using ReponoStorage.Data;
using System.Collections.Concurrent;

namespace ReponoStorage;

public static class Containers
{
    public const int ContainerKeyLength = 15;

    public const int FileKeyLength = 3;

    private static string GetContainerDirPath()
    {
        return Path.Combine(
            Environment.CurrentDirectory,
            "container"
        );
    }

    private static string GetContainerPath(string id)
    {
        return Path.Combine(
            GetContainerDirPath(),
            id
        );
    }

    private static string GetContainerInfoPath(string id)
    {
        return Path.Combine(
            GetContainerPath(id),
            "info.json"
        );
    }

    private static string GetContainerEncryptionPath(string id)
    {
        return Path.Combine(
            GetContainerPath(id),
            "encryption.json"
        );
    }

    public static string GetContainerFilePath(Container container, FileMeta file)
    {
        return Path.Combine(
            GetContainerPath(container.Id),
            "files",
            file.Id
        );
    }

    public static async Task<Container> GenerateNewContainerAsync()
    {
        string id;
        do
        {
            id = Tools.GetRandomKey(ContainerKeyLength);
        }
        while (File.Exists(GetContainerInfoPath(id)));
        var container = new Container
        {
            Id = id,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
        };
        cachedContainer.AddOrUpdate(id,
            _ => new WeakReference<Container>(container),
            (_, _) => new WeakReference<Container>(container)
        );
        await SaveContainerAsync(container);
        return container;
    }

    public static async IAsyncEnumerable<Container> GetContainersAsync()
    {
        var dir = GetContainerDirPath();
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var id = Path.GetFileName(sub);
            var container = await GetContainerAsync(id);
            if (container is not null)
                yield return container;
        }
    }

    private static readonly ConcurrentDictionary<string, WeakReference<Container>> cachedContainer = new();

    public static async Task<Container?> GetContainerAsync(string id)
    {
        if (cachedContainer.TryGetValue(id, out WeakReference<Container>? weakContainer)
            && weakContainer.TryGetTarget(out Container? container)
        )
            return container;

        if (!Tools.ValidKey(id))
            return null;
        var path = GetContainerInfoPath(id);
        if (!File.Exists(path))
            return null;
        try
        {
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                container = await JsonSerializer.DeserializeAsync<Container>(fs);
                if (container is null)
                    return null;
                cachedContainer.AddOrUpdate(id,
                    _ => new WeakReference<Container>(container),
                    (_, weakContainer) =>
                    {
                        weakContainer.SetTarget(container);
                        return weakContainer;
                    }
                );
            }
            var encPath = GetContainerEncryptionPath(id);
            if (File.Exists(encPath))
            {
                using var fs = new FileStream(encPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var encryption = await JsonSerializer.DeserializeAsync<Encryption>(fs, new JsonSerializerOptions
                {
                    Converters =
                    {
                        new MemoryConverter()
                    }
                });
                container.Encryption = encryption;
            }
            return container;
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            return await GetContainerAsync(id);
        }
    }

    public static async Task SaveContainerAsync(Container container)
    {
        var path = GetContainerInfoPath(container.Id);
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);
        try
        {
            {
                using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(fs, container, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                await fs.FlushAsync();
                fs.SetLength(fs.Position);
            }
            var encPath = GetContainerEncryptionPath(container.Id);
            if (container.Encryption is not null)
            {
                using var fs = new FileStream(encPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(fs, container.Encryption, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters =
                    {
                        new MemoryConverter()
                    }
                });
                await fs.FlushAsync();
                fs.SetLength(fs.Position);
            }
            else if (File.Exists(encPath))
                File.Delete(encPath);
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            await SaveContainerAsync(container);
        }
    }
}