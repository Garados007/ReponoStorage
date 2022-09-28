using MaxLib.WebServer;

namespace ReponoStorage;

public class CorsService : WebService
{
    public CorsService() 
        : base(ServerStage.CreateResponse)
    {
    }

    public override bool CanWorkWith(WebProgressTask task)
    {
        return true;
    }

    public override Task ProgressTask(WebProgressTask task)
    {
        var header = task.Request.GetHeader("Origin") ?? "*";
        task.Response.SetHeader("Access-Control-Allow-Origin", header);
        task.Response.SetHeader("Vary", "Origin");
        if ((header = task.Request.GetHeader("Access-Control-Request-Headers")) is not null)
            task.Response.SetHeader("Access-Control-Allow-Headers", header);
        if ((header = task.Request.GetHeader("Access-Control-Request-Method")) is not null)
            task.Response.SetHeader("Access-Control-Allow-Methods", header);
        task.Response.SetHeader("Access-Control-Allow-Private-Network", "true");
        return Task.CompletedTask;
    }
}
