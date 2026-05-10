# Numadora Reference Model

Slasher の Numadora バインディングが使うリソース参照モデル。

> **Note**: v0.2 では本ドキュメントの内容は `numadora-language-spec.md` 第 9 章
> (ホスト バインディング) と `slasher-script.md` (OPAQUE TYPE 節) に統合された。
> 本ドキュメントは要約と歴史的背景を残す位置付け。

## Position

Slasher の Numadora スクリプトはデスクトップ リソースを **明示的な参照値**
としてモデル化する。runner-global の selected state には依存しない。

v0.2 では参照値は `OPAQUE TYPE` (`AppRef`, `WindowRef`, `ElementRef` 等) として
ホスト プラグインの `.numai` で宣言される。

## Object Shape (v0.2)

```numadora
MODULE excel-showcase

IMPORT slasher/app AS app
IMPORT slasher/io AS io

EXPORT FUNC main()
  LET excel = app.start-app(r"artifacts\demo\workbook-app.xlsx")
  LET workbook = excel.wait-for-window("workbook-app", 20000)
                  OR FAIL "workbook window timeout"

  workbook.maximize()
  workbook.capture(1440, 810)
  io.wait(5000)

  excel.close()
END
```

- `excel` は `AppRef` (不透明型)
- `workbook` は `WindowRef` (不透明型)

## Binding Rules

- `AppRef` は起動済プロセスとその初期ウィンドウ メタデータを表す
- `WindowRef` は app または window クエリから解決された top-level ウィンドウを表す
- メソッド呼び出しは UFCS で書く:
  - `app-ref.wait-for-window(title, timeout-ms)` ≡ `app.wait-for-window(app-ref, ...)`
  - `app-ref.close()`
  - `window-ref.focus()`
  - `window-ref.maximize()`
  - `window-ref.capture(max-w, max-h)`
  - `window-ref.close()`
- ホスト関数の戻り値経由でのみ参照値を構築できる (利用側で `AppRef` リテラルは作れない)
- `OPAQUE TYPE` の内部表現は隠蔽 (`type_opaque_field_access` で禁止)
- 等価性 `==` はホスト定義 (典型的にはアイデンティティ等価)
- リソース解放は明示 close 関数を推奨、明示忘れは GC finalizer が host 側で release

## Observation Records (RECORD ペア)

参照値とは別に、観測値 (タイトル・矩形・状態) は通常の `RECORD` で返す:

```numadora
EXPORT OPAQUE TYPE WindowRef

EXPORT RECORD WindowInfo {
  title: string,
  handle: int,
  state: "normal" | "minimized" | "maximized",
}

EXPORT EFFECT FUNC info(target: WindowRef): WindowInfo
```

利用側:

```numadora
LET win = window.find("Notepad") OR FAIL "no notepad"
LET wi = win.info()
io.print(wi.title)              # OK (WindowInfo は RECORD)
LET handle = win.handle         # type_opaque_field_access (WindowRef のフィールドは見えない)
```

## Historical: Bridge Token

v0.1 (旧設計) では `app:last`, `window:last`, `app:<processId>`,
`window:<handle>` のような **文字列ハンドル** をテキスト RPC ブリッジで
やり取りしていた。v0.2 では完全に廃止し、型付き不透明値に置き換える。

旧スタブ モジュール (`scripts/numadora-samples/slasher_*.numa`) はテキスト
ブリッジで動作していた。これらは `numadora-language-redesign.md` 9 章 PR-B+C で
v0.2 構文 + プラグイン ホスト経由に書き直す。
