using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;
using Xunit;

namespace Slasher.Tests;

public sealed class ScriptRunServiceTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly ScriptRunService _service;
    private readonly AutomationRunArtifactStore _artifactStore;

    public ScriptRunServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "slasher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        var environment = new TestHostEnvironment(_workspaceRoot);
        _artifactStore = new AutomationRunArtifactStore(environment);
        _service = new ScriptRunService(
            new WindowsAutomationService(),
            new BrowserAutomationService(),
            new FileSystemAutomationService(),
            new ClipboardService(),
            _artifactStore,
            environment);
    }

    [Fact]
    public async Task RunAsync_ExecutesVariablesArraysLoopsAndTryCatch()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "prepare variables"
            set message hello
            array items alpha beta
            push items gamma
            length items as itemCount
            assert value "${itemCount}" == 3

            test step "loop items"
            set seen 0
            foreach item in items
            add seen
            log "item ${iteration}: ${item}"
            endforeach
            assert value "${seen}" == 3

            test step "recover expected failure"
            try
            fail "expected failure for catch smoke"
            catch e
            log "caught ${e.code}: ${e.message}"
            finally
            log "cleanup"
            endtry
            assert variable exists error.code
            """,
            Name: "unit-language-smoke"),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        Assert.Equal(AutomationRunStatus.Passed, response.Run.Status);
        Assert.Null(response.Error);
        Assert.Contains(response.Events, item => item.Step == "prepare variables");
        Assert.Contains(response.Events, item => item.Logs.Any(log => log.Message.Contains("caught explicit_failure", StringComparison.Ordinal)));
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, response.Run.Artifacts.Run)));
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, response.Run.Artifacts.Events)));
    }

    [Fact]
    public async Task RunAsync_ReturnsStructuredAssertionFailure()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "fail assertion"
            set actual 1
            assert value "${actual}" == 2
            """,
            Name: "unit-assertion-failure"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.NotNull(response.Error);
        Assert.Equal("assertion_failed", response.Error!.Code);
        Assert.Equal("assert.value", response.Error.Action);
        Assert.Equal("inline-script", response.Error.Source?.File);
        Assert.Equal(3, response.Error.Source?.Line);
        Assert.NotNull(response.Error.Expected);
        Assert.NotNull(response.Error.Actual);
    }

    [Fact]
    public async Task RunAsync_PreservesWindowsPathsInQuotedStrings()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            set path "C:\Program Files\dotnet\dotnet.exe"
            log "${path}"
            """,
            Name: "unit-windows-path-string"),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        Assert.Contains(response.Events, item =>
            item.Logs.Any(log => log.Message == @"C:\Program Files\dotnet\dotnet.exe"));
    }

    [Fact]
    public async Task RunAsync_RecordsAgentNotesAndAttachments()
    {
        var attachmentPath = Path.Combine(_workspaceRoot, "expected.txt");
        await File.WriteAllTextAsync(attachmentPath, "attached evidence");

        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "collect evidence"
            agent note "important observation for AI"
            test attach "expected.txt" as expected-output
            """,
            Name: "unit-observability-evidence"),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        var noteEvent = Assert.Single(response.Events, item => item.Action == "agent.note");
        Assert.Contains(noteEvent.Logs, item =>
            item.Level == "note"
            && item.Source == "agent"
            && item.Message == "important observation for AI");

        var attachEvent = Assert.Single(response.Events, item => item.Action == "test.attach");
        var evidence = Assert.Single(attachEvent.Evidence);
        Assert.Equal("attachment", evidence.Kind);
        Assert.Equal("expected-output", evidence.Role);
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, evidence.Path)));

        var logPath = Path.Combine(_workspaceRoot, response.Run.Artifacts.Logs, "script.log");
        Assert.True(File.Exists(logPath));
        Assert.Equal(Path.Combine(response.Run.Artifacts.Logs, "script.log"), response.Run.Artifacts.ScriptLog);
        var logText = await File.ReadAllTextAsync(logPath);
        Assert.Contains("important observation for AI", logText);
        Assert.True(_artifactStore.TryReadScriptLog(response.Run.RunId, out var storedLog));
        Assert.Contains("important observation for AI", storedLog);

        var summary = await File.ReadAllTextAsync(Path.Combine(_workspaceRoot, response.Run.Artifacts.Summary));
        Assert.Contains("attachment:expected-output", summary);

        var reportPath = Path.Combine(_workspaceRoot, response.Run.Artifacts.Report);
        Assert.True(File.Exists(reportPath));
        var html = await File.ReadAllTextAsync(reportPath);
        Assert.Contains("Slasher Run", html);
        Assert.Contains("agent.note", html);
        Assert.Contains("test.attach", html);
        Assert.Contains("expected-output", html);
        Assert.Contains("script.log", html);
        Assert.Contains("/artifacts/raw?path=", html);
    }

    [Fact]
    public async Task ListRuns_ReturnsRecentCompletedRuns()
    {
        var first = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "first run"
            log "first"
            """,
            Name: "unit-run-list-first"),
            CancellationToken.None);
        var second = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "second run"
            log "second"
            """,
            Name: "unit-run-list-second"),
            CancellationToken.None);

        Assert.True(first.Ok);
        Assert.True(second.Ok);

        var runs = _artifactStore.ListRuns(limit: 2);
        Assert.Equal(2, runs.Count);
        Assert.Contains(runs, item => item.RunId == first.Run.RunId);
        Assert.Contains(runs, item => item.RunId == second.Run.RunId);
    }

    [Fact]
    public async Task RunAsync_ReportsMissingAttachmentWithSourceAndErrorScreenshot()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "missing attachment"
            test attach "missing.txt" as expected-output
            """,
            Name: "unit-missing-attachment"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("attachment_file_not_found", response.Error?.Code);
        Assert.Equal("test.attach", response.Error?.Action);
        Assert.Equal(2, response.Error?.Source?.Line);
        Assert.NotNull(response.Error?.Details);
        Assert.True(response.Error!.Details!.ContainsKey("resolvedPath"));
        Assert.True(response.Error.Details.ContainsKey("diagnostics"));
    }

    [Fact]
    public async Task RunAsync_CapturesBeforeAndAfterEachStepWhenPolicyRequestsIt()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "capture policy"
            log "capture around this event"
            """,
            Name: "unit-step-capture-policy",
            CapturePolicy: new CapturePolicy(
                CaptureAfterEachStep: true,
                CaptureBeforeEachStep: true,
                CaptureTarget: "full")),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        var logEvent = Assert.Single(response.Events, item => item.Action == "log");
        Assert.Contains(logEvent.Evidence, item => item.Kind == "screenshot" && item.Role == "before");
        Assert.Contains(logEvent.Evidence, item => item.Kind == "screenshot" && item.Role == "after");
        Assert.All(logEvent.Evidence, item => Assert.True(File.Exists(Path.Combine(_workspaceRoot, item.Path))));
    }

    [Fact]
    public async Task RunAsync_ReportsScreenContainsAsOcrPlaceholderWithEvidence()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            test step "screen placeholder"
            assert screen contains "hello" full
            """,
            Name: "unit-screen-contains-placeholder"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("screen_contains_unavailable", response.Error?.Code);
        Assert.Equal("assert.screen", response.Error?.Action);
        Assert.NotNull(response.Error?.Expected);
        Assert.NotNull(response.Error?.Actual);
        Assert.NotNull(response.Error?.Evidence);
        Assert.Contains(response.Error!.Evidence!, item => item.Kind == "screenshot" && item.Role == "error-preview");

        var reportPath = Path.Combine(_workspaceRoot, response.Run.Artifacts.Report);
        Assert.True(File.Exists(reportPath));
        var html = await File.ReadAllTextAsync(reportPath);
        Assert.Contains("class=\"shot\"", html);
        Assert.Contains("0002-error-preview.bmp", html);
    }

    [Fact]
    public async Task RunFileAsync_ReportsIncludedFileFunctionFailuresWithCallStack()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        var lib = Path.Combine(scripts, "lib");
        Directory.CreateDirectory(lib);

        var helperPath = Path.Combine(lib, "helper.slasher");
        await File.WriteAllTextAsync(helperPath,
            """
            function explode
            fail "helper failed"
            endfunction
            """);

        var mainPath = Path.Combine(scripts, "main.slasher");
        await File.WriteAllTextAsync(mainPath,
            """
            include "lib/helper.slasher"
            call explode
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "main.slasher"),
            Name: "unit-include-stack"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("explicit_failure", response.Error?.Code);
        Assert.Equal(Path.Combine("scripts", "lib", "helper.slasher"), response.Error?.Source?.File);
        Assert.Equal("explode", response.Error?.Source?.Function);
        Assert.NotNull(response.Error?.Source?.Stack);

        var frame = response.Error!.Source!.Stack!.FirstOrDefault(item =>
            item.File == Path.Combine("scripts", "main.slasher")
            && item.Line == 2
            && item.Command == "call explode");
        Assert.NotNull(frame);
        Assert.Null(frame!.Function);
    }

    [Fact]
    public async Task CheckAsync_ReturnsParsedLinesWithoutExecutingScript()
    {
        var response = await _service.CheckAsync(new ScriptCheckRequest(
            """
            test step "check only"
            set value hello
            if "${value}" == "hello"
            log "would run"
            endif
            """),
            CancellationToken.None);

        Assert.True(response.Ok);
        Assert.Empty(response.Diagnostics);
        Assert.Equal(5, response.Lines.Count);
        Assert.Equal("inline-script", response.Lines[0].SourceFile);
        Assert.Equal("test step \"check only\"", response.Lines[0].Command);
        Assert.False(Directory.Exists(Path.Combine(_workspaceRoot, "artifacts", "runs")));
    }

    [Fact]
    public async Task CheckAsync_ReportsUnclosedBlocks()
    {
        var response = await _service.CheckAsync(new ScriptCheckRequest(
            """
            test step "bad block"
            if "a" == "a"
            log "missing endif"
            """),
            CancellationToken.None);

        Assert.False(response.Ok);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal("block_not_closed", diagnostic.Code);
        Assert.Equal("inline-script", diagnostic.File);
        Assert.Equal(2, diagnostic.Line);
        Assert.Equal("if \"a\" == \"a\"", diagnostic.Command);
    }

    [Fact]
    public async Task CheckAsync_CanParseScriptFileWithInclude()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "lib.slasher"),
            """
            function greet name
            return "hello ${name}"
            endfunction
            """);
        await File.WriteAllTextAsync(Path.Combine(scripts, "main.slasher"),
            """
            include "lib.slasher"
            call greet world as result
            """);

        var response = await _service.CheckAsync(new ScriptCheckRequest(Path: Path.Combine("scripts", "main.slasher")), CancellationToken.None);

        Assert.True(response.Ok);
        Assert.Empty(response.Diagnostics);
        Assert.Contains(response.Lines, line => line.SourceFile == Path.Combine("scripts", "lib.slasher") && line.Function == "greet");
        Assert.Contains(response.Lines, line => line.SourceFile == Path.Combine("scripts", "main.slasher") && line.Command == "call greet world as result");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Slasher.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
