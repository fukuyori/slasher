# Migration From Slasher v1

旧 `.slasher` スクリプトを Numadora `.numa` v0.2 スクリプトに移行するためのガイド。

Slasher v1 互換性は長期目標ではない。`.slasher` は Slasher 0.2.4 で公開
script check/run API から削除済 (Q-L3 ハードカット)。残っている `.slasher`
スクリプトは v0.2 構文で **手動で書き直す** 必要がある。

Numadora は汎用言語であり、本ガイドは旧スクリプトからの橋渡しに過ぎない。
Numadora の将来 API 設計を v1 互換性で縛ってはならない。

関連:

- `numadora-language-spec.md` - 言語仕様 v0.2
- `slasher-script.md` - 現行 Numadora スクリプト プロファイル
- `language-system.md` - 言語方針エントリ

## Migration Policy

- 新スクリプトは `.numa` v0.2
- 既存 `.slasher` スクリプトは **本当に必要なものだけ** 移植する
- 不要な `.slasher` は削除可
- Slasher 側は v1 互換パーサを持たない (Q-L3 ハードカット)
- 旧コマンド名は意図の例であって、保持必須の名前ではない

## Current Target Style (v0.2)

```numadora
MODULE notepad-smoke

IMPORT slasher/app AS app
IMPORT slasher/input AS input
IMPORT slasher/io AS io
IMPORT slasher/test AS test

EXPORT FUNC main()
  io.step("open notepad")
  LET ref = app.start-app("notepad.exe")

  LET win = ref.wait-for-window("Notepad", 10000) OR FAIL "no notepad"
  win.focus()

  io.step("type text")
  input.text("hello")
  test.assert-foreground-title("contains", "Notepad")
END
```

要点:

- スラッシュ区切りモジュール パス (`IMPORT slasher/app AS app`)
- lowercase 型 (`int`, `string` 等) と kebab-case 関数名
- `LET name = expr` (`:=` ではない)
- UFCS メソッド呼び出し (`ref.wait-for-window(...)`)
- `OR FAIL` で `Option[T]` の安全 unwrap
- 詳細は `slasher-script.md`

## Common Rewrites

| Slasher v1 | Numadora v0.2 |
|---|---|
| `start notepad.exe` | `LET ref = app.start-app("notepad.exe")` |
| `wait window "Notepad" 10000 as win` | `LET win = ref.wait-for-window("Notepad", 10000) OR FAIL "no window"` |
| `focus ${handle}` | `win.focus()` |
| `text "hello"` | `input.text("hello")` |
| `keys CTRL+S` | `input.keys("CTRL+S")` |
| `mouse move 400 300` | `input.mouse("move", 400, 300, "left")` |
| `mouse wheel 400 300 120` | `input.wheel(400, 300, 120)` |
| `mouse drag 400 300 500 350 left 400 24` | `input.drag(400, 300, 500, 350, "left", 400, 24)` |
| `mouse context-menu 400 300 250` | `input.context-menu(400, 300, 250)` |
| `wait 800` | `io.wait(800)` |
| `log "message"` | `io.log("message")` |
| `step "name"` | `io.step("name")` |
| `assert foreground title contains "Notepad"` | `test.assert-foreground-title("contains", "Notepad")` |
| `foreground as win` | `LET win = window.foreground()` (host が公開していれば) |
| `include lib/common.slasher` | `.numa` モジュールに書き直し、`IMPORT module AS alias` |
| `function name ... endfunction` | `EXPORT FUNC name(...) ... END` |
| `set name value` | `LET name = value` (不変) または `VAR name = value` (可変) |

マッピングは意図的に 1 対 1 ではない。Numadora が今 check できる shape を優先し、
汎用 Windows-control API として意味のある形を選ぶ。

## Porting Order

1. 重要なスモーク テストと AI 駆動シナリオを特定
2. 1 つの小さなスクリプトを v0.2 `.numa` に書き直す
3. ローカルで `/scripts/check` に対して実行して構文・型エラーを潰す
4. ホスト呼び出しが INTERACTIVE なら `allowInteractiveInput` 承認を有効化して run
5. 共有ヘルパは v0.2 モジュールに移す (call shape が安定してから)
6. 不要な `.slasher` ファイルは削除またはアーカイブ

## Unsupported v1 Features

以下は Numadora v0.2 に **存在しない**。書き直し時は別解で表現する:

| v1 機能 | v0.2 での代替 |
|---|---|
| 暗黙グローバル コマンド名前空間 | 必ず `IMPORT module AS alias` を経由 |
| 引用符なしコマンド引数 | 通常の関数呼び出し (`input.text("hello")`) |
| `include` セマンティクス | モジュール システム (`IMPORT`) |
| v1 動的変数スコープ | 字句スコープ + ブロック スコープ |
| v1 専用テスト コマンド表記 | `slasher/test` モジュール |
| 行指向マクロ構文 | マクロ廃止 (原則 4)、関数 + トレーリング ブロック (`DO ... END`) で代替 |

## Tooling

将来 `slasher migrate` コマンドの可能性はあるが、ドラフトと移行レポートを生成する
だけにとどめる。完全自動変換は約束しない。

最低限の有用挙動:

- 初期 `IMPORT` 文の追加
- 一般的な v1 コマンドを kebab-case 関数呼び出しに対応付け
- 未対応のソース行を明示的な TODO コメントとして保持
- 手動リスクのレポート生成

移行ツールはオプショナルで、`.slasher` 削除の前提条件ではない (既に削除済)。

## Verification

```powershell
# v0.2 サンプル (PR-B+C 完了後) のチェック
.\scripts\check-numadora.ps1 -Path scripts\numadora-samples\notepad-check.numa

# 全 N0 ベースライン (将来 v0.2 整合後)
.\scripts\verify-numadora-n0.ps1
```

サンプル `scripts/numadora-samples/*.numa` の v0.2 書き換えは
`numadora-language-redesign.md` 9 章 PR-B+C で実施予定。それまでは旧表記が残る。
