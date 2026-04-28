# Slasher Script Compiler Implementation Plan

This plan describes how to evolve Slasher Script from the current browser/MCP command runner into a typed, block-oriented language that can also be compiled into an executable.

## Goals

- Make Slasher a Windows application operation and test automation bridge for AI agents such as Codex.
- Enable AI agents to run a develop-test-debug loop against real Windows apps with observations, assertions, logs, and screenshots.
- Treat logs, screen captures, and structured errors as mandatory test artifacts.
- Keep Slasher useful as an interactive local control server and browser control panel.
- Support RPA-style usage as the second major use case.
- Define Slasher Script as a small general-purpose procedural language with RPA standard libraries.
- Support variables, scopes, typed values, blocks/functions, errors, arrays, objects, string/number operations, and RPA commands.
- Support three execution modes:
  - `run`: execute a script immediately.
  - `check`: parse and type-check without running.
  - `build`: generate an executable.
- Avoid duplicating script semantics between the Web UI, MCP bridge, CLI, and compiled bots.

## Non-Goals For The First Compiler Slice

- No custom bytecode VM.
- No JIT.
- No full IDE/debugger.
- No UI Automation object recorder in the compiler slice.
- No standalone bundled runtime executable in the first build target.
- No attempt to replace Codex, Playwright, or dedicated test frameworks.

The first compiled executable should be server-connected: it calls a running Slasher HTTP server.

## Product Priority

Slasher has two product goals, in this order:

1. AI-driven Windows app development and testing.
2. RPA-style local automation.

This priority affects implementation order. Features that improve AI observability, deterministic testing, failure diagnosis, and reproducible execution should come before broad RPA package coverage.

For AI agents, each action should answer:

- What did I target?
- What did I do?
- What happened?
- What evidence was captured?
- What should a test/assertion see next?
- Where are the logs, captures, and structured error details?

For RPA, each action should answer:

- Can this be repeated reliably?
- Can it wait/retry safely?
- Can it recover or report failure?
- Can it be packaged and run unattended?

## Target Architecture

```text
src/
  Slasher/
    HTTP server, Web UI, endpoint mapping

  Slasher.Runtime/
    typed client/runtime APIs:
      App, Window, Input, Mouse, Clipboard, File, Folder, Screen, Wait, Assert, Report

  Slasher.Script/
    lexer, parser, AST, binder, type checker, interpreter, C# code generator

  Slasher.Cli/
    slasher check/run/build

  Slasher.CompiledHost/
    template host used by build output
```

Current prototype files to migrate away from:

```text
src/Slasher/wwwroot/app.js
scripts/slasher-mcp.mjs
```

These should eventually call the shared script runtime instead of owning script semantics.

## Language Shape

Preferred syntax:

```text
global defaultTimeout: number = 10000

block main
  let message: string = "Slasher check"
  let apps: array<string> = ["notepad"]

  for app in apps
    try
      let win: window = call openApp(app)
      input.text(message)
      let shot: image = screen.capture(target: win)
      assert screen.contains(message, target: win, timeout: 3000)
    catch e
      log("failed: " + e.message)
      screen.capture() as failure
      fail(e.message)
    finally
      optional window.close(win)
    end
  end
end

block openApp(name: string) -> window
  app.start(name + ".exe")
  retry 10 delay 300
    app.select(name) as win
  end
  return win
end

call main()
```

Legacy command syntax remains as sugar during migration:

```text
start notepad.exe
text "hello"
capture selected
```

The parser should lower legacy commands into standard library calls.

## Type System

Initial value types:

- `string`
- `number`
- `bool`
- `array<T>`
- `object`
- `null`
- `window`
- `image`
- `element`
- `error`
- `any` only for transitional API results

Rules:

- `let` creates local variables.
- `global` creates script-global variables.
- `set` updates an existing variable.
- block parameters and return values may be typed.
- untyped `let` uses inference.
- undeclared variable reads are compile errors.
- incompatible assignments are compile errors unless the target is `any`.

## Standard Libraries

RPA packages should be exposed as namespaces:

- `app.start`, `app.select`, `app.close`
- `window.focus`, `window.move`, `window.close`, `window.title`
- `input.text`, `input.keys`
- `mouse.click`, `mouse.drag`, `mouse.scroll`
- `clipboard.get`, `clipboard.set`, `clipboard.clear`
- `file.copy`, `file.delete`, `file.readText`, `file.writeText`
- `folder.create`, `folder.copy`, `folder.delete`
- `screen.capture`, `screen.contains`
- `wait.window`, `wait.image`, `wait.text`, `wait.stable`
- `assert.true`, `assert.equals`, `assert.contains`
- `report.attach`, `report.step`

AI/test-oriented packages should be first-class:

- `observe.window`, `observe.screen`, `observe.diff`
- `assert.windowTitle`, `assert.screenContains`, `assert.imageContains`
- `test.step`, `test.fail`, `test.attach`
- `agent.note`, `agent.checkpoint`

These can be aliases over `screen`, `assert`, and `report`, but the naming makes agent-authored scripts easier to read.

Core language functions:

- `trim`, `lower`, `upper`, `replace`, `substring`
- `split`, `join`, `length`
- `matches`, `regexMatch`, `regexReplace`
- `number`, `string`, `bool`
- `round`, `floor`, `ceil`, `min`, `max`, `abs`
- `push`, `pop`

## Implementation Phases

### Phase A: AI Automation Contract

Tasks:

- Define the action/observation/result model before broad language work.
- Define mandatory log, capture, and error artifacts.
- Add a durable result envelope for every command:
  - action name
  - target identity
  - input parameters
  - result data
  - started/ended timestamps
  - success/failure
  - error details
  - screenshot/evidence paths when available
- Define a stable JSON report format for AI agents.
- Define artifact layout:
  - `run.json`
  - `events.jsonl`
  - `summary.txt`
  - `screenshots/`
- Define MCP expectations:
  - list windows
  - select target
  - act
  - capture observation
  - run script
  - return image evidence

Exit criteria:

- A Codex-facing prompt can rely on a stable response shape from Slasher actions.
- Failure reports include enough evidence for an AI to decide the next step.
- Each failed command has a structured error and, when possible, a screenshot.

### Phase 0: Stabilize Current Layout

Status: started.

Tasks:

- Keep `Program.cs` small.
- Keep endpoint mapping outside startup.
- Add architecture docs.
- Keep existing Web UI and MCP behavior working.

Done:

- `Api/SlasherEndpointExtensions.cs`
- `docs/architecture.md`

### Phase 1: Create Project Skeletons

Tasks:

- Add `src/Slasher.Runtime/Slasher.Runtime.csproj`.
- Add `src/Slasher.Script/Slasher.Script.csproj`.
- Add `src/Slasher.Cli/Slasher.Cli.csproj`.
- Add projects to `Slasher.sln`.
- Move shared DTOs or introduce runtime DTOs carefully.
- Keep server behavior unchanged.

Validation:

```powershell
dotnet build Slasher.sln
```

### Phase 2: Define Script Spec

Tasks:

- Create `docs/slasher-script.md`.
- Specify lexical rules, comments, strings, numbers, identifiers.
- Specify typed variables and scope.
- Specify expressions and precedence.
- Specify blocks, functions, loops, errors, retry, optional.
- Specify legacy command compatibility.
- Specify standard library namespaces.
- Add examples:
  - Notepad typing/capture
  - file loop
  - retry/wait
  - error capture/report

Exit criteria:

- New syntax is documented enough to implement parser tests.

### Phase 3: Lexer And Parser

Tasks:

- Implement tokenizer.
- Implement AST model.
- Implement parser for:
  - literals
  - expressions
  - variable declarations
  - assignments
  - function calls
  - `block/call/return`
  - `if/else/end`
  - `for/while/repeat/end`
  - `try/catch/finally/end`
  - `retry/end`
  - `optional`
- Implement legacy command lowering.

Testing:

- Unit tests for tokenizer.
- Unit tests for parser AST snapshots.
- Parser failure tests with line/column diagnostics.

### Phase 4: Binder And Type Checker

Tasks:

- Implement symbol tables.
- Implement global/local scopes.
- Implement block parameter/return binding.
- Implement standard library signatures.
- Implement type inference for `let`.
- Implement assignment compatibility checks.
- Implement unknown variable/function diagnostics.

Diagnostics should include:

- code
- message
- file
- line
- column
- hint when possible

CLI:

```powershell
slasher check examples\notepad.slasher
```

### Phase 5: Runtime Client

Tasks:

- Implement `Slasher.Runtime` HTTP client for current server endpoints.
- Provide typed services:
  - `AppRuntime`
  - `WindowRuntime`
  - `InputRuntime`
  - `MouseRuntime`
  - `ScreenRuntime`
  - `ClipboardRuntime`
  - `FileRuntime`
  - `FolderRuntime`
- Add configurable server URL and API key.
- Map HTTP errors into structured runtime exceptions.

Exit criteria:

- C# code can perform the current Notepad scenario through the runtime client.

### Phase 6: Interpreter

Tasks:

- Execute the checked AST directly.
- Implement variables, scopes, arrays, objects.
- Implement calls and returns.
- Implement errors and `try/catch/finally`.
- Implement `retry`, `optional`, and timeout options.
- Invoke `Slasher.Runtime` for RPA calls.
- Produce structured execution events and reports for AI agents.
- Capture logs and screenshots according to run policy.
- Attach errors to the run report.

Consumers:

- `Slasher.Cli run`
- Web UI script runner later
- MCP `slasher_run_script` later

Exit criteria:

```powershell
slasher run examples\notepad.slasher
```

### Phase 7: C# Code Generator

Tasks:

- Generate a C# `Program.cs` from checked AST.
- Generate typed local variables and async runtime calls.
- Generate C# for:
  - expressions
  - blocks
  - loops
  - try/catch/finally
  - retry
  - optional
- Generate diagnostics for unsupported constructs.

Generated code should target `Slasher.Runtime`.

Exit criteria:

- Generated C# compiles for the Notepad scenario.

### Phase 8: Build Command

Tasks:

- Implement `slasher build script.slasher -o dist\Bot.exe`.
- Create temporary project.
- Reference `Slasher.Runtime`.
- Write generated C#.
- Run `dotnet publish`.
- Support:
  - framework-dependent exe
  - self-contained exe later
  - server URL baked in or passed at runtime

Example:

```powershell
slasher build examples\notepad.slasher -o dist\NotepadBot.exe
```

Exit criteria:

- The built executable runs the Notepad scenario through a running Slasher server.

### Phase 9: Web And MCP Migration

Tasks:

- Replace browser-local script semantics with calls to shared script engine. Done for Web UI run/check.
- Replace duplicated MCP script runner with `Slasher.Script` behavior. Done for MCP run/check.
- Keep command bar simple commands working.
- Keep legacy syntax compatible.

Options:

- server-side script execution endpoint
- CLI-backed execution
- generated JS parser mirror only if necessary

Preferred first step:

- Add `POST /scripts/run` to server and make Web UI/MCP call it. Done.

### Phase 10: Reporting And RPA Hardening

Tasks:

- Add structured execution reports.
- Add per-step timestamps.
- Add failure screenshots.
- Add per-run log files.
- Add `report.attach`.
- Add `test.step`, `test.attach`, and `agent.note`.
- Add `wait.screenStable`.
- Add `assert` package.
- Add safer close handling.
- Add configurable stop/pause.

## First Vertical Slice

The first end-to-end compiler slice should be this script:

```text
block main
  test.step("open notepad")
  app.start("notepad.exe")
  wait.ms(1000)
  app.select("notepad") as note

  test.step("type text")
  let message: string = "Slasher compiled check"
  input.text(message)

  test.step("observe and assert")
  screen.capture(target: note, path: "artifacts/shots/compiled-notepad.bmp") as shot
  test.attach(shot)
  assert.screenContains(message, target: note, timeout: 3000)

  test.step("cleanup")
  optional window.close(note)
end

call main()
```

It must work in all three modes:

```powershell
slasher check examples\compiled-notepad.slasher
slasher run examples\compiled-notepad.slasher
slasher build examples\compiled-notepad.slasher -o dist\CompiledNotepad.exe
```

## Risks

- Syntax churn: mitigate by documenting before coding.
- Duplication between Web/MCP/CLI: mitigate by moving execution to server/shared engine.
- Windows focus restrictions: runtime commands must return rich diagnostics and screenshots.
- Compiled executable distribution: start server-connected before standalone runtime.
- Type system overreach: keep `any` only as a bridge for existing JSON responses.

## Recommended Next Task

Implement Phase A, Phase 1, and Phase 2 together:

1. Define the AI action/observation/report envelope.
2. Define logging, screen capture, and structured error behavior.
3. Add project skeletons.
4. Write `docs/slasher-script.md`.
5. Add a small parser test project or test folder.
6. Keep existing server/Web/MCP behavior unchanged.
