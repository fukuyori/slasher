# Slasher Numadora Integration

Slasher が Numadora v0.2 をどうホスト・利用するかを定義する実装契約。

ガイディング ルール: **アプリは Slasher、言語は Numadora**。Slasher は AppOps
プラグイン (`slasher-plugin-architecture.md`) を通じてホスト機能を提供するが、
Numadora 言語の表面に Slasher 固有の構文を持ち込まない。

関連:

- `numadora-language-spec.md` — 言語仕様 v0.2
- `slasher-script.md` — Slasher スクリプト プロファイル (利用者向け)
- `slasher-plugin-architecture.md` — AppOps プラグインの設計
- `slasher-layer-architecture.md` — Slasher 全体の 5 層構成
- `numadora-migration-plan.md` — フェーズ計画
- `numadora-runtime-contract.md` — runtime 境界 (改訂対象)

## Active Shape

v0.2 スクリプトの典型例:

```numadora
MODULE notepad-smoke

IMPORT slasher/app AS app
IMPORT slasher/input AS input
IMPORT slasher/io AS io
IMPORT slasher/test AS test

EXPORT FUNC main()
  io.step("open notepad")
  LET ref = app.start-app("notepad.exe")

  LET win = ref.wait-for-window("Notepad", 10000) OR FAIL "notepad timeout"
  win.focus()

  io.step("type text")
  input.text("Slasher Numadora smoke")
  test.assert-foreground-title("contains", "Notepad")
END
```

スモーク サンプルは `scripts/numadora-samples/notepad-check.numa` にあり、
`scripts/verify-numadora-n0.ps1` で検証する。

## Initial Modules

AppOps プラグインが提供するモジュール (`slasher-plugin-architecture.md` 1.3 参照):

| Module | Purpose | Plugin |
|---|---|---|
| `slasher/app` | app/process operations | WindowsNative (将来 MacOSNative, LinuxNative) |
| `slasher/window` | window operations | (同上) |
| `slasher/input` | keyboard / mouse | (同上) |
| `slasher/screen` | screenshots | (同上) |
| `slasher/element` | UI element observation | (同上) |
| `slasher/dialog` | local dialog | (同上) |
| `slasher/browser` | browser (Selenium) | Browser (cross-platform) |
| `slasher/clipboard` | clipboard | Io 層が公開 (TBD) |
| `slasher/files` | files | Io 層が公開 |
| `slasher/data` | CSV/JSON/Excel | Io 層が公開 |
| `slasher/io` | step/log/wait | Slasher built-in |
| `slasher/test` | assertions | Slasher built-in |

`GET /plugins` は各プラグインの状態 (`available` / `not_applicable` /
`missing_prerequisites` / `disabled`) と提供モジュール一覧を返す。
Available でないプラグインのモジュールは `IMPORT` 解決で `module_not_found`
として失敗し、`details.reason = "plugin_not_available"` と `details.plugin = ...`
を含む。

## Initial Capability Metadata

各ホスト関数の能力分類とポリシー プロファイルは、プラグインの `.numai` 修飾子で
表現される:

- `EXPORT EFFECT(class) FUNC` = 副作用あり (能力クラス必須、純粋でない)
- `EXPORT INTERACTIVE EFFECT(class) FUNC` = ユーザ承認必須 (`allowInteractiveInput`)

能力クラス名は `numadora-language-spec.md` 1.4.1 の 13 種から選ぶ。プロファイルは
`security-policy.md` の能力プロファイル節に対応。

| Module | Function | EFFECT(class) | INTERACTIVE | 最小プロファイル |
|---|---|---|---|---|
| `slasher/app` | `start-app` | `process-app` | ✓ | interactive |
| `slasher/app` | `start-app-with-args` | `process-app` | ✓ | interactive |
| `slasher/app` | `enumerate-windows` | `observe` |   | observe |
| `slasher/app` | `wait-for-window` | `observe` |   | observe |
| `slasher/app` | `info` | `observe` |   | observe |
| `slasher/app` | `close` | `process-app` | ✓ | interactive |
| `slasher/window` | `foreground` | `observe` |   | observe |
| `slasher/window` | `find` | `observe` |   | observe |
| `slasher/window` | `enumerate` | `observe` |   | observe |
| `slasher/window` | `wait-for-title` | `observe` |   | observe |
| `slasher/window` | `info` | `observe` |   | observe |
| `slasher/window` | `focus` | `user-input` | ✓ | interactive |
| `slasher/window` | `set-state` | `user-input` | ✓ | interactive |
| `slasher/window` | `maximize` / `minimize` / `restore` / `show` / `hide` | `user-input` | ✓ | interactive |
| `slasher/window` | `move` | `user-input` | ✓ | interactive |
| `slasher/window` | `capture` | `observe` |   | observe |
| `slasher/window` | `close` | `user-input` | ✓ | interactive |
| `slasher/input` | `text` / `keys` / `mouse` / `wheel` / `drag` / `context-menu` | `user-input` | ✓ | interactive |
| `slasher/screen` | `enumerate` | `observe` |   | observe |
| `slasher/screen` | `capture-full` / `capture-monitor` / `capture-region` / `capture-window` | `observe` |   | observe |
| `slasher/screen` | `match-image` | `observe` |   | observe |
| `slasher/element` | `find` / `exists` / `read-text` / `tree` / `info` / `find-in` | `observe` |   | observe |
| `slasher/element` | `click` | `user-input` | ✓ | interactive |
| `slasher/dialog` | `message` / `confirm` | `user-input` | ✓ | interactive |
| `slasher/browser` | `open` / `close` | `process-app` | ✓ | interactive |
| `slasher/browser` | `current` / `title` / `url` / `locate` / `dom-text` / `attribute` / `screenshot` / `links` / `windows` | `observe` |   | observe |
| `slasher/browser` | `navigate` / `click` / `hover` / `type-text` / `press` / `upload` / `select-option` / `execute-js` | `user-input` | ✓ | interactive |
| `slasher/io` | `step` / `log` / `warn` / `error` / `print` | `observe` |   | observe |
| `slasher/io` | `wait` | `system-info` |   | observe |
| `slasher/test` | `assert-*` (全) / `note` / `attach` | `observe` |   | observe |
| `slasher/clipboard` | `read-text` | `clipboard` |   | interactive |
| `slasher/clipboard` | `write-text` / `clear` | `clipboard` | ✓ | interactive |
| `slasher/clipboard` | `has-content` | `observe` |   | observe |
| `slasher/files` | `exists` / `info` / `list` / `read-text` / `read-bytes` / `watch` / `poll-events` | `file-read` |   | files |
| `slasher/files` | `append-text` / `create-directory` | `file-write` |   | files |
| `slasher/files` | `write-text` / `write-bytes` / `copy` / `move` / `delete` | `file-write, destructive` |   | destructive |
| `slasher/files` | `stop-watch` | `observe` |   | observe |
| `slasher/data` | `csv-read` / `csv-to-json` / `json-read` / `json-query` / `excel-read` / `excel-workbook` / `excel-read-first-sheet` | `file-read` |   | files |
| `slasher/data` | `csv-write` / `json-write` | `file-write, destructive` |   | destructive |
| `slasher/peer` | `list-peers` / `find-peer` / `info` / `capabilities` / `namespace-list` / `namespace-read` / `delegate-status` / `delegate-wait` / `delegate-fetch-log` | `network-out` |   | network |
| `slasher/peer` | `delegate-run` | `network-out, peer-delegate` | ✓ | peer-delegate |

`/scripts/check` は `.numa` スクリプトの `requiredCapabilities` を返す。
`IMPORT module AS alias` と UFCS 呼び出しを静的解析して抽出する。
run モードは同じ能力情報を `numadora.hostCall` イベントに記録する。

INTERACTIVE 関数は `allowInteractiveInput` 承認なしでは fail closed する
(`policy_denied`)。承認済みでも入力送信直前にフォアグラウンド ターゲットを
再検証し、変わっていれば fail closed する。

## Runtime Boundary

```text
[script source (.numa)]
   ↓
1. Slasher が Web/MCP/HTTP/CLI で受信
2. Slasher 内の C# Numadora インタプリタがパース + 検査
3. AppOps プラグインが IMPORT slasher/... の .numai シグネチャを解決
4. プラグイン C# ホスト バインディングが Numadora ホスト呼び出しを実行
5. Slasher が run artifact を書き出す:
   run.json, events.jsonl, summary.txt, report.html, screenshots, logs
```

Numadora インタプリタは **Slasher 内蔵 (C#)**。外部ランタイム依存なし。
過去の Rust プロトタイプは設計参照用にのみ存在し、実行時に呼び出されない。

### モジュール解決順 (numadora-language-spec.md 6.3)

1. プラグイン埋め込みリソース (Available な AppOps プラグイン由来) を最優先
2. ワークスペース ローカル `.numai`
3. ワークスペース ローカル `.numa`
4. `std/` プレフィックス → 標準ライブラリ

### 純粋性検査と式中ホスト呼び出し

すべての `EFFECT` ホスト関数は不純として扱う。式の文脈で呼ぶことは
`type_impure_in_expression` で拒否される (`numadora-language-spec.md` 3.5.1)。
スクリプトは文として呼ぶか、戻り値を `LET` に束縛する。

## Host Binding Policy

`slasher-plugin-architecture.md` に従う:

- 各プラグインは自身の `.numai` を埋め込みリソースとして持つ
- C# 側のホスト バインディング クラス (`<PluginName>HostBindings`) が実装を提供
- 起動時に `.numai` シグネチャと C# 実装の整合性を検査 (不整合は `module_interface_mismatch`)
- リソース参照は `OPAQUE TYPE` (`AppRef`, `WindowRef` 等)

### Slasher が所有する責務

- v0.2 `.numa` プロファイルのパース / 検査 / 実行
- モジュール / IMPORT セマンティクス
- 関数呼び出しの評価 (UFCS 解決を含む)
- AppOps プラグインのライフサイクルと登録 (`/plugins`)
- run artifact の生成

### Slasher が所有しない責務

- Numadora 言語自体への Slasher 固有構文の追加
- 型システムの基本構造の変更

Slasher が新しい言語機能を必要としたら、それは Numadora 全体に有益な機能として
提案する。

## Error And Evidence Contract

### Check モード

各診断は以下のフィールドを持つ:

- `code` (例: `name_undefined_module`, `type_mismatch`)
- `message`
- `file`
- `line`
- `column`
- `severity`
- 対処ヒント (可能な場合)
- 詳細情報 (Numadora 由来の `details`)

Check モードは GUI アクションを実行しない。INTERACTIVE 関数や副作用関数を
呼ぶ `.numa` でも、構文・型のみ検査する。

### Run モード

各ホスト呼び出しは `numadora.hostCall` イベントを生成:

- `module` + `function` (例: `slasher/window`, `focus`)
- `arguments` (リダクション ポリシー適用後)
- `policyInput` (能力評価コンテキスト: profile, target identity, ...)
- `policyDecision` (allow/deny/require_approval の結果)
- `target` メタデータ (例: 解決済み window handle)
- 実行結果 (success または正規化された `RuntimeError`)

失敗したホスト呼び出しは `.numa` のソース位置に加え、プラグイン名と
`.numai` 上の関数定義位置を含む。

ホスト例外正規化テーブル (`numadora-language-spec.md` 9.6) で C# 側例外を
Numadora `RuntimeError.code` に変換:

| C# 例外 | code |
|---|---|
| `ArgumentException` | `host_invalid_argument` |
| `TimeoutException` | `host_timeout` |
| `UnauthorizedAccessException` | `host_access_denied` |
| `IOException` | `host_io_error` |
| `OperationCanceledException` | `host_cancelled` |
| `PlatformNotSupportedException` | `platform_not_supported` |
| `Win32Exception` | `host_win32_error` |
| その他 | `host_unknown_error` |

ポリシー拒否は `code = "policy_denied"`。

## v0.1 で「Non-Goals」だった項目の再評価

旧 integration ドキュメントが「現状不要」とした項目のうち、v0.2 で **採用された**
ものと **依然として不採用** のものを整理:

| 項目 | v0.2 状態 | 根拠 |
|---|---|---|
| `.numai` インターフェイス ロード | **採用** | `slasher-plugin-architecture.md` 4 章 (各プラグインが埋め込みリソースで保持) |
| `IMPORT slasher/app AS app` (slash) | **採用** | `numadora-language-spec.md` 6.3 |
| top-level コマンド構文 (`io.step "..."`) | 不採用 | マクロなし方針 (原則 4) |
| bare v1 コマンド (`start notepad.exe`) | 不採用 | Q-L3 ハードカット |
| `.slasher` 互換パーサ | 不採用 | Q-L3 ハードカット (manual port のみ) |
| compile-to-exe | 不採用 (スコープ外) | Slasher は HTTP サーバとして配布、`PublishSingleFile` で配布 |

## Implementation Phases

詳細は `numadora-migration-plan.md` および `slasher-plugin-architecture.md` 9 章。
横断的な PR シーケンス:

| PR | 内容 | 由来 |
|---|---|---|
| **AppOps PR-1** | フォルダ移動 + namespace + interface 分割 + プラグイン契約 (ハードカット) | layer/plugin |
| **AppOps PR-2** | NetArchTest 依存方向ルール | layer/plugin |
| **AppOps PR-3** | DI を PluginHost ベースに、OS 検出と CheckAvailability | plugin |
| **AppOps PR-4** | `[SupportedOSPlatform("windows")]` + `UnsupportedXxx` スタブ | layer |
| **AppOps PR-5** | self-contained single-file publish (Windows) | layer |
| **AppOps PR-8** | `/plugins` エンドポイント + プラグイン状態の HTTP 公開 | plugin |
| **Lang PR-A** | spec 改訂 (v0.2) | language-redesign (完了) |
| **Lang PR-B+C** | サンプル `.numa` の v0.2 書き換え + パーサ更新 (ハードカット 1 PR) | Q6 |
| **Lang PR-D** | `.numai` ホスト登録機構の C# 側実装 | language-redesign |
| **Lang PR-E** | 既存ホスト関数を `.numai` + プラグイン C# クラスに移行 | language-redesign |
| **Lang PR-F** | `Option[T]` / `MATCH` / `OR FAIL` / `RuntimeError` 実装 | language-redesign |
| **Lang PR-G** | リソース参照を `OPAQUE TYPE` に切替 (`"window:last"` 文字列廃止) | language-redesign |
| **Lang PR-H** | トレーリング ブロック構文 + UFCS | language-redesign |
| **Lang PR-J** | ドキュメント整合 (本ドキュメント、`slasher-script.md`) — 既に完了 | language-redesign |

## Open Questions

- 旧 N0〜N7 フェーズ表記 (`numadora-migration-plan.md`) と新 PR-1〜H の整合をどう取るか
- 各プラグイン `.numai` の最終シグネチャ確定 (本ドキュメントの能力テーブルと一致させる)
- `slasher/clipboard`, `slasher/files`, `slasher/data` の plugin 化判断 (Io 層所有か AppOps プラグインか)
- `numadora-runtime-contract.md` の更新 (テキスト RPC 廃止、型付き直接呼び出しへ)
- v0.2 サンプル `.numa` のリリース時期と互換性表記
