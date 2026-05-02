# Slasher 0.2.2 Handoff

Last confirmed commit:

```text
d02fec4 0.2.0
```

Working tree was clean after the commit.

## Current Direction

- The application name is **Slasher**.
- Slasher will use **Numadora** as its future script language.
- Numadora is a general-purpose language with broad Windows-control capability.
- Slasher v1 `.slasher` scripts are temporary and may stop working after the
  Numadora path covers the core loop.
- Do not preserve old Slasher syntax by adding a Slasher-only Numadora dialect.

## Completed In 0.2.2

- Removed the old standalone Slasher Script compiler plan from active docs.
- Added the Numadora language and migration documentation set.
- Added N0 runtime/check contract and verification scripts.
- Added current Numadora-compatible sample modules and Notepad check sample.
- Added `docs/security-policy.md` for security rules around powerful PC
  automation.
- Set Slasher version metadata to `0.2.2`.

## N0 Status

N0 is complete. No N0-blocking decisions remain.

Verification command:

```powershell
.\scripts\verify-numadora-n0.ps1
```

Expected result:

```text
N0 probe passed: baseline and Slasher current-spec sample both check successfully.
```

N0 confirms:

- local Numadora discovery works through `NUMADORA_HOME` or
  `D:\home\source\rust\Numadora`
- Slasher can invoke Numadora check through `scripts/check-numadora.ps1`
- `scripts/numadora-samples/notepad-check.numa` checks successfully with the
  current Numadora implementation

## Key Documents

- `docs/README.md` - documentation index
- `docs/language-system.md` - Slasher's Numadora-based script direction
- `docs/numadora-migration-plan.md` - N0-N7 implementation plan
- `docs/numadora-runtime-contract.md` - N0 runtime/check/run boundary
- `docs/slasher-script.md` - current Numadora script profile used by Slasher
- `docs/slasher-numadora-integration.md` - Slasher bindings and Numadora
  integration model
- `docs/security-policy.md` - security rules and capability classes
- `docs/numadora-lineage-policy-plan.md` - local lineage and policy input plan
  for future Numadora host-call execution
- `docs/phase-12-rpa-expansion-plan.md` - RPA package expansion plan

## Security Decisions To Carry Forward

Security must be discussed alongside feature development.

Important rules:

- default bind address remains `127.0.0.1`
- dangerous actions require explicit intent
- every action must be auditable
- secrets must not be logged by default
- destructive actions need resolved target metadata
- Web UI, HTTP, MCP, `.slasher`, and future `.numa` should share policy
- Numadora host bindings should carry capability metadata from the beginning

Initial capability classes are defined in `docs/security-policy.md`.

## Next Work

Recommended next phase: **N1**, with the first N2 check-only entry point now
available.

Implemented after the `d02fec4 0.2.0` commit:

- `POST /scripts/check` can dispatch `.numa` paths by extension.
- Inline check requests can set `language: "numadora"`.
- Numadora check uses the local Numadora checkout through Cargo and writes
  Cargo artifacts under `.numadora-targets/check`.
- MCP check and run tools accept
  `language: "numadora"`.
- The Web UI script checker and runner use the Slasher/Numadora selector.
  Pure Numadora scripts can run; host-call scripts outside the policy-enabled
  local observe profile still stop safely.
- Numadora check responses include `requiredCapabilities` for recognized
  initial bindings such as `slasher_app.Start`, `slasher_input.Text`, and
  `slasher_test.AssertForegroundTitle`.
- Failed Numadora check diagnostics preserve process details under `details`:
  `exitCode`, `stdout`, `stderr`, and combined `raw` output.
- Representative Numadora check failures are now classified as
  `numadora_import_failed`, `numadora_unknown_symbol`, and
  `numadora_type_mismatch` when the current stderr messages expose those cases.
- `.numa` run requests now enter a safe preflight path instead of the v1 parser.
  The preflight creates normal run artifacts and runs check first. Pure
  Numadora scripts, and scripts limited to the temporary `slasher_io` stub
  surface, can run through the local Numadora CLI and capture stdout/stderr as
  Slasher logs. Structured stub output is also captured as event `hostCalls`
  and `numadora.hostCall` log entries, with observed safe host calls appended
  as `numadora.hostCall` timeline events. Policy-allowed observe calls now
  execute through Slasher for `slasher_window.WaitForTitle` and
  `slasher_test.AssertForegroundTitle`. `slasher_app.Start` now executes
  through Slasher after policy allow and records process/window metadata or a
  normal `app_start_failed` event. Scripts requiring input, focus, browser,
  file, clipboard, or other non-enabled host-call bindings still return
  `numadora_run_not_implemented`.
- Host-call blocked runs include `blockedCapabilities`, `allowedLocalModules`,
  `allowedLocalHostCalls`, and `runMode` in the error details.
- MCP run summaries and the Web UI diagnostics panel show blocked Numadora
  host-call capabilities and diagnostic `hostCalls`.
- The blocked path now captures a diagnostic `hostCalls` trace from safe
  Slasher-owned stub modules. It records the intended call order but still does
  not execute GUI actions.
- `numadora-lineage-policy-plan.md` maps `information_lineage_paper.md` into a
  local, phased Slasher policy plan. The next real host-call bridge should add
  policy input generation before executing GUI/input actions.
- Numadora runs now accept `purpose`, default to `local-test`, record initial
  lineage metadata with script SHA-256, and attach `policyInput` objects to
  observed or traced host calls.
- `NumadoraPolicyEvaluator` now records diagnostic `policyDecision` values for
  observed and blocked host calls. These decisions are visible in MCP/Web UI
  summaries but do not yet execute GUI/input host calls.
- Policy evaluator tests now cover the local observe allow path plus missing
  capability, missing purpose, dangerous capability, sensitive lineage, and
  interactive-profile denial cases.
- The first policy-gated observe host-call test verifies that
  `slasher_window.WaitForTitle` executes through Slasher and reports a normal
  `window_not_found` error when the target does not appear.
- The first policy-gated process/app host-call test verifies that
  `slasher_app.Start` reaches Slasher's app-start path after policy allow and
  reports `app_start_failed` for a missing executable without launching an app.
- `NumadoraPolicyInput` now carries the current foreground target identity when
  Slasher can observe it. `User-input` host calls deny with
  `numadora_policy_missing_target` when no target identity is available, and
  text input remains disabled even when a target exists.
- `slasher_window.Focus` now derives target identity from its explicit handle
  argument, passes policy as `numadora_policy_allowed_window_focus`, and reaches
  Slasher's focus path. The test uses a missing handle and verifies a normal
  `window_not_found` error without focusing a real app.
- `slasher_input.Text` now reaches the same `numadora.hostCall` policy event
  path and fails closed without explicit approval. `allowInteractiveInput` is
  now available on script run requests, MCP run tools, and the Web UI's
  Numadora-only input checkbox. The checkbox is off by default. The evaluator
  only allows text input when a target identity exists and this approval is true.
- The v1 `.slasher` check path remains unchanged.

N1 goal:

- Keep replacing the current `.numa` stub-module trace with policy-gated
  Slasher host execution while preserving the same Numadora-facing function
  signatures.
- Next risky bridge target is exposing actual `slasher_input.Text` execution in
  broader flows. Keep it opt-in and target-bound; do not make it a default.

Start with these modules:

- `slasher_app`
- `slasher_window`
- `slasher_input`
- `slasher_io`
- `slasher_test`

Before implementation, decide:

- how run mode refuses missing capabilities

Implemented metadata decisions:

- host binding metadata lives in the Slasher-side Numadora binding catalog
- each recognized binding declares a security capability class and profile
- check mode reports recognized requirements as `requiredCapabilities`

Near-term check follow-up:

- parse richer source locations once the Numadora diagnostic shape stabilizes

## Useful Commands

```powershell
git status --short
.\scripts\verify-numadora-n0.ps1
dotnet build Slasher.sln
dotnet test tests\Slasher.Tests\Slasher.Tests.csproj --no-restore
```

Notes:

- In restricted environments, `dotnet build` may need network access for NuGet
  restore.
- Recent tests had two environment-sensitive failures around screen capture
  (`bitblt_failed`). The project builds; those failures were not caused by the
  0.2.2 version change.
