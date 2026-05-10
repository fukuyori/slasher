# Numadora Integration And Migration Plan

Slasher が Numadora v0.2 を統合・実装するための実装計画。

これは実装プランであり、ユーザ移行ガイドではない。利用者向けの構文書き換えは
`migration-from-slasher-v1.md`、言語仕様は `numadora-language-spec.md`、
セキュリティ規則は `security-policy.md`、lineage-aware ポリシーは
`numadora-lineage-policy-plan.md` を参照。

## Goals

- Numadora `.numa` v0.2 を統一スクリプト パスにする
- Slasher をアプリケーション名・ユーザ向けプロダクト名として保持
- Numadora を汎用言語として扱い、外部アプリケーション制御を **AppOps プラグイン**
  経由のホスト能力として提供 (`slasher-plugin-architecture.md`)
- 現行 run artifact モデル (`run.json`, `events.jsonl`, `summary.txt`,
  `report.html`, screenshots, logs) を保持
- Slasher の既存自動化 API を活用し、言語ランタイム内に GUI 自動化を再構築しない
- v1 `.slasher` を public script API から拒否 (Q-L3 ハードカット完了)

## Non-Goals

- Slasher 専用の第二言語を作らない
- v1 コマンド表記で Numadora の module / function / 構文設計を縛らない
- compile-to-exe は対象外 (`.numa` の check/run が安定した後に検討)
- v1 完全機能パリティを `.numa` 移行のブロッカーとしない

## Current Baseline (v0.2)

確定済の設計成果物 (このプランの前提):

- `numadora-language-spec.md` v0.2 - 言語仕様 (canonical)
- `numadora-language-redesign.md` - 再構成方針 (アンカー、`.numai` ホスト、マクロなし、UFCS、トレーリング ブロック)
- `numadora-base-structure.md` - 字句 + 意味の詳細
- `numadora-core-systems.md` - 型 / モジュール / 実行モデル
- `slasher-layer-architecture.md` - 5 層構成 (Api/Core/Io/Network/AppOps)
- `slasher-plugin-architecture.md` - AppOps プラグイン契約
- `slasher-script.md` - スクリプト プロファイル
- `slasher-numadora-integration.md` - 実装契約
- `numadora-runtime-contract.md` - check/run HTTP 境界

## Target Architecture

```text
AI / user
  │
  │ .numa (v0.2)
  ▼
Slasher Api (HTTP / MCP / CLI / Web UI)
  │
  ▼
Slasher Core (C# Numadora interpreter, run artifact, policy)
  │
  ▼
AppOps Plugin Host
  ├── WindowsNative (slasher/window, /input, /screen, /element, /dialog, /app)
  ├── Browser      (slasher/browser, Selenium)
  ├── (将来) Excel  (slasher/excel)
  ├── (将来) GIMP   (slasher/gimp)
  └── ...
  │
  ▼
shared run artifacts
```

旧 Rust Numadora プロトタイプは設計参照のみ。実行時に呼ばれない。

## v0.2 PR Plan (実装フェーズ)

`slasher-plugin-architecture.md` 9 章 と `numadora-language-redesign.md` 9 章 を
統合した PR シーケンス。

### AppOps / 5 層構成系

| PR | 内容 | 依存 |
|---|---|---|
| **AppOps PR-1** | フォルダ移動 + namespace + interface 分割 + プラグイン契約 + 既存 Windows コードを WindowsNativePlugin / BrowserPlugin に再配置 (ハードカット 1 PR) | (前提) |
| **AppOps PR-2** | NetArchTest 導入と依存方向ルール (`slasher-layer-architecture.md` 4 章) | PR-1 |
| **AppOps PR-3** | DI を PluginHost ベースに、OS 検出と CheckAvailability | PR-1 |
| **AppOps PR-4** | `[SupportedOSPlatform("windows")]` + `UnsupportedXxx` スタブ | PR-3 |
| **AppOps PR-5** | self-contained single-file publish (Windows) CI | PR-1 |
| **AppOps PR-6** | Mac/Linux 用 publish ジョブ (Browser のみ動作) | PR-4, PR-5 |
| **AppOps PR-7** | trimming 検証 + トリマー設定 | PR-5 |
| **AppOps PR-8** | `/plugins` エンドポイントとプラグイン状態 HTTP 公開 | PR-3 |

### 言語実装系 (Numadora v0.2)

| PR | 内容 | 依存 |
|---|---|---|
| **Lang PR-A** | spec 改訂 (v0.2) | (完了 - `numadora-language-spec.md`) |
| **Lang PR-B+C** | サンプル `.numa` の v0.2 書き換え + パーサ更新 (ハードカット 1 PR) | PR-A |
| **Lang PR-D** | `.numai` ホスト登録機構の C# 側実装 (属性 + 起動時リンク) | PR-C, AppOps PR-1 |
| **Lang PR-E** | 既存ホスト関数を `.numai` + プラグイン C# クラスに移行 | PR-D, AppOps PR-1 |
| **Lang PR-F** | `Option[T]` / `MATCH` / `OR FAIL` / `RuntimeError` のインタプリタ実装 | PR-C |
| **Lang PR-G** | リソース参照を `OPAQUE TYPE` に切替 (`"window:last"` 文字列廃止) | PR-D, PR-F |
| **Lang PR-H** | トレーリング ブロック構文 + UFCS の実装 | PR-C |
| **Lang PR-J** | 統合ドキュメントの v0.2 整合 | (完了 - 本ドキュメント, `slasher-script.md`, `slasher-numadora-integration.md`) |

### 推奨実施順 (依存解消順)

```
1. AppOps PR-1 (層分割 + プラグイン契約)
2. AppOps PR-2 (規律テスト)
3. AppOps PR-3 (PluginHost と DI)
4. Lang PR-D (.numai ホスト登録)
5. Lang PR-E (既存ホスト関数移行)
6. Lang PR-F (Option/MATCH/OR FAIL)
7. Lang PR-G (OPAQUE TYPE 切替)
8. Lang PR-H (トレーリング ブロック + UFCS)
9. Lang PR-B+C (サンプル書換 + パーサ v0.2 専用化)
10. AppOps PR-4〜8 (OS 属性、配布、/plugins エンドポイント)
```

## Acceptance Criteria

各 PR の合格基準は `slasher-plugin-architecture.md` 9 章および
`numadora-language-redesign.md` 9 章にある。共通基準:

- 既存の `dotnet test` (NetArchTest 含む) が緑
- `/health`, `/scripts/check`, `/scripts/run` の HTTP 動作回帰なし
- 既存の MCP ツール (`slasher_check_script` 等) が動作
- run artifact (`run.json`, `events.jsonl`, `report.html`) のスキーマ互換維持
- 重要サンプル (`scripts/numadora-samples/notepad-check.numa` 等) が check/run できる

## Migration Tooling

`slasher migrate` コマンド (将来) はドラフトと移行レポートを生成する位置付け。
完全自動変換は約束しない。詳細は `migration-from-slasher-v1.md` の Tooling 節。

`.slasher` 削除は既に完了 (Q-L3 ハードカット) しており、tooling の有無は移行の
ブロッカーではない。

## Risks

| リスク | 緩和策 |
|---|---|
| AppOps PR-1 が大きすぎてレビュー困難 | 機械的な移動 + interface 分割が大半なので diff レビュー可能。`git mv` + sed で半自動化 |
| `.numai` シグネチャと C# 実装の不整合 | 起動時 `module_interface_mismatch` チェックで fail-fast |
| プラグイン間の隠れた依存 | NetArchTest の plugin 独立性ルールで CI 検出 |
| run artifact スキーマ互換破壊 | 既存テスト + ゴールデン ファイル比較 |
| ホスト例外の正規化漏れ | `numadora-language-spec.md` 9.6 のテーブルに対する unit test |
| サンプル `.numa` のリリース時期と利用者影響 | PR-B+C 完了まで旧表記サンプルが残る。docs に注記 |
| 単一 csproj 維持下での層境界違反 | NetArchTest が CI で検出 (`tests/Slasher.Tests/Architecture/`) |

## Historical: N0〜N7 Phase Plan

旧 v0.1 計画では N0〜N7 のフェーズ表記を使っていた:

- **N0**: ランタイム探索と契約凍結 (完了)
- **N1**: Slasher モジュール表面公開 (部分完了)
- **N2**: check エンドポイント接続 (完了)
- **N3**: `.numa` run と artifact 出力 (部分完了)
- **N4**: モジュール カバレッジ拡大 (進行中)
- **N5**: マクロ ergonomic 検討 (**v0.2 で却下**)
- **N6**: 履歴 `.slasher` 移植支援 (Q-L3 ハードカットで不要化)
- **N7**: public script を Numadora-only にする (完了)

v0.2 計画では N5 (マクロ) は採用しないことが確定し、N6 は不要化。
他のフェーズは上記 v0.2 PR plan に統合・再構成される。

過去の N0〜N7 詳細は `handoff-0.2.3.md` のスナップショットに残る。

## Open Questions

- 各プラグイン `.numai` の最終シグネチャ確定 (`slasher-numadora-integration.md` の能力テーブルと一致させる)
- `slasher/clipboard`, `slasher/files`, `slasher/data` の plugin 化判断 (Io 層所有か AppOps プラグインか)
- 既存サンプル `.numa` の v0.2 書き換え (Lang PR-B+C) の実施タイミング
- AppOps PR-1 の git mv スクリプトの自動化レベル
