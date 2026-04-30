# AI Test Observability

Slasher's primary purpose is to let AI agents operate, test, and debug real Windows applications. For that purpose, three capabilities are mandatory and must be built into the execution model:

1. log capture
2. screen/window capture
3. structured error handling

These are not optional RPA conveniences. They are the evidence layer that lets an agent decide what happened and what to do next.

See `docs/ai-automation-contract.md` for the broader action/result/report schema.

## Current Implementation Status

As of Phase 11 completion (`7caa579`), the core evidence loop is implemented for server-side script runs:

- `run.json`, `events.jsonl`, `summary.txt`, script logs, and HTML reports
- artifact readback through HTTP and MCP
- automatic error/failure screenshots
- optional per-step screenshot policy
- native element events and assertions
- browser screenshots and browser console logs through WebDriver

Phase 12 work should reuse this evidence model for data/RPA packages instead of adding separate report formats.

## Execution Event Model

Every script command and API action should produce an execution event.

```json
{
  "sequence": 12,
  "step": "Type message",
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
    "sent": true,
    "chars": 5
  },
  "logs": [],
  "captures": [
    {
      "kind": "after",
      "scope": "window",
      "path": "artifacts/runs/run-001/0012-after.bmp",
      "width": 952,
      "height": 839
    }
  ],
  "error": null,
  "startedAt": "2026-04-28T00:00:00.000Z",
  "endedAt": "2026-04-28T00:00:00.250Z",
  "durationMs": 250,
  "ok": true
}
```

The report for a run is a sequence of these events plus run metadata.

## Log Capture

Logs should be collected from multiple layers:

- script logs: `log("message")`
- command logs: action start/end details
- server logs: Slasher server messages
- process logs: stdout/stderr for apps Slasher starts when available
- Windows event logs: optional future source for crashes
- test logs: `test.step`, `test.attach`, `assert` results

Minimum MVP:

- per-run JSONL event log
- plain text summary log
- log file path returned to Web/MCP/CLI callers

Suggested artifact layout:

```text
artifacts/runs/
  20260428-001-notepad/
    run.json
    events.jsonl
    summary.txt
    screenshots/
      0001-before.bmp
      0001-after.bmp
      0002-error.bmp
```

Script syntax:

```slasher
test.step("Open Notepad")
log("Selecting target window")
agent.note("Window title changed after save")
```

## Screen Capture

Captures should support:

- full virtual desktop
- selected window
- explicit window handle
- rectangle region
- future UI element

Capture timing:

- manual: `screen.capture(...)`
- automatic before a step
- automatic after a step
- automatic on error
- automatic on assertion failure

MVP defaults:

- capture on error: enabled
- capture after assertion failure: enabled
- capture after every step: configurable

Script syntax:

```slasher
let shot = screen.capture(target: selected)
test.attach(shot)

screen.capture(
  target: selected,
  path: "artifacts/shots/notepad.bmp"
) as shot
```

## Error Handling

Errors must be structured and recoverable.

```json
{
  "code": "window_not_found",
  "message": "No matching window was found.",
  "action": "app.select",
  "line": 12,
  "column": 3,
  "target": null,
  "recoverable": true,
  "capture": "artifacts/runs/run-001/screenshots/0012-error.bmp"
}
```

Script syntax:

```slasher
try
  app.select("notepad") as note
  input.text("hello")
catch e
  test.attach(screen.capture())
  log("failed: " + e.message)
  fail(e.message)
finally
  optional window.close(note)
end
```

Retry is also part of error handling:

```slasher
retry 10 delay 300
  app.select("notepad") as note
end
```

Optional commands should report failure without failing the whole run:

```slasher
optional window.close(note)
```

## Assertions

Assertions should produce evidence-rich errors.

Examples:

```slasher
assert window.title(note) contains "メモ帳"
assert screen.contains("Slasher check", target: note, timeout: 3000)
assert image.exists("save-button.png", target: note)
```

On failure, an assertion should:

- capture the relevant target
- attach logs
- include expected and actual values
- include timeout and polling details

## MVP Implementation Order

1. Create a run artifact directory for every script run.
2. Write `run.json` and `events.jsonl`.
3. Wrap each script command in an execution event.
4. Add automatic capture on error.
5. Add `test.step`, `log`, `test.attach`, and `fail`.
6. Add `try/catch/finally`, `retry`, and `optional` to the shared script engine.
7. Add assertion commands with failure captures.
8. Return run report paths from Web, MCP, and CLI.

## AI Agent Contract

When an AI agent runs a script, Slasher should return:

- final status
- current/last selected target
- events summary
- paths to logs and screenshots
- structured error if failed
- image content for the most relevant failure or final capture when called through MCP

This lets Codex inspect the actual GUI state instead of guessing from text logs alone.
