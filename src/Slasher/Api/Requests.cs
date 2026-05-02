namespace Slasher.Api;

public sealed record StartAppRequest(
    string FileName,
    string? Arguments = null,
    string? WorkingDirectory = null,
    bool UseShellExecute = true);

public sealed record AppSelectRequest(string Name, string Match = "contains", bool Focus = true);

public sealed record MoveWindowRequest(int X, int Y, int Width, int Height, bool Repaint = true);

public sealed record WindowStateRequest(string State);

public sealed record WindowQueryRequest(
    string? Title = null,
    string Match = "contains",
    int? ProcessId = null,
    string? ProcessName = null,
    int TimeoutMs = 10000);

public sealed record BrowserOpenRequest(
    string Browser = "edge",
    string Url = "about:blank",
    bool Headless = false,
    string? DownloadDirectory = null);

public sealed record BrowserNavigateRequest(
    string Url,
    string? SessionId = null);

public sealed record BrowserSelectorRequest(
    string Using,
    string Value,
    string? SessionId = null,
    int TimeoutMs = 5000);

public sealed record BrowserTypeRequest(
    string Using,
    string Value,
    string Text,
    string? SessionId = null,
    int TimeoutMs = 5000,
    bool Clear = true);

public sealed record BrowserAttributeRequest(
    string Using,
    string Value,
    string Attribute,
    string? SessionId = null,
    int TimeoutMs = 5000);

public sealed record BrowserScriptRequest(
    string Script,
    string? SessionId = null);

public sealed record BrowserWaitTextRequest(
    string Using,
    string Value,
    string Text,
    string? SessionId = null,
    int TimeoutMs = 5000,
    string Match = "contains");

public sealed record BrowserStorageRequest(
    string Key,
    string? Value = null,
    string? SessionId = null);

public sealed record BrowserNewWindowRequest(
    string Type = "tab",
    string? Url = null,
    string? SessionId = null);

public sealed record BrowserSwitchWindowRequest(
    string? Handle = null,
    int? Index = null,
    string? SessionId = null);

public sealed record BrowserScreenshotRequest(string? SessionId = null);

public sealed record BrowserKeyRequest(
    string Keys,
    string? Using = null,
    string? Value = null,
    string? SessionId = null,
    int TimeoutMs = 5000);

public sealed record BrowserUploadFileRequest(
    string Using,
    string Value,
    string Path,
    string? SessionId = null,
    int TimeoutMs = 5000);

public sealed record BrowserDragRequest(
    string Using,
    string Value,
    string TargetUsing,
    string TargetValue,
    string? SessionId = null,
    int TimeoutMs = 5000);

public sealed record BrowserSelectOptionRequest(
    string Using,
    string Value,
    string SelectBy,
    string Option,
    string? SessionId = null,
    int TimeoutMs = 5000,
    bool Clear = false);

public sealed record BrowserDownloadWaitRequest(
    string Directory,
    string Pattern = "*",
    int TimeoutMs = 30000,
    int StableMs = 500);

public sealed record CloseAllWindowsRequest(string? Title = null, string? ProcessName = null);

public sealed record CloseProgramRequest(string? ProcessName = null, int? ProcessId = null, bool Force = false);

public sealed record KeyInputRequest(string Keys, int DelayMs = 0);

public sealed record TextInputRequest(string Text, int DelayMs = 0);

public sealed record MouseInputRequest(
    string Action,
    int? X = null,
    int? Y = null,
    string Button = "left",
    int WheelDelta = 0);

public sealed record MouseDragRequest(
    int FromX,
    int FromY,
    int ToX,
    int ToY,
    string Button = "left",
    int DurationMs = 400,
    int Steps = 24);

public sealed record ContextMenuRequest(int X, int Y, int DelayMs = 250);

public sealed record MessageBoxRequest(string Text, string? Title = null);

public sealed record ScreenshotRequest(
    string? Handle = null,
    bool IncludeCursor = false,
    int? MaxWidth = null,
    int? MaxHeight = null);

public sealed record ImageMatchRequest(
    string TemplatePath,
    string? Handle = null,
    double Threshold = 0.98,
    int? MaxWidth = null,
    int? MaxHeight = null,
    int Step = 1);

public sealed record FileOperationRequest(
    string Path,
    string? Destination = null,
    bool Overwrite = false,
    bool Recursive = false,
    bool UseRegex = false);

public sealed record ShortcutRequest(string TargetPath, string ShortcutPath, string? Arguments = null, string? WorkingDirectory = null);

public sealed record SymbolicLinkRequest(string LinkPath, string TargetPath, bool IsDirectory = false);

public sealed record ClipboardTextRequest(string Text);

public sealed record ErrorResponse(string Code, string Message);

public sealed record StartAppResponse(
    int ProcessId,
    string ProcessName,
    string? MainWindowHandle,
    string? MainWindowTitle);

public sealed record BrowserSessionResponse(
    string SessionId,
    string Browser,
    string Url,
    string Title);

public sealed record BrowserElementResponse(
    bool Found,
    string Using,
    string Value,
    string? TagName,
    string? Text,
    Rect? Bounds,
    bool? Displayed,
    bool? Enabled);

public sealed record BrowserValueResponse(string Value);

public sealed record BrowserScriptResponse(object? Value);

public sealed record BrowserCookieInfo(
    string Name,
    string Value,
    string? Domain,
    string? Path,
    DateTime? Expiry,
    bool Secure,
    bool HttpOnly,
    string? SameSite);

public sealed record BrowserWindowHandleInfo(string Handle, int Index, bool Current);

public sealed record BrowserLinkInfo(
    string Text,
    string Href,
    string? Target,
    string? Id,
    string? ClassName);

public sealed record BrowserSelectedOptionInfo(
    string Text,
    string Value,
    int Index,
    bool Selected);

public sealed record BrowserDownloadInfo(
    string Path,
    string Name,
    long Size,
    DateTimeOffset ModifiedAt);

public sealed record BrowserLogInfo(
    DateTimeOffset Timestamp,
    string Level,
    string Message);

public sealed record WindowInfo(
    string Handle,
    string Title,
    string ClassName,
    int ProcessId,
    string? ProcessName,
    Rect Bounds,
    bool IsVisible,
    bool IsEnabled,
    bool IsMinimized);

public sealed record ScreenshotResponse(string MimeType, string Base64Image, int Width, int Height);

public sealed record ImageMatchResponse(
    bool Found,
    double Score,
    Rect? Bounds,
    int ScreenWidth,
    int ScreenHeight,
    int TemplateWidth,
    int TemplateHeight,
    double Threshold);

public sealed record ContextMenuResponse(
    int X,
    int Y,
    WindowInfo? ForegroundWindow,
    ScreenshotResponse Screenshot,
    string Observation);

public sealed record MessageBoxResponse(string Title, string Text, int Button);

public sealed record ElementTreeResponse(
    WindowElementInfo Root,
    int MaxDepth,
    int MaxChildren,
    int TotalCount,
    bool Truncated);

public sealed record ElementFindResponse(
    IReadOnlyList<WindowElementInfo> Elements,
    int TotalScanned,
    int MaxDepth,
    int MaxResults,
    bool Truncated);

public sealed record ElementExistsResponse(
    bool Exists,
    WindowElementInfo? Element,
    int TotalScanned);

public sealed record ElementTextResponse(
    string Text,
    WindowElementInfo Element);

public sealed record ElementClickRequest(
    string? Handle = null,
    string? Title = null,
    string? ClassName = null,
    int? ControlId = null,
    string Match = "contains",
    int MaxDepth = 8,
    string Button = "left");

public sealed record WindowElementInfo(
    string Handle,
    string Title,
    string ClassName,
    int ControlId,
    Rect Bounds,
    bool IsVisible,
    bool IsEnabled,
    IReadOnlyList<WindowElementInfo> Children);

public sealed record Rect(int X, int Y, int Width, int Height);

public sealed record FileInfoResponse(
    string Name,
    string FullPath,
    bool Exists,
    bool IsDirectory,
    long? Size,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ModifiedAt,
    FileAttributes? Attributes);
