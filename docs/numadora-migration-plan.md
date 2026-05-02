# Numadora Integration And Migration Plan

This plan describes how the Slasher application should use Numadora for broad
Windows-control scripts while retiring the current v1 `.slasher` command
runner.

It is an implementation plan, not a user migration guide. For user-facing
syntax rewrites, see `migration-from-slasher-v1.md`.
For security rules that apply to Numadora host bindings, see
`security-policy.md`. For lineage-aware host-call policy derived from
`information_lineage_paper.md`, see `numadora-lineage-policy-plan.md`.

## Goals

- Make Numadora `.numa` the unified script path.
- Keep Slasher as the application and user-facing product name.
- Treat Numadora as a general-purpose language with typed Windows-control
  libraries, not as a Slasher v1 compatibility layer.
- Preserve the current run artifact model: `run.json`, `events.jsonl`,
  `summary.txt`, `report.html`, screenshots, logs, and structured errors.
- Reuse the Slasher server's existing automation APIs instead of rebuilding GUI
  automation inside the language runtime.
- Make Windows automation capabilities available as typed Numadora modules
  using names accepted by the current Numadora implementation, starting with
  `slasher_app`, `slasher_window`, `slasher_input`, `slasher_io`, and
  `slasher_test`.
- Keep new RPA package work compatible with future Numadora module names.
- Make it acceptable for v1 `.slasher` scripts to stop working after the
  Numadora path is ready.
- Provide migration tooling only if it materially speeds up porting important
  samples or user scripts.

## Non-Goals

- Do not build a second standalone Slasher language.
- Do not let v1 command spelling define Numadora's module, function, or macro
  design.
- Do not start with compile-to-exe support. Build support should wait until
  `.numa` check/run semantics are stable.
- Do not require complete v1 feature parity before switching new development
  to `.numa`.

## Current Baseline

The current v1 runner has useful behavior that informs the host capabilities:

- line-oriented commands with variable assignment using `as`
- `include`, function blocks, local/file/global variable scopes
- arrays, `foreach`, `try/catch/finally`, assertions, and test steps
- server-side run/check endpoints
- event logs, reports, screenshot evidence, and artifact readback
- native element commands, image matching, browser automation, file commands,
  clipboard commands, and mouse/keyboard/window commands

The migration should preserve the evidence loop, not the v1 language surface.
Syntax, module boundaries, and command names should change when the Numadora
shape is cleaner and more general.

## Target Architecture

```text
AI / user
  |
  | .numa
  v
Numadora runtime / bridge
  |
  v
Slasher server APIs
  |
  v
shared run artifacts
```

The first Numadora implementation can be a bridge to an external Numadora CLI
or process. The v1 `ScriptRunService` can remain temporarily as a reference and
fallback during development, but it is not a long-term compatibility target.
Embedding the runtime can be revisited after the first `.numa` vertical slice
is working.

## Phase N0: Runtime Discovery And Contract Freeze

Purpose: decide how Slasher will invoke Numadora and freeze the first boundary.

Status: complete.

Deliverables:

- documented Numadora invocation strategy: `numadora-runtime-contract.md`
- minimal `.numa` check command contract: `numadora-runtime-contract.md`
- minimal `.numa` run command contract: `numadora-runtime-contract.md`
- decision on where Slasher host binding notes live in this repository:
  `numadora-bindings/slasher/`
- first test fixture directory for `.numa` examples:
  `scripts/numadora-samples/`
- compatibility path decision: adapt Slasher's Numadora-facing implementation
  to current Numadora syntax, do not add a Slasher-side source adapter

Recommended decisions:

- Add `numadora-bindings/slasher/` for Slasher-owned host binding notes.
- Add `scripts/numadora-samples/` for `.numa` examples.
- Use a process bridge for the first slice unless an embedded API already
  exists and is cheap to call.
- Return diagnostics in the same shape as `POST /scripts/check` where possible:
  code, message, file, line, column, command, and actionable hints.

Exit criteria:

- A developer can run a documented command that checks a trivial `.numa` file.
- The repository has a stable place for Slasher binding files and examples.

Current N0 findings:

- local source checkout discovery is documented in
  `numadora-runtime-contract.md`
- `scripts/check-numadora.ps1` can invoke the local Numadora checkout
- Numadora 0.0.1 can check its own `examples/module.numa`
- Slasher's target `scripts/numadora-samples/notepad-check.numa` checks
  successfully after adapting it to current Numadora syntax
- `scripts/verify-numadora-n0.ps1` captures the N0 verification state

Non-blocking follow-up work moved to later phases:

- packaged/release Numadora executable discovery: packaging work
- event streaming and host-call transport details for run mode: N3
- replacing sample stub modules with real host bindings: N1/N2
- lineage-aware host-call policy input and enforcement: N3/L0-L3 in
  `numadora-lineage-policy-plan.md`

## Phase N1: Binding Skeleton

Purpose: expose the smallest useful Windows-control surface as Numadora modules.

Current status: initial binding capability metadata is implemented for the N0
modules. Check mode can report recognized `requiredCapabilities` for
alias-qualified calls, but the modules are still source-level stubs and run
mode does not yet enforce policy profiles.

Initial current-Numadora modules:

- `slasher_io`
- `slasher_app`
- `slasher_window`
- `slasher_input`
- `slasher_test`

Initial current-Numadora module shapes should cover:

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
    input.Text("hello")
    test.AssertForegroundTitle("contains", title)
END
```

Implementation notes:

- Use current Numadora alias-qualified function calls in reference examples.
- Do not add bare command support unless Numadora itself grows that feature.
- Map Slasher operational errors to Numadora `RuntimeError`.
- Use current Numadora error/nullability constructs first; introduce
  `Option<T>` only when the runtime supports it.
- Assign initial capability metadata for each binding, following
  `security-policy.md`.

Exit criteria:

- Binding files exist for the initial modules.
- The first Notepad `.numa` sample can be statically checked.
- The binding names match `slasher-script.md` and
  `slasher-numadora-integration.md`.
- The initial bindings have documented capability classes.

## Phase N2: Check Integration

Purpose: let Slasher validate `.numa` scripts without running GUI actions.

Current status: initial check-only dispatch is implemented. `POST
/scripts/check` now accepts `.numa` files by extension and inline scripts with
`language: "numadora"`, invokes the local Numadora checkout through Cargo, and
returns Slasher `ScriptCheckResponse` diagnostics without executing GUI
actions. MCP check tools and the Web UI script checker can pass the same
language selector. Representative Numadora failures are classified as
`numadora_import_failed`, `numadora_unknown_symbol`, and
`numadora_type_mismatch` when the current Numadora stderr shape exposes those
cases.

Deliverables:

- `POST /scripts/check` accepts `.numa` files or a language selector.
- MCP check path can report Numadora diagnostics.
- Web UI can show Numadora check diagnostics.
- `scripts/numadora-samples/notepad-check.numa`
- tests for successful check and representative diagnostics

Design rules:

- Do not execute Slasher actions in check mode.
- Preserve file and line mapping.
- Diagnostics should be useful to AI agents: include fix hints when possible.
- If Numadora diagnostics are richer than v1 diagnostics, keep the richer data
  in `details` while preserving the public check response shape.

Exit criteria:

- `check` works for at least one valid `.numa` sample.
- `check` reports syntax, import, unknown symbol, and type mismatch failures.
- Existing v1 `.slasher` check behavior may be removed once `.numa` check is
  available and documented.

## Phase N3: Run Integration Vertical Slice

Purpose: execute a small `.numa` script through the existing Slasher server and
produce normal run artifacts.

Current status: run requests can enter the Numadora path and produce normal
Slasher run artifacts. The current path runs Numadora check first, then can run
pure Numadora scripts, or scripts limited to the temporary `slasher_io` stub
surface, through the local Numadora CLI and capture stdout/stderr as Slasher
logs. Structured stub output is parsed into event `hostCalls` and
`numadora.hostCall` log entries, and each observed safe host call is appended
as a `numadora.hostCall` timeline event. Scripts that require process, window,
input, browser, file, clipboard, or other host-call bindings still fail with
`numadora_run_not_implemented`.
Invalid scripts fail with `numadora_check_failed` before any GUI action can run.
MCP run tools and the Web UI script runner pass the same `language` selector.
Blocked host-call runs include `blockedCapabilities`, `allowedLocalModules`,
and `runMode` details so the next host bridge can attach policy decisions
without changing the outer run artifact shape. MCP run summaries and the Web
UI diagnostics panel surface both the blocked capability list and the
diagnostic `hostCalls` trace. The current blocked path also runs the safe
Numadora stub modules to capture that trace; it records call order and
arguments but still does not execute GUI actions.
Current runs also carry the first lineage/policy artifacts from
`numadora-lineage-policy-plan.md`: optional `purpose`, script SHA-256 lineage
metadata, per-host-call `policyInput` objects, and diagnostic
`policyDecision` results from the in-process evaluator. These are recorded for
inspection only; real GUI/input host-call execution is still a future step.

First scenario:

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

Deliverables:

- `.numa` run endpoint path
- shared run artifact creation for Numadora runs
- command events emitted for each Slasher API call
- error events with source file/line and evidence
- one automated smoke test for a non-GUI or mocked action path

Implementation notes:

- The Numadora runtime should call Slasher through a narrow host interface, not
  by duplicating automation logic.
- If the first runtime bridge cannot stream command events, collect enough data
  to emit standard Slasher automation events after each host call.
- The run report should clearly show `language: "numadora"` or equivalent
  metadata.

Exit criteria:

- A `.numa` script can create the standard Slasher artifact family.
- Failed Numadora commands include file/line diagnostics and evidence.
- Existing v1 `.slasher` run behavior may be removed after this path is stable.

## Phase N4: Module Coverage Expansion

Purpose: cover enough Slasher functionality for real AI-driven tests.

Expansion order:

1. `slasher_screen`: capture, image-match, wait-stable
2. `slasher_element`: tree, find, click, text, exists
3. `slasher_browser`: launch/open, navigate, find, click, type, press,
   screenshot, logs, downloads
4. `slasher_file`, `slasher_folder`, `slasher_clipboard`
5. Phase 12 packages as they land: `slasher_csv`, `slasher_json`,
   `slasher_excel`

For each module:

- add current-spec module signatures
- add at least one `.numa` sample
- add check tests for signatures
- add run tests where practical
- update `slasher-numadora-integration.md`
- update migration notes when an old scenario has a useful Numadora expression

Exit criteria:

- Important smoke scenarios have `.numa` equivalents or documented blockers.
- AI agent guide points new script authoring to `.numa`.

## Phase N5: Command Macro Library

Purpose: add optional ergonomic macros only after the ordinary Numadora module
surface is working.

Deliverables:

- `slasher_control.numa` macros such as `WithWindow`, `Retry`, `WaitUntil`
- macros for high-frequency side effects, only where they improve Numadora as a
  whole
- macro expansion trace in events
- source mapping from macro call site to expanded Slasher calls

Policy:

- Canonical examples stay alias-qualified.
- Bare commands can be provided through an explicit Slasher prelude.
- Macros must be transparent enough for AI agents to debug failures.
- Macro design must not be driven by old `.slasher` compatibility.

Exit criteria:

- The Notepad smoke script can be written in the preferred ergonomic style.
- Macro failures point back to the user's `.numa` line, not only expansion code.

## Phase N6: Porting Tooling

Purpose: help port important `.slasher` examples into `.numa` when automation is
cheaper than manual rewrite.

Command shape:

```powershell
slasher migrate scripts\samples\ai-agent-smoke.slasher -o scripts\numadora-samples\ai-agent-smoke.numa
```

Optional conversion scope:

- add required `IMPORT` statements
- map common commands to alias-qualified Numadora calls
- rewrite `set`, `array`, `foreach`, `if`, `try/catch/finally`
- rewrite `include` to `IMPORT` when the target can become a module
- preserve unsupported lines as explicit `TODO` comments with source line
  references

Required outputs:

- generated `.numa`
- migration report listing automatic rewrites, TODOs, and manual risks
- optional side-by-side summary for AI review

Exit criteria:

- Important sample scripts have `.numa` replacements.
- Any generated files pass `check` when the source uses only supported patterns.
- Unsupported patterns fail loudly in the migration report, not silently.

## Phase N7: Switchover And v1 Removal

Purpose: make `.numa` the only supported script language and remove the v1
runner when it no longer helps development.

Switch stages:

1. Experimental: `.numa` check/run hidden behind explicit language selection.
2. Supported: docs recommend `.numa` for new scripts that use covered modules.
3. Default-new: UI and templates create `.numa`.
4. v1 frozen: no new features or fixes except emergency cleanup.
5. v1 removed: `.slasher` endpoints, docs, and samples are deleted or archived.

Do not remove v1 until `.numa` covers the core AI-driven testing loop:
window/input, assertions, artifacts, element checks, and browser checks.

## Acceptance Matrix

| Capability | v1 reference | Numadora target | Required before v1 removal |
|---|---|---|---|
| Run artifacts | implemented | shared artifacts | yes |
| Check diagnostics | implemented | Numadora diagnostics | yes |
| Variables | dynamic scopes | `LET` / `VAR` | yes |
| Includes/modules | `include` | `IMPORT` / modules | yes |
| Window/input basics | implemented | `slasher_window`, `slasher_input` | yes |
| Assertions | implemented | `slasher_test` | yes |
| Browser automation | implemented | `slasher_browser` | before browser test migration |
| Element automation | implemented | `slasher_element` | before native UI test migration |
| Data packages | Phase 12 | `slasher_csv`, `slasher_json`, `slasher_excel` | package-specific |
| Porting tooling | none | optional `slasher migrate` | no |
| Build/exe | not active | later | no |

## Risks And Mitigations

| Risk | Mitigation |
|---|---|
| Two languages confuse users | Keep `language-system.md` as the entry point and mark `.slasher` as temporary. |
| Numadora integration blocks RPA work | Design Phase 12 APIs as Numadora modules first; port or remove v1 commands later. |
| Diagnostics regress from v1 | Treat source line, action, error code, evidence, and report parity as acceptance criteria. |
| Runtime bridge is too slow or fragile | Start with a process bridge, measure, then decide whether embedding is worth it. |
| Porting tool over-promises | Generate drafts and reports, not silently rewritten production scripts. |
| Macro expansion hides failures | Require macro expansion events and source mapping before macro-heavy examples become canonical. |

## Immediate Next Steps

1. Parse richer source locations from Numadora diagnostics once the diagnostic
   shape stabilizes.
2. Replace sample stub modules with Slasher host bindings that keep the same
   Numadora-facing function signatures.
3. Draft the host-call protocol implementation for Slasher API calls from
   Numadora.
4. Update `slasher-script.md` and `slasher-numadora-integration.md` to use
   current Numadora syntax when binding names change.
