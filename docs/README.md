# Slasher Docs

Slasher 実装と Numadora スクリプト言語、AI 向け自動化契約のドキュメント インデックス。

---

## 現状一目 (2026-05-10)

| 項目 | 値 |
|---|---|
| **Slasher バージョン** | **0.3.0** |
| **Numadora 言語仕様** | **v0.2.1** (`numadora-language-spec.md`、ハードカット採用) |
| **アーキテクチャ** | 5 層 (Api / Core / Io / Network / AppOps) + AppOps プラグイン (`slasher-layer-architecture.md`, `slasher-plugin-architecture.md`) |
| **実装進捗** | **PR-1 Stage 1 完了** (プラグイン契約 + WindowsNative/Browser shell + PluginRegistry) |
| **直近の次タスク** | `/plugins` HTTP エンドポイント追加 → PR-1 Stage 3 (フォルダ + namespace 一括 rename) |

**今やっていること**: v0.2.1 spec への整合と 5 層 + プラグイン アーキテクチャの実装着手。
設計は全 19 ドキュメントで整合済 (下記 ✅ マーク)。実装は AppOps PR-1 Stage 1 まで進行。

**次のマイルストーン**: PR-1 全体 (フォルダ移動 + namespace 一括書き換え + 既存 `WindowsAutomationService`
等の interface 分割) を完了させ、Lang PR-D (`.numai` ホスト登録機構) へ。

---

## どこから読むか

**初めての人 (利用者・AI エージェント)**
1. `ai-agent-guide.md` — AI からの利用ガイド
2. `slasher-script.md` — Slasher Numadora スクリプトの書き方
3. `migration-from-slasher-v1.md` — 旧 `.slasher` を持っている場合

**コアの設計を理解したい**
1. `slasher-layer-architecture.md` — 5 層構成
2. `slasher-plugin-architecture.md` — AppOps プラグイン契約
3. `numadora-language-spec.md` — Numadora v0.2.1 言語仕様
4. `numadora-security-network-design.md` — 能力クラス + REQUIRES + ピア言語統合

**実装に関わる**
1. `numadora-migration-plan.md` — 実装フェーズ計画 (AppOps PR-1〜8 + Lang PR-A〜J)
2. `slasher-plugin-architecture.md` 9 章 — PR 分割表
3. `peer-implementation-plan.md` — ピア機能の Peer P0〜P9 計画

---

## ドキュメント一覧 (カテゴリ別)

### A. 利用ガイド (How-To)

| 文書 | 状態 | 内容 |
|---|---|---|
| `ai-agent-guide.md` | ✅ 利用 | AI エージェント向け Slasher 利用ガイド |
| `slasher-script.md` | ✅ v0.2.1 | Numadora スクリプト プロファイル (利用者向け) |
| `migration-from-slasher-v1.md` | ✅ v0.2.1 | 旧 `.slasher` → `.numa` 手動移行ガイド |

### B. アーキテクチャ (Architecture)

| 文書 | 状態 | 内容 |
|---|---|---|
| `slasher-layer-architecture.md` | ✅ 確定 | 5 層構成 (Api / Core / Io / Network / AppOps) と依存方向 |
| `slasher-plugin-architecture.md` | ✅ 確定 | AppOps プラグイン契約 (IAppOpsPlugin / PluginRegistry) |
| `architecture.md` | △ 旧 | サーバ構成と所有境界 (一部 v0.1 表記、層分割導入で順次置換) |
| `language-system.md` | ✅ v0.2.1 | 言語方針エントリ (canonical 読み順を提示) |

### C. Numadora 言語仕様 (Language Spec)

| 文書 | 状態 | 内容 |
|---|---|---|
| **`numadora-language-spec.md`** | ✅ **canonical v0.2.1** | 正式言語仕様。これがソース・オブ・トゥルース |
| `numadora-language-redesign.md` | ✅ 確定 | v0.1 → v0.2 の再構成方針と spec 改訂リスト |
| `numadora-base-structure.md` | ✅ 確定 | 字句構造 + 意味構造の詳細設計ノート |
| `numadora-core-systems.md` | ✅ 確定 | 型 / モジュール / 実行モデルの詳細設計ノート |
| `numadora-security-network-design.md` | ✅ 確定 | 能力クラス・`REQUIRES`・`slasher/peer` 言語統合 |

### D. Slasher × Numadora 統合 (Integration)

| 文書 | 状態 | 内容 |
|---|---|---|
| `slasher-numadora-integration.md` | ✅ v0.2.1 | Slasher 統合の実装契約 + 全ホスト関数の能力テーブル |
| `numadora-runtime-contract.md` | ✅ v0.2.1 | check/run の HTTP 境界 |
| `numadora-reference-model.md` | ✅ v0.2.1 | OPAQUE TYPE リソース参照モデル |

### E. セキュリティ + ネットワーク (Security & Network)

| 文書 | 状態 | 内容 |
|---|---|---|
| `security-policy.md` | ✅ v0.2.1 | 脅威モデル、能力クラス 15 種、プロファイル、リダクション |
| `numadora-lineage-policy-plan.md` | ✅ v0.2.1 | lineage-aware ポリシー入力 |
| `peer-network-model.md` | ✅ v0.2.1 | ピア namespace + portable-core 設計 |
| `peer-implementation-plan.md` | ✅ v0.2.1 | Peer P0〜P9 実装フェーズ |

### F. 観測性 + 証跡 (Observability)

| 文書 | 状態 | 内容 |
|---|---|---|
| `ai-automation-contract.md` | △ | action / result / report スキーマ |
| `ai-test-observability.md` | △ | 証跡、スクリーンショット、ログ、失敗報告 |

### G. 実装計画 (Implementation Plans)

| 文書 | 状態 | 内容 |
|---|---|---|
| `implementation-roadmap.md` | ✅ 0.3.0 | 現状、完了領域、次の作業 (本ノートのサマリ元) |
| `development-schedule.md` | △ | 全体開発スケジュール (RPA + ピア横断) |
| `numadora-migration-plan.md` | ✅ v0.2.1 | Numadora 言語実装フェーズ計画 |
| `phase-12-rpa-expansion-plan.md` | △ | 次の RPA パッケージ拡張 |

### H. 参考資料 (Reference)

| 文書 | 状態 | 内容 |
|---|---|---|
| `information_lineage_paper.md` | 参考 | 情報フロー lineage 理論ペーパ |

### I. 履歴 (Historical)

| 文書 | 状態 | 内容 |
|---|---|---|
| `handoff-0.2.3.md` | 履歴 | 0.2.3 時点 handoff スナップショット (現状は本ディレクトリ内設計ノート参照) |

---

## 状態マークの意味

- ✅ **確定** / **canonical** — v0.2.1 (Slasher 0.3.0) と整合。実装作業はこれを基準に行う
- △ **旧** — 内容は概ね有効だが v0.2.1 用語/層構成への部分更新がまだ
- 履歴 — 過去の特定時点の記録。現状判断には使わない

## 進行中の作業

PR-1 Stage 1 (プラグイン契約 + Plugin Registry) は実装済み。

```text
src/Slasher/Core/AppOps/
  IAppOpsPlugin.cs            ← プラグイン契約
  IPluginRegistration.cs      ← 登録 API
  PluginRegistry.cs           ← discovery / 登録ライフサイクル
  PluginRequirements.cs
  PluginAvailability.cs
  PluginStatus.cs

src/Slasher/AppOps/Plugins/
  WindowsNative/WindowsNativePlugin.cs  ← shell (PR-E で実装移行)
  Browser/BrowserPlugin.cs              ← shell (PR-E で実装移行)
```

起動時ログで `Plugin WindowsNative v1.0.0 registered (6 modules)` 等が確認可。
build / test 89/89 通過。

## 次のステップ候補

| 順位 | 内容 | 工数 | リスク |
|---|---|---|---|
| 1 | `/plugins` エンドポイント追加 + 簡易テスト | 小 | 低 |
| 2 | PR-1 Stage 3: フォルダ移動 + namespace 一括 rename | 大 | 高 (専用セッション推奨) |
| 3 | Lang PR-D: `.numai` ホスト登録機構の C# 実装 | 中 | 中 |

詳細は `numadora-migration-plan.md` 参照。
