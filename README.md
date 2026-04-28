# Slasher

Slasher is a small Windows automation server written in C#. It exposes HTTP APIs for starting applications, enumerating and manipulating windows, sending keyboard and mouse input, and taking screenshots.

The intended use cases are local RPA-style workflows and AI-driven application testing.

## Direction

Slasher follows the same basic operating idea as Quorsel:

- Slasher executes GUI actions and captures observations
- a human or AI agent chooses the next action from those observations
- each step should be easy to inspect before and after it runs

The first control surface is a local web UI. It is intentionally direct: choose a target window, focus it, send input, capture the result, then decide the next step.

## Documentation

- `docs/ai-agent-guide.md` - practical guide for AI agents that use Slasher for Windows app testing
- `docs/ai-automation-contract.md` - run/event/error/evidence contract for automation results
- `docs/ai-test-observability.md` - logging, capture, and evidence design for test automation
- `docs/implementation-roadmap.md` - phased implementation plan
- `docs/script-compiler-implementation-plan.md` - script language and compiler plan

## Run

```powershell
dotnet run --project src\Slasher\Slasher.csproj --urls http://127.0.0.1:5055
```

Open the web control panel:

```text
http://127.0.0.1:5055/
```

API metadata is available at:

```text
http://127.0.0.1:5055/api
```

## Codex Integration

Slasher includes a repo-local Codex plugin at:

```text
plugins/slasher
```

Load that plugin from Codex, then start the Slasher HTTP server:

```powershell
dotnet run --project src\Slasher\Slasher.csproj --urls http://127.0.0.1:5055
```

The plugin exposes MCP tools that call the local Slasher API:

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

The MCP bridge uses `SLASHER_URL`, defaulting to `http://127.0.0.1:5055`.
`slasher_run_script` calls the server-side `/scripts/run` endpoint and returns a structured run report with artifact paths.
`slasher_run_script_file` calls `/scripts/run-file` for scripts stored inside this workspace.
`slasher_list_runs`, `slasher_get_run`, `slasher_get_run_log`, and `slasher_get_artifact` let an AI agent recover prior run reports, logs, and evidence without reading local files directly.
`slasher_check_script`, `slasher_check_script_file`, and `POST /scripts/check` validate inline script text or workspace script files without executing GUI actions.
When a script run contains screenshot evidence, the MCP response also includes the most relevant screenshot as image content. Full screenshots are kept as run artifacts, and a smaller `*-preview` screenshot is generated for large captures so AI clients do not have to load the full BMP.
`slasher_browser_open`, `slasher_browser_find`, `slasher_browser_click`, `slasher_browser_hover`, `slasher_browser_double_click`, `slasher_browser_right_click`, `slasher_browser_type`, `slasher_browser_press`, `slasher_browser_upload`, `slasher_browser_drag`, `slasher_browser_select_option`, `slasher_browser_selected_options`, `slasher_browser_wait_download`, `slasher_browser_logs`, `slasher_browser_text`, `slasher_browser_attribute`, `slasher_browser_wait`, `slasher_browser_screenshot`, `slasher_browser_links`, `slasher_browser_windows`, and `slasher_browser_close` expose Selenium WebDriver-style DOM operations for Edge, Chrome, and Firefox.
`slasher_match_image` and `POST /screen/image-match` search a selected/full screenshot for a BMP template image and return match score and bounds. `slasher_get_element_tree` and `GET /elements/tree` read the foreground or specified window's native child window/control tree. `slasher_find_elements`, `slasher_click_element`, `slasher_element_exists`, `slasher_get_element_text`, `GET /elements/find`, `GET /elements/exists`, `GET /elements/text`, and `POST /elements/click` can then find, inspect, or click native controls by title, class name, or control id before falling back to coordinates.

Browser smoke tests can be run against the local server:

```powershell
.\scripts\run-browser-smoke.ps1 -BaseUrl http://127.0.0.1:5055 -Browsers edge,chrome,firefox
```

Example Codex-side script:

```text
start notepad.exe
wait 800
app select notepad
set message "hello from Codex through Slasher"
foreground as win
text "${message} / ${win.title}"
text "hello from Codex through Slasher"
capture selected
```

From the web UI you can:

- run typed commands from the command bar
- create, save, and run multi-line scripts in sequence
- store values and command results in script variables
- use arrays and loop over them with `foreach`
- branch and repeat script steps with `if`, `repeat`, and `while`
- start an app
- select an app by process name or window title
- refresh and filter top-level windows
- select the foreground window
- focus, restore, minimize, maximize, move, resize, or close a window
- send key chords and text
- send mouse move/click/double-click/drag/context-menu/wheel actions
- capture the selected window or full virtual desktop

Command examples:

```text
help
refresh
activate Notepad
foreground
select 0x123456
select app notepad
app select notepad
foreground as win
set message "hello"
array apps notepad mspaint
push apps calc
foreach app in apps
text "${iteration}: ${app}"
endforeach
text "${message}"
text "${selected.title}"
if "${message}" == "hello"
text "matched"
else
text "not matched"
endif
repeat 3
text "loop ${iteration}"
endrepeat
set i 0
while ${i} < 3
text "while ${i}"
add i 1
endwhile
start notepad.exe
focus
title
wait window Notepad 10000
maximize
minimize
restore
move 80 80 900 640
keys CTRL+S
text "hello from Slasher"
mouse move 400 300
mouse click 400 300 left
mouse click 400 300 primary
mouse click 400 300 secondary
mouse doubleclick 400 300 left
mouse rightclick 400 300
mouse drag 100 100 500 500 left 600
mouse context-menu 400 300
mouse wheel -120
clipboard assign "hello"
clipboard get
clipboard paste
file open C:\temp\a.txt
file copy C:\temp\a.txt C:\temp\b.txt --overwrite
file info C:\temp\a.txt
folder create C:\temp\work
folder zip C:\temp\work C:\temp\work.zip --overwrite
browser launch https://example.com
browser launch edge https://example.com
browser launch chrome https://example.com
browser launch firefox https://example.com
browser select edge
browser go https://example.org
browser back
browser forward
browser refresh
browser close
browser webdriver edge https://example.com
browser wait css h1 10000
browser text css h1 as heading
browser title as pageTitle
browser url as currentUrl
browser click css "a"
browser hover css "#menu"
browser double-click css "#item"
browser right-click css "#item"
browser type id q "Slasher browser test"
browser press id q CTRL+A
browser press ENTER
browser upload css "input[type=file]" "C:\tmp\sample.txt"
browser drag css "#source" to css "#target"
browser select-option id choice value b
browser selected-options id choice as selected
browser wait-download "C:\tmp\downloads" "*.csv" 30000 500 as downloaded
browser logs as consoleLogs
browser clear id q
browser attr css "a" href as firstLink
browser js "return document.title" as jsTitle
browser storage local set token abc123
browser storage local get token as token
browser wait text css "#result" "done" 10000 contains
browser links as pageLinks
browser screenshot
browser new-webdriver-tab https://example.org
browser windows as browserWindows
browser switch 0
browser close-tab
browser quit
mouse wheel 120 400 300
capture selected
capture full
log "checkpoint reached"
assert window exists Notepad 10000
assert selected title contains Notepad
assert foreground title contains Notepad
assert window not exists __unexpected_window__
fail "stop with a structured failure"
close
close all
```

Selenium-style `browser webdriver/current/title/url/find/click/hover/double-click/right-click/type/press/upload/drag/select-option/selected-options/wait-download/logs/clear/submit/text/attr/wait/js/cookies/storage/screenshot/links/windows/new-webdriver-tab/switch/close-tab/quit` commands use WebDriver sessions. Supported selector strategies are `css`, `xpath`, `id`, `name`, `tag`, `class`, `link`, and `partialLink`. Use `downloadDir=<path>` on `browser webdriver` to route browser downloads into a known folder. Use `browser logs` to read browser console messages where the driver supports WebDriver browser logs. The older `browser launch/open/select/go/back/forward/refresh/close/new-tab` commands remain OS/browser window commands.

Script example:

```text
# One command per line.
# Blank lines and lines starting with # are skipped.
start notepad.exe
wait 800
app select notepad
set message "hello from Slasher script"
foreground as win
text "${message} / ${win.title}"
capture selected
```

Scripts are stored in the browser's `localStorage` when you press `Save`.

Run a workspace script file through the server-side runner:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/scripts/run-file -Method Post -ContentType application/json -Body '{"path":"scripts/samples/ai-agent-smoke.slasher"}'
```

Validate a script without running it:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/scripts/check -Method Post -ContentType application/json -Body '{"path":"scripts/samples/ai-agent-smoke.slasher"}'
```

Sample server-side AI script:

```text
scripts/samples/ai-agent-smoke.slasher
```

For file runs, `run.entryPoint` and `events[].source.file` point to the script file, and `events[].source.line` points to the original line in that file.

Script files can be split with `include` or `import`:

```text
include lib/common.slasher
import lib/window-helpers.slasher
```

Included paths are resolved relative to the file that contains the `include` line. Inline scripts resolve includes from the Slasher workspace root. Included files must stay inside the workspace. Source metadata still points to the actual file and line that produced each event, so AI agents can report failures in helper files precisely.
On failure, `error.source.function` contains the current `step` / `test step` name, and `error.source.stack` contains include/import call sites. This makes it clear which file and logical function failed.

Sample split script:

```text
scripts/samples/include-main.slasher
```

Script variables:

```text
set name value
set global runName value
set file fileName value
set local stepName value
let name = value
foreground as win
app select notepad as app
text "${name}"
text "${win.title}"
vars
unset name
```

`_` contains the last command result. `selected` contains the currently selected window when a command selects one.
Unqualified `set`, `let`, `array`, `push`, `add`, and `unset` keep existing behavior and write to the global run scope unless the variable already exists in a narrower scope.
Explicit scopes are:

- `global` - visible to the whole run and included files
- `file` - visible only to commands from the same `.slasher` file
- `local` - visible only inside the current logical function, currently the active `step` / `test step`

Variable lookup resolves `local`, then `file`, then `global`. `vars` returns `global`, `file`, `local`, and merged `resolved` snapshots.

Scope sample:

```text
scripts/samples/scope-main.slasher
```

Functions:

```text
function addItem name amount
set local itemName "${name}"
add global total "${amount}"
return "${total}"
endfunction

call addItem alpha 2 as firstTotal
```

Each `call` creates a fresh `local` scope. Function parameters are assigned as local variables. Function definitions are skipped during normal top-level execution and run only when called. `return` exits the current function; `call ... as name` stores the returned value.

Function sample:

```text
scripts/samples/function-main.slasher
```

Arrays:

```text
array items alpha beta
push items gamma
get items 1 as second
length items as count
join items "," as csv
pop items as last
text "${items[0]} / ${items.length}"

foreach item in items
text "${iteration}: ${item}"
endforeach
```

`push` creates the array if it does not already exist. `foreach` sets the loop item variable plus `index` and `iteration`.

Control flow:

```text
if "${name}" == "value"
text "then branch"
else
text "else branch"
endif

repeat 3
text "repeat ${iteration}"
endrepeat

set i 0
while ${i} < 3
text "while ${i}"
add i 1
endwhile
```

Conditions support `==`, `!=`, `>`, `>=`, `<`, `<=`, `contains`, `startsWith`, `endsWith`, `exists`, `empty`, and `not`. Loops expose `index` starting at 0 and `iteration` starting at 1. `while` stops with an error after 1000 iterations.

Server-side AI test script assertions:

```text
test step "open notepad"
set message "hello"
wait 1 as pause
log "${message} / ${pause.waitedMs}"
vars
unset message
log "opened app"
assert window exists Notepad 10000
assert selected title contains Notepad
assert foreground title contains Notepad
assert window not exists Error
assert variable exists message
assert variable not exists missing
assert value "${message}" == hello
agent note "record an observation for the next agent"
test attach "artifacts/expected.txt" as expected-output
fail "explicit test failure"
```

`step "name"` and `test step "name"` set the current test step. Later events keep that step name until another step command changes it.
`agent note "message"` records an agent-facing note as a structured log entry.
`test attach "path" as role` copies a file into the run's `attachments` directory and adds it to the event evidence. Relative paths in inline scripts are resolved from the Slasher workspace root; relative paths in script files are resolved from the current `.slasher` file.
Per-run script logs are also written to `artifacts/runs/<run-id>/logs/script.log`, also available from `GET /automation/runs/{runId}/logs/script`.
Completed runs write a human-readable HTML report to `artifacts/runs/<run-id>/report.html`, also available from `GET /automation/runs/{runId}/report`.
HTML reports embed preview screenshots inline and keep full-size captures as artifact links.
The Web UI Run Report panel links to the HTML report, `summary.txt`, `script.log`, `events.jsonl`, and `run.json` after a script run. It also lists recent runs so a browser refresh or interrupted agent turn can reopen prior reports.
MCP `slasher_run_script` starts its response with a compact run summary, including `report.html`, `summary.txt`, `script.log`, failed source location, diagnostics, and the most relevant screenshot evidence.

Native child controls can be inspected from scripts before using coordinates:

```text
element tree selected depth 2 as controls
element find title OK match exact as okButton
element exists title OK match exact as okExists
element text title OK match exact as okText
element find class Button controlId 1 limit 5 as buttons
element click title OK match exact
assert element exists title OK match exact
assert element text title OK match exact contains OK
image match expected/button-ok.bmp selected threshold 0.99 as okImage
assert image match expected/button-ok.bmp selected threshold 0.99
```

`element tree` returns the selected window tree by default; use `foreground`, `selected`, or `handle <hwnd>` to choose the root. `element find`, `element exists`, `element text`, and `element click` support `title`, `class`, `controlId`, `match exact|contains`, `in selected|foreground|<hwnd>`, `depth`, `limit`, and `button`.
`assert element exists`, `assert element not exists`, and `assert element text` use the same query syntax. `assert element text` supports the same string operators as value assertions, including `==`, `!=`, `contains`, `startsWith`, and `endsWith`.
`image match <template.bmp> [selected|full] [threshold n] [maxWidth n] [maxHeight n] [step n]` and `assert image match ...` currently support uncompressed BMP templates. Relative paths resolve from the current `.slasher` file, or from the workspace root for inline scripts.
`wait screenStable [selected|full] [stableMs] [timeoutMs]` waits until repeated preview screenshots stop changing. Use it before assertions or captures after animations, app launches, or slow UI updates.
`assert screen contains <text> [selected|full]` is currently an OCR placeholder. It fails with `screen_contains_unavailable`, expected/actual metadata, and failure screenshots so agents can still inspect the evidence.
Failed events include `error.details.diagnostics` when Slasher can detect likely test setup problems such as no selected target window, selected/foreground mismatch, or a likely browser/Slasher control-surface capture. The Web UI Run Report displays these diagnostics inline.
When `capturePolicy.captureBeforeEachStep` or `capturePolicy.captureAfterEachStep` is true, each script event records `before` / `after` screenshot evidence using `capturePolicy.captureTarget`.

Server-side script variables support:

- `set name value` or `let name = value`
- `${name}` and `${object.property}` interpolation
- `command ... as name` result assignment
- `_` for the last command result
- `selected` for the current selected window
- `vars` to inspect variables
- `unset name` to remove a variable

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

Errors handled by `catch` allow the run to finish as `passed`. `finally` runs for both success and failure. If an error is not caught, the run remains `failed`.

Assertion failures return `assertion_failed`, include expected/actual values, and save error screenshots.
Malformed blocks such as a missing `endif` return structured script errors such as `block_not_closed` and also save error screenshots.
Script errors include `error.source.file`, `error.source.line`, `error.source.function`, and, for included files, `error.source.stack`. `summary.txt` also prints source as `@file:line#function`.

Mouse demo script:

```powershell
dotnet run --project src\Slasher\Slasher.csproj --urls http://127.0.0.1:5055
.\scripts\run-mouse-demo.ps1
```

The sample commands live in `scripts\samples\mouse-demo.slasher`. The demo saves captures to:

- `artifacts\shots\mouse-demo-context-menu.bmp`
- `artifacts\shots\mouse-demo-final.bmp`

Implemented command areas:

- Window: activate, close, close all, maximize, minimize, restore, resize/move, active title, wait for window
- File: copy, delete, rename, open, print, name, path, info
- Folder: create, copy, delete, rename, open, zip, unzip, info
- Application: select by process name or title, open/start, close by process name or process id
- Mouse: move, primary click, secondary click, doubleclick, context menu observation, drag and drop, wheel/scroll
- Keystroke: keys, text/type
- Clipboard: assign, get, clear, copy, paste
- Browser: launch/open URL, explicit Edge/Chrome/Firefox launch, browser selection, address-bar navigation, back, forward, refresh, close via standard keyboard shortcuts
- AI test assertions: log, fail, window exists / not exists, selected or foreground title assertions

Not implemented yet:

- Recorder object capture and UI element selectors
- Image recognition
- OCR
- Terminal emulator automation

Optional bearer-token protection:

```powershell
$env:SLASHER_API_KEY = "change-me"
dotnet run --project src\Slasher\Slasher.csproj --urls http://127.0.0.1:5055
```

Then send `Authorization: Bearer change-me`.

## API examples

Start an app:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/apps/start -Method Post -ContentType application/json -Body '{"fileName":"notepad.exe"}'
```

List windows:

```powershell
Invoke-RestMethod "http://127.0.0.1:5055/windows?title=Notepad"
```

Get the current foreground window:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/windows/foreground
```

Focus a window:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/windows/0x123456/focus -Method Post
```

Send a key chord:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/input/keys -Method Post -ContentType application/json -Body '{"keys":"CTRL+S"}'
```

Send text:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/input/text -Method Post -ContentType application/json -Body '{"text":"hello from Slasher"}'
```

Click:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/input/mouse -Method Post -ContentType application/json -Body '{"action":"click","x":400,"y":300,"button":"left"}'
```

Take a screenshot:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/screenshot -Method Post -ContentType application/json -Body '{}'
```

Take a smaller preview screenshot:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/screenshot -Method Post -ContentType application/json -Body '{"maxWidth":1280,"maxHeight":720}'
```

## Endpoints

- `GET /health`
- `POST /apps/start`
- `POST /apps/select`
- `GET /windows?title=&processId=`
- `GET /windows/foreground`
- `GET /windows/{handle}`
- `GET /windows/{handle}/elements`
- `GET /windows/{handle}/elements/find`
- `GET /elements/tree`
- `GET /elements/find`
- `GET /elements/exists`
- `GET /elements/text`
- `POST /elements/click`
- `POST /windows/{handle}/focus`
- `POST /windows/{handle}/close`
- `POST /windows/{handle}/move`
- `POST /windows/{handle}/state`
- `POST /input/keys`
- `POST /input/text`
- `POST /input/mouse`
- `POST /screenshot`
- `POST /screen/image-match`
- `POST /browser/open`
- `POST /browser/navigate`
- `GET /browser/current`
- `GET /browser/title`
- `GET /browser/url`
- `POST /browser/find`
- `POST /browser/click`
- `POST /browser/hover`
- `POST /browser/double-click`
- `POST /browser/right-click`
- `POST /browser/type`
- `POST /browser/press`
- `POST /browser/upload`
- `POST /browser/drag`
- `POST /browser/select-option`
- `POST /browser/selected-options`
- `POST /browser/clear`
- `POST /browser/submit`
- `POST /browser/text`
- `POST /browser/attribute`
- `POST /browser/wait`
- `POST /browser/wait-text`
- `POST /browser/js`
- `GET /browser/cookies`
- `POST /browser/storage/{storage}/get`
- `POST /browser/storage/{storage}/set`
- `POST /browser/screenshot`
- `GET /browser/links`
- `GET /browser/windows`
- `POST /browser/new-window`
- `POST /browser/switch-window`
- `POST /browser/close-window`
- `POST /browser/close`
- `POST /browser/downloads/wait`
- `GET /browser/logs`
- `POST /automation/runs`
- `GET /automation/runs?limit=20`
- `GET /automation/runs/{runId}`
- `GET /automation/runs/{runId}/events`
- `GET /automation/runs/{runId}/summary`
- `GET /automation/runs/{runId}/logs/script`
- `GET /automation/runs/{runId}/report`
- `GET /automation/runs/{runId}/artifacts/raw?path=...`
- `GET /automation/runs/{runId}/artifacts/content?path=...`
- `POST /scripts/check`
- `POST /scripts/run`
- `POST /scripts/run-file`

## Notes

- Run Slasher in the same interactive desktop session as the apps you want to control.
- The web UI is the initial operation surface. The JSON API remains available for scripts and agents.
- Screenshot responses currently return `image/bmp` as base64 to avoid extra runtime dependencies. Image matching currently accepts uncompressed 24-bit or 32-bit BMP templates.
- Selenium-style browser operations use Selenium WebDriver plus NuGet-provided ChromeDriver, GeckoDriver, and MSEdgeDriver binaries when available. Selenium Manager remains a fallback. The target browser itself must still be installed.
- Use `maxWidth` and `maxHeight` for preview captures. Server-side script runs keep full captures and add `*-preview` evidence for large screenshots.
- Windows may reject focus changes from a background process. Calling `/windows/{handle}/focus` from a user-initiated context is more reliable.
- Elevated apps generally require Slasher itself to run elevated before input/window control works against them.
- Keep the server bound to `127.0.0.1` unless you have added authentication and network controls.
