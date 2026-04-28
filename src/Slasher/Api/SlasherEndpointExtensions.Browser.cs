using OpenQA.Selenium;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapBrowserEndpoints(WebApplication app)
    {
        app.MapPost("/browser/open", (BrowserOpenRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Open(request));
        });

        app.MapPost("/browser/navigate", (BrowserNavigateRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Navigate(request));
        });

        app.MapGet("/browser/current", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Current(sessionId));
        });

        app.MapGet("/browser/title", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Title(sessionId));
        });

        app.MapGet("/browser/url", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Url(sessionId));
        });

        app.MapPost("/browser/find", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Find(request));
        });

        app.MapPost("/browser/click", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Click(request));
        });

        app.MapPost("/browser/hover", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Hover(request));
        });

        app.MapPost("/browser/double-click", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.DoubleClick(request));
        });

        app.MapPost("/browser/right-click", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.RightClick(request));
        });

        app.MapPost("/browser/type", (BrowserTypeRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Type(request));
        });

        app.MapPost("/browser/press", (BrowserKeyRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Press(request));
        });

        app.MapPost("/browser/upload", (BrowserUploadFileRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.UploadFile(request));
        });

        app.MapPost("/browser/drag", (BrowserDragRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Drag(request));
        });

        app.MapPost("/browser/select-option", (BrowserSelectOptionRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.SelectOption(request));
        });

        app.MapPost("/browser/selected-options", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.SelectedOptions(request));
        });

        app.MapPost("/browser/clear", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Clear(request));
        });

        app.MapPost("/browser/submit", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Submit(request));
        });

        app.MapPost("/browser/text", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Text(request));
        });

        app.MapPost("/browser/attribute", (BrowserAttributeRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Attribute(request));
        });

        app.MapPost("/browser/wait", (BrowserSelectorRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Wait(request));
        });

        app.MapPost("/browser/wait-text", (BrowserWaitTextRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.WaitText(request));
        });

        app.MapPost("/browser/js", (BrowserScriptRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.ExecuteScript(request));
        });

        app.MapGet("/browser/cookies", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Cookies(sessionId));
        });

        app.MapPost("/browser/storage/{storage}/get", (string storage, BrowserStorageRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.GetStorage(storage, request));
        });

        app.MapPost("/browser/storage/{storage}/set", (string storage, BrowserStorageRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.SetStorage(storage, request));
        });

        app.MapPost("/browser/screenshot", (BrowserScreenshotRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Screenshot(request));
        });

        app.MapGet("/browser/links", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Links(sessionId));
        });

        app.MapGet("/browser/windows", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Windows(sessionId));
        });

        app.MapPost("/browser/new-window", (BrowserNewWindowRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.NewWindow(request));
        });

        app.MapPost("/browser/switch-window", (BrowserSwitchWindowRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.SwitchWindow(request));
        });

        app.MapPost("/browser/close-window", (string? sessionId, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.CloseCurrentWindow(sessionId));
        });

        app.MapPost("/browser/close", (string? sessionId, BrowserAutomationService browser) =>
        {
            return browser.Close(sessionId, out var error)
                ? Results.Ok(new { closed = true, sessionId })
                : Results.BadRequest(error);
        });

        app.MapPost("/browser/downloads/wait", (BrowserDownloadWaitRequest request, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.WaitForDownload(request));
        });

        app.MapGet("/browser/logs", (string? sessionId, string? type, BrowserAutomationService browser) =>
        {
            return BrowserResult(() => browser.Logs(sessionId, type));
        });
    }

    private static IResult BrowserResult<T>(Func<T> action)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (NoSuchElementException ex)
        {
            return Results.BadRequest(new ErrorResponse("browser_element_not_found", ex.Message));
        }
        catch (WebDriverException ex)
        {
            return Results.BadRequest(new ErrorResponse("browser_webdriver_error", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse("browser_error", ex.Message));
        }
    }
}
