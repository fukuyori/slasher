using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using Slasher.Api;

namespace Slasher.Windows;

public sealed class BrowserAutomationService : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IWebDriver> _drivers = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentSessionId;

    public BrowserSessionResponse Open(BrowserOpenRequest request)
    {
        var browser = NormalizeBrowser(request.Browser);
        var driver = CreateDriver(browser, request.Headless, request.DownloadDirectory);
        driver.Navigate().GoToUrl(request.Url);

        var sessionId = Guid.NewGuid().ToString("N");
        lock (_gate)
        {
            _drivers[sessionId] = driver;
            _currentSessionId = sessionId;
        }

        return ReadSession(sessionId, browser, driver);
    }

    public BrowserSessionResponse Navigate(BrowserNavigateRequest request)
    {
        var (sessionId, driver) = GetDriver(request.SessionId);
        driver.Navigate().GoToUrl(request.Url);
        return ReadSession(sessionId, "webdriver", driver);
    }

    public BrowserSessionResponse Current(string? sessionId)
    {
        var resolved = GetDriver(sessionId);
        return ReadSession(resolved.SessionId, "webdriver", resolved.Driver);
    }

    public BrowserValueResponse Title(string? sessionId)
    {
        var (_, driver) = GetDriver(sessionId);
        return new BrowserValueResponse(driver.Title);
    }

    public BrowserValueResponse Url(string? sessionId)
    {
        var (_, driver) = GetDriver(sessionId);
        return new BrowserValueResponse(driver.Url);
    }

    public BrowserElementResponse Find(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Click(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        element.Click();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Hover(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        new Actions(driver).MoveToElement(element).Perform();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse DoubleClick(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        new Actions(driver).DoubleClick(element).Perform();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse RightClick(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        new Actions(driver).ContextClick(element).Perform();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Type(BrowserTypeRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        if (request.Clear)
        {
            element.Clear();
        }

        element.SendKeys(request.Text);
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Press(BrowserKeyRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var keys = ToSeleniumKeys(request.Keys);
        if (!string.IsNullOrWhiteSpace(request.Using) && !string.IsNullOrWhiteSpace(request.Value))
        {
            var by = ToBy(request.Using, request.Value);
            var element = WaitForElement(driver, by, request.TimeoutMs);
            element.SendKeys(keys);
            return ToResponse(element, request.Using, request.Value, found: true);
        }

        new Actions(driver).SendKeys(keys).Perform();
        return new BrowserElementResponse(true, "active", string.Empty, null, null, null, null, null);
    }

    public BrowserElementResponse UploadFile(BrowserUploadFileRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.Path));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Upload file was not found: {path}");
        }

        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        element.SendKeys(path);
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Drag(BrowserDragRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var sourceBy = ToBy(request.Using, request.Value);
        var targetBy = ToBy(request.TargetUsing, request.TargetValue);
        var source = WaitForElement(driver, sourceBy, request.TimeoutMs);
        var target = WaitForElement(driver, targetBy, request.TimeoutMs);
        new Actions(driver).DragAndDrop(source, target).Perform();
        return ToResponse(target, request.TargetUsing, request.TargetValue, found: true);
    }

    public IReadOnlyList<BrowserSelectedOptionInfo> SelectOption(BrowserSelectOptionRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        var select = new SelectElement(element);
        if (request.Clear && select.IsMultiple)
        {
            select.DeselectAll();
        }

        switch (request.SelectBy.Trim().ToLowerInvariant())
        {
            case "text":
            case "label":
                select.SelectByText(request.Option);
                break;
            case "value":
                select.SelectByValue(request.Option);
                break;
            case "index":
                select.SelectByIndex(int.Parse(request.Option, System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException("SelectBy must be text, value, or index.");
        }

        return SelectedOptions(select);
    }

    public IReadOnlyList<BrowserSelectedOptionInfo> SelectedOptions(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        return SelectedOptions(new SelectElement(element));
    }

    public BrowserElementResponse Clear(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        element.Clear();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserElementResponse Submit(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        element.Submit();
        return ToResponse(element, request.Using, request.Value, found: true);
    }

    public BrowserValueResponse Text(BrowserSelectorRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        return new BrowserValueResponse(element.Text);
    }

    public BrowserValueResponse Attribute(BrowserAttributeRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var element = WaitForElement(driver, by, request.TimeoutMs);
        return new BrowserValueResponse(element.GetAttribute(request.Attribute) ?? string.Empty);
    }

    public BrowserElementResponse Wait(BrowserSelectorRequest request)
    {
        return Find(request);
    }

    public BrowserValueResponse WaitText(BrowserWaitTextRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        var by = ToBy(request.Using, request.Value);
        var expected = request.Text ?? string.Empty;
        var match = request.Match.Trim().ToLowerInvariant();
        var wait = CreateWait(driver, request.TimeoutMs);
        var actual = wait.Until(current =>
        {
            var element = current.FindElement(by);
            var text = element.Text;
            var matches = match switch
            {
                "exact" or "==" or "=" => text.Equals(expected, StringComparison.Ordinal),
                "startswith" or "starts-with" => text.StartsWith(expected, StringComparison.Ordinal),
                "endswith" or "ends-with" => text.EndsWith(expected, StringComparison.Ordinal),
                _ => text.Contains(expected, StringComparison.Ordinal)
            };
            return matches ? text : null;
        });
        return new BrowserValueResponse(actual ?? string.Empty);
    }

    public BrowserScriptResponse ExecuteScript(BrowserScriptRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        if (driver is not IJavaScriptExecutor executor)
        {
            throw new InvalidOperationException("Current browser driver does not support JavaScript execution.");
        }

        return new BrowserScriptResponse(executor.ExecuteScript(request.Script));
    }

    public IReadOnlyList<BrowserCookieInfo> Cookies(string? sessionId)
    {
        var (_, driver) = GetDriver(sessionId);
        return driver.Manage().Cookies.AllCookies
            .Select(cookie => new BrowserCookieInfo(
                cookie.Name,
                cookie.Value,
                cookie.Domain,
                cookie.Path,
                cookie.Expiry,
                cookie.Secure,
                cookie.IsHttpOnly,
                cookie.SameSite))
            .ToArray();
    }

    public BrowserValueResponse GetStorage(string storage, BrowserStorageRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        if (driver is not IJavaScriptExecutor executor)
        {
            throw new InvalidOperationException("Current browser driver does not support JavaScript execution.");
        }

        var value = executor.ExecuteScript($"return window.{StorageName(storage)}.getItem(arguments[0]);", request.Key);
        return new BrowserValueResponse(value?.ToString() ?? string.Empty);
    }

    public BrowserValueResponse SetStorage(string storage, BrowserStorageRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        if (driver is not IJavaScriptExecutor executor)
        {
            throw new InvalidOperationException("Current browser driver does not support JavaScript execution.");
        }

        executor.ExecuteScript($"window.{StorageName(storage)}.setItem(arguments[0], arguments[1]);", request.Key, request.Value ?? string.Empty);
        return new BrowserValueResponse(request.Value ?? string.Empty);
    }

    public ScreenshotResponse Screenshot(BrowserScreenshotRequest request)
    {
        var (_, driver) = GetDriver(request.SessionId);
        if (driver is not ITakesScreenshot screenshotDriver)
        {
            throw new InvalidOperationException("Current browser driver does not support screenshots.");
        }

        var screenshot = screenshotDriver.GetScreenshot();
        var bytes = screenshot.AsByteArray;
        var (width, height) = ReadPngSize(bytes);
        return new ScreenshotResponse("image/png", Convert.ToBase64String(bytes), width, height);
    }

    public IReadOnlyList<BrowserLinkInfo> Links(string? sessionId)
    {
        var (_, driver) = GetDriver(sessionId);
        return driver.FindElements(By.TagName("a"))
            .Select(element => new BrowserLinkInfo(
                element.Text,
                element.GetAttribute("href") ?? string.Empty,
                EmptyToNull(element.GetAttribute("target")),
                EmptyToNull(element.GetAttribute("id")),
                EmptyToNull(element.GetAttribute("class"))))
            .ToArray();
    }

    public IReadOnlyList<BrowserWindowHandleInfo> Windows(string? sessionId)
    {
        var (_, driver) = GetDriver(sessionId);
        var current = driver.CurrentWindowHandle;
        return driver.WindowHandles
            .Select((handle, index) => new BrowserWindowHandleInfo(handle, index, handle == current))
            .ToArray();
    }

    public BrowserSessionResponse NewWindow(BrowserNewWindowRequest request)
    {
        var (sessionId, driver) = GetDriver(request.SessionId);
        var type = request.Type.Equals("window", StringComparison.OrdinalIgnoreCase)
            ? WindowType.Window
            : WindowType.Tab;
        driver.SwitchTo().NewWindow(type);
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            driver.Navigate().GoToUrl(request.Url);
        }

        return ReadSession(sessionId, "webdriver", driver);
    }

    public BrowserSessionResponse SwitchWindow(BrowserSwitchWindowRequest request)
    {
        var (sessionId, driver) = GetDriver(request.SessionId);
        string handle;
        if (!string.IsNullOrWhiteSpace(request.Handle))
        {
            handle = request.Handle;
        }
        else if (request.Index is { } index
            && index >= 0
            && index < driver.WindowHandles.Count)
        {
            handle = driver.WindowHandles[index];
        }
        else
        {
            throw new InvalidOperationException("Browser switch requires a valid window handle or index.");
        }

        driver.SwitchTo().Window(handle);
        return ReadSession(sessionId, "webdriver", driver);
    }

    public BrowserSessionResponse CloseCurrentWindow(string? sessionId)
    {
        var (resolvedSessionId, driver) = GetDriver(sessionId);
        driver.Close();
        if (driver.WindowHandles.Count > 0)
        {
            driver.SwitchTo().Window(driver.WindowHandles[0]);
            return ReadSession(resolvedSessionId, "webdriver", driver);
        }

        lock (_gate)
        {
            _drivers.Remove(resolvedSessionId);
            if (string.Equals(_currentSessionId, resolvedSessionId, StringComparison.OrdinalIgnoreCase))
            {
                _currentSessionId = _drivers.Keys.LastOrDefault();
            }
        }

        return new BrowserSessionResponse(resolvedSessionId, "webdriver", string.Empty, string.Empty);
    }

    public BrowserDownloadInfo WaitForDownload(BrowserDownloadWaitRequest request)
    {
        var directory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.Directory));
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"Download directory was not found: {directory}");
        }

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(1, request.TimeoutMs));
        FileInfo? candidate = null;
        long candidateSize = -1;
        DateTimeOffset stableSince = DateTimeOffset.MinValue;
        while (DateTimeOffset.UtcNow < deadline)
        {
            candidate = Directory.EnumerateFiles(directory, request.Pattern)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists && !IsTemporaryDownload(file.Name))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (candidate is not null)
            {
                candidate.Refresh();
                if (candidate.Length == candidateSize)
                {
                    if (stableSince == DateTimeOffset.MinValue)
                    {
                        stableSince = DateTimeOffset.UtcNow;
                    }

                    if ((DateTimeOffset.UtcNow - stableSince).TotalMilliseconds >= Math.Max(0, request.StableMs))
                    {
                        return ToDownloadInfo(candidate);
                    }
                }
                else
                {
                    candidateSize = candidate.Length;
                    stableSince = DateTimeOffset.MinValue;
                }
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException($"No completed download matching '{request.Pattern}' appeared in '{directory}' before timeout.");
    }

    public IReadOnlyList<BrowserLogInfo> Logs(string? sessionId, string? type)
    {
        var (_, driver) = GetDriver(sessionId);
        var logType = string.IsNullOrWhiteSpace(type) ? "browser" : type.Trim();
        try
        {
            return driver.Manage().Logs.GetLog(logType)
                .Select(entry => new BrowserLogInfo(
                    entry.Timestamp,
                    entry.Level.ToString(),
                    entry.Message))
                .ToArray();
        }
        catch (WebDriverException ex)
        {
            throw new InvalidOperationException($"Browser log type '{logType}' is not available for the current driver: {ex.Message}", ex);
        }
    }

    public bool Close(string? sessionId, out Slasher.Api.ErrorResponse? error)
    {
        error = null;
        IWebDriver driver;
        string resolved;
        lock (_gate)
        {
            resolved = ResolveSessionId(sessionId);
            if (!_drivers.Remove(resolved, out driver!))
            {
                error = new Slasher.Api.ErrorResponse("browser_session_not_found", $"Browser session '{resolved}' was not found.");
                return false;
            }

            if (string.Equals(_currentSessionId, resolved, StringComparison.OrdinalIgnoreCase))
            {
                _currentSessionId = _drivers.Keys.LastOrDefault();
            }
        }

        driver.Quit();
        driver.Dispose();
        return true;
    }

    public void Dispose()
    {
        List<IWebDriver> drivers;
        lock (_gate)
        {
            drivers = _drivers.Values.ToList();
            _drivers.Clear();
            _currentSessionId = null;
        }

        foreach (var driver in drivers)
        {
            try
            {
                driver.Quit();
                driver.Dispose();
            }
            catch (WebDriverException)
            {
            }
        }
    }

    private static IWebDriver CreateDriver(string browser, bool headless, string? downloadDirectory)
    {
        return browser switch
        {
            "edge" => CreateEdgeDriver(headless, downloadDirectory),
            "chrome" => CreateChromeDriver(headless, downloadDirectory),
            "firefox" => CreateFirefoxDriver(headless, downloadDirectory),
            _ => throw new InvalidOperationException($"Unsupported browser '{browser}'.")
        };
    }

    private static IWebDriver CreateEdgeDriver(bool headless, string? downloadDirectory)
    {
        var options = new EdgeOptions();
        if (headless)
        {
            options.AddArgument("--headless=new");
        }

        options.SetLoggingPreference(LogType.Browser, OpenQA.Selenium.LogLevel.All);
        AddChromiumDownloadPreferences(options, downloadDirectory);
        var driverDirectory = DriverDirectory("msedgedriver.exe");
        var service = driverDirectory is null
            ? EdgeDriverService.CreateDefaultService()
            : EdgeDriverService.CreateDefaultService(driverDirectory);
        service.HideCommandPromptWindow = true;
        return new EdgeDriver(service, options);
    }

    private static IWebDriver CreateChromeDriver(bool headless, string? downloadDirectory)
    {
        var options = new ChromeOptions();
        if (headless)
        {
            options.AddArgument("--headless=new");
        }

        options.SetLoggingPreference(LogType.Browser, OpenQA.Selenium.LogLevel.All);
        AddChromiumDownloadPreferences(options, downloadDirectory);
        var driverDirectory = DriverDirectory("chromedriver.exe");
        var service = driverDirectory is null
            ? ChromeDriverService.CreateDefaultService()
            : ChromeDriverService.CreateDefaultService(driverDirectory);
        service.HideCommandPromptWindow = true;
        return new ChromeDriver(service, options);
    }

    private static IWebDriver CreateFirefoxDriver(bool headless, string? downloadDirectory)
    {
        var options = new FirefoxOptions();
        if (headless)
        {
            options.AddArgument("-headless");
        }

        if (!string.IsNullOrWhiteSpace(downloadDirectory))
        {
            var directory = NormalizeDirectory(downloadDirectory);
            options.SetPreference("browser.download.folderList", 2);
            options.SetPreference("browser.download.dir", directory);
            options.SetPreference("browser.download.useDownloadDir", true);
            options.SetPreference("browser.helperApps.neverAsk.saveToDisk", "text/plain,application/octet-stream,application/pdf,text/csv,application/json");
        }

        var driverDirectory = DriverDirectory("geckodriver.exe");
        var service = driverDirectory is null
            ? FirefoxDriverService.CreateDefaultService()
            : FirefoxDriverService.CreateDefaultService(driverDirectory);
        service.HideCommandPromptWindow = true;
        return new FirefoxDriver(service, options);
    }

    private static string NormalizeBrowser(string browser)
    {
        return browser.Trim().ToLowerInvariant() switch
        {
            "" => "edge",
            "msedge" or "microsoft-edge" or "microsoftedge" => "edge",
            "google-chrome" or "googlechrome" => "chrome",
            "ff" or "mozilla-firefox" or "mozillafirefox" => "firefox",
            "edge" or "chrome" or "firefox" => browser.Trim().ToLowerInvariant(),
            _ => throw new InvalidOperationException("Browser must be edge, chrome, or firefox.")
        };
    }

    private (string SessionId, IWebDriver Driver) GetDriver(string? sessionId)
    {
        lock (_gate)
        {
            var resolved = ResolveSessionId(sessionId);
            if (!_drivers.TryGetValue(resolved, out var driver))
            {
                throw new InvalidOperationException($"Browser session '{resolved}' was not found.");
            }

            _currentSessionId = resolved;
            return (resolved, driver);
        }
    }

    private string ResolveSessionId(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return _currentSessionId ?? throw new InvalidOperationException("No browser WebDriver session is active.");
    }

    private static BrowserSessionResponse ReadSession(string sessionId, string browser, IWebDriver driver)
    {
        return new BrowserSessionResponse(sessionId, browser, driver.Url, driver.Title);
    }

    private static By ToBy(string usingValue, string value)
    {
        return usingValue.Trim().ToLowerInvariant() switch
        {
            "css" or "cssselector" or "selector" => By.CssSelector(value),
            "xpath" => By.XPath(value),
            "id" => By.Id(value),
            "name" => By.Name(value),
            "tag" or "tagname" => By.TagName(value),
            "class" or "classname" => By.ClassName(value),
            "link" or "linktext" => By.LinkText(value),
            "partiallink" or "partiallinktext" => By.PartialLinkText(value),
            _ => throw new InvalidOperationException("Selector strategy must be css, xpath, id, name, tag, class, link, or partialLink.")
        };
    }

    private static IWebElement WaitForElement(IWebDriver driver, By by, int timeoutMs)
    {
        var wait = CreateWait(driver, timeoutMs);
        return wait.Until(current =>
        {
            var element = current.FindElement(by);
            return element.Displayed ? element : null;
        }) ?? throw new NoSuchElementException($"Element was not found before timeout: {by}");
    }

    private static WebDriverWait CreateWait(IWebDriver driver, int timeoutMs)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs)))
        {
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        return wait;
    }

    private static string StorageName(string storage)
    {
        return storage.Trim().ToLowerInvariant() switch
        {
            "local" or "localstorage" => "localStorage",
            "session" or "sessionstorage" => "sessionStorage",
            _ => throw new InvalidOperationException("Storage must be localStorage or sessionStorage.")
        };
    }

    private static IReadOnlyList<BrowserSelectedOptionInfo> SelectedOptions(SelectElement select)
    {
        var selected = new HashSet<IWebElement>(select.AllSelectedOptions);
        return select.Options
            .Select((option, index) => new BrowserSelectedOptionInfo(
                option.Text,
                option.GetAttribute("value") ?? string.Empty,
                index,
                selected.Contains(option)))
            .Where(option => option.Selected)
            .ToArray();
    }

    private static void AddChromiumDownloadPreferences(ChromeOptions options, string? downloadDirectory)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            return;
        }

        var directory = NormalizeDirectory(downloadDirectory);
        options.AddUserProfilePreference("download.default_directory", directory);
        options.AddUserProfilePreference("download.prompt_for_download", false);
        options.AddUserProfilePreference("download.directory_upgrade", true);
        options.AddUserProfilePreference("safebrowsing.enabled", true);
    }

    private static void AddChromiumDownloadPreferences(EdgeOptions options, string? downloadDirectory)
    {
        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            return;
        }

        var directory = NormalizeDirectory(downloadDirectory);
        options.AddUserProfilePreference("download.default_directory", directory);
        options.AddUserProfilePreference("download.prompt_for_download", false);
        options.AddUserProfilePreference("download.directory_upgrade", true);
        options.AddUserProfilePreference("safebrowsing.enabled", true);
    }

    private static string NormalizeDirectory(string directory)
    {
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directory));
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static bool IsTemporaryDownload(string name)
    {
        return name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static BrowserDownloadInfo ToDownloadInfo(FileInfo file)
    {
        return new BrowserDownloadInfo(
            file.FullName,
            file.Name,
            file.Length,
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));
    }

    private static string ToSeleniumKeys(string keys)
    {
        var parts = keys.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            return SeleniumKey(keys);
        }

        var modifiers = new List<string>();
        var regular = new List<string>();
        foreach (var part in parts)
        {
            var key = part.Trim();
            if (IsModifier(key))
            {
                modifiers.Add(SeleniumKey(key));
            }
            else
            {
                regular.Add(SeleniumKey(key));
            }
        }

        if (modifiers.Count == 0)
        {
            return string.Concat(regular);
        }

        return string.Concat(modifiers) + string.Concat(regular) + string.Concat(modifiers.Select(_ => Keys.Null));
    }

    private static bool IsModifier(string key)
    {
        return key.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
            || key.Equals("control", StringComparison.OrdinalIgnoreCase)
            || key.Equals("shift", StringComparison.OrdinalIgnoreCase)
            || key.Equals("alt", StringComparison.OrdinalIgnoreCase);
    }

    private static string SeleniumKey(string key)
    {
        return key.Trim().ToLowerInvariant() switch
        {
            "enter" or "return" => Keys.Enter,
            "tab" => Keys.Tab,
            "escape" or "esc" => Keys.Escape,
            "backspace" => Keys.Backspace,
            "delete" or "del" => Keys.Delete,
            "space" => Keys.Space,
            "ctrl" or "control" => Keys.Control,
            "shift" => Keys.Shift,
            "alt" => Keys.Alt,
            "arrowleft" or "left" => Keys.Left,
            "arrowright" or "right" => Keys.Right,
            "arrowup" or "up" => Keys.Up,
            "arrowdown" or "down" => Keys.Down,
            "home" => Keys.Home,
            "end" => Keys.End,
            "pageup" or "pgup" => Keys.PageUp,
            "pagedown" or "pgdn" => Keys.PageDown,
            "f1" => Keys.F1,
            "f2" => Keys.F2,
            "f3" => Keys.F3,
            "f4" => Keys.F4,
            "f5" => Keys.F5,
            "f6" => Keys.F6,
            "f7" => Keys.F7,
            "f8" => Keys.F8,
            "f9" => Keys.F9,
            "f10" => Keys.F10,
            "f11" => Keys.F11,
            "f12" => Keys.F12,
            _ => key
        };
    }

    private static (int Width, int Height) ReadPngSize(byte[] bytes)
    {
        if (bytes.Length >= 24
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4e
            && bytes[3] == 0x47)
        {
            var width = ReadBigEndianInt32(bytes, 16);
            var height = ReadBigEndianInt32(bytes, 20);
            return (width, height);
        }

        return (0, 0);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24)
            | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? DriverDirectory(string driverName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDirectory, driverName)))
        {
            return baseDirectory;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDirectory, driverName)))
        {
            return currentDirectory;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, driverName)))
                {
                    return directory;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }

    private static BrowserElementResponse ToResponse(IWebElement element, string usingValue, string value, bool found)
    {
        var location = element.Location;
        var size = element.Size;
        return new BrowserElementResponse(
            found,
            usingValue,
            value,
            element.TagName,
            element.Text,
            new Rect(location.X, location.Y, size.Width, size.Height),
            element.Displayed,
            element.Enabled);
    }
}
