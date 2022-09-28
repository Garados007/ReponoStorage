using MaxLib.WebServer;
using MaxLib.WebServer.Chunked;
using MaxLib.WebServer.Services;
using Serilog;
using Serilog.Events;

namespace ReponoStorage;

public class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(LogEventLevel.Verbose,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        WebServerLog.LogPreAdded += WebServerLog_LogPreAdded;

        await MimeType.LoadMimeTypesForExtensions(true);

        var rootToken = await Tokens.GetRootTokenAsync();
        Log.Information("Root token: {token}", rootToken.Id);

        var server = new Server(new WebServerSettings(8015, 5000));
        server.InitialDefault();
        server.RemoveWebService(server.GetWebService<HttpSender>()!);
        server.AddWebService(new ChunkedResponseCreator());
        server.AddWebService(new ChunkedSender());
        var build = MaxLib.WebServer.Builder.Service.Build(typeof(Program).Assembly);
        if (build != null)
            server.AddWebService(build);
        else Log.Error("Cannot build web services");

        server.AddWebService(new CorsService());

        await server.RunAsync();
    }
    
    private static readonly MessageTemplate serilogMessageTemplate =
        new Serilog.Parsing.MessageTemplateParser().Parse(
            "{infoType}: {info}"
        );

    private static void WebServerLog_LogPreAdded(ServerLogArgs e)
    {
        e.Discard = true;
        Log.Write(new LogEvent(
            e.LogItem.Date,
            e.LogItem.Type switch
            {
                ServerLogType.Debug => LogEventLevel.Verbose,
                ServerLogType.Information => LogEventLevel.Debug,
                ServerLogType.Error => LogEventLevel.Error,
                ServerLogType.FatalError => LogEventLevel.Fatal,
                _ => LogEventLevel.Information,
            },
            null,
            serilogMessageTemplate,
            new[]
            {
                new LogEventProperty("infoType", new ScalarValue(e.LogItem.InfoType)),
                new LogEventProperty("info", new ScalarValue(e.LogItem.Information))
            }
        ));
    }
}
