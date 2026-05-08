using Slasher.Api;
using Slasher.Automation;
using Slasher.Data;
using Slasher.Files;
using Slasher.Peers;
using Slasher.Windows;

var workspaceRoot = ResolveWorkspaceRoot();
var outputWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var sourceWebRoot = Path.Combine(workspaceRoot, "src", "Slasher", "wwwroot");
var webRootPath = Directory.Exists(outputWebRoot)
    ? outputWebRoot
    : Directory.Exists(sourceWebRoot)
        ? sourceWebRoot
        : null;

var builderOptions = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = workspaceRoot,
    WebRootPath = webRootPath
};

var builder = WebApplication.CreateBuilder(builderOptions);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<PeerOptions>(builder.Configuration.GetSection("PeerMode"));
builder.Services.AddSingleton<WindowsAutomationService>();
builder.Services.AddSingleton<BrowserAutomationService>();
builder.Services.AddSingleton<FileSystemAutomationService>();
builder.Services.AddSingleton<FileWatcherService>();
builder.Services.AddSingleton<ClipboardService>();
builder.Services.AddSingleton<CsvAutomationService>();
builder.Services.AddSingleton<JsonAutomationService>();
builder.Services.AddSingleton<ExcelAutomationService>();
builder.Services.AddSingleton<AutomationRunArtifactStore>();
builder.Services.AddSingleton<ScriptRunService>();
builder.Services.AddSingleton<PeerIdentityStore>();
builder.Services.AddSingleton<PeerRegistry>();
builder.Services.AddSingleton<PeerEndpointService>();
builder.Services.AddSingleton<NamespaceService>();
builder.Services.AddSingleton<ResourceReadService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var apiKey = Environment.GetEnvironmentVariable("SLASHER_API_KEY");
app.Use(async (context, next) =>
{
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        await next();
        return;
    }

    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.Equals($"Bearer {apiKey}", StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("unauthorized", "Missing or invalid bearer token."));
        return;
    }

    await next();
});

app.MapSlasherEndpoints();

app.Run();

static string ResolveWorkspaceRoot()
{
    var currentDirectory = Directory.GetCurrentDirectory();
    foreach (var start in new[] { currentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "Slasher", "Slasher.csproj"))
                && Directory.Exists(Path.Combine(directory.FullName, "scripts")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    return currentDirectory;
}
