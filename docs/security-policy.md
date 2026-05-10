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

Slasher classifies actions into 15 capability classes. **これらは Numadora v0.2.1 の
言語キーワード** として `EXPORT EFFECT(class) FUNC` および `REQUIRES (class, ...)` の
括弧内で参照される (`numadora-language-spec.md` 1.4.1)。

| Class (Numadora 識別子) | Examples | Default |
|---|---|---|
| `observe` | list windows, capture, element tree, file info, ログ記録 | allowed |
| `user-input` | keys, text, mouse, focus, ウィンドウ操作, ダイアログ | allowed locally, logged carefully |
| `file-read` | read/open/list files | allow with path policy |
| `file-write` | write/copy/rename/zip/unzip | require target evidence |
| `destructive` | delete, overwrite, recursive delete, close all | require explicit dangerous flag or approval policy |
| `browser-data` | cookies, storage, downloads/uploads | redact values where needed |
| `clipboard` | get, assign, paste | redact or mark sensitive values |
| `process-app` | start, close, kill, select | log executable/process/window metadata |
| `network-out` | アウトバウンド HTTP / ピア通信 | opt-in with authentication |
| `network-in` | 着信受付 (peer namespace export 等) | opt-in with authentication |
| `peer-delegate` | Slasher-to-Slasher delegated runs | registered peer, trust profile, and capability policy |
| `scheduling` | unattended recurring runs | opt-in with stored policy |
| `unattended` | 無人実行 (UI なしの run) | opt-in with stored policy |
| `secrets` | credentials, tokens, secure variables | never log raw values by default |
| `system-info` | 時刻、CWD、軽い env、wait | allowed (基本) |

`network-out` と `network-in` は能力としては独立 (送信/受信の方向が違う)。
ピア委譲は `network-out` (送信) + `peer-delegate` (委譲意図) を併記。

## Script Policy

Numadora v0.2.1 統合では、能力宣言が **言語の一級概念**:

- 各ホスト関数は `EXPORT EFFECT(class) FUNC` (および `INTERACTIVE` 修飾) で必要能力を宣言
- 各スクリプト (main 持ちモジュール) は `REQUIRES (class, ...)` で使用能力を **静的宣言**
- check 段階: スクリプトの REQUIRES と実際に呼ばれているホスト関数の能力集合を突合 (推移検証)
- 不足は `requires_missing_capability`、過多は `requires_unused_capability` (warning)
- run 段階: スクリプトの REQUIRES と現行プロファイルを突合、不適合は `policy_denied` で拒否
- ホスト呼び出しはさらに lineage-aware ポリシー入力で評価 (`numadora-lineage-policy-plan.md`)

### 能力プロファイル

各プロファイルは **能力クラスの集合** として定義される:

| Profile | 含まれる能力クラス |
|---|---|
| `observe` | `observe`, `system-info` |
| `interactive` | `observe`, `system-info`, `user-input`, `process-app`, `clipboard` |
| `files` | `interactive` の全て + `file-read`, `file-write` (`destructive` 除く) |
| `browser-data` | `interactive` の全て + `browser-data` |
| `network` | `interactive` の全て + `network-out` |
| `peer-delegate` | `network` の全て + `peer-delegate` |
| `destructive` | `files` の全て + `destructive` |
| `secrets` | (基底プロファイル + `secrets` を opt-in 追加) |
| `unattended` | (基底プロファイル + `unattended` + `scheduling`) |

プロファイルは集合演算で組み合わせ可能 (run metadata に組み合わせ結果を記録)。

### スクリプト側の REQUIRES 例

```numadora
MODULE notepad-check
REQUIRES (process-app, user-input, observe)
```

このスクリプトは少なくとも `interactive` プロファイル相当が許可されたコンテキストで run 可能。
`observe` プロファイルでは `process-app` と `user-input` を含まないため `policy_denied`。

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

## Peer Delegation Rules

Slasher-to-Slasher communication should be treated as delegated execution, not
as direct remote control. A peer may request a run, but the executor peer must
apply its own local policy before touching local windows, input, files,
clipboard, browser state, or other machine resources.

Initial peer rules:

- peer mode is disabled unless explicitly configured
- unknown peers can use neither delegated runs nor artifact readback
- peer namespace export is a Slasher resource view, not a raw OS file system or
  desktop mount
- namespace `list`, resource `read`, and resource `invoke` all require policy
  evaluation
- registered peers receive a trust profile (`TrustProfile`): `known`, `observed`,
  or `interactive` (`numadora-language-spec.md` 9.6.1 の `slasher/peer.numai` で
  string-literal union として公開)
- `observed` peers may request observe-only runs but not input, file-write,
  clipboard, browser-data, destructive, secret, or unattended actions
- relay is denied by default
- **再帰委譲は禁止** (`policy_recursive_delegation`): 委譲経由で起動された run は
  さらに `delegate-run` を呼べない。run コンテキストの `delegation-depth >= 1` で拒否
- the executor peer records requester identity, coordinator peer, executor peer,
  trust profile, requested capabilities, granted capabilities, and refusal
  reasons in run artifacts
- capability negotiation is advisory; every concrete host call is still checked
  at run time
- remote artifact access must be authorized and redacted with at least the same
  strictness as MCP responses

### Numadora 側からの利用

委譲スクリプトは `slasher/peer` モジュール経由で書く:

```numadora
MODULE remote-deploy
REQUIRES (network-out, peer-delegate, observe)

IMPORT slasher/peer AS peer

EXPORT FUNC main()
  LET workstation = peer.find-peer("workstation") OR FAIL "not registered"
  LET run-id = workstation.delegate-run(script-source, "interactive", "remote-deploy")
END
```

詳細は `peer-network-model.md` (プロトコル・ポータブル コア) と
`numadora-security-network-design.md` (言語側統合)。

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

Before peer delegation:

- define peer identity and registry storage
- add peer capability names and trust profiles
- define portable namespace paths and resource kinds
- expose `GET /peer/hello` and `GET /peer/capabilities`
- expose read-only namespace inspection before any remote mutation
- keep `POST /peer/runs` observe-only until audit fields and artifact readback
  are proven
- add tests that unknown or under-trusted peers fail closed

## Open Questions

- Should Slasher have a policy file, environment variables, Web UI settings, or
  all three?
- What is the first allowed-root model for file operations: workspace-only,
  user home, explicit allowlist, or per-run policy?
- Should AI-agent MCP calls default to stricter policy than Web UI commands?
- How should interactive approval work when the run is started by MCP?
- Which actions need Windows elevation detection and refusal messages?
- Should peer mode share the local Web UI port or use a separate listener?
- Should peer authentication start with per-peer bearer tokens, signed requests,
  or both?
