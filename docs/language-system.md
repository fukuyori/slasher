# Slasher Language System

Slasher の言語方針のエントリ ドキュメント。

アプリケーションは **Slasher**。Slasher のスクリプト面は **Numadora v0.2** を使う。

製品方針:

- Slasher はアプリケーション名であり、ユーザ向け自動化プロダクト
- Slasher スクリプトは Numadora を統一汎用言語として使う
- Numadora は型付きライブラリとホスト能力で外部アプリケーション (OS native ウィンドウ、
  ブラウザ、Excel/GIMP 等のアプリ) を制御できる
- Slasher は AppOps プラグインを通じて自動化 / 証跡 / Web / MCP / HTTP / artifact
  機能を Numadora ホスト モジュールとして公開する
- v1 `.slasher` ランナーは公開スクリプト面から撤去済

旧スタンドアロン Slasher Script コンパイラの計画は active docs から撤去された。
新しい言語作業は本ドキュメントと下記 v0.2 spec から始める。

## Canonical Documents

以下の順で読む:

1. `slasher-script.md`
   - Slasher の Numadora v0.2 スクリプト プロファイル (利用者向け)
   - Slasher 専用方言になってはならない

2. `numadora-language-spec.md`
   - 汎用 Numadora 言語仕様 v0.2 (canonical reference)
   - 構文、型システム、モジュール、エラー、標準ライブラリ、ホスト バインディング規則

3. `slasher-numadora-integration.md`
   - C# Slasher アプリケーション/サーバと Numadora の統合契約
   - ホスト バインディング戦略、HTTP/MCP 統合、Windows 自動化モジュール、イベント、診断

4. `slasher-plugin-architecture.md`
   - AppOps プラグイン アーキテクチャ
   - WindowsNative / Browser / 将来の Excel・GIMP 等のプラグイン契約

5. `numadora-migration-plan.md`
   - `.numa` v0.2 サポート追加の実装フェーズ計画
   - PR 分割、移行ツール、互換性ゲート

6. `migration-from-slasher-v1.md`
   - 旧 `.slasher` ファイルから v0.2 への手動移行ガイド (履歴の参照)

## v0.2 設計の背景

v0.1 → v0.2 の改訂理由と詳細は以下:

- `numadora-language-redesign.md` - 再構成方針 (アンカー、ホスト モデル、マクロなし、トレーリング ブロック)
- `numadora-base-structure.md` - 字句 + 意味の詳細
- `numadora-core-systems.md` - 型 / モジュール / 実行モデルの詳細

## Design Decisions

### File Extensions

| Extension | Meaning |
|---|---|
| `.numa` | アクティブな Numadora スクリプト |
| `.numai` | ホスト バインディング インターフェイス宣言 (本体なし、シグネチャのみ) |

`.slasher` ファイルは Slasher script check/run API で受け付けない (Q-L3 ハードカット)。

### Language Ownership

- Numadora が言語コア (構文、型、モジュール、ランタイム モデル) を所有
- 外部アプリケーション制御は Numadora ライブラリとホスト能力 (`.numai` + プラグイン)
  としてモデル化
- Slasher はアプリケーション挙動、自動化実装、証跡モデル、API 表面、ユーザ体験を所有
- Slasher は Numadora 構文を Slasher の都合で fork してはならない
- Slasher 固有のコマンド形式は言語コアに含めない

### Script Style

`.numa` スクリプトは v0.2 構文を使う:

```numadora
MODULE notepad-smoke

IMPORT slasher/app AS app
IMPORT slasher/input AS input
IMPORT slasher/io AS io

EXPORT FUNC main()
  io.step("open notepad")
  LET ref = app.start-app("notepad.exe")
  LET win = ref.wait-for-window("Notepad", 10000) OR FAIL "no notepad"
  win.focus()
  input.text("hello from Slasher")
  ref.close()
END
```

要点:

- スラッシュ区切りモジュール パス (`slasher/app`)
- alias 必須 IMPORT
- lowercase 型 (`int`, `string`, `array[T]`)
- `LET name = expr` (`:=` ではない)
- kebab-case 関数名
- UFCS メソッド呼び出し糖衣 (`win.focus()` ≡ `focus(win)`)
- `OR FAIL` で `Option[T]` の unwrap
- `OPAQUE TYPE` リソース参照 (`AppRef`, `WindowRef`)

詳細は `slasher-script.md` および `numadora-language-spec.md` を参照。

### Compatibility Policy

- 新しい言語作業は Numadora `.numa` v0.2 を対象とする
- 既存 `.slasher` スクリプトは Slasher script check/run API で **サポート対象外**
- 互換シュガーは言語設計の判断基準にしない
- 共有挙動は Numadora モジュールとホスト API に集約

## Implementation Track

詳細実装計画は `numadora-migration-plan.md`。

高レベル サマリ:

1. v0.2 spec 反映 (完了): `numadora-language-spec.md` v0.2
2. Slasher を 5 層構成に再編 (`slasher-layer-architecture.md` PR-1〜)
3. AppOps プラグイン契約導入と既存 Windows コードのプラグイン化 (`slasher-plugin-architecture.md` 9 章)
4. `.numai` ホスト バインディング機構の C# 側実装 (Lang PR-D)
5. 既存ホスト関数を `.numai` + プラグイン C# クラスに移行 (Lang PR-E)
6. `Option[T]` / `MATCH` / `OR FAIL` / `RuntimeError` 実装 (Lang PR-F)
7. リソース参照を `OPAQUE TYPE` に切替 (Lang PR-G)
8. トレーリング ブロック構文 + UFCS 実装 (Lang PR-H)
9. サンプル `.numa` の v0.2 書き換え (Lang PR-B+C)

Phase 12 RPA パッケージ作業は継続。新パッケージは Numadora モジュールとして公開できる
形 (`slasher/csv`, `slasher/excel` 等のプラグインまたはホスト モジュール) を選ぶ。

## Open Questions

- 各プラグイン `.numai` の最終シグネチャ確定 (本ノートのスタイル例と整合させる)
- `slasher/clipboard`, `slasher/files`, `slasher/data` の plugin 化判断 (Io 層所有か AppOps プラグインか)
- v0.2 サンプル `.numa` のリリース時期と、それまでの旧サンプルの扱い
