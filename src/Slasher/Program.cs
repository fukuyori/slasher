using Slasher.Api;
using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

var outputWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var sourceWebRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "Slasher", "wwwroot");
var webRootPath = Directory.Exists(outputWebRoot)
    ? outputWebRoot
    : Directory.Exists(sourceWebRoot)
        ? sourceWebRoot
        : null;

var builderOptions = new WebApplicationOptions
{
    Args = args,
    WebRootPath = webRootPath
};

var builder = WebApplication.CreateBuilder(builderOptions);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton<WindowsAutomationService>();
builder.Services.AddSingleton<BrowserAutomationService>();
builder.Services.AddSingleton<FileSystemAutomationService>();
builder.Services.AddSingleton<ClipboardService>();
builder.Services.AddSingleton<AutomationRunArtifactStore>();
builder.Services.AddSingleton<ScriptRunService>();

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
