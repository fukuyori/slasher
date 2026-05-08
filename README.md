# Slasher

Current version: 0.2.4.

Slasher is a small Windows automation server written in C#. It exposes HTTP APIs for starting applications, enumerating and manipulating windows, sending keyboard and mouse input, and taking screenshots.

The intended use cases are local RPA-style workflows and AI-driven application testing.

## Direction

Slasher currently follows a simple local automation loop:

- Slasher executes GUI actions and captures observations
- a human or AI agent chooses the next action from those observations
- each step should be easy to inspect before and after it runs

The first control surface is a local web UI. It is intentionally direct: choose a target window, focus it, send input, capture the result, then decide the next step.

The longer-term architecture keeps that evidence-first loop, but makes the core
portable and able to coordinate multiple Slasher instances. The design borrows
Plan 9's namespace idea and a HarmonyOS-like device coordination model: remote
machines are not raw mounts or generic remote-control targets; they are trusted
peers that expose typed Slasher resources through policy-gated namespaces.

## Documentation

Start here:

- `docs/README.md` - documentation index
- `docs/ai-agent-guide.md` - practical guide for AI agents using Slasher
- `docs/implementation-roadmap.md` - current implementation status and next work
- `docs/development-schedule.md` - whole-project development order across RPA and peer work
- `docs/language-system.md` - entry point for Slasher's Numadora-based script direction

Design references:

- `docs/architecture.md` - server structure and ownership
- `docs/ai-automation-contract.md` - run/event/error/evidence contract
- `docs/ai-test-observability.md` - logging, capture, and evidence design
- `docs/security-policy.md` - security rules for powerful local PC automation
- `docs/peer-network-model.md` - Plan 9/HarmonyOS-inspired peer namespace and portable-core model
- `docs/peer-implementation-plan.md` - implementation phases for peer namespace and portable core
- `docs/phase-12-rpa-expansion-plan.md` - RPA package expansion plan

Language references:

- `docs/numadora-migration-plan.md` - implementation plan for using Numadora in Slasher scripts
- `docs/numadora-runtime-contract.md` - Phase N0 runtime/check/run boundary contract
- `docs/slasher-script.md` - current Numadora script profile used by Slasher
- `docs/numadora-language-spec.md` - Numadora の汎用言語仕様
- `docs/slasher-numadora-integration.md` - Slasher bindings and Numadora integration model
- `docs/migration-from-slasher-v1.md` - `.slasher` から `.numa` への移行ガイド

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

## Numadora N0 Probe

During the Numadora migration, the local Numadora source checkout can be probed
with:

```powershell
.\scripts\check-numadora.ps1 -Path scripts\numadora-samples\notepad-check.numa
```

Set `NUMADORA_HOME` if Numadora is not located at
`D:\home\source\rust\Numadora`.

To verify the full N0 probe state:

```powershell
.\scripts\verify-numadora-n0.ps1
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

Current script surface:

- Slasher script execution is Numadora-only.
- `.slasher` paths and `language=slasher` are rejected by the public check/run APIs.
- Historical `.slasher` migration notes live in `docs/migration-from-slasher-v1.md`.
- Current Slasher bindings and examples live under `docs/slasher-script.md`,
  `docs/slasher-numadora-integration.md`, and `scripts/numadora-samples/`.

Example Numadora script:

```text
IMPORT slasher_io AS io
IMPORT slasher_app AS app
IMPORT slasher_window AS window

FUNC main()
  io.Log("hello from Codex through Slasher")
  app.Start("notepad.exe")
  window.WaitForTitle("Notepad", 10000)
END
```

Run a workspace Numadora script file through the server-side runner:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/scripts/run-file -Method Post -ContentType application/json -Body '{"language":"numadora","path":"scripts/numadora-samples/notepad-check.numa","purpose":"local-test"}'
```

Validate a script without running GUI automation actions:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/scripts/check -Method Post -ContentType application/json -Body '{"language":"numadora","path":"scripts/numadora-samples/notepad-check.numa"}'
```

For file runs, `run.entryPoint` and `events[].source.file` point to the script
file when source information is available. Completed runs write `run.json`,
`events.jsonl`, `summary.txt`, `logs/script.log`, and `report.html` under
`artifacts/runs/<run-id>/`. The Web UI Run Report panel and MCP
`slasher_get_run`/`slasher_get_artifact` tools read the same artifacts.

Current implemented automation areas:

- Window and app observation/action APIs
- keyboard, mouse, clipboard, screen capture, image matching, and native element APIs
- Selenium WebDriver browser automation for Edge, Chrome, and Firefox
- server-side Numadora check/run preflight and structured run artifacts
- policy-gated Numadora observe and selected interactive host calls

Planned or design-stage areas:

- Phase 12 CSV/JSON/Excel and safer destructive RPA packages
- OCR command
- richer UI Automation selector model
- peer resource namespace and Slasher-to-Slasher communication
- portable core extraction

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
