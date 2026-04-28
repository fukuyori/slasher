using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed record BrowserSpec(string Name, string Executable, string ProcessName);

    private static readonly BrowserSpec[] BrowserSpecs =
    [
        new("edge", "msedge.exe", "msedge"),
        new("chrome", "chrome.exe", "chrome"),
        new("firefox", "firefox.exe", "firefox")
    ];

    private ScriptCommandResult ExecuteBrowserCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 1, "browser syntax is: browser <launch|open|select|go|address|back|forward|refresh|reload|close|new-tab|webdriver|current|title|url|find|click|hover|double-click|right-click|type|press|upload|drag|select-option|selected-options|wait-download|logs|clear|submit|text|attr|wait|js|cookies|storage|quit> [...]");
        var action = args[0].ToLowerInvariant();

        return action switch
        {
            "webdriver" or "session" => ExecuteBrowserWebDriverOpenCommand(args.Skip(1).ToArray()),
            "current" => ExecuteBrowserCurrentCommand(),
            "title" => ExecuteBrowserTitleCommand(),
            "url" or "current-url" => ExecuteBrowserUrlCommand(),
            "find" => ExecuteBrowserFindCommand(args.Skip(1).ToArray()),
            "click" => ExecuteBrowserClickCommand(args.Skip(1).ToArray()),
            "hover" or "move-to" => ExecuteBrowserHoverCommand(args.Skip(1).ToArray()),
            "double-click" or "doubleclick" or "dblclick" => ExecuteBrowserDoubleClickCommand(args.Skip(1).ToArray()),
            "right-click" or "rightclick" or "context-click" or "contextclick" => ExecuteBrowserRightClickCommand(args.Skip(1).ToArray()),
            "type" or "sendkeys" => ExecuteBrowserTypeCommand(args.Skip(1).ToArray()),
            "press" or "key" => ExecuteBrowserPressCommand(args.Skip(1).ToArray()),
            "upload" or "upload-file" => ExecuteBrowserUploadCommand(args.Skip(1).ToArray()),
            "drag" or "drag-drop" or "drag-and-drop" => ExecuteBrowserDragCommand(args.Skip(1).ToArray()),
            "select-option" or "selectoption" => ExecuteBrowserSelectOptionCommand(args.Skip(1).ToArray()),
            "selected-options" or "selectedoptions" => ExecuteBrowserSelectedOptionsCommand(args.Skip(1).ToArray()),
            "wait-download" or "wait-downloads" or "download-wait" => ExecuteBrowserWaitDownloadCommand(args.Skip(1).ToArray()),
            "logs" or "console" => ExecuteBrowserLogsCommand(args.Skip(1).ToArray()),
            "clear" => ExecuteBrowserClearCommand(args.Skip(1).ToArray()),
            "submit" => ExecuteBrowserSubmitCommand(args.Skip(1).ToArray()),
            "text" => ExecuteBrowserTextCommand(args.Skip(1).ToArray()),
            "attr" or "attribute" => ExecuteBrowserAttributeCommand(args.Skip(1).ToArray()),
            "wait" => ExecuteBrowserWaitCommand(args.Skip(1).ToArray()),
            "js" or "javascript" => ExecuteBrowserJavaScriptCommand(args.Skip(1).ToArray()),
            "cookies" => ExecuteBrowserCookiesCommand(),
            "storage" => ExecuteBrowserStorageCommand(args.Skip(1).ToArray()),
            "screenshot" or "capture" => ExecuteBrowserScreenshotCommand(),
            "links" => ExecuteBrowserLinksCommand(),
            "windows" or "tabs" => ExecuteBrowserWindowsCommand(),
            "new-window" or "newwindow" => ExecuteBrowserNewWindowCommand(args.Skip(1).ToArray(), "window"),
            "new-webdriver-tab" or "newtab-webdriver" => ExecuteBrowserNewWindowCommand(args.Skip(1).ToArray(), "tab"),
            "switch" or "switch-window" or "switch-tab" => ExecuteBrowserSwitchWindowCommand(args.Skip(1).ToArray()),
            "close-tab" or "close-window" => ExecuteBrowserCloseWindowCommand(),
            "quit" => ExecuteBrowserQuitCommand(args.Skip(1).ToArray()),
            "launch" or "open" or "website" => ExecuteBrowserLaunchCommand(args),
            "select" or "activate" => ExecuteBrowserSelectCommand(args),
            "go" or "navigate" or "address" => ExecuteBrowserAddressCommand(args, selectedHandle),
            "back" => ExecuteBrowserKeyCommand("ALT+LEFT", selectedHandle, "back"),
            "forward" => ExecuteBrowserKeyCommand("ALT+RIGHT", selectedHandle, "forward"),
            "refresh" or "reload" => ExecuteBrowserKeyCommand("CTRL+R", selectedHandle, "refresh"),
            "close" => ExecuteBrowserKeyCommand("CTRL+W", selectedHandle, "close"),
            "new-tab" or "newtab" => ExecuteBrowserKeyCommand("CTRL+T", selectedHandle, "new-tab"),
            _ => throw new ScriptCommandException(
                "unsupported_browser_command",
                "browser supports launch/open, select, go/address, back, forward, refresh, close, new-tab, webdriver, current, title, url, find, click, hover, double-click, right-click, type, press, upload, drag, select-option, selected-options, wait-download, logs, clear, submit, text, attr, wait, js, cookies, storage, and quit.")
        };
    }

    private ScriptCommandResult ExecuteBrowserWebDriverOpenCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "browser webdriver syntax is: browser webdriver <edge|chrome|firefox> [url] [headless].");
        var browser = args[0];
        var headless = args.Any(item => item.Equals("headless", StringComparison.OrdinalIgnoreCase));
        string? downloadDirectory = null;
        var urlParts = args.Skip(1).Where(item =>
        {
            if (item.Equals("headless", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var separator = item.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                var key = item[..separator];
                if (key.Equals("downloadDir", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("downloads", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("downloadDirectory", StringComparison.OrdinalIgnoreCase))
                {
                    downloadDirectory = item[(separator + 1)..];
                    return false;
                }
            }

            return true;
        }).ToArray();
        var url = urlParts.Length == 0 ? "about:blank" : string.Join(' ', urlParts);
        var session = _browser.Open(new BrowserOpenRequest(browser, url, headless, downloadDirectory));
        return new ScriptCommandResult(new { opened = true, session }, AssignmentValue: session);
    }

    private ScriptCommandResult ExecuteBrowserFindCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser find syntax is: browser find <css|xpath|id|name|tag|class|link|partialLink> <selector> [timeoutMs].");
        var element = _browser.Find(request);
        return new ScriptCommandResult(element, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserCurrentCommand()
    {
        var session = _browser.Current(null);
        return new ScriptCommandResult(session, AssignmentValue: session);
    }

    private ScriptCommandResult ExecuteBrowserTitleCommand()
    {
        var value = _browser.Title(null);
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserUrlCommand()
    {
        var value = _browser.Url(null);
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserClickCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser click syntax is: browser click <using> <selector> [timeoutMs].");
        var element = _browser.Click(request);
        return new ScriptCommandResult(new { clicked = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserHoverCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser hover syntax is: browser hover <using> <selector> [timeoutMs].");
        var element = _browser.Hover(request);
        return new ScriptCommandResult(new { hovered = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserDoubleClickCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser double-click syntax is: browser double-click <using> <selector> [timeoutMs].");
        var element = _browser.DoubleClick(request);
        return new ScriptCommandResult(new { doubleClicked = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserRightClickCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser right-click syntax is: browser right-click <using> <selector> [timeoutMs].");
        var element = _browser.RightClick(request);
        return new ScriptCommandResult(new { rightClicked = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserTypeCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "browser type syntax is: browser type <using> <selector> <text> [timeoutMs] [append].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var clear = !args.Any(item => item.Equals("append", StringComparison.OrdinalIgnoreCase));
        var timeoutMs = 5000;
        var textEnd = args.Count;
        if (args.Count > index
            && int.TryParse(args[^1], out var parsedTimeout))
        {
            timeoutMs = parsedTimeout;
            textEnd--;
        }

        if (textEnd > index && args[textEnd - 1].Equals("append", StringComparison.OrdinalIgnoreCase))
        {
            textEnd--;
        }

        var text = string.Join(' ', args.Skip(index).Take(textEnd - index));
        var element = _browser.Type(new BrowserTypeRequest(usingValue, selector, text, TimeoutMs: timeoutMs, Clear: clear));
        return new ScriptCommandResult(new { typed = true, chars = text.Length, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserPressCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "browser press syntax is: browser press <keys> OR browser press <using> <selector> <keys> [timeoutMs].");
        if (args.Count >= 3 && IsBrowserSelectorStrategy(args[0]))
        {
            var (usingValue, selector, index) = ReadBrowserSelector(args);
            var timeoutMs = 5000;
            var keyEnd = args.Count;
            if (int.TryParse(args[^1], out var parsedTimeout))
            {
                timeoutMs = parsedTimeout;
                keyEnd--;
            }

            var keys = string.Join(' ', args.Skip(index).Take(keyEnd - index));
            var element = _browser.Press(new BrowserKeyRequest(keys, usingValue, selector, TimeoutMs: timeoutMs));
            return new ScriptCommandResult(new { pressed = true, keys, element }, AssignmentValue: element);
        }

        var active = _browser.Press(new BrowserKeyRequest(JoinArgs(args, 0) ?? string.Empty));
        return new ScriptCommandResult(new { pressed = true, element = active }, AssignmentValue: active);
    }

    private ScriptCommandResult ExecuteBrowserUploadCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "browser upload syntax is: browser upload <using> <selector> <path> [timeoutMs].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var timeoutMs = 5000;
        var pathEnd = args.Count;
        if (int.TryParse(args[^1], out var parsedTimeout))
        {
            timeoutMs = parsedTimeout;
            pathEnd--;
        }

        var path = string.Join(' ', args.Skip(index).Take(pathEnd - index));
        var element = _browser.UploadFile(new BrowserUploadFileRequest(usingValue, selector, path, TimeoutMs: timeoutMs));
        return new ScriptCommandResult(new { uploaded = true, path, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserDragCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 4, "browser drag syntax is: browser drag <using> <selector> [to] <targetUsing> <targetSelector> [timeoutMs].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        if (index < args.Count && args[index].Equals("to", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        RequireArgs(args.Skip(index).ToArray(), 2, "browser drag requires a target selector.");
        var (targetUsing, targetSelector, targetNext) = ReadBrowserSelector(args.Skip(index).ToArray());
        var timeoutIndex = index + targetNext;
        var timeoutMs = args.Count > timeoutIndex ? ParseInt(args[timeoutIndex], "timeoutMs") : 5000;
        var element = _browser.Drag(new BrowserDragRequest(usingValue, selector, targetUsing, targetSelector, TimeoutMs: timeoutMs));
        return new ScriptCommandResult(new { dragged = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserSelectOptionCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 4, "browser select-option syntax is: browser select-option <using> <selector> <text|value|index> <option> [timeoutMs] [clear].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var selectBy = args[index];
        index++;
        var clear = args.Any(item => item.Equals("clear", StringComparison.OrdinalIgnoreCase));
        var timeoutMs = 5000;
        var optionEnd = args.Count;
        if (optionEnd > index && args[^1].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            optionEnd--;
        }

        if (optionEnd > index && int.TryParse(args[optionEnd - 1], out var parsedTimeout))
        {
            timeoutMs = parsedTimeout;
            optionEnd--;
        }

        var option = string.Join(' ', args.Skip(index).Take(optionEnd - index));
        var selected = _browser.SelectOption(new BrowserSelectOptionRequest(usingValue, selector, selectBy, option, TimeoutMs: timeoutMs, Clear: clear));
        return new ScriptCommandResult(selected, AssignmentValue: selected);
    }

    private ScriptCommandResult ExecuteBrowserSelectedOptionsCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser selected-options syntax is: browser selected-options <using> <selector> [timeoutMs].");
        var selected = _browser.SelectedOptions(request);
        return new ScriptCommandResult(selected, AssignmentValue: selected);
    }

    private ScriptCommandResult ExecuteBrowserWaitDownloadCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "browser wait-download syntax is: browser wait-download <directory> [pattern] [timeoutMs] [stableMs].");
        var directory = args[0];
        var pattern = args.Count > 1 ? args[1] : "*";
        var timeoutMs = args.Count > 2 ? ParseInt(args[2], "timeoutMs") : 30000;
        var stableMs = args.Count > 3 ? ParseInt(args[3], "stableMs") : 500;
        var download = _browser.WaitForDownload(new BrowserDownloadWaitRequest(directory, pattern, timeoutMs, stableMs));
        return new ScriptCommandResult(download, AssignmentValue: download);
    }

    private ScriptCommandResult ExecuteBrowserLogsCommand(IReadOnlyList<string> args)
    {
        var type = args.Count > 0 ? args[0] : "browser";
        var logs = _browser.Logs(null, type);
        return new ScriptCommandResult(logs, AssignmentValue: logs);
    }

    private ScriptCommandResult ExecuteBrowserClearCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser clear syntax is: browser clear <using> <selector> [timeoutMs].");
        var element = _browser.Clear(request);
        return new ScriptCommandResult(new { cleared = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserSubmitCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser submit syntax is: browser submit <using> <selector> [timeoutMs].");
        var element = _browser.Submit(request);
        return new ScriptCommandResult(new { submitted = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserTextCommand(IReadOnlyList<string> args)
    {
        var request = ParseBrowserSelectorRequest(args, "browser text syntax is: browser text <using> <selector> [timeoutMs].");
        var value = _browser.Text(request);
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserAttributeCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "browser attr syntax is: browser attr <using> <selector> <attribute> [timeoutMs].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var attribute = args[index];
        var timeoutMs = args.Count > index + 1 ? ParseInt(args[index + 1], "timeoutMs") : 5000;
        var value = _browser.Attribute(new BrowserAttributeRequest(usingValue, selector, attribute, TimeoutMs: timeoutMs));
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserWaitCommand(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && args[0].Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteBrowserWaitTextCommand(args.Skip(1).ToArray());
        }

        var request = ParseBrowserSelectorRequest(args, "browser wait syntax is: browser wait <using> <selector> [timeoutMs].");
        var element = _browser.Wait(request);
        return new ScriptCommandResult(new { waited = true, element }, AssignmentValue: element);
    }

    private ScriptCommandResult ExecuteBrowserWaitTextCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "browser wait text syntax is: browser wait text <using> <selector> <expected> [timeoutMs] [match].");
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var timeoutMs = 5000;
        var match = "contains";
        var textEnd = args.Count;
        if (textEnd > index && IsBrowserTextMatch(args[^1]))
        {
            match = args[^1];
            textEnd--;
        }

        if (textEnd > index && int.TryParse(args[textEnd - 1], out var parsedTimeout))
        {
            timeoutMs = parsedTimeout;
            textEnd--;
        }

        var expected = string.Join(' ', args.Skip(index).Take(textEnd - index));
        var value = _browser.WaitText(new BrowserWaitTextRequest(usingValue, selector, expected, TimeoutMs: timeoutMs, Match: match));
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserJavaScriptCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "browser js syntax is: browser js <script>.");
        var script = JoinArgs(args, 0) ?? string.Empty;
        var value = _browser.ExecuteScript(new BrowserScriptRequest(script));
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserCookiesCommand()
    {
        var cookies = _browser.Cookies(null);
        return new ScriptCommandResult(cookies, AssignmentValue: cookies);
    }

    private ScriptCommandResult ExecuteBrowserStorageCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "browser storage syntax is: browser storage <local|session> <get|set> <key> [value].");
        var storage = args[0];
        var action = args[1].ToLowerInvariant();
        var key = args[2];
        return action switch
        {
            "get" => StorageResult(_browser.GetStorage(storage, new BrowserStorageRequest(key))),
            "set" => StorageResult(_browser.SetStorage(storage, new BrowserStorageRequest(key, JoinArgs(args, 3) ?? string.Empty))),
            _ => throw new ScriptCommandException("unsupported_browser_storage_command", "browser storage supports get and set.")
        };
    }

    private ScriptCommandResult ExecuteBrowserScreenshotCommand()
    {
        var screenshot = _browser.Screenshot(new BrowserScreenshotRequest());
        return new ScriptCommandResult(null, Screenshot: screenshot, AssignmentValue: screenshot);
    }

    private ScriptCommandResult ExecuteBrowserLinksCommand()
    {
        var links = _browser.Links(null);
        return new ScriptCommandResult(links, AssignmentValue: links);
    }

    private ScriptCommandResult ExecuteBrowserWindowsCommand()
    {
        var windows = _browser.Windows(null);
        return new ScriptCommandResult(windows, AssignmentValue: windows);
    }

    private ScriptCommandResult ExecuteBrowserNewWindowCommand(IReadOnlyList<string> args, string type)
    {
        var url = JoinArgs(args, 0);
        var session = _browser.NewWindow(new BrowserNewWindowRequest(type, url));
        return new ScriptCommandResult(session, AssignmentValue: session);
    }

    private ScriptCommandResult ExecuteBrowserSwitchWindowCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "browser switch syntax is: browser switch <index|handle>.");
        BrowserSwitchWindowRequest request;
        if (int.TryParse(args[0], out var index))
        {
            request = new BrowserSwitchWindowRequest(Index: index);
        }
        else
        {
            request = new BrowserSwitchWindowRequest(Handle: args[0]);
        }

        var session = _browser.SwitchWindow(request);
        return new ScriptCommandResult(session, AssignmentValue: session);
    }

    private ScriptCommandResult ExecuteBrowserCloseWindowCommand()
    {
        var session = _browser.CloseCurrentWindow(null);
        return new ScriptCommandResult(session, AssignmentValue: session);
    }

    private static ScriptCommandResult StorageResult(BrowserValueResponse value)
    {
        return new ScriptCommandResult(value, AssignmentValue: value.Value);
    }

    private ScriptCommandResult ExecuteBrowserQuitCommand(IReadOnlyList<string> args)
    {
        var sessionId = args.Count > 0 ? args[0] : null;
        if (!_browser.Close(sessionId, out var error))
        {
            throw FromError(error, "browser_quit_failed");
        }

        return new ScriptCommandResult(new { closed = true, sessionId });
    }

    private ScriptCommandResult ExecuteBrowserLaunchCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "browser launch requires a URL or a browser name.");
        if (TryGetBrowserSpec(args[1], out var browser))
        {
            var launchUrl = JoinArgs(args, 2) ?? "about:blank";
            return LaunchBrowser(browser, launchUrl);
        }

        var url = JoinArgs(args, 1) ?? string.Empty;
        var result = _automation.StartApp(new StartAppRequest(url));
        return new ScriptCommandResult(
            new
            {
                launched = true,
                url,
                processId = result.ProcessId,
                processName = result.ProcessName,
                handle = result.MainWindowHandle,
                title = result.MainWindowTitle
            },
            result.MainWindowHandle,
            AssignmentValue: result);
    }

    private ScriptCommandResult LaunchBrowser(BrowserSpec browser, string url)
    {
        StartAppResponse result;
        try
        {
            result = _automation.StartApp(new StartAppRequest(browser.Executable, url));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new ScriptCommandException(
                "browser_launch_failed",
                $"Failed to launch {browser.Name} ({browser.Executable}): {ex.Message}");
        }

        WindowInfo? window = null;
        var selectedHandle = result.MainWindowHandle;
        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
            _automation.TryGetWindow(selectedHandle, out window);
        }

        if (window is null)
        {
            Thread.Sleep(1000);
            window = _automation.SelectApp(new AppSelectRequest(browser.ProcessName, Focus: true), out _);
            selectedHandle = window?.Handle ?? selectedHandle;
        }

        return new ScriptCommandResult(
            new
            {
                launched = true,
                browser = browser.Name,
                url,
                executable = browser.Executable,
                processName = browser.ProcessName,
                processId = result.ProcessId,
                handle = selectedHandle,
                title = window?.Title ?? result.MainWindowTitle
            },
            selectedHandle,
            window,
            AssignmentValue: window is not null ? window : result);
    }

    private ScriptCommandResult ExecuteBrowserSelectCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "browser select requires a browser name: edge, chrome, or firefox.");
        if (!TryGetBrowserSpec(args[1], out var browser))
        {
            throw new ScriptCommandException(
                "unsupported_browser",
                "browser select supports edge, chrome, and firefox.");
        }

        var window = _automation.SelectApp(new AppSelectRequest(browser.ProcessName, Focus: true), out var error);
        if (window is null)
        {
            throw FromError(error, "browser_window_not_found");
        }

        return new ScriptCommandResult(
            new
            {
                selected = true,
                browser = browser.Name,
                processName = browser.ProcessName,
                handle = window.Handle,
                title = window.Title
            },
            window.Handle,
            window,
            AssignmentValue: window);
    }

    private static bool TryGetBrowserSpec(string value, out BrowserSpec browser)
    {
        var normalized = value.Trim().ToLowerInvariant();
        browser = BrowserSpecs.FirstOrDefault(item =>
            item.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || item.ProcessName.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || item.Executable.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileNameWithoutExtension(item.Executable).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? BrowserSpecs[0];

        if (normalized is "ms-edge" or "microsoft-edge" or "microsoftedge")
        {
            browser = BrowserSpecs[0];
            return true;
        }

        if (normalized is "google-chrome" or "googlechrome")
        {
            browser = BrowserSpecs[1];
            return true;
        }

        if (normalized is "ff" or "mozilla-firefox" or "mozillafirefox")
        {
            browser = BrowserSpecs[2];
            return true;
        }

        return BrowserSpecs.Any(item =>
            item.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || item.ProcessName.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || item.Executable.Equals(normalized, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileNameWithoutExtension(item.Executable).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private ScriptCommandResult ExecuteBrowserAddressCommand(IReadOnlyList<string> args, string? selectedHandle)
    {
        RequireArgs(args, 2, "browser go requires a URL.");
        var url = JoinArgs(args, 1) ?? string.Empty;

        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
        }

        if (!_automation.SendKeys(new KeyInputRequest("CTRL+L"), out var keyError))
        {
            throw FromError(keyError, "browser_address_failed");
        }

        if (!_automation.SendText(new TextInputRequest(url), out var textError))
        {
            throw FromError(textError, "browser_address_failed");
        }

        if (!_automation.SendKeys(new KeyInputRequest("ENTER"), out var enterError))
        {
            throw FromError(enterError, "browser_address_failed");
        }

        return new ScriptCommandResult(new { navigated = true, url });
    }

    private ScriptCommandResult ExecuteBrowserKeyCommand(string keys, string? selectedHandle, string action)
    {
        if (selectedHandle is not null)
        {
            _automation.FocusWindow(selectedHandle, out _);
        }

        if (!_automation.SendKeys(new KeyInputRequest(keys), out var error))
        {
            throw FromError(error, $"browser_{action.Replace("-", "_", StringComparison.Ordinal)}_failed");
        }

        return new ScriptCommandResult(new { sent = true, action, keys });
    }

    private static BrowserSelectorRequest ParseBrowserSelectorRequest(IReadOnlyList<string> args, string message)
    {
        RequireArgs(args, 2, message);
        var (usingValue, selector, index) = ReadBrowserSelector(args);
        var timeoutMs = args.Count > index ? ParseInt(args[index], "timeoutMs") : 5000;
        return new BrowserSelectorRequest(usingValue, selector, TimeoutMs: timeoutMs);
    }

    private static (string Using, string Selector, int NextIndex) ReadBrowserSelector(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "browser selector syntax is: <using> <selector>.");
        return (args[0], args[1], 2);
    }

    private static bool IsBrowserTextMatch(string value)
    {
        return value.Equals("contains", StringComparison.OrdinalIgnoreCase)
            || value.Equals("exact", StringComparison.OrdinalIgnoreCase)
            || value.Equals("startswith", StringComparison.OrdinalIgnoreCase)
            || value.Equals("starts-with", StringComparison.OrdinalIgnoreCase)
            || value.Equals("endswith", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ends-with", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrowserSelectorStrategy(string value)
    {
        return value.Equals("css", StringComparison.OrdinalIgnoreCase)
            || value.Equals("cssSelector", StringComparison.OrdinalIgnoreCase)
            || value.Equals("selector", StringComparison.OrdinalIgnoreCase)
            || value.Equals("xpath", StringComparison.OrdinalIgnoreCase)
            || value.Equals("id", StringComparison.OrdinalIgnoreCase)
            || value.Equals("name", StringComparison.OrdinalIgnoreCase)
            || value.Equals("tag", StringComparison.OrdinalIgnoreCase)
            || value.Equals("tagName", StringComparison.OrdinalIgnoreCase)
            || value.Equals("class", StringComparison.OrdinalIgnoreCase)
            || value.Equals("className", StringComparison.OrdinalIgnoreCase)
            || value.Equals("link", StringComparison.OrdinalIgnoreCase)
            || value.Equals("linkText", StringComparison.OrdinalIgnoreCase)
            || value.Equals("partialLink", StringComparison.OrdinalIgnoreCase)
            || value.Equals("partialLinkText", StringComparison.OrdinalIgnoreCase);
    }
}
