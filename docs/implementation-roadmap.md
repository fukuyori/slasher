# Slasher Implementation Roadmap

Slasher's primary goal is to let AI agents such as Codex operate, test, and debug real Windows applications. Its secondary goal is RPA-style local automation.

This roadmap defines the implementation phases needed to reach that goal while keeping the codebase maintainable and extensible.

## Guiding Principles

1. AI observability comes first.
   - Every action should produce logs, captures, target metadata, and structured errors.
   - The agent should not need to guess what happened.

2. The script language should be small but refined.
   - General-purpose procedural core.
   - Typed variables.
   - Blocks/functions.
   - Clear scopes.
   - Strong standard libraries for Windows automation and testing.

3. RPA actions should be libraries, not syntax bloat.
   - `app.start`, `window.focus`, `input.text`, `screen.capture`, `assert.screenContains`.
   - Legacy commands such as `start notepad.exe` remain as sugar.

4. Compile-to-exe should be designed in from the start.
   - First target: generated C# that calls a running Slasher server.
   - Later target: standalone runtime-bundled executable.

5. Web UI, MCP, CLI, and compiled bots should share semantics.
   - Avoid duplicated script implementations.
   - Move toward a shared script engine and runtime.

## Phase A: AI Automation Contract

### Purpose

Define the stable action, observation, logging, screenshot, and error model that Codex and other agents can rely on.

This phase is higher priority than broad RPA feature expansion.

### Deliverables

- `docs/ai-automation-contract.md`
- `docs/ai-test-observability.md` updates as needed
- action result envelope
- execution event schema
- run report schema
- artifact layout
- MCP response expectations

### Design Output

Every action should eventually produce:

```json
{
  "sequence": 1,
  "action": "input.text",
  "target": {
    "kind": "window",
    "handle": "0x123456",
    "title": "Untitled - Notepad",
    "processName": "Notepad"
  },
  "parameters": {},
  "result": {},
  "logs": [],
  "captures": [],
  "error": null,
  "startedAt": "2026-04-28T00:00:00Z",
  "endedAt": "2026-04-28T00:00:01Z",
  "durationMs": 1000,
  "ok": true
}
```

Every script run should produce:

```text
artifacts/runs/<run-id>/
  run.json
  events.jsonl
  summary.txt
  screenshots/
```

### Implementation Tasks

- Define C# DTOs for run reports and execution events.
- Define error DTOs with code, message, line, command, target, and capture path.
- Decide when captures are created:
  - on demand
  - on error
  - on assertion failure
  - optional per-step capture
- Decide how MCP returns evidence:
  - text summary
  - image content for most relevant screenshot
  - paths to full artifacts
- Add document examples for:
  - success event
  - failure event
  - assertion failure
  - run summary
- Choose initial implementation location for report DTOs.
- Choose artifact directory naming rules.

### Completion Criteria

- The report/event schema is documented.
- A future script runner can implement the schema without ambiguity.
- A Codex prompt can be written against the schema.
- The contract answers how MCP, HTTP, script runs, and compiled runs report evidence.

### Risks

- Too much schema too early.
- Evidence files becoming too large.
- Capturing every step slowing tests down.

### Mitigation

- Start with JSONL events and file paths.
- Make per-step screenshots configurable.
- Always capture on error and assertion failure.

## Phase 0: Stabilize Current Server Layout

### Purpose

Keep existing features working while making future changes easier.

### Current Status

Started.

Already done:

- `Program.cs` reduced to startup/middleware/service registration.
- Endpoint mapping moved to `Api/SlasherEndpointExtensions.cs`.
- Architecture notes added.

### Deliverables

- Small `Program.cs`.
- Organized endpoint mapping.
- Architecture documentation.
- Existing Web UI and MCP behavior preserved.

### Implementation Tasks

- Continue splitting endpoint groups when they grow:
  - `ApplicationEndpoints`
  - `WindowEndpoints`
  - `InputEndpoints`
  - `FileSystemEndpoints`
  - `ScriptEndpoints`
- Keep DTOs grouped by action package.
- Keep `WindowsAutomationService` stable until runtime split begins.

### Completion Criteria

- Existing commands still work.
- Server starts with the same command.
- Build passes when no running server locks output files.

### Risks

- Refactor accidentally changes endpoint behavior.

### Mitigation

- Keep changes mechanical.
- Add smoke scripts for Notepad and screenshot capture.

## Phase 1: Project Skeleton Split

### Purpose

Prepare the solution for shared runtime, script compiler, CLI, and compiled bot support.

### Deliverables

```text
src/Slasher.Runtime/
src/Slasher.Script/
src/Slasher.Cli/
```

Later:

```text
src/Slasher.CompiledHost/
```

### Implementation Tasks

- Add `Slasher.Runtime` class library.
- Add `Slasher.Script` class library.
- Add `Slasher.Cli` console app.
- Add projects to `Slasher.sln`.
- Add references:
  - `Slasher.Cli` -> `Slasher.Script`, `Slasher.Runtime`
  - `Slasher.Script` -> no server dependency if possible
  - `Slasher.Runtime` -> DTOs/client abstractions
- Decide where shared DTOs live:
  - option A: keep server DTOs in `Slasher` initially
  - option B: move common DTOs to `Slasher.Runtime`
  - recommended: introduce runtime DTOs gradually to avoid breaking server

### Completion Criteria

- Solution builds.
- Existing server behavior unchanged.
- CLI can print version/help.

### Risks

- Moving DTOs too early creates churn.

### Mitigation

- Add new DTOs first.
- Move old DTOs only when needed.

## Phase 2: Slasher Script Language Specification

### Purpose

Define the refined language before implementing the compiler.

### Deliverables

- `docs/slasher-script.md`
- example scripts under `examples/`

### Language Decisions

Core style:

```slasher
global timeout: number = 10000

block main
  let message: string = "hello"
  let note: window = call openApp("notepad")
  input.text(message)
end
```

Types:

- `string`
- `number`
- `bool`
- `array<T>`
- `object`
- `window`
- `image`
- `element`
- `error`
- `null`
- `any` for transitional values only

Statements:

- `let`
- `global`
- `set`
- `block`
- `call`
- `return`
- `if/else/end`
- `for/end`
- `while/end`
- `repeat/end`
- `try/catch/finally/end`
- `retry/end`
- `optional`
- `fail`
- `log`

Expressions:

- arithmetic: `+ - * / %`
- comparison: `== != > >= < <=`
- boolean: `and or not`
- string operations: `contains startsWith endsWith matches`
- calls: `trim(value)`, `length(items)`
- member access: `note.title`
- index access: `items[0]`

Standard library namespaces:

- `app`
- `window`
- `input`
- `mouse`
- `clipboard`
- `file`
- `folder`
- `screen`
- `wait`
- `assert`
- `test`
- `agent`
- `report`

### Implementation Tasks

- Write grammar overview.
- Write type rules.
- Write scope rules.
- Write error handling rules.
- Write standard library function signatures.
- Write legacy command compatibility section.
- Write examples:
  - Notepad type/capture/assert
  - retry selecting a window
  - foreach over files/apps
  - try/catch/finally with failure capture
  - compile-ready script

### Completion Criteria

- Parser can be implemented from the spec.
- Users can understand the preferred syntax.
- Legacy syntax compatibility is explicitly scoped.

### Risks

- Over-designing the language.

### Mitigation

- Keep the core small.
- Put most behavior in standard libraries.

## Phase 3: Lexer, Parser, And AST

### Purpose

Turn `.slasher` source into a structured syntax tree.

### Deliverables

- tokenizer
- parser
- AST model
- diagnostics with line/column
- parser tests

### Implementation Tasks

- Implement tokens:
  - identifiers
  - keywords
  - strings
  - numbers
  - punctuation
  - comments
- Implement expression parser with precedence.
- Implement statement parser.
- Implement block parser.
- Implement legacy command lowering.
- Add AST snapshot tests.
- Add syntax error tests.

### Completion Criteria

- `slasher check` can parse scripts and report syntax errors.
- Parser handles the first vertical slice script.

### Risks

- Legacy command syntax conflicts with function-call syntax.

### Mitigation

- Parse preferred syntax first.
- Treat legacy commands as line-level fallback.

## Phase 4: Binder And Type Checker

### Purpose

Catch mistakes before scripts run or compile.

### Deliverables

- symbol table
- scope resolver
- type checker
- standard library signatures
- diagnostics

### Implementation Tasks

- Resolve global/local variables.
- Resolve block names and parameters.
- Resolve function calls.
- Infer `let` types.
- Validate assignments.
- Validate return types.
- Validate `try/catch` error variable type.
- Validate `for` item type.
- Support `any` for transitional JSON-like values.

### Completion Criteria

- `slasher check` catches:
  - unknown variables
  - unknown functions
  - type mismatches
  - wrong argument count
  - wrong return type

### Risks

- Runtime API results may be dynamic.

### Mitigation

- Use typed wrappers for common APIs.
- Use `object` or `any` only at edges.

## Phase 5: Slasher Runtime Client

### Purpose

Provide one typed automation runtime used by interpreter, CLI, compiled bots, and later server-side script execution.

### Deliverables

- `Slasher.Runtime` HTTP client.
- typed service classes.
- structured runtime exceptions.

### Runtime Services

- `AppRuntime`
- `WindowRuntime`
- `InputRuntime`
- `MouseRuntime`
- `ScreenRuntime`
- `ClipboardRuntime`
- `FileRuntime`
- `FolderRuntime`
- `WaitRuntime`
- `AssertRuntime`
- `ReportRuntime`

### Implementation Tasks

- Add `SlasherClient`.
- Add server URL/API key configuration.
- Add typed methods for current endpoints.
- Add response DTOs.
- Add runtime exception type:
  - code
  - message
  - action
  - target
  - raw response
- Add Notepad scenario as runtime smoke test.

### Completion Criteria

- A C# console app can:
  - start Notepad
  - select Notepad
  - type text
  - capture screenshot
  - close window

### Risks

- Server-connected runtime requires server to be running.

### Mitigation

- Clear error when server is unavailable.
- Later add runtime-bundled mode.

## Phase 6: Interpreter

### Purpose

Execute checked AST directly.

### Deliverables

- AST interpreter.
- variable scope runtime.
- execution event logging.
- run artifact creation.
- CLI `run`.

### Implementation Tasks

- Implement value model.
- Implement global/local scopes.
- Implement expression evaluator.
- Implement block calls and returns.
- Implement `if`, `for`, `while`, `repeat`.
- Implement `try/catch/finally`.
- Implement `retry`.
- Implement `optional`.
- Invoke `Slasher.Runtime`.
- Create `run.json`, `events.jsonl`, `summary.txt`.
- Capture screenshot on error.

### Completion Criteria

- `slasher run examples/notepad.slasher` works.
- Failure produces logs and screenshot.
- MCP/Web can later call the same engine.

### Risks

- Interpreter and code generator semantics diverge later.

### Mitigation

- Share AST, binder, type checker.
- Add semantic tests that run in both modes.

## Phase 7: C# Code Generator

### Purpose

Generate C# source from checked AST.

### Deliverables

- code generator
- generated project template
- generated source inspection option

### Implementation Tasks

- Generate async `Main`.
- Generate variables with C# types.
- Generate blocks as methods.
- Generate expressions.
- Generate loops and conditions.
- Generate try/catch/finally.
- Generate retry helper calls.
- Generate runtime API calls.
- Generate report/event calls.

### Completion Criteria

- Generated code compiles for the first vertical slice.
- Generated code runs through a Slasher server.

### Risks

- Complex language features difficult to generate.

### Mitigation

- Start with a small supported subset.
- Fail clearly for unsupported constructs.

## Phase 8: Build Command

### Purpose

Create executable files from `.slasher` scripts.

### Deliverables

- `slasher build`
- temporary build project generation
- publish output

### Implementation Tasks

- Add `slasher build <script> -o <path>`.
- Run parse/bind/type-check.
- Generate C#.
- Create temporary project.
- Reference `Slasher.Runtime`.
- Run `dotnet publish`.
- Copy output to destination.
- Support:
  - framework-dependent executable
  - self-contained later
  - server URL argument/env var

### Completion Criteria

- `dist/CompiledNotepad.exe` can run the Notepad scenario.

### Risks

- Build environment differences.

### Mitigation

- Log generated project path.
- Keep generated source on failure.

## Phase 9: Web And MCP Migration

### Purpose

Remove duplicate script semantics from browser JS and MCP JS.

### Deliverables

- server-side script run endpoint: Done
- Web UI calls shared engine: Done for script run/check
- MCP calls shared engine: Done for script run/check

### Implementation Tasks

- Add `POST /scripts/check`. Done for parse, include resolution, and block structure validation.
- Add `POST /scripts/run`. Done for the current server-side runner.
- Return run report envelope. Done.
- Web UI displays events and captures. Done for run summary, event list, error details, screenshot evidence, and report/artifact links.
- MCP returns structured text plus relevant image. Done for script runs.
- Keep command bar quick commands working. Preserved.
- Keep legacy syntax working. Preserved through the server runner.

### Completion Criteria

- Web and MCP run the same script through the same engine.
- Existing sample scripts still work or have documented migration.

### Risks

- Browser-only localStorage script workflows need migration.

### Mitigation

- Keep textarea UI.
- Server executes posted script text.

## Phase 10: Test Observability Hardening

### Purpose

Make Slasher excellent for AI-driven testing.

### Deliverables

- robust reports
- assertions
- failure screenshots
- test steps
- log capture

### Implementation Tasks

- Add `test.step`. Done for server-side scripts.
- Add `test.attach`. Done for file attachments copied into run artifacts.
- Add `agent.note`. Done as structured note logs and `logs/script.log` entries.
- Add `assert` package. Started for value, variable, window, selected, and foreground assertions.
- Add `screen.contains` placeholder implementation. Done as `assert screen contains` with OCR-unavailable failure metadata and screenshots.
- Add `wait.screenStable`. Started with preview-screenshot stability polling.
- Add HTML report later. Done for completed script runs as `report.html`, inline preview screenshots, and `GET /automation/runs/{runId}/report`.
- Add summary for MCP. Done for script run status, artifact paths, HTML report path, failed event source, diagnostics, and most relevant screenshot evidence.
- Add log and artifact readback. Done with `GET /automation/runs/{runId}/logs/script` and raw artifact streaming for HTML/browser display.
- Add MCP run readback. Done with `slasher_list_runs`, `slasher_get_run`, `slasher_get_run_log`, and `slasher_get_artifact`.
- Add per-step screenshot policy. Done for before/after evidence based on `CapturePolicy`.
- Add common failure diagnostics. Started for missing selected target, selected/foreground mismatch, likely control-surface foreground windows, and Web UI inline display.
- Add recent run discovery. Done with `GET /automation/runs?limit=20` and the Web UI Recent Runs list.

### Completion Criteria

- Failed test gives:
  - error code/message
  - line
  - action
  - target
  - screenshot
  - logs
  - summary

### Risks

- OCR/image matching may not be ready.

### Mitigation

- Start with screenshots and window metadata.
- Add OCR/image later.

## Phase 11: UI Automation, OCR, And Image Recognition

### Purpose

Reduce coordinate fragility and improve test assertions.

### Deliverables

- UI Automation tree endpoint.
- element selectors.
- OCR integration.
- image matching.

### Implementation Tasks

- Add UIA service. Started with native child window/control tree readback via `GET /elements/tree`, `GET /windows/{handle}/elements`, and MCP `slasher_get_element_tree`.
- Add `element.find`. Started with native child control search via `GET /elements/find`, `GET /windows/{handle}/elements/find`, MCP `slasher_find_elements`, and script `element find`.
- Add `element.click`. Started with native child control center-click via `POST /elements/click`, MCP `slasher_click_element`, and script `element click`.
- Add `element.text`. Started with native child control title readback via `GET /elements/text`, MCP `slasher_get_element_text`, and script `element text`.
- Add `element.exists`. Started with native child control existence checks via `GET /elements/exists`, MCP `slasher_element_exists`, and script `element exists`.
- Add element assertions. Started with script `assert element exists`, `assert element not exists`, and `assert element text`.
- Add OCR command.
- Add image matching command. Started with `POST /screen/image-match`, MCP `slasher_match_image`, script `image match`, and `assert image match` for uncompressed BMP templates.
- Add context menu item extraction.

### Completion Criteria

- Scripts can click buttons by name/AutomationId.
- Tests can assert visible text without manual inspection.

### Risks

- UIA behavior varies by app framework.

### Mitigation

- Keep screenshot fallback.
- Return rich UIA diagnostics.

## Phase 12: RPA Expansion

### Purpose

Broaden unattended automation features after AI/test foundation is strong.

### Deliverables

- data processing
- triggers
- credentials
- reporting distribution

### Implementation Tasks

- Add Selenium-style browser operations for web app testing. Started with Selenium WebDriver-backed `POST /browser/open`, `POST /browser/find`, `POST /browser/click`, `POST /browser/hover`, `POST /browser/double-click`, `POST /browser/right-click`, `POST /browser/type`, `POST /browser/press`, `POST /browser/upload`, `POST /browser/drag`, `POST /browser/select-option`, `POST /browser/selected-options`, `POST /browser/downloads/wait`, `GET /browser/logs`, `POST /browser/clear`, `POST /browser/submit`, `POST /browser/text`, `POST /browser/attribute`, `POST /browser/wait`, `POST /browser/wait-text`, `POST /browser/js`, `GET /browser/cookies`, `POST /browser/storage/{storage}/get`, `POST /browser/storage/{storage}/set`, `POST /browser/screenshot`, `GET /browser/links`, `GET /browser/windows`, `POST /browser/new-window`, `POST /browser/switch-window`, `POST /browser/close-window`, `POST /browser/close`, MCP `slasher_browser_*`, and script `browser webdriver/current/title/url/find/click/hover/double-click/right-click/type/press/upload/drag/select-option/selected-options/wait-download/logs/clear/submit/text/attr/wait/js/cookies/storage/screenshot/links/windows/new-webdriver-tab/switch/close-tab/quit`.
- Keep OS-level browser commands for exploratory or non-DOM workflows.
- Add richer browser work later: network/devtools hooks and richer download metadata.
- CSV read/write.
- JSON read/write.
- Excel support.
- file watchers.
- download wait. Basic local directory wait is implemented for browser tests.
- scheduling hooks.
- credential/secrets handling.
- report export.
- safer destructive actions.

### Completion Criteria

- Common office-style workflows can be automated reliably.

### Risks

- Scope grows too broad.

### Mitigation

- Add packages one by one.
- Keep test evidence model common.

## First Vertical Slice

The first cross-phase scenario should be:

```slasher
block main
  test.step("open notepad")
  app.start("notepad.exe")
  wait.ms(1000)
  app.select("notepad") as note

  test.step("type text")
  let message: string = "Slasher compiled check"
  input.text(message)

  test.step("capture evidence")
  screen.capture(target: note, path: "artifacts/shots/compiled-notepad.bmp") as shot
  test.attach(shot)

  test.step("cleanup")
  optional window.close(note)
end

call main()
```

It should eventually work in these modes:

```powershell
slasher check examples\compiled-notepad.slasher
slasher run examples\compiled-notepad.slasher
slasher build examples\compiled-notepad.slasher -o dist\CompiledNotepad.exe
```

## Recommended Immediate Next Steps

1. Create `docs/ai-automation-contract.md`.
2. Create `docs/slasher-script.md`.
3. Add project skeletons:
   - `Slasher.Runtime`
   - `Slasher.Script`
   - `Slasher.Cli`
4. Add CLI `--help`.
5. Add the first parser skeleton and tests.
6. Add run artifact DTOs.

## Tracking Checklist

- [ ] Phase A documented and approved
- [ ] Phase 0 complete
- [ ] Phase 1 project skeletons
- [ ] Phase 2 language spec
- [ ] Phase 3 parser
- [ ] Phase 4 type checker
- [ ] Phase 5 runtime client
- [ ] Phase 6 interpreter
- [ ] Phase 7 code generator
- [ ] Phase 8 build command
- [x] Phase 9 Web/MCP migration
- [ ] Phase 10 observability hardening
- [ ] Phase 11 UIA/OCR/image
- [ ] Phase 12 RPA expansion
