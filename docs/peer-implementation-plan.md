# Peer Namespace Implementation Plan

This document turns `peer-network-model.md` into implementation phases.

The implementation must stay local-first and fail-closed. Peer functionality
should begin as read-only namespace inspection, then observe-only delegated
runs, and only later interactive or mutating operations.

## Phase Names

Use `Peer P0` through `Peer P9` for tracking. These phases are independent from
the Phase 12 RPA package plan, but they reuse the same run, event, evidence,
policy, and artifact contracts.

## Cross-Phase Rules

- Peer mode is disabled by default.
- `127.0.0.1` remains the default bind address.
- Non-local peer access requires explicit configuration and authentication.
- Unknown peers fail closed.
- Namespace export is a Slasher resource view, not a raw OS mount.
- `list`, `read`, and `invoke` all pass through policy.
- Every side-effecting peer operation produces normal Slasher events or run
  artifacts.
- Peer protocol DTOs must not depend on Windows-specific types.

## Peer P0: Contracts Only

Goal: add portable model types without exposing network behavior.

Primary changes:

- add peer/resource DTOs under the current project, avoiding Windows
  dependencies
- add capability constants for peer namespace and peer runs
- add execution-scope metadata to run models where needed
- add tests for pure model/policy behavior

Suggested files:

- `src/Slasher/Automation/AutomationModels.cs`
- `src/Slasher/Automation/NumadoraPolicyEvaluator.cs`
- `src/Slasher/Peers/PeerModels.cs`
- `src/Slasher/Peers/PeerCapabilities.cs`
- `src/Slasher/Peers/ResourceAddress.cs`
- `tests/Slasher.Tests/PeerModelTests.cs`
- `tests/Slasher.Tests/PeerPolicyTests.cs`

Minimum model set:

```text
PeerIdentity
PeerRegistryEntry
PeerTrustProfile
PeerCapability
PeerCapabilityStatus
ResourceAddress
NamespaceEntry
NamespaceListResponse
ResourceReadResponse
ResourceInvokeRequest
PeerExecutionScope
```

Acceptance criteria:

- project builds
- peer DTOs serialize to stable JSON names
- invalid resource paths are rejected by parser tests
- no endpoint is exposed yet
- no peer setting changes the default local server behavior

Verification:

```powershell
dotnet build
dotnet test
```

## Peer P1: Local Identity And Manual Registry

Goal: create the local peer identity and read a manual peer registry.

Primary changes:

- generate or load local peer identity
- load a peer registry file from configuration
- keep registry disabled unless explicitly configured
- expose registry state to internal services only

Suggested files:

- `src/Slasher/Peers/PeerIdentityStore.cs`
- `src/Slasher/Peers/PeerRegistry.cs`
- `src/Slasher/Peers/PeerOptions.cs`
- `src/Slasher/Program.cs`
- `tests/Slasher.Tests/PeerRegistryTests.cs`

Configuration shape:

```json
{
  "peerMode": {
    "enabled": false,
    "identityPath": "config/slasher-peer-identity.json",
    "registryPath": "config/slasher-peers.json"
  }
}
```

Acceptance criteria:

- missing registry is not fatal while peer mode is disabled
- invalid registry fails clearly when peer mode is enabled
- trust profiles parse as `unknown`, `known`, `observed`, `interactive`,
  `operator`, or `admin-peer`
- registry entries do not grant capabilities by themselves

Verification:

```powershell
dotnet test
```

## Peer P2: Read-Only Peer Metadata Endpoints

Goal: expose authenticated peer metadata without exposing machine resources.

Endpoints:

```http
GET /peer/hello
GET /peer/capabilities
```

Primary changes:

- add `Api/SlasherEndpointExtensions.Peers.cs`
- add peer endpoint mapping
- require authentication for peer endpoints when peer mode is enabled
- return only metadata and policy-filtered capability status

Suggested files:

- `src/Slasher/Api/SlasherEndpointExtensions.Peers.cs`
- `src/Slasher/Api/SlasherEndpointExtensions.cs`
- `src/Slasher/Peers/PeerEndpointService.cs`
- `tests/Slasher.Tests/PeerEndpointTests.cs`

Acceptance criteria:

- `/peer/hello` returns protocol version, peer id, display name, and features
- `/peer/capabilities` returns capability status for the caller trust profile
- unknown or disabled peer mode cannot perform privileged operations
- existing `/health`, `/api`, and local automation endpoints keep existing
  behavior

Verification:

```powershell
dotnet test
dotnet run --project src\Slasher\Slasher.csproj --urls http://127.0.0.1:5055
```

Manual probe:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/peer/hello
```

## Peer P3: Read-Only Namespace Listing

Goal: expose a filtered Slasher resource namespace.

Endpoint:

```http
GET /peer/ns?path=...
```

Primary changes:

- implement namespace tree for local resources
- filter entries by trust profile and policy
- keep `invoke` operations hidden until implemented
- treat namespace paths as Slasher resource addresses, not file paths

Initial namespace:

```text
/
  identity
  capabilities
  runs
  artifacts
  windows
  screen
```

Suggested files:

- `src/Slasher/Peers/NamespaceService.cs`
- `src/Slasher/Peers/NamespaceResourceCatalog.cs`
- `tests/Slasher.Tests/NamespaceServiceTests.cs`

Acceptance criteria:

- `GET /peer/ns?path=/` lists only allowed resource entries
- invalid paths return structured errors
- `files`, `clipboard`, `input`, and destructive resources are not listed for
  `observed`
- namespace listing does not run GUI actions

Verification:

```powershell
dotnet test
```

## Peer P4: Observe-Only Resource Reads

Goal: read safe resources through the namespace.

Endpoint:

```http
GET /peer/resource?path=...
```

Initial reads:

- `/identity`
- `/capabilities`
- `/windows`
- `/windows/{handle}`
- `/screen/primary` metadata only
- `/runs`
- `/runs/{runId}`
- `/artifacts/runs/{runId}/...` through existing artifact read policy

Primary changes:

- map namespace resources to existing services
- return typed resource responses
- add audit records for remote resource reads where practical
- keep screenshot capture as `invoke`, not `read`, because it creates evidence

Suggested files:

- `src/Slasher/Peers/ResourceReadService.cs`
- `src/Slasher/Peers/ResourceReadModels.cs`
- `src/Slasher/Automation/AutomationRunArtifactStore.Read.cs`
- `tests/Slasher.Tests/ResourceReadServiceTests.cs`

Acceptance criteria:

- observed peer can read window list and run metadata
- unknown peer cannot read resources
- resource read responses include `path`, `kind`, and `capabilitiesUsed`
- file-system paths inside artifact requests remain constrained by existing
  artifact-store validation

Verification:

```powershell
dotnet test
```

Manual probes:

```powershell
Invoke-RestMethod "http://127.0.0.1:5055/peer/ns?path=/"
Invoke-RestMethod "http://127.0.0.1:5055/peer/resource?path=/windows"
```

## Peer P5: Observe-Only Resource Invoke

Goal: allow side-effecting observe actions that create normal evidence.

Endpoint:

```http
POST /peer/resource/invoke
```

Initial invokes:

- `/screen/primary` operation `capture`
- `/windows` operation `refresh` if needed

Primary changes:

- invoke creates a lightweight run or normal run event
- response returns run/artifact references instead of raw large payloads
- capture policy and redaction follow existing automation artifact rules

Suggested files:

- `src/Slasher/Peers/ResourceInvokeService.cs`
- `src/Slasher/Peers/ResourceInvokeModels.cs`
- `src/Slasher/Automation/AutomationRunArtifactStore.Writing.cs`
- `tests/Slasher.Tests/ResourceInvokeServiceTests.cs`

Acceptance criteria:

- screen capture invoke produces `run.json`, `events.jsonl`, and screenshot
  evidence
- invoke response includes run id and artifact links
- unsupported invoke returns a structured `peer_resource_operation_unsupported`
  error
- input, clipboard, file-write, browser-data, and destructive invokes remain
  denied

Verification:

```powershell
dotnet test
```

## Peer P6: Observe-Only Delegated Script Run

Goal: allow another peer to request an observe-only Numadora run.

Endpoint:

```http
POST /peer/runs
GET  /peer/runs/{runId}
GET  /peer/runs/{runId}/events
GET  /peer/runs/{runId}/artifacts/content?path=...
POST /peer/runs/{runId}/cancel
```

Primary changes:

- validate requesting peer identity and trust profile
- preflight requested capabilities
- execute only observe-capable scripts
- add `executionScope=peer`, `coordinatorPeer`, `executorPeer`, and
  `delegation` metadata to run artifacts
- reuse existing `/scripts/run` machinery internally

Suggested files:

- `src/Slasher/Peers/PeerRunService.cs`
- `src/Slasher/Peers/PeerRunModels.cs`
- `src/Slasher/Automation/AutomationRunModels.cs`
- `src/Slasher/Automation/AutomationRunArtifactStore.Events.cs`
- `tests/Slasher.Tests/PeerRunServiceTests.cs`

Acceptance criteria:

- observe-only script can run through `/peer/runs`
- script requiring input or file-write is denied before execution
- failed peer policy decisions still produce inspectable refusal details
- run artifact metadata identifies coordinator and executor peers
- artifact readback uses the same path validation as local runs

Verification:

```powershell
dotnet test
```

Manual probe:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/peer/runs -Method Post -ContentType application/json -Body '{"language":"numadora","script":"IMPORT slasher_io AS io\nFUNC main()\n  io.Log(\"peer observe\")\nEND","policyProfile":"observe","requestedCapabilities":["observe.log"]}'
```

## Peer P7: Portable Core Extraction

Goal: move contracts and policy-neutral logic out of the Windows application
project.

Primary changes:

- add `src/Slasher.Core/Slasher.Core.csproj`
- move portable models into `Slasher.Core`
- keep ASP.NET endpoints in `src/Slasher`
- keep Windows services behind adapter-facing interfaces
- update tests to reference `Slasher.Core` where appropriate

Suggested project shape:

```text
src/
  Slasher.Core/
    Runs/
    Capabilities/
    Policy/
    Evidence/
    Namespace/
    Peers/
  Slasher/
    Api/
    Automation/
    Peers/
    Windows/
    Files/
```

Acceptance criteria:

- `Slasher.Core` has no dependency on ASP.NET Core or Windows-specific APIs
- existing app still builds and runs
- existing tests pass
- peer DTO and namespace tests primarily target `Slasher.Core`

Verification:

```powershell
dotnet build
dotnet test
```

## Peer P8: Interactive And Mutating Resources

Goal: allow carefully constrained non-observe operations.

Initial candidates:

- `window.focus`
- `window.move`
- `input.text`
- `input.keys`

Deferred candidates:

- clipboard read/write
- file write/delete
- browser data APIs
- destructive actions
- unattended remote runs
- relay

Primary changes:

- require `interactive` trust profile
- require explicit request capability and local policy approval
- revalidate target identity immediately before input
- capture before/after evidence for risky actions
- record refusal details when the target changes

Acceptance criteria:

- interactive operations fail closed without target identity
- interactive operations fail closed if foreground target changes
- event logs show requested peer, executor peer, target identity, and approval
  path
- destructive, secrets, unattended, and relay remain denied

Verification:

```powershell
dotnet test
```

Manual verification should use a harmless app such as Notepad and should record
the run artifact path in the test notes.

## Peer P9: Discovery And Stronger Transport

Goal: add convenience after the policy and namespace model is proven.

Candidates:

- mDNS discovery as untrusted visibility only
- signed peer requests
- mTLS
- QUIC or a compact 9P-inspired transport
- relay after audit-chain support exists

Acceptance criteria:

- discovery never grants trust
- discovered peers appear as `unknown`
- signed requests include timestamp, nonce, method, path, and body hash
- replayed requests are rejected
- relay remains disabled unless explicitly configured and audited

## Recommended First Issues

1. Add peer model and resource-address parser tests.
2. Add local peer identity and registry loader.
3. Add `/peer/hello` behind peer-mode configuration.
4. Add `/peer/capabilities`.
5. Add read-only `/peer/ns`.
6. Add read-only `/peer/resource` for `/identity`, `/capabilities`, and
   `/windows`.
7. Add artifact-safe `/runs` and `/artifacts` resource reads.
8. Add observe-only `/screen/primary` capture invoke.
9. Add observe-only `/peer/runs`.
10. Extract `Slasher.Core`.

## Stop Conditions

Pause implementation and revisit the design if any of these appear:

- peer route requires bypassing existing artifact path validation
- peer route needs direct calls into `WindowsAutomationService` from transport
  code
- policy decisions differ between local script execution and peer execution
- an unknown peer can list or read machine resources
- an observe-only profile can trigger input, clipboard, file-write, browser-data,
  destructive, unattended, or relay behavior
