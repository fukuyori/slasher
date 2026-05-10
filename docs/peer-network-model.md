# Slasher Peer Network Model

This document defines the first design direction for communication between
Slasher instances.

The goal is not to turn Slasher into a generic remote desktop tool. The goal is
to let trusted Slasher peers expose machine capabilities as a small, typed,
auditable resource namespace. Other peers can inspect that namespace, read
observations, and request constrained operations while preserving capability
policy, audit records, and portable core semantics.

The original mental model is Plan 9-like: resources on other machines should
feel addressable through a uniform namespace. Slasher should borrow that
principle without blindly copying Plan 9 or exposing unsafe raw system access.

The modern product analogy is HarmonyOS-like: multiple devices can participate
in one coordinated environment while each device keeps local ownership of its
resources. Slasher should borrow that coordination model for automation peers,
not the exact OS implementation.

## Design Goals

- Slasher instances can discover, identify, and call other Slasher instances.
- Slasher resources can be represented through a portable namespace rather than
  only through ad hoc endpoint names.
- A peer that receives a request always evaluates the request with its own
  local policy before touching local resources.
- The automation core remains portable: run planning, capability evaluation,
  event shaping, redaction, and artifact schemas should not depend on Windows.
- Platform-specific resource control lives behind adapters.
- Peer communication reuses the same run, event, evidence, and structured error
  model used by local Web, HTTP, MCP, and script execution.
- The first implementation is manually configured and fail-closed. Automatic
  discovery, relay, and NAT traversal are later features.

## Non-Goals For V1

- no automatic trust
- no internet-scale mesh
- no peer relay
- no remote shell
- no raw OS-level cross-machine file system mounting
- no unbounded namespace export
- no unauthenticated LAN discovery
- no bypass around local safety policy

## Plan 9 Inspiration

Plan 9's useful idea for Slasher is not the exact protocol. The useful idea is
that distributed resources can be presented through a coherent namespace with a
small set of operations.

For Slasher, that means a remote PC should not appear only as a bag of RPC
methods. It should appear as a peer namespace:

```text
/peers/workstation/
  identity
  capabilities
  policy
  runs/
  artifacts/
  windows/
  screen/
  input/
  clipboard/
  files/
  browser/
```

Each entry is a typed Slasher resource. Reading a resource observes state.
Writing or invoking a resource requests an action. The executor peer still
applies policy before performing the action.

This keeps the interface portable:

- Windows can expose `windows/`, `screen/`, `input/`, and native element trees.
- A future Linux adapter can expose equivalent resources with different local
  implementation details.
- A headless peer can expose `files/`, `browser/`, `runs/`, or service-specific
  resources without pretending to have a desktop.

The namespace is a Slasher namespace, not the host OS namespace. A path such as
`/peers/workstation/files/workspace/report.txt` is a Slasher resource address
that must be resolved through policy and adapter code. It is not a promise that
the remote machine's raw file system is mounted.

## HarmonyOS-Like Modernization

Plan 9 gives Slasher the namespace idea. A HarmonyOS-like view gives Slasher the
modern device-coordination idea: separate machines should contribute
capabilities to a shared automation environment without losing their local
policy boundary.

For Slasher, that means:

- a peer is both a device and an automation capability provider
- resource access is capability-scoped rather than globally mounted
- the coordinator can compose work across peers
- the executor peer remains the authority for its own machine
- user-visible identity matters: actions should say which peer will observe or
  operate which resource
- portability matters: the same namespace shape should survive different
  platform adapters

This differs from classic remote control:

| Remote-control framing | Slasher peer framing |
|---|---|
| one machine controls another machine | one peer requests another peer to operate its own resources |
| screen and input are the main interface | typed resources and evidence are the main interface |
| trust is often session-wide | trust is capability-scoped |
| the remote machine is a target | the remote machine is an executor with policy |
| success is visual interaction | success is structured result plus artifacts |

The long-term design target is a distributed automation environment:

```text
Slasher namespace
  /local
  /peers/workstation
  /peers/laptop
  /peers/buildbox

Portable core
  policy
  capabilities
  resource addresses
  runs
  evidence

Adapters
  Windows desktop
  browser
  files
  future Linux/macOS/headless devices
```

The system should feel like one automation surface while still recording which
peer actually executed every action.

## Conceptual Layers

```text
Slasher application
  Web UI / HTTP / MCP / CLI / scripts

Portable automation core
  run model
  capability model
  policy evaluator
  event and artifact model
  resource namespace model
  peer request model

Platform adapters
  Windows window/input/screen/browser/file adapters
  future Linux/macOS or headless adapters

Peer transport
  peer identity
  authenticated HTTP/JSON protocol
  artifact transfer
  future discovery/relay transports
```

The portable core must not call Windows APIs directly. It should describe what
must be done, evaluate whether it is allowed, and normalize the result. The
adapter performs the platform action and reports evidence back to the core.

## Peer Roles

A distributed run separates roles that are identical in local runs:

| Role | Meaning |
|---|---|
| caller | user, agent, UI, script, or peer that requested the run |
| coordinator peer | peer that owns the outer run and aggregates results |
| executor peer | peer that controls the target machine resources |
| target | resource being observed or controlled |

For a local run, the coordinator and executor are the same peer. For a peer run,
the coordinator asks another peer to execute a constrained run.

## Peer Identity

IP address and host name are not stable identities. A peer should have a stable
identity document:

```json
{
  "schemaVersion": 1,
  "peerId": "peer_7K4M2Q",
  "displayName": "workstation",
  "publicKey": "base64-public-key",
  "owner": "n_fuk",
  "createdAt": "2026-05-07T00:00:00Z"
}
```

The local registry pins known peers:

```json
{
  "schemaVersion": 1,
  "peers": [
    {
      "peerId": "peer_7K4M2Q",
      "displayName": "workstation",
      "baseUrl": "https://workstation.local:5055",
      "publicKey": "base64-public-key",
      "trustProfile": "observed",
      "enabled": true
    }
  ]
}
```

Unknown peers may be visible in a future discovery UI, but they must not accept
delegated runs until explicitly registered and trusted.

## Trust Profiles

Trust is layered, not boolean.

| Profile | Default allowance |
|---|---|
| unknown | refuse delegated runs |
| known | health and hello only |
| observed | observe-only runs |
| interactive | observe plus target-revalidated input/window actions |
| operator | selected file/write/process actions with policy limits |
| admin-peer | destructive, unattended, or relay powers; reserved for future use |

The executor peer must always apply its own trust profile. A coordinator cannot
grant itself more power by requesting a higher profile.

## Capability Names

Peer capabilities should be explicit and composable.

```text
peer.hello
peer.capabilities.read
peer.namespace.read
peer.resource.read
peer.resource.invoke
peer.run.delegate
peer.run.cancel
peer.artifact.read
peer.relay

observe.window.list
observe.screen.capture
observe.element.tree
observe.browser.read

input.text
input.keys
input.mouse
window.focus
window.move
app.start
app.close

file.read
file.write
file.delete
clipboard.read
clipboard.write
browser.data.read
browser.data.write
destructive
unattended
secrets
```

Capability negotiation reports what the executor can support and what its
current policy can allow for the requesting peer. The final decision still
happens at run time, with concrete targets and parameters.

### Numadora 言語側との対応 (v0.2.1)

ピア通信は **`slasher/peer`** ホスト モジュール (`scripts/numadora-host/slasher/peer.numai`)
として Numadora の一級概念で公開される。スクリプトは:

```numadora
MODULE remote-deploy
REQUIRES (network-out, peer-delegate, observe)

IMPORT slasher/peer AS peer

EXPORT FUNC main()
  LET ws = peer.find-peer("workstation") OR FAIL "not registered"
  LET run-id = ws.delegate-run(script-source, "interactive", "remote-deploy")
END
```

このスクリプトの `REQUIRES` に挙がる能力クラス (`network-out`, `peer-delegate`,
`observe`) は本ドキュメントのピア能力名と以下のように対応する:

| Numadora 能力クラス | ピア能力 (本ドキュメント) | 意味 |
|---|---|---|
| `network-out` | `peer.namespace.read`, `peer.resource.read`, `peer.capabilities.read`, `peer.artifact.read` | アウトバウンド ネットワーク呼び出し |
| `peer-delegate` | `peer.run.delegate` | 他ピアへの run 委譲 |
| `observe` | `observe.*` | 観測型操作 |
| `user-input` | `input.*`, `window.*` | 入力操作 |
| `process-app` | `app.start`, `app.close` | プロセス操作 |
| `file-read` | `file.read` | ファイル読み取り |
| `file-write` | `file.write` | ファイル書き込み |
| `destructive` | `file.delete`, `destructive` | 破壊操作 |
| `clipboard` | `clipboard.*` | クリップボード操作 |
| `browser-data` | `browser.data.*` | ブラウザ データ操作 |
| `secrets` | `secrets` | 秘密値アクセス |
| `unattended` | `unattended` | 無人実行 |

**再帰委譲は禁止** (`policy_recursive_delegation`): 委譲経由で起動された run は
さらに `delegate-run` を呼べない (`numadora-language-spec.md` 9.6.1, 6.5.3)。
`peer.relay` 能力は denied by default で、これも再帰委譲禁止と整合する。

## Resource Namespace

The peer protocol should include namespace operations in addition to delegated
runs. Runs remain the evidence-first way to perform multi-step automation, but
namespace operations make the Plan 9-like model visible and portable.

Recommended core operations:

| Operation | Meaning |
|---|---|
| `list` | list child resources under a namespace path |
| `read` | observe a resource and return typed data |
| `invoke` | request a side-effecting operation on a resource |
| `watch` | future event stream for resource changes |

Initial resource kinds:

```text
peer.identity
peer.capabilities
run.collection
run.record
artifact
window.collection
window.record
screen
input
clipboard
file.collection
file.record
browser.session
browser.dom
```

Example addresses:

```text
/peers/workstation/identity
/peers/workstation/capabilities
/peers/workstation/windows
/peers/workstation/windows/0x00012345
/peers/workstation/screen/primary
/peers/workstation/runs/20260507-101500-peer-capture-a1b2
/peers/workstation/artifacts/runs/20260507-101500-peer-capture-a1b2/report.html
```

The portable core should understand these addresses and the policy inputs they
imply. The adapter decides how a resource is implemented locally.

## Slasher Peer Protocol V1

V1 should use authenticated HTTP/JSON so it can reuse the existing ASP.NET Core
server, report model, and MCP bridge behavior. It should expose both namespace
operations and evidence-first delegated runs.

Initial endpoints:

```http
GET  /peer/hello
GET  /peer/capabilities
GET  /peer/ns?path=...
GET  /peer/resource?path=...
POST /peer/resource/invoke
POST /peer/runs
GET  /peer/runs/{runId}
GET  /peer/runs/{runId}/events
GET  /peer/runs/{runId}/artifacts/content?path=...
POST /peer/runs/{runId}/cancel
```

V1 deployment rules:

- default bind remains `127.0.0.1`
- peer mode requires explicit non-local bind configuration
- peer mode requires authentication
- peer mode requires a peer registry
- relay is disabled
- destructive and unattended capabilities are denied unless explicitly enabled

## Hello Response

```json
{
  "schemaVersion": 1,
  "protocol": "slasher-peer",
  "protocolVersion": 1,
  "peerId": "peer_7K4M2Q",
  "displayName": "workstation",
  "serverVersion": "0.3.0",
  "publicKey": "base64-public-key",
  "features": [
    "capability-negotiation",
    "delegated-run",
    "artifact-read"
  ]
}
```

`hello` identifies the peer. It does not grant execution rights.

## Namespace Response

```json
{
  "schemaVersion": 1,
  "path": "/",
  "entries": [
    {
      "name": "identity",
      "path": "/identity",
      "kind": "peer.identity",
      "operations": ["read"]
    },
    {
      "name": "windows",
      "path": "/windows",
      "kind": "window.collection",
      "operations": ["list", "read"]
    },
    {
      "name": "screen",
      "path": "/screen",
      "kind": "screen",
      "operations": ["read", "invoke"]
    },
    {
      "name": "runs",
      "path": "/runs",
      "kind": "run.collection",
      "operations": ["list", "read", "invoke"]
    }
  ]
}
```

Namespace entries are filtered by the requesting peer's trust profile and by
local policy. A peer should not list resources that the requester can never
observe.

## Resource Read Response

```json
{
  "schemaVersion": 1,
  "path": "/windows/0x00012345",
  "kind": "window.record",
  "capabilitiesUsed": ["observe.window.list"],
  "value": {
    "handle": "0x00012345",
    "title": "Untitled - Notepad",
    "processName": "notepad",
    "bounds": {
      "x": 80,
      "y": 80,
      "width": 900,
      "height": 640
    }
  }
}
```

## Resource Invoke Request

```json
{
  "schemaVersion": 1,
  "requestId": "req_20260507_002",
  "path": "/screen/primary",
  "operation": "capture",
  "coordinatorPeerId": "peer_LAPTOP",
  "caller": {
    "surface": "mcp",
    "agent": "codex"
  },
  "parameters": {
    "captureTarget": "screen"
  }
}
```

Side-effecting invokes should create a normal run event or a lightweight run so
that evidence and audit behavior match script execution.

## Capability Response

```json
{
  "schemaVersion": 1,
  "peerId": "peer_7K4M2Q",
  "requestingPeerId": "peer_LAPTOP",
  "trustProfile": "observed",
  "capabilities": [
    {
      "name": "observe.window.list",
      "status": "allowed"
    },
    {
      "name": "observe.screen.capture",
      "status": "allowed"
    },
    {
      "name": "input.text",
      "status": "denied",
      "reason": "trust_profile"
    }
  ],
  "limits": {
    "maxRunSeconds": 300,
    "maxArtifactBytes": 104857600,
    "relayAllowed": false
  }
}
```

## Delegated Run Request

```json
{
  "schemaVersion": 1,
  "requestId": "req_20260507_001",
  "idempotencyKey": "req_20260507_001",
  "coordinatorPeerId": "peer_LAPTOP",
  "caller": {
    "surface": "mcp",
    "agent": "codex"
  },
  "requestedCapabilities": [
    "observe.window.list",
    "observe.screen.capture"
  ],
  "policyProfile": "observe",
  "mode": "script",
  "language": "numadora",
  "entryPoint": "<inline>",
  "script": "IMPORT slasher_screen AS screen\nscreen.Capture()\n",
  "capturePolicy": {
    "captureAfterEachStep": true,
    "captureTarget": "screen"
  }
}
```

The executor validates:

- the peer is registered and enabled
- authentication is valid
- the requested capabilities fit the trust profile
- the concrete host calls are allowed by local policy
- target identities and path roots are valid where applicable

## Delegated Run Response

```json
{
  "schemaVersion": 1,
  "accepted": true,
  "runId": "20260507-101500-peer-capture-a1b2",
  "executorPeerId": "peer_7K4M2Q",
  "status": "running",
  "artifacts": {
    "run": "/peer/runs/20260507-101500-peer-capture-a1b2",
    "events": "/peer/runs/20260507-101500-peer-capture-a1b2/events"
  }
}
```

Refusals are structured errors:

```json
{
  "schemaVersion": 1,
  "accepted": false,
  "error": {
    "code": "peer_capability_denied",
    "message": "The requesting peer is not trusted for input.text.",
    "details": {
      "requestingPeerId": "peer_LAPTOP",
      "trustProfile": "observed",
      "capability": "input.text"
    }
  }
}
```

## Run Artifact Extensions

Peer runs extend the existing run schema with peer metadata:

```json
{
  "executionScope": "peer",
  "coordinatorPeer": {
    "peerId": "peer_LAPTOP",
    "displayName": "laptop"
  },
  "executorPeer": {
    "peerId": "peer_7K4M2Q",
    "displayName": "workstation"
  },
  "delegation": {
    "requestedCapabilities": ["observe.screen.capture"],
    "grantedCapabilities": ["observe.screen.capture"],
    "trustProfile": "observed",
    "relayDepth": 0
  }
}
```

Every event created by a peer run should identify the executor peer. This keeps
future multi-peer reports readable.

## Portable Core Boundary

The following concepts belong in the portable core:

- run request and run report models
- event, target, evidence, log, and error models
- capability classification
- policy input and policy decision models
- redaction rules
- peer identity and registry models
- resource address and namespace models
- peer protocol DTOs
- artifact naming and schema rules
- script host-call planning and validation

The following concepts belong in platform adapters:

- window enumeration and manipulation
- keyboard, mouse, and clipboard access
- screen capture
- UI Automation or native element inspection
- browser driver control
- file system operations that depend on OS-specific behavior
- process start/close details

The peer transport should depend on the portable core and call an executor
interface. It should not call `WindowsAutomationService` directly.

Recommended future shape:

```text
src/
  Slasher.Core/
    Runs/
    Capabilities/
    Policy/
    Evidence/
    Namespace/
    Peers/
  Slasher.Adapters.Windows/
    Windows/
    Files/
    Browser/
    Clipboard/
  Slasher/
    Program.cs
    Api/
    PeerApi/
```

This split can be incremental. New peer DTOs and policy models can start in the
current project, but they should avoid direct Windows dependencies.

## Implementation Phases

Implementation details are tracked in `peer-implementation-plan.md`. This
section keeps the conceptual phase order only.

### P0: Core Contracts

- add peer DTOs and capability names
- add portable resource address and namespace entry models
- document run artifact extensions
- keep peer mode disabled by default

### P1: Manual Peer Registry

- load a local peer registry file
- expose `GET /peer/hello`
- expose `GET /peer/capabilities`
- expose read-only `GET /peer/ns`
- require `SLASHER_API_KEY` for non-local peer routes

### P2: Observe-Only Namespace Reads

- expose read-only `GET /peer/resource`
- map `/windows`, `/screen`, `/runs`, and `/artifacts` to existing services
- filter namespace entries and resource reads by trust profile
- record namespace reads as audit events where practical

### P3: Observe-Only Delegated Run

- implement `POST /peer/runs`
- allow only observe capabilities
- convert accepted peer runs into existing script runs
- include coordinator and executor peer metadata in artifacts
- support artifact readback

### P4: Interactive Resource Invoke And Delegation

- allow input/window actions only with target revalidation
- require explicit interactive profile
- record before/after target identity

### P5: Portable Core Extraction

- move contracts and policy types into `Slasher.Core`
- introduce platform adapter interfaces
- keep the Windows adapter as the first concrete implementation

### P6: Discovery And Advanced Transport

- add mDNS discovery as an untrusted visibility feature
- consider signed requests
- consider mTLS or QUIC
- consider relay only after audit chain support is complete

## Open Questions

- Should peer identity be generated on first launch or by an explicit setup
  command?
- Where should the registry live on Windows?
- Should peer tokens be per-peer, per-profile, or global?
- Should artifact redaction differ for local caller and remote caller?
- How much of Numadora host-call planning should move into the portable core
  before peer runs are implemented?
- Should peer mode have a separate port from local Web UI mode?
- Should the namespace protocol remain HTTP/JSON, or should Slasher eventually
  grow a compact 9P-inspired transport after the semantics settle?
