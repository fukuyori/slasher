# Slasher Implementation Roadmap

Slasher's primary goal is to let AI agents such as Codex operate, test, and
debug real Windows applications. Its secondary goal is RPA-style local
automation. Its longer-term architecture should keep the automation core
portable and allow trusted Slasher peers to expose typed resources through a
policy-gated namespace.

This roadmap is intentionally short. Detailed contracts live in their own
documents, and the language direction is tracked under `language-system.md`.
Security rules for the PC-control surface are tracked in
`security-policy.md`. Peer namespace and portable-core direction is tracked in
`peer-network-model.md`. The cross-track implementation order is tracked in
`development-schedule.md`.

## Current Status

Phase 11 is complete as of commit `7caa579` (`Complete Slasher phase 11`).

The implementation now includes:

- structured script run artifacts, event logs, HTML reports, and artifact readback
- Web UI and MCP paths that call the shared server-side script runner
- native window/control tree inspection, native element search/click/text/existence, and element assertions
- image matching and image-match assertions
- Selenium WebDriver browser automation for Edge, Chrome, and Firefox
- browser DOM actions, screenshots, tabs/windows, downloads, selected options, and console log readback

The next implementation focus is Phase 12: practical RPA package expansion on
top of the completed evidence model. Peer namespace work is a separate
architecture track and should not weaken the local-first security defaults.

## Guiding Principles

1. AI observability comes first.
   Every action should produce logs, captures, target metadata, and structured errors.

2. Script actions should be libraries, not syntax bloat.
   New command shapes should map to Numadora modules. Current examples should
   use names accepted by the existing Numadora implementation, such as
   `slasher_csv` or `slasher_browser`. Legacy `.slasher` commands are no longer
   part of the active script surface.

3. Web UI, MCP, HTTP, and scripts should share semantics.
   Avoid adding behavior in one control surface without the corresponding
   script and documentation shape.

4. Destructive actions must be auditable.
   Delete, overwrite, close-all, and unattended operations should expose enough
   parameters and evidence to be reviewed after the run.

5. Security policy moves with capability expansion.
   New PC-control powers should declare their capability class, audit fields,
   and redaction behavior before they become broadly available.

6. Peer namespace work preserves local semantics.
   A peer-executed action should produce the same conceptual run, event,
   evidence, policy, and error shapes as a local action, with additional
   coordinator/executor peer metadata.

## Completed Tracks

### Phase A: AI Automation Contract

Status: complete.

Key document: `ai-automation-contract.md`.

Defines action/result envelopes, event logs, run artifacts, evidence paths,
structured errors, and MCP response expectations.

### Phase 0: Server Layout Stabilization

Status: complete.

Key document: `architecture.md`.

Moved startup and endpoint mapping into a cleaner layout while preserving the
existing server and Web UI behavior.

### Phase 9: Web And MCP Script Runs

Status: complete for the current runner.

Web UI and MCP now use the shared server-side script runner and report model.

### Phase 10: Test Observability

Status: complete for the current evidence loop.

Key document: `ai-test-observability.md`.

Implemented script run reports, logs, screenshots, HTML reports, assertion
events, readback endpoints, and common diagnostics.

### Phase 11: UI, Image, And Browser Test Automation

Status: complete for the agreed Phase 11 scope.

Implemented native element inspection/actions, element assertions, image
matching, and Selenium WebDriver browser automation.

Deferred follow-ups:

- OCR command
- richer UI Automation selector model with AutomationId/control patterns
- context menu item extraction
- browser DevTools/network capture

These are useful, but they are not blockers for Phase 12.

## Active Track: Phase 12 RPA Expansion

Key document: `phase-12-rpa-expansion-plan.md`.
Security gate: `security-policy.md`.

Priority order:

1. CSV package
2. JSON package
3. Excel package
4. Safer destructive action policy
5. File/folder watcher package
6. Scheduling hooks
7. Credentials/secrets
8. Report export/distribution

Phase 12 packages should reuse the existing event/report/artifact model. They
should not introduce separate reporting formats.

## Architecture Track: Portable Core And Peer Namespace

Key document: `peer-network-model.md`.
Implementation plan: `peer-implementation-plan.md`.
Security gate: `security-policy.md`.

This track captures the Plan 9/HarmonyOS-inspired direction:

- Plan 9-like resource namespace for Slasher resources
- HarmonyOS-like coordination across trusted devices
- portable core models for runs, resources, capabilities, policy, and evidence
- platform adapters for Windows and future non-Windows or headless peers
- peer protocol starting with authenticated, read-only namespace inspection

Implementation should follow the `Peer P0` through `Peer P9` phases in
`peer-implementation-plan.md`, starting with contracts, identity, and read-only
namespace inspection before delegated runs or interactive operations.

This is not a replacement for Phase 12. It is the architectural path that
prevents future Slasher-to-Slasher communication from becoming unsafe ad hoc
remote control.

## Language Track

Key entry point: `language-system.md`.
Detailed migration plan: `numadora-migration-plan.md`.

Language direction:

- Slasher remains the application and user-facing automation product.
- Slasher scripts should use Numadora as the unified general-purpose language.
- Windows automation should be exposed as Slasher-owned, Numadora-facing typed
  modules and host capabilities.
- The v1 `.slasher` runner has been removed from the public script surface.
- The active script target is `.numa`; old `.slasher` scripts are rejected.

The old standalone Slasher Script compiler direction has been removed from the
active docs. New language work should target Slasher scripts written in
Numadora:

- `slasher-script.md` defines the current Numadora script profile used by
  Slasher.
- `numadora-language-spec.md` defines generic Numadora.
- `slasher-numadora-integration.md` defines the Slasher server bindings.
- `migration-from-slasher-v1.md` remains as historical porting reference.

Near-term language work:

1. Decide how this repository locates or invokes the Numadora runtime.
2. Add implementation-ready `.numa` examples that are not shaped by v1 syntax.
3. Add current-spec Windows-control module stubs or host bindings for the first
   useful modules.
4. Add `.numa` check/run support.
5. Port any still-useful historical samples to `.numa`.
6. Keep public script execution Numadora-only.

Security work should run in parallel with N1/N2 so Numadora host bindings can
carry capability metadata from the beginning.

Build/compile-to-exe support should wait until `.numa` check/run semantics are
stable.

## Tracking Checklist

- [x] Phase A automation contract
- [x] Phase 0 server layout stabilization
- [x] Phase 9 Web/MCP script run migration
- [x] Phase 10 observability hardening
- [x] Phase 11 UI/image/browser test automation
- [ ] Phase 12 RPA expansion
- [x] Phase 12 local foundation: CSV/JSON/Excel data API slice
- [x] Phase 12 local foundation: destructive file/folder approval and dry-run slice
- [x] Phase 12 local foundation: file/folder watcher API slice
- [x] Peer namespace core contracts
- [x] Peer identity and metadata endpoints
- [x] Read-only peer namespace inspection
- [ ] Observe-only delegated peer run
- [ ] Portable core extraction
- [x] Language direction docs
- [ ] Implementation-ready `.numa` examples
- [ ] Numadora runtime integration plan
- [ ] Slasher Numadora module stubs or host bindings
- [x] Initial `.numa` check dispatch
- [x] MCP/Web UI language selector for `.numa` check
- [x] Initial Numadora binding capability metadata
- [x] Safe `.numa` run preflight
- [x] Pure `.numa` run artifact path with stdout/stderr logs
- [x] Web UI run entrypoint for safe `.numa` run path
- [x] Blocked host-call capability details in `.numa` run artifacts
- [x] Blocked host-call capability display in MCP/Web UI summaries
- [x] Diagnostic host-call trace capture from safe Numadora stubs
- [x] Diagnostic host-call trace display in MCP/Web UI summaries
- [x] Structured host-call logs on successful safe `.numa` runs
- [x] Observed safe host calls as `numadora.hostCall` timeline events
- [x] Lineage-aware Numadora policy plan
- [x] Initial Numadora run lineage metadata and policy input capture
- [x] Initial Numadora in-process policy decision capture
- [x] Numadora policy evaluator allow/deny tests
- [x] First policy-gated Numadora observe host calls
- [x] First policy-gated Numadora process/app host call
- [x] Numadora policy target identity input and missing-target deny
- [x] Policy-gated Numadora window focus host call
- [x] Numadora text input host call reaches policy-denied event path
- [x] Explicit Numadora interactive input approval flag
- [x] Web UI and MCP opt-in for Numadora interactive input approval
- [x] Target-revalidated Numadora text input bridge
- [x] Target-revalidated Numadora key input bridge
- [x] Target-revalidated Numadora basic mouse input bridge
- [x] Target-revalidated Numadora wheel and drag input bridge
- [x] Target-revalidated Numadora context-menu input bridge
- [x] Numadora screen capture observe bridge
- [x] Numadora native element observe bridges
- [x] Numadora browser observe bridges
- [x] `.numa` check/run support
- [x] Security policy gates for Numadora host bindings
- [x] Legacy `.slasher` samples removed from active scripts
- [x] v1 runner removed from public script entry points
