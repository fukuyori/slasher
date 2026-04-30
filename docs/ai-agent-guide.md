# Slasher AI Agent Guide

This document is written for AI agents that use Slasher to operate Windows applications and run automated tests.

Slasher is not only an input driver. Treat it as an observation-and-evidence system:

1. Choose a target app or window.
2. Make the target foreground and stable.
3. Perform one small action.
4. Capture or inspect the result.
5. Record enough evidence for a human or another agent to understand the run.

## Primary Goal

Slasher exists first to let agents such as Codex automate Windows app development and testing.

The agent should use Slasher to:

- launch the app under test
- select the correct window
- move and size windows into predictable positions
- send keyboard and mouse input
- capture the selected window or full desktop
- collect run artifacts, logs, screenshots, and structured errors
- close or clean up test windows when the run is done

RPA workflows are a secondary goal. For AI-driven tests, prefer repeatability, evidence, and clear failures over silent recovery.

## Current Project State

Phase 11 is complete as of commit `7caa579`.

Agents can now rely on:

- server-side script runs with structured events, logs, screenshots, and HTML reports
- native window/control inspection and element actions
- image matching
- Selenium WebDriver browser automation for Edge, Chrome, and Firefox
- browser screenshots, downloads, selected options, tabs/windows, and console logs

The next work is Phase 12 RPA expansion. Start with `docs/phase-12-rpa-expansion-plan.md`, and keep new data/RPA packages aligned across HTTP, script, MCP, and documentation.

## Current Connection Modes

### HTTP

Default local URL:

```text
http://127.0.0.1:5055
```

Health check:

```http
GET /health
```

API index:

```http
GET /api
```

### MCP

The repo includes a Codex plugin at:

```text
plugins/slasher
```

The MCP bridge uses:

```text
SLASHER_URL=http://127.0.0.1:5055
```

Important MCP tools:

- `slasher_get_status`
- `slasher_list_windows`
- `slasher_get_foreground_window`
- `slasher_start_app`
- `slasher_select_app`
- `slasher_focus_window`
- `slasher_set_window_state`
- `slasher_move_window`
- `slasher_close_window`
- `slasher_send_keys`
- `slasher_type_text`
- `slasher_mouse`
- `slasher_drag_mouse`
- `slasher_get_context_menu`
- `slasher_capture`
- `slasher_match_image`
- `slasher_browser_open`
- `slasher_browser_find`
- `slasher_browser_click`
- `slasher_browser_hover`
- `slasher_browser_double_click`
- `slasher_browser_right_click`
- `slasher_browser_type`
- `slasher_browser_press`
- `slasher_browser_upload`
- `slasher_browser_drag`
- `slasher_browser_select_option`
- `slasher_browser_selected_options`
- `slasher_browser_wait_download`
- `slasher_browser_logs`
- `slasher_browser_clear`
- `slasher_browser_submit`
- `slasher_browser_text`
- `slasher_browser_attribute`
- `slasher_browser_wait`
- `slasher_browser_wait_text`
- `slasher_browser_title`
- `slasher_browser_url`
- `slasher_browser_js`
- `slasher_browser_cookies`
- `slasher_browser_storage_get`
- `slasher_browser_storage_set`
- `slasher_browser_screenshot`
- `slasher_browser_links`
- `slasher_browser_windows`
- `slasher_browser_new_window`
- `slasher_browser_switch_window`
- `slasher_browser_close_window`
- `slasher_browser_close`
- `slasher_get_element_tree`
- `slasher_find_elements`
- `slasher_click_element`
- `slasher_element_exists`
- `slasher_get_element_text`
- `slasher_check_script`
- `slasher_check_script_file`
- `slasher_run_script`
- `slasher_run_script_file`
- `slasher_list_runs`
- `slasher_get_run`
- `slasher_get_run_log`
- `slasher_get_artifact`

## Agent Operating Loop

Use this loop for every GUI test:

1. Check Slasher health.
2. Start or locate the app under test.
3. Select the target app by process name or title.
4. Restore, focus, and optionally resize the target window.
5. Capture the selected window before risky steps when debugging.
6. Execute a small action.
7. Capture or inspect the result.
8. Stop on unexpected state unless the test explicitly covers recovery.
9. Write down the final artifact paths and failure reason.
10. Close test windows only when the test scenario is complete.

Do not send text, keys, or mouse input until the correct foreground window has been confirmed.

## Recommended Test Pattern

For a basic Windows app smoke test:

```text
start notepad.exe
wait window Notepad 10000
app select notepad
restore
move 80 80 900 640
assert selected title contains Notepad
set message "Slasher AI smoke test"
text "${message}"
capture selected
close
```

For MCP, prefer a single `slasher_run_script` call when the flow is sequential and the script language can express it. Use individual MCP tools when you need to inspect results between steps.
For reusable tests, store the script in the workspace and call `slasher_run_script_file`.
Before running a generated or edited script, call `slasher_check_script`, `slasher_check_script_file`, or `POST /scripts/check` to catch quote errors, include errors, and unclosed blocks without touching the desktop.
After an interrupted turn, call `slasher_list_runs`, then `slasher_get_run`, `slasher_get_run_log`, or `slasher_get_artifact` to recover the HTML report, event timeline, script log, and screenshot evidence.

## Targeting Rules

Prefer structured targeting in this order:

1. Process id returned from app launch.
2. Window handle returned from `app select`, `foreground`, or `windows`.
3. Process name with `app select`.
4. Title substring.
5. Full-screen coordinates only when no better target exists.

After selecting a window, keep using that selected handle. Window focus can change unexpectedly after app launches, dialogs, IME windows, context menus, or browser activity.

Before relying on coordinates, call `slasher_get_element_tree` or `GET /windows/{handle}/elements` when the app exposes native child controls. The response includes child handles, titles, class names, control ids, bounds, visibility, and enabled state. Use `slasher_find_elements` or `GET /elements/find` to narrow candidates by title/class/control id; use `slasher_element_exists` / `GET /elements/exists` and `slasher_get_element_text` / `GET /elements/text` for assertions and readback; use `slasher_click_element` or `POST /elements/click` only after the candidate is clear. This is a native control-tree bridge and will later be complemented by full UI Automation selectors.

When the app does not expose useful native controls, use `slasher_match_image` or `POST /screen/image-match` with a small BMP template. Prefer selected-window matching after moving the target to a stable size. Use full-screen matching for popups, menus, or wrong-window diagnosis.

For web app tests, prefer Selenium-style browser operations before OS-level input. Use `slasher_browser_open` or script `browser webdriver edge|chrome|firefox <url>`, then operate DOM elements with `find`, `click`, `hover`, `double-click`, `right-click`, `type`, `press`, `upload`, `drag`, `select-option`, `selected-options`, `text`, `attr`, `wait`, `links`, `screenshot`, browser logs, download waits, and window/tab commands using selector strategies `css`, `xpath`, `id`, `name`, `tag`, `class`, `link`, or `partialLink`.

The same native control bridge is available inside scripts:

```text
element tree selected depth 2 as controls
element find title OK match exact as okButton
element exists title OK match exact as okExists
element text title OK match exact as okText
element click title OK match exact
assert element exists title OK match exact
assert element text title OK match exact contains OK
image match expected/button-ok.bmp selected threshold 0.99 as okImage
assert image match expected/button-ok.bmp selected threshold 0.99
browser webdriver edge https://example.com
browser wait css h1 10000
browser text css h1 as heading
browser title as pageTitle
browser js "return document.title" as jsTitle
browser hover css "#menu"
browser double-click css "#item"
browser press ENTER
browser select-option id choice value b
browser wait-download "C:\tmp\downloads" "*.csv" 30000 500 as downloaded
browser logs as consoleLogs
browser storage local set token abc123
browser storage local get token as token
browser links as pageLinks
browser screenshot
browser new-webdriver-tab https://example.org
browser windows as browserWindows
browser switch 0
browser close-tab
browser quit
```

## Coordinates

Mouse coordinates are screen coordinates.

Before clicking:

- capture the target window
- check window bounds from `GET /windows` or MCP window metadata
- prefer moving the target window to a known rectangle

Example stable setup:

```text
app select notepad
restore
move 80 80 900 640
capture selected
```

Then calculate click positions relative to that known window placement.

## Capture Strategy

Use selected-window capture for most tests:

```text
capture selected
```

Use full desktop capture when:

- no window has been selected yet
- the failure may involve focus stealing
- a menu, popup, dialog, or another app is involved
- selected-window capture appears blank or wrong

```text
capture full
```

For direct screenshot API calls, request a preview size when the image is only needed for AI inspection:

```json
{
  "maxWidth": 1280,
  "maxHeight": 720
}
```

For AI debugging, capture:

- before the first input into a newly selected app
- after a meaningful state change
- immediately after failure

## Run Artifacts

Phase A introduces run artifact metadata.

Start a run:

```http
POST /automation/runs
```

Example body:

```json
{
  "name": "notepad-smoke",
  "mode": "mcp",
  "entryPoint": "agent",
  "capturePolicy": {
    "captureOnError": true,
    "captureOnAssertionFailure": true,
    "captureAfterEachStep": false,
    "captureBeforeEachStep": false,
    "captureTarget": "selected",
    "imageFormat": "bmp"
  }
}
```

The response contains:

- `runId`
- `status`
- `artifactRoot`
- `artifacts.run`
- `artifacts.events`
- `artifacts.summary`
- `artifacts.screenshots`

Read a completed run:

```http
GET /automation/runs?limit=20
GET /automation/runs/{runId}
GET /automation/runs/{runId}/events
GET /automation/runs/{runId}/summary
GET /automation/runs/{runId}/logs/script
GET /automation/runs/{runId}/artifacts/raw?path=<artifact-path>
GET /automation/runs/{runId}/artifacts/content?path=<artifact-path>
```

Use `GET /automation/runs?limit=20` when you need to recover the most recent run after an interrupted agent turn or browser refresh. The Web UI also exposes the same recovery path in the Run Report panel's Recent Runs list.
`artifacts/raw` streams the artifact bytes for browsers and HTML reports. `artifacts/content` returns base64 content with a MIME type. Use it when an AI client cannot directly read local files. MCP `slasher_run_script` returns a short run summary first, including `report.html`, `summary.txt`, `script.log`, the failed event source, diagnostics, selected/foreground window summaries, and the most relevant evidence path. It also returns the most relevant screenshot as image content when screenshot evidence exists.

Large screenshots produce two evidence records during server-side script runs:

- `after` or `error` - full-size artifact for durable evidence
- `after-preview` or `error-preview` - smaller artifact for AI/MCP display

When both exist, AI clients should inspect the preview first and read the full artifact only when detailed pixels are needed.

Artifact layout:

```text
artifacts/runs/<run-id>/
  run.json
  events.jsonl
  summary.txt
  screenshots/
  logs/
    script.log
  attachments/
```

Current Phase A note: the artifact store, `POST /automation/runs`, and server-side `POST /scripts/run` endpoint exist. The initial server-side runner records one `AutomationEvent` per supported command and saves screenshot evidence for `capture` and command errors.
Script files can be executed with `POST /scripts/run-file` or MCP `slasher_run_script_file`. Script paths must be inside the Slasher workspace.
For file runs, `run.entryPoint` and each `events[].source.file` point to the `.slasher` file, and `events[].source.line` points to the original script line.
Script files can be split with `include <path>` or `import <path>`. Included paths are relative to the current script file; inline scripts resolve includes from the workspace root. Included files must stay inside the workspace, and events from included files keep the included file path and original line number.
When a script fails, inspect `error.source.file`, `error.source.line`, and `error.source.function`. `function` is currently the active `step` / `test step` name. For failures inside included files, `error.source.stack` lists the include/import call site chain.

## Error Handling Rules

Agents should treat these as hard failures:

- target app was not found
- selected target is not the intended app
- foreground window changed before input
- text input returns success but capture shows no visible change
- screenshot captures the browser/control UI instead of the app under test
- expected UI state is not visible after a timeout

On failure:

1. Capture full desktop.
2. Capture selected window if a target exists.
3. Read or report `run.json`, `events.jsonl`, and `summary.txt` when available.
4. Include the action that failed, source file, source line, source function, expected state, actual state, and screenshot path.
5. Avoid continuing with destructive actions.

## Useful Script Commands

Workspace script file example:

```text
scripts/samples/ai-agent-smoke.slasher
```

Split script example:

```text
scripts/samples/include-main.slasher
```

Inside a script file:

```text
include lib/common.slasher
```

Window and app:

```text
start notepad.exe
wait window Notepad 10000
app select notepad
foreground
restore
maximize
minimize
move 80 80 900 640
close
```

Keyboard and text:

```text
text "hello"
keys CTRL+S
keys ALT+F4
```

Mouse:

```text
mouse move 400 300
primaryclick 400 300
secondaryclick 400 300
doubleclick 400 300
drag 100 100 500 500 600
contextmenu 400 300
scroll -120
```

Variables and loops:

```text
set message "hello from Slasher"
array items alpha beta gamma
foreach item in items
text "${iteration}: ${item}"
endforeach
```

Branching:

```text
if "${message}" contains "hello"
text "matched"
else
text "not matched"
endif
```

Server-side AI test assertions:

```text
test step "open app"
set message "hello"
wait 1 as pause
log "${message} / ${pause.waitedMs}"
vars
log "checkpoint reached"
assert window exists Notepad 10000
assert selected title contains Notepad
assert foreground title contains Notepad
assert window not exists Error
assert variable exists message
assert variable not exists missing
assert value "${message}" == hello
fail "explicit failure"
```

Use `step "name"` or `test step "name"` before a meaningful test phase. Subsequent events keep that step name, which makes `summary.txt` and `events.jsonl` easier for agents and humans to scan.

Use `agent note "message"` when a test discovers context that the next agent or a human should see without parsing screenshots. It is stored as a `note` log entry in the event and in `logs/script.log`.

Use `test attach "path" as role` to copy expected output, debug logs, downloaded files, or other evidence into the run's `attachments` directory. The event receives an `attachment` evidence item with the chosen role, and `summary.txt` includes the attachment path.

After a script run completes, inspect `report.html` for a quick human-readable timeline. It is stored beside `run.json`, `events.jsonl`, and `summary.txt`, and can also be fetched with `GET /automation/runs/{runId}/report`.
HTML reports embed preview screenshots inline while preserving full-size screenshot artifacts as links.
In the Web UI, the Run Report panel links directly to the HTML report, script log, text summary, event list, and run JSON after a script run completes. Use Recent Runs to load an earlier run back into the panel and reopen its artifacts.

Use `wait screenStable [selected|full] [stableMs] [timeoutMs]` before reading the screen after a launch, resize, animation, or web navigation. It samples preview screenshots until the image stops changing, then records `wait.screenStable` in the run events.

Use per-step capture only when debugging or producing a detailed audit trail. Set `capturePolicy.captureBeforeEachStep` and/or `capturePolicy.captureAfterEachStep` to true. The event evidence will include `before` / `after` screenshots using `capturePolicy.captureTarget`.

`image match <template.bmp> [selected|full] [threshold n] [maxWidth n] [maxHeight n] [step n]` and `assert image match ...` currently support uncompressed BMP templates. Relative paths resolve from the current `.slasher` file, or from the workspace root for inline scripts.

Selenium-style browser commands use WebDriver sessions: `browser webdriver`, `browser current`, `browser title`, `browser url`, `browser find`, `browser click`, `browser hover`, `browser double-click`, `browser right-click`, `browser type`, `browser press`, `browser upload`, `browser drag`, `browser select-option`, `browser selected-options`, `browser wait-download`, `browser logs`, `browser clear`, `browser submit`, `browser text`, `browser attr`, `browser wait`, `browser js`, `browser cookies`, `browser storage`, `browser screenshot`, `browser links`, `browser windows`, `browser new-webdriver-tab`, `browser switch`, `browser close-tab`, and `browser quit`. Use `downloadDir=<path>` on `browser webdriver` when the test needs deterministic download evidence, and use `browser logs` to inspect console messages after navigation or actions. The older `browser launch/open/select/go/back/forward/refresh/close/new-tab` commands still operate the browser as a normal Windows app with keyboard shortcuts.
Slasher includes NuGet-provided ChromeDriver, GeckoDriver, and MSEdgeDriver binaries when available and falls back to Selenium Manager when a packaged driver is not present. Edge, Chrome, and Firefox still need to be installed on the machine.

`assert screen contains <text> [selected|full]` is intentionally conservative until OCR is implemented. It fails with `screen_contains_unavailable`, records the expected text, and captures screenshots for agent review instead of pretending the text was verified.

On failures, check `error.details.diagnostics` before guessing. Slasher adds warnings for common automation mistakes such as no selected target window, a selected/foreground mismatch, or a foreground window that looks like the Slasher control UI, Codex, or a browser. The Web UI Run Report shows these diagnostics inline below failed events.

Server-side script variables:

- Use `set name value` or `let name = value` for text variables.
- Use `set global name value`, `set file name value`, or `set local name value` for explicit scope.
- Use `command ... as name` to store command results.
- Use `${name}` and `${object.property}` inside later commands.
- `_` contains the last command result.
- `selected` contains the current selected window when available.
- Use `vars` to inspect variables in the run events.

Variable scopes:

- `global` is visible to the whole run and included files.
- `file` is visible only to commands from the same `.slasher` file.
- `local` is visible only inside the current logical function, currently the active `step` / `test step`.

Unqualified writes keep existing behavior and write to the global run scope unless the variable already exists in `local` or `file`. Reads resolve `local`, then `file`, then `global`.

Scope example:

```text
scripts/samples/scope-main.slasher
```

Server-side functions:

```text
function addItem name amount
set local itemName "${name}"
add global total "${amount}"
return "${total}"
endfunction

call addItem alpha 2 as firstTotal
```

Function definitions are skipped during top-level execution and run only through `call`. Each call gets a fresh `local` scope; parameters become local variables. `return` exits the current function; `call ... as name` stores the returned value. Use functions to group reusable test setup, assertions, and cleanup helpers.

Function example:

```text
scripts/samples/function-main.slasher
```

Server-side arrays:

```text
array items alpha beta
push items gamma
get items 1 as second
length items as count
join items "," as csv
log "${second}/${count}/${csv}/${items.length}/${items.2}"
pop items as last
```

Server-side control flow:

```text
if "${mode}" == "fast"
log "fast path"
else
log "slow path"
endif

repeat 3
log "repeat ${iteration}"
endrepeat

foreach item in items
log "${index}: ${item}"
endforeach

set i 0
while ${i} < 3
log "while ${i}"
add i 1
endwhile
```

Server-side error handling:

```text
try
fail "something went wrong"
catch e
log "caught ${e.code}: ${e.message}"
finally
log "cleanup"
endtry
```

Use `catch` for errors that are expected and recoverable in the scenario. Use `finally` for cleanup such as closing test windows. Uncaught errors keep the run failed.

Assertion failures return `assertion_failed`, include expected and actual values, and save error screenshots when capture-on-error is enabled.
Malformed script blocks such as a missing `endif`, `endrepeat`, `endforeach`, or `endwhile` return structured script errors such as `block_not_closed` and save error screenshots.
Error events include `source.file`, `source.line`, `source.function`, and, when relevant, `source.stack`. `summary.txt` includes the same source in compact `@file:line#function` form.

## Agent Prompt Template

Give this prompt to an AI agent that should use Slasher:

```text
You can use Slasher to operate Windows applications through HTTP or MCP.

Before sending input, always verify that Slasher is reachable and that the intended app window is selected or foreground.

Use this loop:
1. Check Slasher health.
2. Start or select the target app.
3. Restore/focus/resize the window to a predictable rectangle.
4. Perform one small action.
5. Capture evidence.
6. Use `assert` for states the test can verify structurally.
7. If the result is unexpected, stop, capture full desktop, and report artifact paths.

Prefer selected-window screenshots for normal checks and full-desktop screenshots for focus, popup, menu, or wrong-window failures.

For multi-step flows, use slasher_run_script. It returns a structured run report with artifact paths. For exploratory tests, use individual MCP tools and inspect each result.

Never assume text or keys went to the correct app without checking the selected/foreground window and capturing the result.
```

## Known Limitations

Current limitations:

- UI element recorder/selectors are not implemented yet.
- OCR is not implemented yet.
- Image matching is implemented for uncompressed BMP templates; PNG/JPEG templates and OCR are still pending.
- Server-side script runs support Selenium-style DOM operations for Edge, Chrome, and Firefox. Link enumeration and richer WebDriver actions are still pending.
- Mouse coordinates are screen coordinates, so stable window placement is important.
- Elevated apps usually require Slasher to run elevated.
- Focus changes can be rejected by Windows or stolen by another process.

## Related Documents

- `docs/ai-automation-contract.md`
- `docs/ai-test-observability.md`
- `docs/implementation-roadmap.md`
- `docs/language-system.md`
- `docs/slasher-script.md`
- `docs/slasher-numadora-integration.md`
