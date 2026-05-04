# Slasher Security Policy

Slasher is the application. It uses Numadora for future scripts and is designed
to control Windows resources broadly. That power is useful only if the security
rules are explicit, inspectable, and enforced consistently across Web, HTTP,
MCP, and script execution.

This document defines the security direction that should evolve alongside
feature development.

## Threat Model

Slasher should assume these risks are real:

- a local web page, script, or MCP client sends unintended commands
- an AI agent produces a harmful but syntactically valid script
- a useful script accidentally targets the wrong file, window, browser, or
  process
- credentials, clipboard contents, browser storage, screenshots, or files leak
  into logs or reports
- a script deletes, overwrites, closes, sends, uploads, downloads, or types into
  the wrong place
- a server bound beyond localhost is reachable by another user or machine
- a powerful Numadora script is reused in a different trust context

The baseline assumption is that Slasher is powerful enough to damage user data
or leak private information if misused.

## Core Rules

1. Slasher is local-first.
   The default bind address must remain `127.0.0.1`. Any non-local binding must
   require explicit configuration and authentication.

2. Dangerous actions require intent.
   Delete, overwrite, recursive operations, process/window close-all,
   clipboard paste, browser upload/download, credential access, and unattended
   scheduling must be distinguishable from ordinary read/inspect actions.

3. Every action must be auditable.
   Runs should record action name, target, source file/line when available,
   resolved paths/handles/process metadata, result status, and evidence links.

4. Dry-run should exist where practical.
   File deletes, folder deletes, recursive moves/copies, bulk operations, and
   scheduled actions should support a planning mode before execution.

5. Least privilege should be the default.
   Slasher should expose capabilities incrementally. A script should not gain
   file, browser, clipboard, process, or network powers just because it can run.

6. Secrets must not be logged by default.
   Credentials, tokens, cookies, clipboard values, browser storage values, and
   typed text marked as secret must be redacted in events, reports, and MCP
   responses.

7. Generated paths must be constrained.
   Artifacts may be written under known artifact roots. Recursive delete/move
   must verify resolved absolute paths and must not operate outside the
   intended workspace or user-approved target.

8. UI automation must preserve target evidence.
   Window and input actions should record the selected/foreground window
   identity so accidental focus changes are visible after the run.

9. Policy must be shared across surfaces.
   Web UI, HTTP API, MCP tools, and `.numa` execution should
   enforce the same safety categories and produce the same audit fields.

10. Unsafe bypasses must be explicit.
    If an action supports `force`, `recursive`, `overwrite`, unattended mode, or
    remote access, that choice must be visible in the command/API request and in
    the run report.

## Capability Classes

Slasher should classify actions before adding more power.

| Class | Examples | Default |
|---|---|---|
| Observe | list windows, capture, element tree, file info | allowed |
| User-input | keys, text, mouse, focus | allowed locally, logged carefully |
| File-read | read/open/list files | allow with path policy |
| File-write | write/copy/rename/zip/unzip | require target evidence |
| Destructive | delete, overwrite, recursive delete, close all | require explicit dangerous flag or approval policy |
| Browser-data | cookies, storage, downloads/uploads | redact values where needed |
| Clipboard | get, assign, paste | redact or mark sensitive values |
| Process/app | start, close, kill, select | log executable/process/window metadata |
| Network/remote | non-local bind, remote clients, HTTP calls | opt-in with authentication |
| Scheduling | unattended recurring runs | opt-in with stored policy |
| Secrets | credentials, tokens, secure variables | never log raw values by default |

## Script Policy

Future Numadora integration should treat security as part of the runtime
contract:

- scripts declare or are assigned a capability profile
- check mode can report required capabilities before run mode
- run mode refuses missing capabilities instead of silently prompting from deep
  inside an action
- script reports include the capability profile used for the run
- imported modules should not grant hidden extra powers
- host calls should be evaluated with lineage-aware policy input as described
  in `numadora-lineage-policy-plan.md`

Initial profiles:

| Profile | Purpose |
|---|---|
| `observe` | read-only inspection and screenshots |
| `interactive` | normal local input/window automation |
| `files` | file read/write without destructive recursion |
| `destructive` | delete, overwrite, close-all, recursive operations |
| `browser-data` | cookies, storage, downloads, uploads |
| `unattended` | scheduled or background execution |
| `secrets` | access to protected secret values |

Profiles can be combined, but the combination should appear in run metadata.

## Approval Model

The first version can be configuration-based rather than interactive:

- default local interactive runs allow observation, window focus, input, and
  normal app start
- destructive and secret actions require an explicit policy setting
- remote HTTP access requires `SLASHER_API_KEY`
- unattended runs require a saved policy profile

Later versions can add interactive approval prompts in the Web UI.

## Redaction Rules

Values should be redacted when they are:

- passed through credential/secret APIs
- browser cookies or storage values
- clipboard contents unless explicitly marked inspectable
- typed text marked secret
- environment variables or paths matching known secret names
- authorization headers and API keys

Redaction should preserve shape where possible:

```json
{
  "value": "[redacted]",
  "redacted": true,
  "kind": "secret"
}
```

## Destructive Action Requirements

Before implementing new destructive operations, require:

- resolved absolute target path/window/process metadata
- command source and caller surface
- whether the action is recursive, overwrite, force, or unattended
- dry-run support where practical
- post-action result with affected count or target identity
- refusal when the resolved target is outside allowed roots

Examples:

- `folder delete` must report the resolved path and whether it is recursive
- `file copy --overwrite` must report source, destination, and overwrite
- `close all` must report every target window/process before closing
- browser upload must report local path and selector, but redact sensitive
  field values

## Network And API Rules

- Default bind: `127.0.0.1`.
- Non-local bind requires explicit configuration.
- Any non-local bind requires bearer-token authentication at minimum.
- Authentication failures must be logged without echoing token values.
- CORS should remain closed unless a specific trusted origin is configured.
- MCP should inherit the same server-side policy as HTTP.

## Development Gates

Before N1/N2 host bindings:

- define initial capability metadata for the first Slasher Numadora modules
- make check/run contracts capable of carrying required capabilities
- keep sample `.numa` scripts in the `interactive` profile
- define the local lineage and policy input shape before executing real
  Numadora host calls

Before Phase 12 destructive expansion:

- implement resolved-path validation policy
- document dry-run behavior
- add tests for refusing unsafe paths

Before credentials/secrets:

- add redaction helpers
- add secret-aware event/report fields
- add tests proving secrets do not appear in `events.jsonl`, `summary.txt`,
  `report.html`, or MCP responses

Before remote access:

- require `SLASHER_API_KEY`
- document bind address and firewall expectations
- add audit events for remote caller metadata

## Open Questions

- Should Slasher have a policy file, environment variables, Web UI settings, or
  all three?
- What is the first allowed-root model for file operations: workspace-only,
  user home, explicit allowlist, or per-run policy?
- Should AI-agent MCP calls default to stricter policy than Web UI commands?
- How should interactive approval work when the run is started by MCP?
- Which actions need Windows elevation detection and refusal messages?
