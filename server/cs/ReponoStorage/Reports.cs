using System.Text.Json;
using ReponoStorage.Data;
using System.Collections.Concurrent;

namespace ReponoStorage;

public static class Reports
{
    public const int ReportKeyLength = 30;

    private static string GetContainerPath()
    {
        return Path.Combine(
            Environment.CurrentDirectory,
            "container"
        );
    }

    private static string GetReportDirPath(Container container)
    {
        return Path.Combine(
            GetContainerPath(),
            container.Id,
            "reports"
        );
    }

    private static string GetReportPath(Container container, string id)
    {
        return Path.Combine(
            GetReportDirPath(container),
            $"{id}.json"
        );
    }

    public static async Task WritePassword(Container container, string password)
    {
        var path = Path.Combine(
            GetContainerPath(),
            container.Id,
            "report-pw.txt"
        );
        await File.WriteAllTextAsync(path, password);
    }

    public static async Task<ReportInfo> GenerateNewReportAsync(Container container)
    {
        string id;
        do { id = Tools.GetRandomKey(ReportKeyLength); }
        while (File.Exists(GetReportPath(container, id)));
        var report = new ReportInfo
        {
            Id = id,
            ContainerId = container.Id,
            Created = DateTime.UtcNow,
        };
        cachedReports.AddOrUpdate(id,
            _ => new WeakReference<ReportInfo>(report),
            (_, _) => new WeakReference<ReportInfo>(report)
        );
        await SaveReportAsync(container, report);
        return report;
    }

    public static async IAsyncEnumerable<ReportInfo> GetReportsAsync()
    {
        await foreach (var container in Containers.GetContainersAsync())
            await foreach (var report in GetReportsAsync(container))
                yield return report;
    }

    public static async IAsyncEnumerable<ReportInfo> GetReportsAsync(Container container)
    {
        var dir = GetReportDirPath(container);
        if (!Directory.Exists(dir))
            yield break;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var report = await GetReportAsync(container, id);
            if (report is not null)
                yield return report;
        }
    }

    private static readonly ConcurrentDictionary<string, WeakReference<ReportInfo>> cachedReports = new();

    public static async Task<ReportInfo?> GetReportAsync(Container container, string id)
    {
        if (cachedReports.TryGetValue(id, out WeakReference<ReportInfo>? weakReport)
            && weakReport.TryGetTarget(out ReportInfo? report)
        )
            return report;
        
        if (!Tools.ValidKey(id))
            return null;
        var path = GetReportPath(container, id);
        if (!File.Exists(path))
            return null;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            report = await JsonSerializer.DeserializeAsync<ReportInfo>(fs);
            if (report is null)
                return null;
            cachedReports.AddOrUpdate(id,
                _ => new WeakReference<ReportInfo>(report),
                (_, weakReport) =>
                {
                    weakReport.SetTarget(report);
                    return weakReport;
                }
            );
            return report;
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            return await GetReportAsync(container, id);
        }
    }

    public static async Task SaveReportAsync(Container container, ReportInfo report)
    {
        var path = GetReportPath(container, report.Id);
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);
        try
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, report, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            await fs.FlushAsync();
            fs.SetLength(fs.Position);
        }
        catch (UnauthorizedAccessException)
        {
            await Task.Delay(1);
            await SaveReportAsync(container, report);
        }
    }
}
