# Slasher Numadora Integration

This document defines how the Slasher application uses Numadora.

The rule for this repository is simple: **the application is Slasher; the
language is Numadora**. Adapt Slasher's script implementation to the Numadora
implementation that exists now. Do not create a Slasher-only source adapter that
accepts old v1 syntax, slash-separated module paths, or `.numai` files before
Numadora supports them as normal language features.

For the language-facing script profile, see `slasher-script.md`. For the
runtime invocation boundary, see `numadora-runtime-contract.md`. For the
implementation phase plan, see `numadora-migration-plan.md`.

## Active Shape

Current `.numa` files use normal Numadora modules and alias-qualified calls:

```numadora
IMPORT slasher_app AS app
IMPORT slasher_window AS win
IMPORT slasher_input AS input
IMPORT slasher_io AS io
IMPORT slasher_test AS test

FUNC main()
    io.Step("open notepad")
    LET handle := app.Start("notepad.exe")

    LET title := win.WaitForTitle("Notepad", 10000)
    win.Focus(handle)

    io.Step("type text")
    input.Text("Slasher Numadora smoke")
    test.AssertForegroundTitle("contains", title)
END
```

The N0 fixture lives at `scripts/numadora-samples/notepad-check.numa` and is
validated by `scripts/verify-numadora-n0.ps1`.

## Initial Modules

The first integration slice uses these module names because they are accepted
by the current Numadora prototype:

| Module | Purpose | Initial calls |
|---|---|---|
| `slasher_app` | application process control | `Start(fileName)` |
| `slasher_window` | window lookup and focus | `WaitForTitle(title, timeoutMs)`, `Focus(handle)` |
| `slasher_input` | keyboard and mouse input | `Text(content)`, `Keys(keys)`, `Mouse(action, x, y, button)`, `Wheel(x, y, delta)`, `Drag(fromX, fromY, toX, toY, button, durationMs, steps)`, `ContextMenu(x, y, delayMs)` |
| `slasher_screen` | screenshots and visual observations | `Capture(scope, maxWidth, maxHeight)` |
| `slasher_element` | native element observation | `Find(scope, title, className, controlId, match, maxDepth, maxResults)`, `Exists(...)`, `ReadText(...)`, `Tree(scope, maxDepth, maxChildren)` |
| `slasher_browser` | browser observation | `Current(sessionId)`, `Title(sessionId)`, `Url(sessionId)`, `Locate(using, value, timeoutMs, sessionId)`, `DomText(...)`, `Attribute(...)`, `Screenshot(sessionId)`, `Links(sessionId)`, `Windows(sessionId)` |
| `slasher_io` | run log and step markers | `Step(name)`, `Log(message)`, `Wait(ms)` |
| `slasher_test` | assertions | `AssertForegroundTitle(operator, expected)` |

These names are a current-runtime module shape, not a permanent branding
decision. If Numadora later gains package-style module paths, Windows-control
libraries can move toward a broader namespace in a separate language-level
change.

## Initial Capability Metadata

Slasher now keeps an initial host binding catalog for check-time reporting. The
catalog maps known Numadora-facing calls to the security classes in
`security-policy.md`:

| Module | Function | Capability class | Profile |
|---|---|---|---|
| `slasher_app` | `Start` | Process/app | `interactive` |
| `slasher_window` | `WaitForTitle` | Observe | `observe` |
| `slasher_window` | `Focus` | User-input | `interactive` |
| `slasher_input` | `Text` | User-input | `interactive` |
| `slasher_input` | `Keys` | User-input | `interactive` |
| `slasher_input` | `Mouse` | User-input | `interactive` |
| `slasher_input` | `Wheel` | User-input | `interactive` |
| `slasher_input` | `Drag` | User-input | `interactive` |
| `slasher_input` | `ContextMenu` | User-input | `interactive` |
| `slasher_screen` | `Capture` | Observe | `observe` |
| `slasher_element` | `Find` | Observe | `observe` |
| `slasher_element` | `Exists` | Observe | `observe` |
| `slasher_element` | `ReadText` | Observe | `observe` |
| `slasher_element` | `Tree` | Observe | `observe` |
| `slasher_browser` | `Current` | Observe | `observe` |
| `slasher_browser` | `Title` | Observe | `observe` |
| `slasher_browser` | `Url` | Observe | `observe` |
| `slasher_browser` | `Locate` | Observe | `observe` |
| `slasher_browser` | `DomText` | Observe | `observe` |
| `slasher_browser` | `Attribute` | Observe | `observe` |
| `slasher_browser` | `Screenshot` | Observe | `observe` |
| `slasher_browser` | `Links` | Observe | `observe` |
| `slasher_browser` | `Windows` | Observe | `observe` |
| `slasher_io` | `Step` | Observe | `observe` |
| `slasher_io` | `Log` | Observe | `observe` |
| `slasher_io` | `Wait` | Observe | `observe` |
| `slasher_test` | `AssertForegroundTitle` | Observe | `observe` |

`/scripts/check` reports these as `requiredCapabilities` for `.numa` scripts
when it can statically recognize `IMPORT module AS alias` plus
`alias.Function(...)` calls. Run mode records the same capabilities in
`numadora.hostCall` events. Interactive input requires both target identity and
explicit `allowInteractiveInput` approval; otherwise `slasher_input.Text`,
`slasher_input.Keys`, `slasher_input.Mouse`, `slasher_input.Wheel`, and
`slasher_input.Drag`, and `slasher_input.ContextMenu` fail closed without
sending input. Approved input revalidates the foreground target immediately
before sending.

## Runtime Boundary

Slasher should host Numadora through a narrow runtime boundary:

1. Slasher receives `.numa` source through the existing Web, MCP, HTTP, or CLI
   surfaces.
2. Slasher invokes Numadora check/run through the contract in
   `numadora-runtime-contract.md`.
3. Numadora resolves Windows-control modules and calls Slasher host functions.
4. Slasher executes GUI, browser, file, and RPA actions through the existing C#
   server code.
5. Slasher emits the same artifact family as v1 runs: `run.json`,
   `events.jsonl`, `summary.txt`, `report.html`, screenshots, and logs.

The first implementation can use a process bridge to the local Numadora CLI.
Embedding can be reconsidered after check/run behavior is stable.

## Host Binding Policy

N0 uses `.numa` stub modules only to prove that Slasher scripts can be parsed
and checked by the current Numadora implementation.

N1/N2 should replace or back those stubs with host bindings while preserving
the same Numadora-facing signatures. The Slasher side owns:

- function names and argument order
- mapping to existing C# automation APIs
- event and artifact emission
- structured error conversion
- compatibility with Web, MCP, HTTP, and CLI surfaces

Numadora should own:

- parsing and checking `.numa`
- module/import semantics
- type checking
- function call evaluation
- any future generic host-call mechanism

Slasher-specific syntax should not be added to Numadora. Slasher can provide
application-owned modules and host bindings, but if Windows automation needs a
language feature, it should be justified as a general Numadora feature.

## Error And Evidence Contract

Check mode must not execute GUI actions. It should return diagnostics that can
be mapped to the existing Slasher check response shape:

- code
- message
- file
- line
- column
- actionable hint, when available
- raw Numadora diagnostic details, when useful

Run mode should preserve Slasher's evidence loop. Each host call should produce
the usual command event metadata and, when relevant, screenshots or other
artifacts. Failed commands should include the `.numa` source location when the
runtime can provide it.

## Explicit Non-Goals

The current integration does not require:

- `.numai` interface loading
- `IMPORT slasher/app AS app`
- top-level command syntax such as `io.step "open notepad"`
- bare v1-style commands such as `start notepad.exe`
- a compatibility parser for old `.slasher` files
- compile-to-exe support

These can be revisited only after the current `.numa` check/run path is useful.

## Implementation Phases

The detailed phase plan lives in `numadora-migration-plan.md`. At a high level:

1. N0 freezes runtime discovery and validates a current-spec sample.
2. N1 exposes the smallest useful Slasher module surface.
3. N2 connects Slasher's check endpoint to Numadora.
4. N3 runs a `.numa` script through Slasher and emits normal artifacts.
5. N4 expands module coverage for real AI-driven tests.
6. N5 adds ergonomic macro support only if Numadora supports it.
7. N6 optionally provides porting assistance for important `.slasher` files.
8. N7 removes or archives the v1 runner once `.numa` covers the core loop.

## Open Questions

- How should host bindings be represented in the current Numadora runtime?
- Should Slasher run Numadora as an external process long term, or embed it?
- What is the minimal source-location payload needed for useful run errors?
- Which v1 samples are important enough to port before v1 removal?
