# Slasher Architecture

This note records the source layout Slasher should grow toward. The first product goal is to let AI agents such as Codex operate and test real Windows applications. The second goal is RPA-style local automation. The source layout should keep AI observations, RPA actions, script language changes, and API surfaces independently editable.

## Product Priority

1. AI-driven Windows app development and testing.
2. RPA-style local automation.

This means the architecture should prioritize:

- observable action results
- stable machine-readable reports
- screenshots and evidence attachments
- log capture
- structured errors
- deterministic target selection
- assertions and test steps
- MCP-friendly APIs
- recovery information after failures

Broad RPA package coverage is important, but it should build on that action/observation foundation.

## Current Backend Layout

- `Program.cs`
  - application startup, JSON configuration, static file hosting, API-key middleware
  - should stay small
- `Api/SlasherEndpointExtensions.cs`
  - HTTP endpoint mapping
  - split into smaller endpoint files when a package grows large
- `Api/Requests.cs`
  - request/response DTOs
  - future split target: `WindowRequests.cs`, `InputRequests.cs`, `FileRequests.cs`
- `Windows/WindowsAutomationService.cs`
  - Windows API orchestration
  - future split target:
    - `Windows/WindowDiscoveryService.cs`
    - `Windows/WindowActionService.cs`
    - `Windows/InputService.cs`
    - `Windows/ScreenshotService.cs`
    - `Windows/AppAutomationService.cs`
- `Windows/NativeMethods.cs`
  - low-level P/Invoke declarations only
- `Files/FileSystemAutomationService.cs`
  - file/folder/package actions

## Script Runtime Direction

The script language now runs through the server-side `ScriptRunService` for Web UI and MCP script execution. The Web UI posts script text to `/scripts/run` and `/scripts/check`; the MCP bridge exposes `slasher_run_script`, `slasher_run_script_file`, `slasher_check_script`, and `slasher_check_script_file` over the same HTTP endpoints.

Remaining split targets:

- move browser HTTP helpers out of `src/Slasher/wwwroot/app.js`
- keep MCP as a thin HTTP bridge in `scripts/slasher-mcp.mjs`
- split `ScriptRunService` further by parser, checker, runtime, command packages, and reporting as the language grows

The interpreter should not know about the DOM, MCP framing, or logging UI. It should accept callbacks for:

- command execution
- logging
- cancellation
- screenshot/result attachment

## RPA Package Boundary

Actions should be grouped by package, matching the user-facing command groups:

- Application
- Window
- Input
- Mouse
- Clipboard
- File
- Folder
- Browser
- Wait
- Observe
- Assert
- Report
- Agent/Test

Each package should eventually have:

- DTOs
- API endpoints
- service implementation
- script command bindings
- README/docs examples

## Error Handling Direction

Script-level error handling should be implemented in the interpreter, not inside individual command handlers.

Planned constructs:

```text
try
  ...
catch
  ...
finally
  ...
endtry

retry 5 delay 500
  ...
endretry

optional command ...
```

Command handlers should throw structured errors with:

- code
- message
- command
- line
- target window/app when available
- screenshot path when captured

## AI/Test Result Envelope

Every command should eventually be able to produce a structured event:

```json
{
  "step": "type text",
  "action": "input.text",
  "target": {
    "kind": "window",
    "handle": "0x123456",
    "title": "Untitled - Notepad",
    "processName": "Notepad"
  },
  "parameters": {
    "text": "hello"
  },
  "result": {
    "sent": true
  },
  "evidence": [
    {
      "kind": "screenshot",
      "path": "artifacts/shots/step-001.bmp"
    }
  ],
  "startedAt": "2026-04-28T00:00:00Z",
  "endedAt": "2026-04-28T00:00:01Z",
  "ok": true
}
```

This format is primarily for Codex and other agents. It also becomes the basis for RPA execution reports.

## Test Observability Requirements

The test execution layer must always be designed around:

- logs
- screen/window captures
- structured error handling

Every script run should eventually create:

- `run.json`
- `events.jsonl`
- `summary.txt`
- `screenshots/`

See `docs/ai-test-observability.md` for the detailed contract.

## Refactoring Rule

When adding a new feature, prefer this order:

1. Add or update the DTO/API/service for the action package.
2. Add the script command binding.
3. Add web UI support if it needs direct controls.
4. Add MCP tool support if AI agents need direct access.
5. Add README and script examples.
6. Add a smoke test or sample script.
