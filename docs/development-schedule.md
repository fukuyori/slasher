# Slasher Development Schedule

This document describes the intended development order for Slasher as a whole.

Slasher is local-first today. The schedule keeps that stable while growing two
tracks in parallel:

- practical local RPA expansion
- Plan 9/HarmonyOS-inspired peer namespace and portable core work

The peer track must not weaken local security defaults. New distributed
features should first prove their contracts, policy behavior, and evidence
model before enabling remote actions.

## Schedule Overview

| Stage | Focus | Main outcome |
|---|---|---|
| S1 | Phase 12 data packages | useful local CSV/JSON/Excel automation |
| S2 | Peer P0-P2 | peer contracts, identity, and metadata endpoints |
| S3 | Peer P3-P4 | read-only resource namespace |
| S4 | Phase 12 safety packages | destructive policy, watcher, scheduling, secrets |
| S5 | Peer P5-P6 | observe-only resource invoke and delegated runs |
| S6 | Peer P7 | portable core extraction |
| S7 | Peer P8 | interactive peer operations |
| S8 | Peer P9 | discovery and advanced transport |

## S1: Phase 12 Data Packages

Goal: add practical local automation value without changing the trust boundary.

Work:

- CSV package
- JSON package
- Excel package
- HTTP, script, MCP, and docs alignment
- structured results that work with existing run/event/artifact reports

Why first:

- these features are useful immediately
- they exercise the existing evidence model
- their result shapes can later become peer namespace resources

Exit criteria:

- data package commands produce normal Slasher events
- package docs and examples are current
- destructive or remote export behavior is not introduced accidentally

## S2: Peer P0-P2 Contracts And Metadata

Goal: establish peer vocabulary without exposing machine resources.

Work:

- peer DTOs
- resource address parser
- peer capability constants
- local peer identity
- manual peer registry
- `GET /peer/hello`
- `GET /peer/capabilities`

Why now:

- it creates the language for peer work
- it is low risk because it does not operate resources yet
- it lets the implementation validate identity and capability assumptions early

Exit criteria:

- peer mode remains disabled by default
- unknown peers fail closed
- metadata endpoints do not expose windows, files, screenshots, clipboard, or
  browser data

## S3: Peer P3-P4 Read-Only Namespace

Goal: make the Plan 9-like namespace real in observe-only form.

Work:

- `GET /peer/ns?path=...`
- `GET /peer/resource?path=...`
- initial resources:
  - `/identity`
  - `/capabilities`
  - `/windows`
  - `/runs`
  - `/artifacts`
  - `/screen/primary` metadata

Why before delegated runs:

- namespace listing is easier to secure than remote execution
- it proves resource address parsing, trust filtering, and policy checks
- it gives agents a stable way to inspect peer capabilities

Exit criteria:

- namespace entries are filtered by trust profile
- resource reads are typed and policy-gated
- no read-only peer can trigger input, file-write, clipboard, browser-data,
  destructive, unattended, or relay behavior

## S4: Phase 12 Safety Packages

Goal: strengthen local automation before allowing broader peer operations.

Work:

- safer destructive action policy
- file/folder watcher package
- scheduling hooks
- credentials/secrets
- report export/distribution

Why here:

- peer operations will eventually expose more surfaces
- local destructive and secret behavior must be explicit before peer export
- scheduling and report distribution need clear audit rules

Exit criteria:

- destructive operations have resolved target metadata and dry-run behavior
  where practical
- secrets are redacted from events, reports, and MCP responses
- unattended behavior requires stored policy
- peer export of these resources remains denied until later phases

## S5: Peer P5-P6 Observe-Only Execution

Goal: allow remote observation with normal Slasher evidence.

Work:

- `POST /peer/resource/invoke`
- observe-only screen capture invoke
- `POST /peer/runs`
- `GET /peer/runs/{runId}`
- `GET /peer/runs/{runId}/events`
- peer artifact readback
- observe-only delegated Numadora runs

Why after namespace reads:

- invoke and delegated run are side-effecting, even when observe-only
- they should reuse proven namespace, identity, policy, and artifact paths

Exit criteria:

- peer capture creates normal run artifacts
- delegated observe run records `coordinatorPeer` and `executorPeer`
- scripts requiring input, file-write, clipboard, browser-data, destructive,
  unattended, or relay are denied before execution

## S6: Peer P7 Portable Core Extraction

Goal: make the core Slasher model portable.

Work:

- create `Slasher.Core`
- move portable models:
  - runs
  - events
  - targets
  - evidence
  - capabilities
  - policy inputs/decisions
  - namespace resources
  - peer DTOs
- introduce adapter-facing interfaces
- keep Windows behavior in the application/adapter layer

Why after observe-only peer execution:

- enough concrete peer behavior exists to know what belongs in core
- extraction can be evidence-driven rather than speculative

Exit criteria:

- `Slasher.Core` has no ASP.NET Core or Windows API dependency
- existing local behavior still works
- peer model tests primarily target portable core types

## S7: Peer P8 Interactive Operations

Goal: allow narrowly scoped peer interaction.

Initial candidates:

- `window.focus`
- `window.move`
- `input.text`
- `input.keys`

Requirements:

- `interactive` trust profile
- explicit requested capability
- local executor policy approval
- target identity before the action
- target revalidation immediately before input
- before/after evidence

Exit criteria:

- interactive operations fail closed when target identity is missing
- interactive operations fail closed when foreground target changes
- events show caller, coordinator peer, executor peer, target identity, and
  approval path
- destructive, secrets, unattended, and relay remain denied

## S8: Peer P9 Discovery And Advanced Transport

Goal: improve ergonomics only after the safety model is proven.

Candidates:

- mDNS discovery as untrusted visibility
- signed requests
- mTLS
- QUIC
- compact 9P-inspired transport
- relay after audit-chain support exists

Exit criteria:

- discovered peers are `unknown` by default
- discovery never grants trust
- signed requests include replay protection
- relay is explicitly configured and audited, or remains disabled

## Dependency Rules

- Peer P3 requires Peer P0-P2.
- Peer P5 requires Peer P3-P4.
- Peer P6 requires Peer P5.
- Peer P8 requires Peer P6 and the relevant local policy gates.
- Peer P9 requires Peer P8-level audit confidence unless it is discovery-only.
- Peer export of Phase 12 packages requires both the local package policy and
  peer namespace policy to exist.

## Recommended Near-Term Order

1. Implement CSV package.
2. Implement JSON package.
3. Implement Excel package.
4. Implement Peer P0 contracts.
5. Implement Peer P1 identity and registry.
6. Implement Peer P2 metadata endpoints.
7. Implement Peer P3 namespace listing.
8. Implement Peer P4 read-only resources.
9. Reassess Phase 12 safety package priority.
10. Implement Peer P5 observe-only invoke.

## Status Tracking

| Item | Status |
|---|---|
| Phase 12 data packages | in progress: CSV/JSON HTTP APIs added; Excel pending |
| Phase 12 safety packages | planned |
| Peer P0 contracts | in progress: peer DTOs, capabilities, resource address models added |
| Peer P1 identity and registry | in progress: local identity store and manual registry loader added |
| Peer P2 metadata endpoints | in progress: `/peer/hello` and `/peer/capabilities` added |
| Peer P3 read-only namespace | planned |
| Peer P4 read-only resources | planned |
| Peer P5 observe-only invoke | planned |
| Peer P6 delegated observe runs | planned |
| Peer P7 portable core extraction | planned |
| Peer P8 interactive peer operations | planned |
| Peer P9 discovery and transport | planned |

Update this table when implementation phases land.
