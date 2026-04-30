# Slasher Implementation Roadmap

Slasher's primary goal is to let AI agents such as Codex operate, test, and
debug real Windows applications. Its secondary goal is RPA-style local
automation.

This roadmap is intentionally short. Detailed contracts live in their own
documents, and the language direction is tracked under `language-system.md`.
Security rules for the PC-control surface are tracked in
`security-policy.md`.

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
top of the completed evidence model.

## Guiding Principles

1. AI observability comes first.
   Every action should produce logs, captures, target metadata, and structured errors.

2. Script actions should be libraries, not syntax bloat.
   New command shapes should map to Numadora modules. Current examples should
   use names accepted by the existing Numadora implementation, such as
   `slasher_csv` or `slasher_browser`. Existing `.slasher` commands are
   temporary.

3. Web UI, MCP, HTTP, and scripts should share semantics.
   Avoid adding behavior in one control surface without the corresponding
   script and documentation shape.

4. Destructive actions must be auditable.
   Delete, overwrite, close-all, and unattended operations should expose enough
   parameters and evidence to be reviewed after the run.

5. Security policy moves with capability expansion.
   New PC-control powers should declare their capability class, audit fields,
   and redaction behavior before they become broadly available.

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

## Language Track

Key entry point: `language-system.md`.
Detailed migration plan: `numadora-migration-plan.md`.

Language direction:

- Slasher remains the application and user-facing automation product.
- Slasher scripts should use Numadora as the unified general-purpose language.
- Windows automation should be exposed as Slasher-owned, Numadora-facing typed
  modules and host capabilities.
- Slasher's current v1 `.slasher` runner is temporary during the transition.
- The final script target is `.numa`; old `.slasher` scripts may stop working.

The old standalone Slasher Script compiler direction has been removed from the
active docs. New language work should target Slasher scripts written in
Numadora:

- `slasher-script.md` defines the current Numadora script profile used by
  Slasher.
- `numadora-language-spec.md` defines generic Numadora.
- `slasher-numadora-integration.md` defines the Slasher server bindings.
- `migration-from-slasher-v1.md` defines migration from `.slasher`.

Near-term language work:

1. Decide how this repository locates or invokes the Numadora runtime.
2. Add implementation-ready `.numa` examples that are not shaped by v1 syntax.
3. Add current-spec Windows-control module stubs or host bindings for the first
   useful modules.
4. Add `.numa` check/run support.
5. Port important `.slasher` samples to `.numa`.
6. Remove or archive v1 runner once `.numa` covers the core loop.

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
- [x] Language direction docs
- [ ] Implementation-ready `.numa` examples
- [ ] Numadora runtime integration plan
- [ ] Slasher Numadora module stubs or host bindings
- [ ] `.numa` check/run support
- [ ] Security policy gates for Numadora host bindings
- [ ] Important `.slasher` samples ported to `.numa`
- [ ] v1 runner removed or archived
