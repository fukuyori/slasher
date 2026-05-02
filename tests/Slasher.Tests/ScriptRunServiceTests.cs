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

    [Fact]
    public async Task CheckAsync_CanCheckInlineNumadoraScript()
    {
        var response = await _service.CheckAsync(new ScriptCheckRequest(
            """
            FUNC main()
                Print("hello from Numadora")
            END
            """,
            Language: "numadora"),
            CancellationToken.None);

        Assert.True(response.Ok, AssertDiagnostics(response.Diagnostics));
        Assert.Equal("numadora", response.Language);
        Assert.Empty(response.Diagnostics);
        Assert.Equal(3, response.Lines.Count);
        Assert.All(response.Lines, line => Assert.EndsWith(".numa", line.SourceFile));
        Assert.Empty(response.RequiredCapabilities ?? []);
    }

    [Fact]
    public async Task CheckAsync_DispatchesNumaFilesToNumadora()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "hello.numa"),
            """
            FUNC main()
                Print("hello from file")
            END
            """);

        var response = await _service.CheckAsync(new ScriptCheckRequest(Path: Path.Combine("scripts", "hello.numa")), CancellationToken.None);

        Assert.True(response.Ok, AssertDiagnostics(response.Diagnostics));
        Assert.Equal("numadora", response.Language);
        Assert.Empty(response.Diagnostics);
        Assert.Empty(response.Lines);
        Assert.Empty(response.RequiredCapabilities ?? []);
    }

    [Fact]
    public async Task CheckAsync_ReportsNumadoraRequiredCapabilities()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "capabilities.numa"),
            """
            IMPORT slasher_app AS app
            IMPORT slasher_window AS win
            IMPORT slasher_input AS input
            IMPORT slasher_screen AS screen
            IMPORT slasher_element AS element
            IMPORT slasher_browser AS browser
            IMPORT slasher_io AS io
            IMPORT slasher_test AS test

            FUNC main()
                io.Step("open notepad")
                LET handle := app.Start("notepad.exe")
                LET title := win.WaitForTitle("Notepad", 10000)
                win.Focus(handle)
                input.Text("hello")
                input.Keys("CTRL+S")
                input.Mouse("move", 1, 1, "left")
                input.Wheel(1, 1, 120)
                input.Drag(1, 1, 2, 2, "left", 1, 1)
                input.ContextMenu(1, 1, 1)
                screen.Capture("full", 320, 180)
                element.Exists("foreground", "Notepad", "-", -1, "contains", 8, 1)
                element.Find("foreground", "Notepad", "-", -1, "contains", 8, 20)
                element.ReadText("foreground", "Notepad", "-", -1, "contains", 8, 1)
                element.Tree("foreground", 2, 20)
                browser.Current("-")
                browser.Title("-")
                browser.Url("-")
                browser.Locate("css", "body", 5000, "-")
                browser.DomText("css", "body", 5000, "-")
                browser.Attribute("css", "body", "class", 5000, "-")
                browser.Screenshot("-")
                browser.Links("-")
                browser.Windows("-")
                test.AssertForegroundTitle("contains", title)
            END
            """);

        var response = await _service.CheckAsync(new ScriptCheckRequest(
            Path: Path.Combine("scripts", "capabilities.numa")),
            CancellationToken.None);

        Assert.True(response.Ok, AssertDiagnostics(response.Diagnostics));
        var capabilities = response.RequiredCapabilities ?? [];
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_app"
            && item.Function == "Start"
            && item.CapabilityClass == "Process/app"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "Text"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "Keys"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "Mouse"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "Wheel"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "Drag"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_input"
            && item.Function == "ContextMenu"
            && item.CapabilityClass == "User-input"
            && item.Profile == "interactive");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_screen"
            && item.Function == "Capture"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_element"
            && item.Function == "Exists"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_element"
            && item.Function == "Find"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_element"
            && item.Function == "ReadText"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_element"
            && item.Function == "Tree"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_browser"
            && item.Function == "Current"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_browser"
            && item.Function == "DomText"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_browser"
            && item.Function == "Screenshot"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
        Assert.Contains(capabilities, item =>
            item.Module == "slasher_test"
            && item.Function == "AssertForegroundTitle"
            && item.CapabilityClass == "Observe"
            && item.Profile == "observe");
    }

    [Fact]
    public async Task CheckAsync_ReturnsNumadoraDiagnostics()
    {
        var response = await _service.CheckAsync(new ScriptCheckRequest(
            """
            FUNC main(
                Print("missing close")
            END
            """,
            Language: "numadora"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("numadora", response.Language);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal("numadora_check_failed", diagnostic.Code);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Message));
        Assert.NotNull(diagnostic.Details);
        Assert.True(diagnostic.Details!.ContainsKey("exitCode"));
        Assert.True(diagnostic.Details.ContainsKey("stdout"));
        Assert.True(diagnostic.Details.ContainsKey("stderr"));
        Assert.True(diagnostic.Details.ContainsKey("raw"));
    }

    [Theory]
    [InlineData(
        """
        IMPORT missing_module AS missing

        FUNC main()
            missing.Call()
        END
        """,
        "numadora_import_failed",
        "failed to read")]
    [InlineData(
        """
        FUNC main()
            MissingCall()
        END
        """,
        "numadora_unknown_symbol",
        "undefined function")]
    [InlineData(
        """
        FUNC main()
            LET value: Int := "text"
        END
        """,
        "numadora_type_mismatch",
        "type mismatch")]
    public async Task CheckAsync_ClassifiesRepresentativeNumadoraDiagnostics(
        string script,
        string expectedCode,
        string expectedMessage)
    {
        var response = await _service.CheckAsync(new ScriptCheckRequest(
            script,
            Language: "numadora"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("numadora", response.Language);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Contains(expectedMessage, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(diagnostic.Details);
        Assert.True(diagnostic.Details!.ContainsKey("raw"));
    }

    [Fact]
    public async Task RunAsync_NumadoraCanRunPureScriptAndCaptureStdout()
    {
        var response = await _service.RunAsync(new ScriptRunRequest(
            """
            FUNC main()
                Print("preflight")
            END
            """,
            Name: "unit-numadora-run-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false),
            Language: "numadora"),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        Assert.Equal(AutomationRunStatus.Passed, response.Run.Status);
        Assert.Null(response.Error);
        Assert.NotNull(response.Run.Metadata);
        Assert.Equal("local-test", response.Run.Metadata!["purpose"]);
        Assert.True(response.Run.Metadata.ContainsKey("lineage"));
        var runEvent = Assert.Single(response.Events);
        Assert.Equal("numadora.run", runEvent.Action);
        Assert.Contains(runEvent.Logs, item => item.Source == "numadora" && item.Message == "preflight");
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, response.Run.Artifacts.Run)));
        Assert.True(File.Exists(Path.Combine(_workspaceRoot, response.Run.Artifacts.Events)));
    }

    [Fact]
    public async Task RunFileAsync_NumaFileCanRunPureScriptAndCaptureStdout()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "run-preflight.numa"),
            """
            FUNC main()
                Print("preflight file")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "run-preflight.numa"),
            Name: "unit-numadora-file-run-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        Assert.Equal(AutomationRunStatus.Passed, response.Run.Status);
        Assert.Null(response.Error);
        Assert.Equal(Path.Combine("scripts", "run-preflight.numa"), response.Run.EntryPoint);
        var runEvent = Assert.Single(response.Events);
        Assert.Equal("numadora.run", runEvent.Action);
        Assert.Contains(runEvent.Logs, item => item.Source == "numadora" && item.Message == "preflight file");
    }

    [Fact]
    public async Task RunFileAsync_NumaSlasherIoCallsAreCapturedAsHostCalls()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "io-host-calls.numa"),
            """
            IMPORT slasher_io AS io

            FUNC main()
                io.Step("phase one")
                io.Log("ordinary log")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "io-host-calls.numa"),
            Name: "unit-numadora-io-host-calls",
            CapturePolicy: new CapturePolicy(CaptureOnError: false),
            Purpose: "lineage-smoke"),
            CancellationToken.None);

        Assert.True(response.Ok, response.Error?.Message);
        Assert.NotNull(response.Run.Metadata);
        Assert.Equal("lineage-smoke", response.Run.Metadata!["purpose"]);
        var lineage = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Run.Metadata["lineage"]);
        Assert.Equal("lineage-smoke", lineage["purpose"]);
        Assert.Equal(2, response.Events.Count);
        var runEvent = response.Events[0];
        Assert.Equal("numadora.run", runEvent.Action);
        Assert.Contains(runEvent.Logs, item => item.Source == "numadora.hostCall" && item.Message.Contains("slasher_io.Step", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(runEvent.Logs, item => item.Source == "numadora" && item.Message == "ordinary log");
        Assert.True(runEvent.Parameters.ContainsKey("hostCalls"));
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.Equal("slasher_io", hostCallEvent.Parameters["module"]);
        Assert.Equal("Step", hostCallEvent.Parameters["function"]);
        var policyInput = Assert.IsType<NumadoraPolicyInput>(hostCallEvent.Parameters["policyInput"]);
        Assert.Equal("lineage-smoke", policyInput.Purpose);
        Assert.Equal("slasher_io", policyInput.HostCall.Module);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_local_observe", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaInputTextReachesPolicyGateAndFailsClosed()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "interactive.numa"),
            """
            IMPORT slasher_input AS input

            FUNC main()
                input.Text("blocked")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "interactive.numa"),
            Name: "unit-numadora-interactive-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_policy_denied", response.Error?.Code);
        Assert.Equal(Path.Combine("scripts", "interactive.numa"), response.Run.EntryPoint);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_input", hostCallEvent.Parameters["module"]);
        Assert.Equal("Text", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-policy", hostCallEvent.Parameters["executedBy"]);
        var policyInput = Assert.IsType<NumadoraPolicyInput>(hostCallEvent.Parameters["policyInput"]);
        Assert.NotNull(policyInput.Approvals);
        Assert.False(Assert.IsType<bool>(policyInput.Approvals!["interactiveInput"]));
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.False(policyDecision.Allow);
        Assert.Equal("numadora_policy_interactive_input_not_approved", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaInputKeysReachesPolicyGateAndFailsClosed()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "interactive-keys.numa"),
            """
            IMPORT slasher_input AS input

            FUNC main()
                input.Keys("CTRL+S")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "interactive-keys.numa"),
            Name: "unit-numadora-interactive-keys-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_policy_denied", response.Error?.Code);
        Assert.Equal(Path.Combine("scripts", "interactive-keys.numa"), response.Run.EntryPoint);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_input", hostCallEvent.Parameters["module"]);
        Assert.Equal("Keys", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-policy", hostCallEvent.Parameters["executedBy"]);
        var policyInput = Assert.IsType<NumadoraPolicyInput>(hostCallEvent.Parameters["policyInput"]);
        Assert.NotNull(policyInput.Approvals);
        Assert.False(Assert.IsType<bool>(policyInput.Approvals!["interactiveInput"]));
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.False(policyDecision.Allow);
        Assert.Equal("numadora_policy_interactive_input_not_approved", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaInputMouseReachesPolicyGateAndFailsClosed()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "interactive-mouse.numa"),
            """
            IMPORT slasher_input AS input

            FUNC main()
                input.Mouse("move", 1, 1, "left")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "interactive-mouse.numa"),
            Name: "unit-numadora-interactive-mouse-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_policy_denied", response.Error?.Code);
        Assert.Equal(Path.Combine("scripts", "interactive-mouse.numa"), response.Run.EntryPoint);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_input", hostCallEvent.Parameters["module"]);
        Assert.Equal("Mouse", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-policy", hostCallEvent.Parameters["executedBy"]);
        var policyInput = Assert.IsType<NumadoraPolicyInput>(hostCallEvent.Parameters["policyInput"]);
        Assert.NotNull(policyInput.Approvals);
        Assert.False(Assert.IsType<bool>(policyInput.Approvals!["interactiveInput"]));
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.False(policyDecision.Allow);
        Assert.Equal("numadora_policy_interactive_input_not_approved", policyDecision.Code);
    }

    [Theory]
    [InlineData("Wheel", """input.Wheel(1, 1, 120)""")]
    [InlineData("Drag", """input.Drag(1, 1, 2, 2, "left", 1, 1)""")]
    [InlineData("ContextMenu", "input.ContextMenu(1, 1, 1)")]
    public async Task RunFileAsync_NumaMouseVariantsReachPolicyGateAndFailClosed(string function, string call)
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, $"interactive-{function.ToLowerInvariant()}.numa"),
            $$"""
            IMPORT slasher_input AS input

            FUNC main()
                {{call}}
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", $"interactive-{function.ToLowerInvariant()}.numa"),
            Name: $"unit-numadora-interactive-{function.ToLowerInvariant()}-preflight",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_policy_denied", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_input", hostCallEvent.Parameters["module"]);
        Assert.Equal(function, hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-policy", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.False(policyDecision.Allow);
        Assert.Equal("numadora_policy_interactive_input_not_approved", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaObserveHostCallExecutesThroughSlasherPolicy()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "wait-for-missing-window.numa"),
            """
            IMPORT slasher_window AS win

            FUNC main()
                win.WaitForTitle("definitely-missing-slasher-window", 1)
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "wait-for-missing-window.numa"),
            Name: "unit-numadora-observe-host-call",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("window_not_found", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        Assert.Equal("numadora.run", response.Events[0].Action);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_window", hostCallEvent.Parameters["module"]);
        Assert.Equal("WaitForTitle", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-window", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_observe", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaScreenCaptureRejectsInvalidScopeBeforeCapture()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "invalid-screen-capture.numa"),
            """
            IMPORT slasher_screen AS screen

            FUNC main()
                screen.Capture("bad-scope", 320, 180)
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "invalid-screen-capture.numa"),
            Name: "unit-numadora-screen-capture-invalid-scope",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_host_call_invalid_arguments", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_screen", hostCallEvent.Parameters["module"]);
        Assert.Equal("Capture", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-screen", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_observe", policyDecision.Code);
    }

    [Theory]
    [InlineData("Exists", """element.Exists("foreground", "Title", "-", -1, "bad-match", 8, 1)""")]
    [InlineData("Find", """element.Find("foreground", "-", "-", -1, "contains", 8, 20)""")]
    [InlineData("ReadText", """element.ReadText("foreground", "Title", "-", -1, "bad-match", 8, 1)""")]
    [InlineData("Tree", """element.Tree("foreground", -1, 20)""")]
    public async Task RunFileAsync_NumaElementObserveCallsReachPolicyGateAndRejectInvalidArguments(string function, string call)
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, $"invalid-element-{function.ToLowerInvariant()}.numa"),
            $$"""
            IMPORT slasher_element AS element

            FUNC main()
                {{call}}
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", $"invalid-element-{function.ToLowerInvariant()}.numa"),
            Name: $"unit-numadora-element-{function.ToLowerInvariant()}-invalid-args",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_host_call_invalid_arguments", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_element", hostCallEvent.Parameters["module"]);
        Assert.Equal(function, hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-element", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_observe", policyDecision.Code);
    }

    [Theory]
    [InlineData("Locate", """browser.Locate("css", "body", 0, "-")""")]
    [InlineData("DomText", """browser.DomText("css", "body", 0, "-")""")]
    [InlineData("Attribute", """browser.Attribute("css", "body", "class", 0, "-")""")]
    public async Task RunFileAsync_NumaBrowserObserveCallsReachPolicyGateAndRejectInvalidArguments(string function, string call)
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, $"invalid-browser-{function.ToLowerInvariant()}.numa"),
            $$"""
            IMPORT slasher_browser AS browser

            FUNC main()
                {{call}}
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", $"invalid-browser-{function.ToLowerInvariant()}.numa"),
            Name: $"unit-numadora-browser-{function.ToLowerInvariant()}-invalid-args",
            CapturePolicy: new CapturePolicy(CaptureOnError: false)),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("numadora_host_call_invalid_arguments", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_browser", hostCallEvent.Parameters["module"]);
        Assert.Equal(function, hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-browser", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_observe", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaAppStartExecutesThroughSlasherPolicy()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "start-missing-app.numa"),
            """
            IMPORT slasher_app AS app

            FUNC main()
                app.Start("definitely-missing-slasher-app.exe")
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "start-missing-app.numa"),
            Name: "unit-numadora-app-start-host-call",
            CapturePolicy: new CapturePolicy(CaptureOnError: false),
            Purpose: "app-start-policy-smoke"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("app_start_failed", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_app", hostCallEvent.Parameters["module"]);
        Assert.Equal("Start", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-app", hostCallEvent.Parameters["executedBy"]);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_process_app_start", policyDecision.Code);
    }

    [Fact]
    public async Task RunFileAsync_NumaWindowFocusExecutesThroughSlasherPolicyWithTarget()
    {
        var scripts = Path.Combine(_workspaceRoot, "scripts");
        Directory.CreateDirectory(scripts);
        await WriteNumadoraStubModulesAsync(scripts);
        await File.WriteAllTextAsync(Path.Combine(scripts, "focus-missing-window.numa"),
            """
            IMPORT slasher_window AS win

            FUNC main()
                win.Focus(1)
            END
            """);

        var response = await _service.RunFileAsync(new ScriptFileRunRequest(
            Path.Combine("scripts", "focus-missing-window.numa"),
            Name: "unit-numadora-window-focus-host-call",
            CapturePolicy: new CapturePolicy(CaptureOnError: false),
            Purpose: "window-focus-policy-smoke"),
            CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal(AutomationRunStatus.Failed, response.Run.Status);
        Assert.Equal("window_not_found", response.Error?.Code);
        Assert.Equal(2, response.Events.Count);
        var hostCallEvent = response.Events[1];
        Assert.Equal("numadora.hostCall", hostCallEvent.Action);
        Assert.False(hostCallEvent.Ok);
        Assert.Equal("slasher_window", hostCallEvent.Parameters["module"]);
        Assert.Equal("Focus", hostCallEvent.Parameters["function"]);
        Assert.Equal("slasher-window", hostCallEvent.Parameters["executedBy"]);
        var policyInput = Assert.IsType<NumadoraPolicyInput>(hostCallEvent.Parameters["policyInput"]);
        Assert.NotNull(policyInput.Target);
        Assert.Equal("1", policyInput.Target!.Handle);
        var policyDecision = Assert.IsType<NumadoraPolicyDecision>(hostCallEvent.Parameters["policyDecision"]);
        Assert.True(policyDecision.Allow);
        Assert.Equal("numadora_policy_allowed_window_focus", policyDecision.Code);
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

    private static string AssertDiagnostics(IReadOnlyList<ScriptDiagnostic> diagnostics)
    {
        return string.Join(Environment.NewLine, diagnostics.Select(item => $"{item.Code}: {item.Message}"));
    }

    private static async Task WriteNumadoraStubModulesAsync(string directory)
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_app.numa"),
            """
            MODULE slasher_app
                EXPORT Start

                FUNC Start(fileName: String) -> Int
                    Print("__SLASHER_HOST_CALL__ slasher_app.Start " + fileName)
                    RETURN 1
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_window.numa"),
            """
            MODULE slasher_window
                EXPORT WaitForTitle, Focus

                FUNC WaitForTitle(title: String, timeoutMs: Int) -> String
                    Print("__SLASHER_HOST_CALL__ slasher_window.WaitForTitle " + title + " " + ToString(timeoutMs))
                    RETURN title
                END

                FUNC Focus(handle: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_window.Focus " + ToString(handle))
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_input.numa"),
            """
            MODULE slasher_input
                EXPORT Text, Keys, Mouse, Wheel, Drag, ContextMenu

                FUNC Text(content: String)
                    Print("__SLASHER_HOST_CALL__ slasher_input.Text " + content)
                END

                FUNC Keys(keys: String)
                    Print("__SLASHER_HOST_CALL__ slasher_input.Keys " + keys)
                END

                FUNC Mouse(action: String, x: Int, y: Int, button: String)
                    Print("__SLASHER_HOST_CALL__ slasher_input.Mouse " + action + " " + ToString(x) + " " + ToString(y) + " " + button)
                END

                FUNC Wheel(x: Int, y: Int, delta: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_input.Wheel " + ToString(x) + " " + ToString(y) + " " + ToString(delta))
                END

                FUNC Drag(fromX: Int, fromY: Int, toX: Int, toY: Int, button: String, durationMs: Int, steps: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_input.Drag " + ToString(fromX) + " " + ToString(fromY) + " " + ToString(toX) + " " + ToString(toY) + " " + button + " " + ToString(durationMs) + " " + ToString(steps))
                END

                FUNC ContextMenu(x: Int, y: Int, delayMs: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_input.ContextMenu " + ToString(x) + " " + ToString(y) + " " + ToString(delayMs))
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_screen.numa"),
            """
            MODULE slasher_screen
                EXPORT Capture

                FUNC Capture(scope: String, maxWidth: Int, maxHeight: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_screen.Capture " + scope + " " + ToString(maxWidth) + " " + ToString(maxHeight))
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_element.numa"),
            """
            MODULE slasher_element
                EXPORT Find, Exists, ReadText, Tree

                FUNC Find(scope: String, title: String, className: String, controlId: Int, match: String, maxDepth: Int, maxResults: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_element.Find " + scope + " " + title + " " + className + " " + ToString(controlId) + " " + match + " " + ToString(maxDepth) + " " + ToString(maxResults))
                END

                FUNC Exists(scope: String, title: String, className: String, controlId: Int, match: String, maxDepth: Int, maxResults: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_element.Exists " + scope + " " + title + " " + className + " " + ToString(controlId) + " " + match + " " + ToString(maxDepth) + " " + ToString(maxResults))
                END

                FUNC ReadText(scope: String, title: String, className: String, controlId: Int, match: String, maxDepth: Int, maxResults: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_element.ReadText " + scope + " " + title + " " + className + " " + ToString(controlId) + " " + match + " " + ToString(maxDepth) + " " + ToString(maxResults))
                END

                FUNC Tree(scope: String, maxDepth: Int, maxChildren: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_element.Tree " + scope + " " + ToString(maxDepth) + " " + ToString(maxChildren))
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_browser.numa"),
            """
            MODULE slasher_browser
                EXPORT Current, Title, Url, Locate, DomText, Attribute, Screenshot, Links, Windows

                FUNC Current(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Current " + sessionId)
                END

                FUNC Title(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Title " + sessionId)
                END

                FUNC Url(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Url " + sessionId)
                END

                FUNC Locate(usingValue: String, value: String, timeoutMs: Int, sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Locate " + usingValue + " " + value + " " + ToString(timeoutMs) + " " + sessionId)
                END

                FUNC DomText(usingValue: String, value: String, timeoutMs: Int, sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.DomText " + usingValue + " " + value + " " + ToString(timeoutMs) + " " + sessionId)
                END

                FUNC Attribute(usingValue: String, value: String, attribute: String, timeoutMs: Int, sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Attribute " + usingValue + " " + value + " " + attribute + " " + ToString(timeoutMs) + " " + sessionId)
                END

                FUNC Screenshot(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Screenshot " + sessionId)
                END

                FUNC Links(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Links " + sessionId)
                END

                FUNC Windows(sessionId: String)
                    Print("__SLASHER_HOST_CALL__ slasher_browser.Windows " + sessionId)
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_io.numa"),
            """
            MODULE slasher_io
                EXPORT Step, Log, Wait

                FUNC Step(name: String)
                    Print("__SLASHER_HOST_CALL__ slasher_io.Step " + name)
                END

                FUNC Log(message: String)
                    Print(message)
                END

                FUNC Wait(ms: Int)
                    Print("__SLASHER_HOST_CALL__ slasher_io.Wait " + ToString(ms))
                END
            END
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "slasher_test.numa"),
            """
            MODULE slasher_test
                EXPORT AssertForegroundTitle

                FUNC AssertForegroundTitle(operator: String, expected: String)
                    Print("__SLASHER_HOST_CALL__ slasher_test.AssertForegroundTitle " + operator + " " + expected)
                END
            END
            """);
    }
}
