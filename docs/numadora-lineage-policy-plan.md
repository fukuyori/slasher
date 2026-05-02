# Numadora Lineage Policy Plan

This document adapts the ideas from `information_lineage_paper.md` into the
current Slasher + Numadora implementation plan.

The goal is not to introduce SPIRE, OPA, OpenLineage, or a service mesh in the
first Slasher slice. The goal is to define a local, inspectable lineage and
policy shape that can later be mapped to those systems without changing the
Numadora host-call contract.

## Design Position

Slasher controls local PC resources. Numadora scripts will eventually be able
to start applications, focus windows, type text, read files, use browser state,
and produce artifacts. A capability gate answers whether a script may use a
class of power. A lineage gate answers a different question:

> Given where this information came from and why it is being used, is this
> host call still appropriate?

The two gates should be evaluated together before host calls execute.

## Scope For The First Implementation

Start local and artifact-first:

- no external OPA dependency
- no SPIRE workload identity
- no OpenLineage backend
- no network sidecar
- no transparent packet or TLS interception

Instead, Slasher should write lineage information into the existing run
artifacts:

- `run.json`
- `events.jsonl`
- `summary.txt`
- `report.html`
- MCP run summaries

This keeps the first implementation auditable without adding operational
systems that are larger than the current Slasher runtime.

## Lineage Model

Add a lightweight lineage object to run and event metadata.

```json
{
  "lineage": {
    "runId": "run-...",
    "parentRunId": null,
    "purpose": "local-test",
    "actor": {
      "kind": "local-user",
      "surface": "mcp"
    },
    "script": {
      "language": "numadora",
      "entryPoint": "scripts/example.numa",
      "sha256": "..."
    },
    "inputs": [
      {
        "kind": "window",
        "title": "Untitled - Notepad",
        "processName": "notepad"
      }
    ],
    "outputs": [
      {
        "kind": "artifact",
        "path": "artifacts/runs/.../events.jsonl"
      }
    ],
    "data": {
      "classification": "local",
      "redaction": "default"
    }
  }
}
```

Field meanings:

| Field | Meaning |
|---|---|
| `runId` | Current Slasher run ID. |
| `parentRunId` | Previous Slasher run that produced input material, if known. |
| `purpose` | Declared reason for the run. Defaults to `local-test`. |
| `actor.surface` | `web`, `http`, `mcp`, `script`, or future scheduled runner. |
| `script.sha256` | Hash of inline script content or script file content. |
| `inputs` | Windows, files, browser pages, clipboard, or artifacts observed by the run. |
| `outputs` | Files, artifacts, clipboard changes, typed text targets, or network destinations produced by the run. |
| `data.classification` | `local`, `sensitive`, `secret`, `external`, or future project-specific class. |

## Host Call Policy Input

Every Numadora host call should be checked against a stable JSON input shape.

```json
{
  "language": "numadora",
  "runId": "run-...",
  "purpose": "local-test",
  "surface": "mcp",
  "capability": {
    "module": "slasher_input",
    "function": "Text",
    "class": "User-input",
    "profile": "interactive"
  },
  "hostCall": {
    "module": "slasher_input",
    "function": "Text",
    "arguments": ["hello"]
  },
  "target": {
    "kind": "window",
    "title": "Untitled - Notepad",
    "processName": "notepad"
  },
  "lineage": {
    "scriptSha256": "...",
    "parentRunId": null,
    "inputClassifications": ["local"],
    "outputClassifications": []
  }
}
```

The first evaluator can be a C# in-process rule engine. The shape should be
kept OPA-friendly so it can later become Rego input.

## Initial Policy Rules

These rules should be enforced before real host-call execution lands:

1. Missing capability metadata denies the host call.
2. Missing run purpose denies non-observe host calls.
3. `secret` or `sensitive` input lineage denies clipboard, browser upload,
   network, and unredacted log output unless an explicit policy allows it.
4. `User-input` calls require a selected or foreground target identity in the
   run event.
5. `Process/app` calls must record executable name and arguments.
6. Destructive, browser-data, secrets, scheduling, and network/remote classes
   remain denied until explicit policy settings exist.
7. A denied host call must produce a normal Slasher error event with the policy
   input and decision reason, with secret values redacted.

## Relationship To Current Numadora Work

Already implemented groundwork:

- Numadora check reports `requiredCapabilities`.
- Blocked host calls report `blockedCapabilities`.
- Blocked host calls can include diagnostic `hostCalls`.
- Safe `slasher_io` host-call output is parsed into `hostCalls`.
- Observed safe host calls appear as `numadora.hostCall` timeline events.
- Policy-allowed observe calls can now execute through Slasher after the
  Numadora CLI run, starting with `slasher_window.WaitForTitle` and
  `slasher_test.AssertForegroundTitle`.
- The first non-observe bridge target, `slasher_app.Start`, now executes
  through Slasher after policy allow and records process/window metadata or a
  normal `app_start_failed` error.
- Policy input now includes the current foreground target identity when Slasher
  can observe it. `User-input` class calls deny with
  `numadora_policy_missing_target` when no target identity is available.
- `slasher_window.Focus` now derives target identity from the host-call handle
  argument and executes through Slasher after policy allow.
- `slasher_input.Text` now reaches the same host-call policy event path, but
  still fails closed without explicit `allowInteractiveInput` approval; no text
  is sent by default.

Bridge steps completed so far:

1. Add a `NumadoraPolicyInput` model in Slasher.
2. Build policy input from `ScriptCapabilityRequirement`, parsed host call,
   run source, and current target evidence.
3. Add an in-process `NumadoraPolicyEvaluator`.
4. Return `allow`, `denyCode`, and `reason`.
5. Emit policy decision details into `events.jsonl`.
6. Connect selected observe host calls to actual Slasher observation actions.
7. Connect `slasher_app.Start` to actual Slasher process start handling.
8. Attach foreground target identity to policy input and fail closed for
   target-dependent input calls without target evidence.
9. Connect `slasher_window.Focus` to actual Slasher focus handling when the
   host call carries an explicit handle target.
10. Route `slasher_input.Text` through the policy event path without sending
    input until approval semantics are explicit.
11. Add an explicit `allowInteractiveInput` run approval. The policy evaluator
    allows `slasher_input.Text` only when target identity exists and this
    approval is true.
12. Expose the approval as an off-by-default Numadora-only checkbox in the Web
    UI and as an explicit MCP/tool request field.

Next bridge step:

- Extend the same policy envelope to input and focus calls after target
  metadata and approval rules are explicit.

## Phased Plan

### L0: Artifact Schema

- [x] Add lineage fields to Numadora run metadata.
- [x] Add script content hash for inline and file-based Numadora runs.
- [x] Add `purpose` to script run requests, defaulting to `local-test`.
- Document redaction expectations for lineage fields.

### L1: Policy Input Without Enforcement

- [x] Generate policy input for each parsed safe Numadora host call.
- [x] Attach the input to diagnostic `numadora.hostCall` events.
- [x] Attach policy inputs to blocked host-call run details when a trace is
  available.
- Keep behavior unchanged.

### L2: Enforced Local Policy

- [x] Add a C# evaluator with fail-closed defaults.
- [x] Record policy decisions in `numadora.hostCall` event parameters and
  blocked host-call details.
- [x] Allow `Observe` and current `slasher_io` calls.
- [x] Deny unknown, destructive, secret, network, browser-data, and file-write
  calls unless explicitly enabled.
- [x] Add tests for allow, deny, missing purpose, and sensitive lineage.

The current evaluator records decisions for inspection before real GUI/input
host-call execution. That keeps the L2 policy shape testable while the actual
host-call bridge remains disabled.

### L3: Real Host Calls Behind Policy

- [x] Execute the first observe host calls only after policy allow.
- [x] Preserve the existing `numadora.hostCall` event as the policy and call
  envelope.
- [x] Execute the first non-observe host call only after policy allow.
- [x] Bridge `slasher_app.Start` with process/window event metadata.
- [x] Include foreground target identity in policy input when available.
- [x] Deny target-dependent input calls when no target identity is present.
- [x] Bridge `slasher_window.Focus` only when an explicit handle target is
  present.
- [x] Route `slasher_input.Text` to a policy-denied `numadora.hostCall` event
  without sending input.
- [x] Add explicit `allowInteractiveInput` approval for text input policy.
- [x] Expose interactive input approval in MCP and Web UI as an explicit opt-in.
- [ ] Bridge text input in normal UI flows only after selected or foreground
  target identity is explicit and policy-approved.

### L4: External Policy Adapter

- Make the policy input serializable as OPA/Rego input.
- Add optional external evaluator process or HTTP endpoint.
- Keep the in-process evaluator as the default local-safe path.

## Paper Integration Notes

`information_lineage_paper.md` is useful as a long-term architecture note, but
Slasher should not adopt its full stack immediately.

Use now:

- provenance as an authorization input
- purpose-bound automation
- fail-closed policy evaluation
- immutable-ish local audit via existing artifacts
- future compatibility with OpenLineage-style events

Defer:

- SPIRE/SPIFFE identity
- Envoy sidecar interception
- Marquez or graph database storage
- eBPF/ETW transparent data capture
- WORM or distributed audit chain

The first Slasher implementation should prove that lineage-aware local
automation decisions are understandable in the run report. External systems can
come later.
