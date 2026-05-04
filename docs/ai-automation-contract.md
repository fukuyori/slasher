# AI Automation Contract

This document defines the contract Slasher should expose to AI agents such as Codex. It is the Phase A foundation for all later script, runtime, compiler, MCP, and RPA work.

Slasher's primary purpose is not just to perform GUI actions. It must let an AI agent know:

- what target was selected
- what action was attempted
- what happened
- what evidence was captured
- what error occurred, if any
- what artifact paths can be inspected next

## Scope

This contract applies to:

- HTTP API actions
- script commands
- CLI runs
- MCP tool calls
- compiled script executables

The same action should produce the same conceptual result shape regardless of entry point.

## Core Concepts

### Run

A run is one execution session. It may contain one command, one script, one MCP tool call, or a compiled executable run.

Every run has:

- `runId`
- start/end timestamps
- status
- artifact directory
- event list
- final selected target, if any
- final error, if any

### Event

An event is one observable step inside a run.

Every command-like operation should produce an event, including failed and optional operations.

### Target

A target describes the thing being acted on or observed.

Initial target kinds:

- `window`
- `app`
- `screen`
- `region`
- `file`
- `folder`
- `clipboard`
- `browser`
- `element` future

### Evidence

Evidence is any artifact that helps an agent inspect what happened.

Initial evidence kinds:

- `screenshot`
- `log`
- `json`
- `text`
- `report`

### Error

Errors must be structured. Text-only errors are not enough for AI-driven debugging.

## Artifact Layout

Every script or multi-step run should create an artifact directory.

```text
artifacts/runs/<run-id>/
  run.json
  events.jsonl
  summary.txt
  report.html
  screenshots/
    0001-before.bmp
    0001-after.bmp
    0002-error.bmp
  logs/
    server.log
    script.log
  attachments/
```

Single HTTP actions may not create a full run directory immediately, but the response shape should still be compatible with this contract.

## Run ID

Recommended format:

```text
yyyyMMdd-HHmmss-<short-name>-<random>
```

Example:

```text
20260428-101530-notepad-a1b2
```

Rules:

- filesystem-safe
- stable for the whole run
- included in all event records
- included in MCP responses

## Run Report Schema

`run.json` should use this shape:

```json
{
  "schemaVersion": 1,
  "runId": "20260428-101530-notepad-a1b2",
  "name": "notepad smoke",
  "status": "passed",
  "mode": "script",
  "entryPoint": "scripts/numadora-samples/notepad-check.numa",
  "startedAt": "2026-04-28T10:15:30.000Z",
  "endedAt": "2026-04-28T10:15:34.200Z",
  "durationMs": 4200,
  "artifactRoot": "artifacts/runs/20260428-101530-notepad-a1b2",
  "eventCount": 5,
  "failedEventSequence": null,
  "selectedTarget": {
    "kind": "window",
    "handle": "0x123456",
    "title": "Untitled - Notepad",
    "processId": 1234,
    "processName": "Notepad"
  },
  "error": null,
  "artifacts": {
    "events": "artifacts/runs/20260428-101530-notepad-a1b2/events.jsonl",
    "summary": "artifacts/runs/20260428-101530-notepad-a1b2/summary.txt",
    "report": "artifacts/runs/20260428-101530-notepad-a1b2/report.html",
    "screenshots": "artifacts/runs/20260428-101530-notepad-a1b2/screenshots"
  }
}
```

Run status values:

- `passed`
- `failed`
- `stopped`
- `cancelled`
- `timed_out`

Run mode values:

- `http`
- `web`
- `mcp`
- `cli`
- `script`
- `compiled`

## Event Schema

Each line in `events.jsonl` should be one JSON object.

```json
{
  "schemaVersion": 1,
  "runId": "20260428-101530-notepad-a1b2",
  "sequence": 3,
  "step": "Type text",
  "action": "input.text",
  "source": {
    "file": "scripts/numadora-samples/notepad-check.numa",
    "line": 8,
    "column": 3,
    "command": "input.text(message)"
  },
  "target": {
    "kind": "window",
    "handle": "0x123456",
    "title": "Untitled - Notepad",
    "processId": 1234,
    "processName": "Notepad",
    "className": "Notepad",
    "bounds": {
      "x": 260,
      "y": 260,
      "width": 952,
      "height": 839
    }
  },
  "parameters": {
    "text": "Slasher check"
  },
  "result": {
    "sent": true,
    "chars": 13
  },
  "logs": [
    {
      "level": "info",
      "message": "Typed 13 characters",
      "timestamp": "2026-04-28T10:15:32.000Z"
    }
  ],
  "evidence": [
    {
      "kind": "screenshot",
      "role": "after",
      "path": "artifacts/runs/20260428-101530-notepad-a1b2/screenshots/0003-after.bmp",
      "mimeType": "image/bmp",
      "width": 952,
      "height": 839
    }
  ],
  "error": null,
  "startedAt": "2026-04-28T10:15:31.900Z",
  "endedAt": "2026-04-28T10:15:32.200Z",
  "durationMs": 300,
  "ok": true
}
```

## Target Schema

Window target:

```json
{
  "kind": "window",
  "handle": "0x123456",
  "title": "Untitled - Notepad",
  "className": "Notepad",
  "processId": 1234,
  "processName": "Notepad",
  "bounds": {
    "x": 0,
    "y": 0,
    "width": 900,
    "height": 640
  },
  "isVisible": true,
  "isEnabled": true,
  "isMinimized": false
}
```

Screen target:

```json
{
  "kind": "screen",
  "scope": "virtualDesktop",
  "bounds": {
    "x": -1920,
    "y": 0,
    "width": 3840,
    "height": 1080
  }
}
```

Region target:

```json
{
  "kind": "region",
  "bounds": {
    "x": 100,
    "y": 100,
    "width": 400,
    "height": 300
  }
}
```

## Error Schema

```json
{
  "code": "window_not_found",
  "message": "No matching window was found.",
  "action": "app.select",
  "source": {
    "file": "scripts/numadora-samples/notepad-check.numa",
    "line": 7,
    "column": 3,
    "command": "app.select(\"notepad\") as note",
    "function": "open notepad",
    "stack": [
      {
        "file": "scripts/numadora-samples/main.numa",
        "line": 3,
        "column": 1,
        "function": "main setup",
        "command": "IMPORT notepad_check AS sample"
      }
    ]
  },
  "target": null,
  "recoverable": true,
  "expected": {
    "processName": "notepad"
  },
  "actual": null,
  "evidence": [
    {
      "kind": "screenshot",
      "role": "error",
      "path": "artifacts/runs/20260428-101530-notepad-a1b2/screenshots/0002-error.bmp"
    }
  ],
  "details": {
    "timeoutMs": 10000
  }
}
```

Required error fields:

- `code`
- `message`
- `action`
- `recoverable`

Required when available:

- `source`
- `target`
- `expected`
- `actual`
- `evidence`
- `details`

Source fields:

- `source.file` is the script file that produced the failing command.
- `source.line` is the original line in that file.
- `source.function` is the current logical function. In Phase A this is the active `step` / `test step` name; future function syntax should write to the same field.
- `source.stack` lists include/import call sites from outermost to innermost when the failing command came from an included file.

## Capture Policy

Default policy for AI/test runs:

```json
{
  "captureOnError": true,
  "captureOnAssertionFailure": true,
  "captureAfterEachStep": false,
  "captureBeforeEachStep": false,
  "captureTarget": "selected",
  "imageFormat": "bmp"
}
```

Rules:

- Capture on error must be enabled by default.
- Capture on assertion failure must be enabled by default.
- Capture after every step is useful for debugging but should be opt-in.
- Captures should prefer the selected target window when available.
- If no selected target exists, capture the full virtual desktop.
- Large captures should keep a full artifact and provide a smaller preview artifact for AI/MCP display.

## Logging Policy

Log levels:

- `trace`
- `debug`
- `info`
- `warn`
- `error`

Minimum log sources:

- `script`
- `runtime`
- `server`
- `mcp`
- `web`
- `compiled`

Log record:

```json
{
  "timestamp": "2026-04-28T10:15:31.000Z",
  "level": "info",
  "source": "script",
  "message": "Selecting Notepad",
  "data": {
    "processName": "notepad"
  }
}
```

## MCP Response Contract

MCP tool calls should return:

- concise text summary
- structured JSON text for machine reading
- image content for the most relevant screenshot when available

For script runs:

```json
{
  "runId": "20260428-101530-notepad-a1b2",
  "status": "failed",
  "summary": "Step 3 failed: window_not_found",
  "selectedTarget": null,
  "artifacts": {
    "run": "artifacts/runs/20260428-101530-notepad-a1b2/run.json",
    "events": "artifacts/runs/20260428-101530-notepad-a1b2/events.jsonl",
    "summary": "artifacts/runs/20260428-101530-notepad-a1b2/summary.txt",
    "report": "artifacts/runs/20260428-101530-notepad-a1b2/report.html",
    "logs": "artifacts/runs/20260428-101530-notepad-a1b2/logs",
    "scriptLog": "artifacts/runs/20260428-101530-notepad-a1b2/logs/script.log"
  },
  "mostRelevantEvidence": {
    "kind": "screenshot",
    "path": "artifacts/runs/20260428-101530-notepad-a1b2/screenshots/0003-error.bmp",
    "mimeType": "image/bmp"
  },
  "error": {
    "code": "window_not_found",
    "message": "No matching window was found."
  }
}
```

If an image is available, the MCP response should also include it as MCP image content.

MCP should also expose readback tools for agent recovery:

- `slasher_list_runs` - recent runs from `GET /automation/runs`
- `slasher_get_run` - one run plus optional events
- `slasher_get_run_log` - `logs/script.log`
- `slasher_get_artifact` - JSON-safe artifact content, returning MCP image content for image artifacts
- `slasher_get_element_tree` - foreground or specified window native child control tree for handle/title/class/bounds inspection
- `slasher_find_elements` - find native child controls by title, class name, or control id
- `slasher_click_element` - click the center point of the first matching native child control
- `slasher_element_exists` - check whether a native child control exists without failing the run
- `slasher_get_element_text` - read the text/title of the first matching native child control
- `slasher_match_image` - search a selected/full screenshot for an uncompressed BMP template and return score/bounds

The script language should mirror these capabilities with `element tree`, `element find`, `element exists`, `element text`, `element click`, and `image match` so generated tests can inspect controls, assign the first candidate to a variable, and only fall back to mouse coordinates or template matching when structured targeting is unavailable. Assertions should include `assert element exists`, `assert element not exists`, `assert element text`, and `assert image match` so tests can fail with structured expected/actual metadata instead of relying on screenshot inspection.

## HTTP Response Contract

Existing endpoints can continue returning their current simple responses during migration.

New or upgraded endpoints should support the envelope:

```json
{
  "ok": true,
  "event": {},
  "result": {},
  "error": null
}
```

For scripts:

```json
{
  "ok": true,
  "run": {},
  "events": [],
  "error": null
}
```

Migration rule:

- Do not break current endpoints just to adopt envelopes.
- Add script/report endpoints first.
- Later add `?envelope=true` or `/v2` for action endpoints if needed.

## Script Surface

Agent/test oriented syntax should make evidence explicit:

```slasher
test.step("open notepad")
app.start("notepad.exe")

retry 10 delay 300
  app.select("notepad") as note
end

test.step("type message")
let message = "Slasher check"
input.text(message)

test.step("capture evidence")
screen.capture(target: note) as shot
test.attach(shot)

assert screen.contains(message, target: note, timeout: 3000)
```

Failure handling:

```slasher
try
  call mainFlow()
catch e
  log("failed: " + e.message)
  screen.capture() as failure
  test.attach(failure)
  fail(e.message)
finally
  optional window.close(note)
end
```

## Phase A Completion Checklist

- [ ] Event schema documented.
- [ ] Run schema documented.
- [ ] Target schema documented.
- [ ] Error schema documented.
- [ ] Capture policy documented.
- [ ] Logging policy documented.
- [ ] MCP response contract documented.
- [ ] HTTP migration contract documented.
- [ ] First implementation DTO locations chosen.

## Recommended First Implementation Slice

After this contract is accepted:

1. Add C# DTOs for:
   - `AutomationRunReport`
   - `AutomationEvent`
   - `AutomationTarget`
   - `AutomationEvidence`
   - `AutomationError`
   - `CapturePolicy`
2. Add an artifact writer service.
3. Add a minimal `POST /scripts/run` prototype that wraps current script execution or a placeholder command sequence. Done for the server-side AI test core command set.
4. Make MCP `slasher_run_script` return the new run summary shape. Done.
5. Add automatic screenshot on script failure. Done for server-side script runs.

## Current Implementation Notes

Initial DTOs and artifact writer live in:

```text
src/Slasher/Automation/AutomationModels.cs
src/Slasher/Automation/AutomationRunArtifactStore.cs
```

The initial store can:

- create `artifacts/runs/<run-id>/`
- write `run.json`
- append `events.jsonl`
- write `summary.txt`
- write `report.html` for completed runs
- save screenshot evidence under `screenshots/`

The first API entry point is:

```http
POST /automation/runs
```

It accepts `name`, `mode`, `entryPoint`, and `capturePolicy`, then returns the initial `AutomationRunReport`.

Server-side script execution is now available through:

```http
POST /scripts/run
POST /scripts/run-file
```

It accepts `script`, `name`, `stopOnError`, and `capturePolicy`, then returns `ScriptRunResponse` with:

- final `run`
- per-command `events`
- structured `error`
- artifact paths for `run.json`, `events.jsonl`, `summary.txt`, `script.log`, screenshots, and attachments

`/scripts/run-file` accepts `path`, `name`, `stopOnError`, and `capturePolicy`. Paths must resolve inside the Slasher workspace. Missing or rejected paths return structured run failures such as `script_file_not_found` or `script_path_outside_workspace`.
For file runs, `run.entryPoint` and `events[].source.file` should be the workspace-relative script path. `events[].source.line` should use the original line number in the script file.
Script files may be split with `include <path>` or `import <path>`. Included paths are resolved relative to the file that contains the include/import statement; inline scripts resolve them from the Slasher workspace root. Included files must remain inside the workspace. Missing, rejected, cyclic, or too-deep includes return structured failures such as `include_file_not_found`, `include_path_outside_workspace`, `include_cycle`, or `include_depth_exceeded`.
Events produced by included files should keep the included file path in `events[].source.file` and the original included line in `events[].source.line`. Errors should also include `error.source.function` and `error.source.stack` so agents can identify both the failing helper file and the include/import caller.

Run artifacts can be read back through:

```http
GET /automation/runs?limit=20
GET /automation/runs/{runId}
GET /automation/runs/{runId}/events
GET /automation/runs/{runId}/summary
GET /automation/runs/{runId}/logs/script
GET /automation/runs/{runId}/report
GET /automation/runs/{runId}/artifacts/raw?path=<artifact-path>
GET /automation/runs/{runId}/artifacts/content?path=<artifact-path>
```

`artifacts/raw` streams the artifact bytes with a MIME type for browser/report display. `artifacts/content` returns `AutomationArtifactContent` with `path`, `mimeType`, `base64Content`, and `length` for agent clients that need JSON-safe evidence.

Large script-run screenshots are stored as both full evidence and preview evidence:

- `after` / `error` for full-size artifacts
- `after-preview` / `error-preview` for reduced-size artifacts

MCP `slasher_run_script` should prefer preview evidence when returning image content.

The initial server-side runner supports the AI test core commands: `start`, `wait`, `wait window`, `app select`, `select`, `foreground`, `focus`, `restore`, `maximize`, `minimize`, `move`, `text`, `keys`, `capture`, and `close`.

It also supports first-pass test commands:

- `include <path>` / `import <path>`
- `set <name> <value>` / `let <name> = <value>`
- `set global <name> <value>` / `set file <name> <value>` / `set local <name> <value>`
- `<command> as <name>` result assignment
- `${name}` and `${object.property}` interpolation
- `vars`
- `unset <name>`
- `unset global <name>` / `unset file <name>` / `unset local <name>`
- `add <name> [amount]` / `inc <name> [amount]`
- `array <name> [values...]`
- `push <name> <values...>`
- `pop <name> [as <target>]`
- `get <name> <index> [as <target>]`
- `length <name> [as <target>]`
- `join <name> [separator] [as <target>]`
- `if <condition>` / `else` / `endif`
- `repeat <count>` / `endrepeat`
- `foreach <item> in <array>` / `endforeach`
- `while <condition>` / `endwhile`
- `try` / `catch [errorName]` / `finally` / `endtry`
- `function <name> [params...]` / `endfunction`
- `call <name> [args...] [as <target>]`
- `return [value]`
- `step <name>` / `test step <name>`
- `log <message>`
- `fail <message>`
- `assert window exists <title> [timeoutMs]`
- `assert window not exists <title>`
- `assert selected title contains <text>`
- `assert foreground title contains <text>`
- `assert value <left> <operator> <right>`
- `assert variable exists <path>`
- `assert variable not exists <path>`

Assertion failures use `assertion_failed`, include expected/actual fields, and follow the same screenshot-on-error policy.
Malformed script blocks use structured script errors such as `block_not_closed`, are written to `events.jsonl`, and follow the same screenshot-on-error policy.
Errors handled by `catch` can allow the run to complete as `passed`; failed events remain in `events.jsonl` for auditability. Uncaught errors keep the run `failed`. `finally` runs after both successful and failed try blocks.

Runtime variables:

- `_` contains the last command result.
- `selected` contains the current selected window when available.

`step` / `test step` set the current event step label. Subsequent events inherit that label until the next step command.

Variable scopes:

- `global` variables are visible for the whole run, including included/imported files.
- Numadora source locations should point to `.numa` files when the runtime can provide them.
- `local` variables are visible only within the current function call. Outside a function call, Phase A uses the active `step` / `test step` as the local scope key.
- Reads resolve `local`, then `file`, then `global`.
- Unqualified writes preserve compatibility: they write to the nearest existing variable scope, or to `global` when the variable is new.
- Scoped array and numeric commands use the same optional scope prefix, for example `array local items a b`, `push file items c`, and `add global count 1`.
- `vars` returns `global`, `file`, `local`, and merged `resolved` snapshots.

Function rules:

- Function definitions are skipped during top-level execution.
- `call <name> [args...]` executes the matching function body.
- `call <name> [args...] as <target>` stores the returned value in `<target>`.
- Function parameters are assigned as `local` variables in call order.
- Each call creates a fresh `local` scope so repeated calls do not share local variables.
- `return [value]` exits the current function. Using `return` outside a function returns `return_outside_function`.
- Missing functions return `function_not_found`; too many arguments return `too_many_arguments`; missing `endfunction` returns `block_not_closed`.

This is intentionally server-local for Phase A. The DTOs can later move to `Slasher.Runtime` when Phase 1 introduces shared projects.
