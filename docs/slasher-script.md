# Slasher Numadora Script Profile

Numadora v0.2 (`numadora-language-spec.md`) を使う Slasher スクリプトのプロファイル。

ガイディング ルール: **Slasher はアプリケーション、Numadora は言語**。
このプロファイルは Slasher 専用方言になってはならない。Slasher は AppOps プラグイン
(`slasher-plugin-architecture.md`) を通じてホスト機能を提供するが、Numadora 言語の
表面に Slasher 固有の構文を持ち込まない。

関連:

- `numadora-language-spec.md` — 言語仕様 v0.2
- `slasher-plugin-architecture.md` — AppOps プラグイン アーキテクチャ
- `slasher-numadora-integration.md` — Slasher 統合の実装契約
- `migration-from-slasher-v1.md` — `.slasher` v1 からの移行

## v0.2 構文の要点

- スラッシュ区切りモジュール パス: `IMPORT slasher/window AS window`
- alias 必須: `IMPORT std/array` はエラー
- lowercase 型名: `int`, `string`, `bool`, `array[T]`, `Option[T]`
- `LET name = expr` (`:=` ではない)
- `FUNC name(arg: type): type ... END`
- kebab-case 識別子: `wait-for-title`, `start-app`
- `EXPORT EFFECT FUNC` で副作用ありホスト関数を宣言
- `EXPORT INTERACTIVE EFFECT FUNC` でユーザ承認が必要なアクションを宣言
- `OPAQUE TYPE WindowRef`, `OPAQUE TYPE AppRef` 等のホスト リソース参照
- UFCS: `win.focus()` ≡ `focus(win)`
- raw 文字列: `r"C:\Users\foo"`
- トレーリング ブロック: `retry(3, 500) DO ... END`

## 最小スクリプト

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

スモーク サンプルは `scripts/numadora-samples/notepad-check.numa` にある
(v0.2 構文への書き換えは `numadora-language-redesign.md` 9 章 PR-B+C で実施)。

## モジュール一覧

Slasher の AppOps プラグインが提供するホスト モジュール:

| Slasher 領域 | モジュール | 提供プラグイン |
|---|---|---|
| app/process | `slasher/app` | WindowsNative (将来 MacOSNative, LinuxNative) |
| window | `slasher/window` | (同上) |
| input | `slasher/input` | (同上) |
| screen capture | `slasher/screen` | (同上) |
| UI element | `slasher/element` | (同上) |
| dialog | `slasher/dialog` | (同上) |
| browser | `slasher/browser` | Browser (cross-platform Selenium) |
| clipboard | `slasher/clipboard` | (Io 層からホスト関数として公開、TBD) |
| files | `slasher/files` | (Io 層) |
| data (CSV/JSON/Excel) | `slasher/data` | (Io 層) |
| logging/steps/wait | `slasher/io` | (Slasher built-in) |
| assertions | `slasher/test` | (Slasher built-in) |

モジュール可用性は `GET /plugins` で報告される。プラグインが `not_applicable` /
`missing_prerequisites` / `disabled` の場合、その提供モジュールは Numadora の
`IMPORT` 解決でも見えない (`module_not_found` として失敗)。

## 関数命名と呼び出し例

すべてのホスト関数は `.numai` 内で kebab-case で宣言される。スクリプトからは
UFCS (メソッド呼び出し糖衣) で呼ぶのが慣用:

```numadora
LET ref = app.start-app("notepad.exe")
LET win = ref.wait-for-window("Notepad", 10000) OR FAIL "no notepad"
ref.close()
win.focus()
win.maximize()
win.capture(1280, 720)
win.close()

input.text("hello")
input.keys("CTRL+S")
input.mouse("move", 400, 300, "left")
input.wheel(400, 300, 120)
input.drag(400, 300, 500, 350, "left", 400, 24)
input.context-menu(400, 300, 250)

screen.capture-full(1280, 720)
screen.capture-monitor(0, 1280, 720)

LET ok = element.find("foreground", "OK", "-", -1, "contains", 8, 20)
LET exists = element.exists("foreground", "OK", "-", -1, "contains", 8, 1)
LET text = element.read-text("foreground", "Status", "-", -1, "contains", 8, 1)
LET tree = element.tree("foreground", 2, 50)

LET cur = browser.current("-")
LET title = browser.title("-")
LET url = browser.url("-")
LET el = browser.locate("css", "body", 5000, "-")
LET txt = browser.dom-text("css", "body", 5000, "-")
LET attr = browser.attribute("css", "body", "class", 5000, "-")
LET shot = browser.screenshot("-")
LET links = browser.links("-")
LET wins = browser.windows("-")

test.assert-foreground-title("contains", "Notepad")
```

## INTERACTIVE 関数

以下の関数は `.numai` で `EXPORT INTERACTIVE EFFECT FUNC` として宣言され、
run 時に明示的な `allowInteractiveInput` 承認を要求する:

- `slasher/input.text`, `slasher/input.keys`, `slasher/input.mouse`,
  `slasher/input.wheel`, `slasher/input.drag`, `slasher/input.context-menu`
- `slasher/window.focus`, `slasher/window.set-state`, `slasher/window.maximize`,
  `slasher/window.minimize`, `slasher/window.restore`, `slasher/window.close`
- `slasher/dialog.message`, `slasher/dialog.confirm`
- `slasher/app.start-app`, `slasher/app.close`

未承認で呼ぶと `RuntimeError` (`code = "policy_denied"`) を返す。承認済みでも
Slasher は入力送信直前にフォアグラウンド ターゲットを再検証し、ターゲットが
変わっていれば fail closed する。

Web UI ではこの承認は `Interactive` チェックボックスとして表示される。

## OPAQUE TYPE リソース参照

ホスト関数は `OPAQUE TYPE` 値 (`AppRef`, `WindowRef`, `ElementRef` 等) を返す。
スクリプトから内部表現を直接触ることはできない:

```numadora
LET ref: AppRef = app.start-app("notepad.exe")  # OPAQUE
LET handle = ref.handle                         # type_opaque_field_access
```

観測値が必要なときは対応する `info` 関数を使う:

```numadora
LET info: AppInfo = ref.info()
io.print(info.process-id)                       # OK (AppInfo は RECORD)
```

リソース解放は `ref.close()` を明示的に呼ぶことを推奨。明示 close を忘れても
GC finalizer がホスト リソースを解放する (二重 close は host 側で idempotent)。

## エラーと証跡

Slasher は既存の自動化証跡モデルを保持する:

- run メタデータ (`run.json`)
- イベント ログ (`events.jsonl`: `numadora.hostCall`, `numadora.log`, `step`, ...)
- ソース ファイルと行情報
- 構造化 `RuntimeError`
- スクリーンショットと添付ファイル
- HTML レポートと artifact 読み戻し

Numadora の `RuntimeError.code` は `numadora-language-spec.md` 9.6 のホスト例外
正規化テーブルを介して Slasher run timeline に記録される。

## v1 からの移行

Slasher v1 (`.slasher`) は `/scripts/check`, `/scripts/run` で受け付けない
(Q-L3 ハードカット)。旧スクリプトは手動で `.numa` v0.2 構文に書き直す。
具体例は `migration-from-slasher-v1.md` を参照。

## サンプルスクリプト

```numadora
MODULE excel-showcase

IMPORT slasher/app AS app
IMPORT slasher/window AS window
IMPORT slasher/screen AS screen
IMPORT slasher/io AS io

EXPORT FUNC main()
  io.step("open workbook")
  LET excel = app.start-app(r"artifacts\demo\workbook-app.xlsx")
  LET workbook = excel.wait-for-window("workbook-app", 20000)
                  OR FAIL "workbook window timeout"
                  CODE "workbook_not_found"

  io.step("maximize and capture")
  workbook.maximize()
  workbook.capture(1440, 810)
  io.wait(5000)

  io.step("close")
  excel.close()
END
```

## 残課題

- v0.2 構文への移行: `scripts/numadora-samples/*.numa` の書き換え (PR-B+C)
- 各プラグインの `.numai` 最終シグネチャの確定 (本ドキュメントの「関数命名」と整合)
- `slasher/clipboard`, `slasher/files`, `slasher/data` の plugin 化判断
- INTERACTIVE 関数の完全な一覧化 (`.numai` から自動抽出する仕組み)
