using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using MaxLib.WebServer.Post;
using ReponoStorage.Data;
using System.Text.Json;

namespace ReponoStorage;

public sealed class ReportService : Service
{
    [Method(HttpProtocolMethod.Get)]
    [Path("/v1/report/")]
    [return: JsonDataConverter]
    public async Task<List<ReportInfo>> GetReportInfosAsync(
        HttpLocation location,
        HttpResponseHeader response
    )
    {
        if (!location.GetParameter.TryGetValue("container_id", out string? containerId))
            containerId = null;
        if (!location.GetParameter.TryGetValue("path", out string? path))
            path = null;
        
        var result = new List<ReportInfo>();
        if (containerId is null)
        {
            await foreach (var report in Reports.GetReportsAsync())
                result.Add(report);
            return result;
        }

        var container = await Containers.GetContainerAsync(containerId);
        if (container is null)
            return result;

        await foreach (var report in Reports.GetReportsAsync(container))
        {
            if (path is null || report.Report.Files.Contains(path))
                result.Add(report);
        }

        return result;
    }

    [Method(HttpProtocolMethod.Post)]
    [Path("/v1/report/")]
    [return: JsonDataConverter]
    public async Task<ReportInfo?> GetReportInfoAsync(
        [Get("container_id")] string containerId,
        HttpLocation location,
        HttpPost post,
        HttpResponseHeader response
    )
    {
        var container = await FileService.GetContainer(containerId, out string? password, location, response);
        if (container is null)
            return null;
        
        if (post.DataAsync is null
            || await post.DataAsync is not UnknownPostData postData
            || postData.MimeType != MimeType.ApplicationJson
        )
        {
            response.StatusCode = HttpStateCode.BadRequest;
            return null;
        }

        var report = await JsonSerializer.DeserializeAsync<Report>(
            postData.Data
        );

        if (report is null)
        {
            response.StatusCode = HttpStateCode.BadRequest;
            return null;
        }

        var info = await Reports.GenerateNewReportAsync(container);
        info.Report = report;
        await Reports.SaveReportAsync(container, info);

        if (password is not null)
            await Reports.WritePassword(container, password);

        return info;
    }
}
