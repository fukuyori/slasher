# Numadora Runtime Contract

This document freezes the first integration boundary between Slasher and a
Numadora runtime.

It belongs to Phase N0 of `numadora-migration-plan.md`.

## Runtime Strategy

The first implementation should use a **process bridge**:

- Slasher invokes an external Numadora command for check/run.
- Slasher owns run artifact creation and HTTP/MCP response shape.
- Numadora owns parsing, type checking, module loading, macro expansion, and
  script execution.
- Slasher automation actions are exposed through host calls implemented by the
  Slasher server.

Embedding the runtime can be reconsidered after the first `.numa` vertical
slice works and performance or packaging data justifies it.

## Compatibility Path Decision

Decision: adapt Slasher's Numadora-facing implementation to the currently
available Numadora language/runtime surface.

Rejected alternative: preserve Slasher's planned slash-import and command-style
surface by adding a Slasher-side source adapter.

Rationale:

- Slasher should not create a second dialect that only looks like Numadora.
- Current Numadora already supports sibling modules, `IMPORT module AS alias`,
  exported functions, typed signatures, `FUNC main()`, and normal function
  calls.
- Slasher examples can be expressed in that surface today using modules such as
  `slasher_app` and calls such as `app.Start("notepad.exe")`.
- Host integration can be added behind those functions later without changing
  the script shape.

N1/N2 should therefore prefer:

- current Numadora module syntax (`IMPORT slasher_app AS app`)
- current Numadora type spelling (`Int`, `String`, `Bool`, `Array<T>`)
- normal alias-qualified function calls (`app.Start(...)`)
- Slasher-side host bindings that conform to this shape

Potential Numadora runtime changes should be limited to general-purpose host
binding support, not Slasher-specific syntax.

## Runtime Discovery

Development discovery order:

1. `NUMADORA_HOME` environment variable, if set.
2. `D:\home\source\rust\Numadora`, when it exists.
3. Future release layout, to be decided when Slasher packages Numadora.

The current local Numadora checkout reports:

```text
Numadora 0.0.1 (Phase 0/1 prototype)
```

Manual check helper:

```powershell
.\scripts\check-numadora.ps1 -Path scripts\numadora-samples\notepad-check.numa
```

This helper uses a separate Cargo target directory under Slasher's workspace
(`.numadora-target`) by default to avoid local `.cargo-lock` contention in the
Numadora repository and to keep Slasher-side probes out of the Numadora
worktree.

## Repository Layout

```text
numadora-bindings/
  slasher/
    README.md
scripts/
  numadora-samples/
    notepad-check.numa
    slasher_app.numa
    slasher_window.numa
    slasher_input.numa
    slasher_io.numa
    slasher_test.numa
```

`numadora-bindings/slasher/` contains design notes for future host bindings.
`scripts/numadora-samples/` contains current Numadora-compatible examples and
stub modules that pass `check` today.

## Check Contract

Input:

```json
{
  "language": "numadora",
  "script": "IMPORT slasher_io AS io\nio.Log(\"hello\")\n",
  "path": null,
  "workspaceRoot": "D:\\home\\source\\csharp\\slasher",
  "bindingRoots": ["numadora-bindings"],
  "entryPoint": "<inline>"
}
```

Output:

```json
{
  "ok": true,
  "language": "numadora",
  "diagnostics": [],
  "files": [
    {
      "path": "<inline>",
      "lineCount": 2
    }
  ]
}
```

Diagnostic output:

```json
{
  "ok": false,
  "language": "numadora",
  "diagnostics": [
    {
      "code": "name_undefined_module",
      "message": "module 'slasher_foo' was not found",
      "file": "scripts/numadora-samples/example.numa",
      "line": 1,
      "column": 8,
      "severity": "error",
      "details": {
        "module": "slasher_foo"
      }
    }
  ]
}
```

Mapping rules:

- Slasher should preserve top-level `code`, `message`, `file`, `line`,
  `column`, and `severity`.
- Richer Numadora diagnostics should be preserved under `details`.
- Check mode must not execute GUI automation actions.

## Run Contract

Input:

```json
{
  "language": "numadora",
  "script": null,
  "path": "scripts/numadora-samples/notepad-check.numa",
  "workspaceRoot": "D:\\home\\source\\csharp\\slasher",
  "bindingRoots": ["numadora-bindings"],
  "runId": "run-...",
  "capturePolicy": {
    "captureAfterEachStep": false,
    "captureBeforeEachStep": false,
    "captureTarget": "selected"
  }
}
```

Output:

```json
{
  "ok": true,
  "language": "numadora",
  "exitCode": 0,
  "events": [],
  "diagnostics": []
}
```

Slasher remains responsible for the final public run response and artifact
layout. The Numadora bridge can either stream events during execution or return
host-call records that Slasher converts into normal automation events.

## Host Call Contract

Numadora calls Slasher actions through a small host-call protocol.

Request:

```json
{
  "id": 1,
  "method": "slasher.window.wait_for_title",
  "params": {
    "title": "Notepad",
    "timeoutMs": 10000
  },
  "source": {
    "file": "scripts/numadora-samples/notepad-check.numa",
    "line": 7,
    "column": 12
  }
}
```

Success response:

```json
{
  "id": 1,
  "ok": true,
  "result": {
    "title": "Untitled - Notepad",
    "handle": 123456,
    "className": "Notepad",
    "isVisible": true
  }
}
```

Expected miss:

```json
{
  "id": 1,
  "ok": true,
  "result": null
}
```

Operational failure:

```json
{
  "id": 1,
  "ok": false,
  "error": {
    "code": "focus_blocked",
    "message": "Windows rejected the focus request",
    "details": {},
    "evidence": []
  }
}
```

Expected misses should map to `Option[T]`. Operational failures should map to
Numadora `RuntimeError`.

## N0 Closure

No N0-blocking decisions remain.

Non-blocking follow-up decisions:

- Exact installed/release command name for Numadora outside a source checkout:
  packaging work.
- Whether event streaming is required in the first run slice: N3.
- Whether the first run bridge uses stdio JSON-RPC, temporary JSON files, or
  HTTP: N3 host-call implementation.
- Release packaging discovery for bundled Slasher + Numadora: Slasher
  packaging work.

## Current Compatibility Findings

Verified on the local Numadora 0.0.1 checkout:

- `cargo run -- version` works.
- `.\scripts\check-numadora.ps1 -Path D:\home\source\rust\Numadora\examples\module.numa`
  works.
- `scripts\numadora-samples\notepad-check.numa` checks successfully after
  adapting the sample to current Numadora syntax.

Former blocker:

```text
error at 1:15: expected top-level FUNC, CONST, LET, or VAR
```

This happened when the Slasher sample used a planned syntax surface instead of
the current Numadora implementation:

- module paths such as `slasher/app`
- `.numai` interface loading from `numadora-bindings`
- top-level alias-qualified command syntax such as `io.step "open notepad"`

The selected path is to adapt Slasher examples and host bindings to current
Numadora syntax. The sample now uses sibling modules such as `slasher_io` and
normal calls such as `io.Step("open notepad")`.

N0 verification helper:

```powershell
.\scripts\verify-numadora-n0.ps1
```

This validates that the local Numadora checkout can check its own module sample
and the Slasher current-spec sample.
