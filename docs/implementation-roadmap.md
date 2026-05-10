# Slasher Implementation Roadmap

> **0.3.0 (2026-05-10) 現在**: Numadora v0.2.1 spec への整合と 5 層 + プラグイン
> アーキテクチャの実装着手フェーズ。設計は全 19 ドキュメントで整合済、AppOps PR-1
> Stage 1 (プラグイン契約 + Plugin Registry) まで実装。
>
> 詳細リンク:
> - 言語: `language-system.md` → `numadora-language-spec.md` (v0.2.1)
> - 5 層構成: `slasher-layer-architecture.md`
> - プラグイン: `slasher-plugin-architecture.md`
> - 実装計画: `numadora-migration-plan.md`
> - セキュリティ: `security-policy.md`
> - ピア: `peer-network-model.md` / `peer-implementation-plan.md`
> - 全体スケジュール: `development-schedule.md`

## ゴール (再確認)

1. **AI エージェント (Codex 等) による Windows アプリの操作・テスト・デバッグ** を支える
2. RPA スタイルのローカル自動化を整備
3. 長期的には portable core + 信頼ピア間 namespace を実現

## 1 行サマリ (直近の状態)

| トラック | 状態 |
|---|---|
| Slasher コア機能 (RPA / 評価) | Phase 11 完了、Phase 12 進行中 (CSV/JSON/Excel 完了、scheduling/secrets 未) |
| Numadora 言語 | **v0.2.1 spec 確定** (ハードカット採用)、`.numai` 13 モジュール宣言済 |
| 5 層アーキテクチャ | 設計確定 (Api/Core/Io/Network/AppOps)、AppOps PR-1 Stage 1 完了 |
| ピア機能 | Peer P0〜P5 (read-only namespace まで) 完了、P6 (delegated run) 未 |

## 進行中: AppOps PR-1 (5 層 + プラグイン化)

詳細: `slasher-plugin-architecture.md` 9 章 PR-1〜8 + `numadora-migration-plan.md`。

| Stage | 内容 | 状態 |
|---|---|---|
| **Stage 1** | プラグイン契約 (`IAppOpsPlugin`, `PluginRegistry`) + WindowsNative/Browser shell + Program.cs 起動時登録 | ✅ **完了** |
| Stage 2 | `Core/AppOps/Abstractions/` interface 定義 (`IAppLauncher`, `IWindowControl`, ...) | 未 |
| Stage 3 | フォルダ移動 + namespace 一括 rename (`Slasher.Automation` → `Slasher.Core.Numadora` 等、~80 ファイル) | 未 (専用セッション推奨) |

その後 `Lang PR-D` (`.numai` ホスト登録機構の C# 実装) → `Lang PR-E` (既存ホスト関数移行) → `Lang PR-F〜H` (Option/MATCH/UFCS/トレーリング ブロック実装) → `Lang PR-B+C` (サンプル + パーサ更新)。

## 設計フェーズで確定済 (v0.2.1 整合)

ハードカット方針 (互換シムなし) で v0.2 → v0.2.1 を直接更新済:

- 言語仕様 v0.2.1 (`numadora-language-spec.md`):
  - 字句構造 (raw 文字列、行頭演算子、kebab-case + `-` 解決)
  - 型システム (`OPAQUE TYPE`, ジェネリクス, Option/MATCH, UFCS)
  - トレーリング ブロック (`DO |x| ... END`)
  - `EFFECT(class)` 必須化、`INTERACTIVE EFFECT(class)` 必須
  - `script-requires` (REQUIRES) 宣言
  - `slasher/peer` モジュール、再帰委譲禁止
  - 能力クラス 15 種を言語キーワード化 (コンテキスト認識)
- Slasher 構成:
  - 5 層 + AppOps プラグイン (NetArchTest で規律強制 16 テスト)
  - `appsettings.json` の `Plugins:<Name>` セクション
  - `slasher/peer.numai` 含む 13 ホスト バインディング `.numai`
  - `verify-numadora-n0.ps1` の v0.2.1 静的検査

## ガイディング原則

1. **AI 観測性が第一** — 各アクションは log / capture / target metadata / 構造化エラーを出す
2. **スクリプト アクションはライブラリ** — 構文を増やさず、Numadora モジュールに集約
3. **Web UI / MCP / HTTP / スクリプトはセマンティクス共有** — どの surface も同じ動作
4. **破壊操作は audit 可能** — delete / overwrite / close-all / unattended は明示的に
5. **能力拡張に security policy が伴う** — capability class / audit field / redaction を先決め
6. **ピア機能はローカル意味論を保つ** — peer-executed action も同じ run/event/evidence/policy/error 形

## トラック別状態

### A. Slasher コア (Phase 11/12)

完了: 構造化 run artifact, event log, HTML report, native element/UI Automation, image match,
Selenium browser automation, CSV/JSON/Excel データ API, destructive action approval (`dryRun`/`allowDestructive`),
file/folder watcher。

進行中: scheduling, credentials/secrets, report export (Phase 12 残)。

### B. 言語 (Numadora)

設計: ✅ v0.2.1 spec 確定。
実装: パーサは v0.1 のまま (Lang PR-B+C 待ち)。`.numai` 13 モジュール宣言済。

### C. アーキテクチャ (5 層 + プラグイン)

設計: ✅ 確定 (Q-L1〜L6, Q-P1〜P6 すべて採用済)。
実装: AppOps PR-1 Stage 1 完了 (プラグイン契約)。Stage 2/3 未。

### D. セキュリティ + lineage

設計: ✅ v0.2.1 整合 (能力クラス 15 種、INTERACTIVE 承認、再帰委譲禁止)。
実装: `NumadoraPolicyEvaluator` で観測 + 入力 + 一部の操作系がポリシー判定済。

### E. ピア機能

設計: ✅ v0.2.1 整合 (`slasher/peer` モジュール、`PeerRef`, `TrustProfile`, namespace, delegate-run)。
実装: Peer P0〜P5 (read-only namespace まで) 完了、P6 (delegated run) 未。

## トラッキング チェックリスト (v0.3.0 時点)

### Slasher コア

- [x] Phase A: 自動化契約
- [x] Phase 0: サーバ レイアウト
- [x] Phase 9: Web/MCP スクリプト ラン
- [x] Phase 10: 観測性
- [x] Phase 11: UI/image/browser 自動化
- [x] Phase 12 ローカル基盤: CSV/JSON/Excel API
- [x] Phase 12: 破壊操作の dryRun/allowDestructive
- [x] Phase 12: file/folder watcher
- [ ] Phase 12: scheduling
- [ ] Phase 12: credentials/secrets
- [ ] Phase 12: report export

### 設計 (v0.2.1)

- [x] Numadora 言語 v0.2.1 spec
- [x] 5 層アーキテクチャ
- [x] プラグイン契約 + 設定スキーマ
- [x] セキュリティ・ネットワーク言語統合 (能力クラス + REQUIRES + slasher/peer)
- [x] ホスト バインディング `.numai` 13 モジュール宣言
- [x] サンプル `.numa` 6 種 v0.2.1 整合
- [x] `verify-numadora-n0.ps1` 静的検査
- [x] NetArchTest 雛形 16 テスト
- [x] `appsettings.json` の `Plugins:` 設定スケルトン
- [x] 全 19 ドキュメント v0.2.1 整合

### AppOps PR-1 (5 層 + プラグイン化)

- [x] Stage 1: プラグイン契約 + Plugin Registry + WindowsNative/Browser shell
- [ ] Stage 2: `Core/AppOps/Abstractions/` interface 定義
- [ ] Stage 3: フォルダ移動 + namespace 一括 rename
- [ ] `/plugins` HTTP エンドポイント

### 言語実装 (Numadora v0.2.1)

- [ ] Lang PR-B+C: パーサ更新 + サンプル v0.2.1 化
- [ ] Lang PR-D: `.numai` ホスト登録機構
- [ ] Lang PR-E: 既存ホスト関数を `.numai` + プラグイン C# クラスに移行
- [ ] Lang PR-F: `Option[T]` / `MATCH` / `OR FAIL` / `RuntimeError` 実装
- [ ] Lang PR-G: リソース参照を `OPAQUE TYPE` に切替
- [ ] Lang PR-H: トレーリング ブロック構文 + UFCS

### ピア機能

- [x] Peer P0: 契約 DTO
- [x] Peer P1: 識別子 + 手動レジストリ
- [x] Peer P2: read-only メタデータ エンドポイント
- [x] Peer P3: read-only namespace listing
- [x] Peer P4: read-only resource read
- [x] Peer P5: observe-only resource invoke (進行中、要確認)
- [ ] Peer P6: observe-only delegated run
- [ ] Peer P7: portable core 整合 (5 層構成)
- [ ] Peer P8: interactive resource invoke
- [ ] Peer P9: discovery + stronger transport

### Lineage / Policy

- [x] 初期 lineage メタデータ + policy input
- [x] policy evaluator allow/deny テスト
- [x] interactive 入力承認フラグ
- [x] target 再検証 (input/keys/mouse/wheel/drag/context-menu)
- [x] observe-only screen/element/browser bridges
- [ ] 能力クラス対応への拡張 (Sec PR-F)
- [ ] 再帰委譲ガード (Sec PR-F)
