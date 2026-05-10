# Numadora 言語再構成 設計ノート

> Status: 設計ドキュメント。実装は別フェーズで行う。
> このノートは `numadora-language-spec.md` と現状実装 (`scripts/numadora-samples/*.numa`,
> `src/Slasher/Automation/ScriptRunService.Numadora*.cs`) の乖離を整理し、
> 言語としての統一性と拡張性を回復するための **確定方針と未決事項** をまとめる。

## 0. 動機

現状、Numadora は以下のように仕様書と実装が大きく乖離している。

- **基本型表記**: spec は `int / string / bool / array[T] / Option[T]`、実装は `Int / String / Bool / Array<T>`、`Option` 不在
- **LET 区切り**: spec は `=`、実装は `:=`
- **戻り値**: spec は `: T`、実装は `-> T`
- **EXPORT**: spec は各宣言修飾、実装はリスト
- **モジュールパス**: spec は `lib/string-utils`、実装は `slasher_desktop`
- **ホスト呼び出し**: spec は型付き `.numai` シグネチャ、実装は `Print("__SLASHER_HOST_CALL__ ...")` の **文字列 RPC**
- **リソース参照**: spec は構造的レコード、実装は `"window:last"` の **文字列ハンドル**
- **マクロ / Option / MATCH / 純粋性検査**: spec にあり、実装にはない

このまま機能を増やすほど、両者の距離は拡大する。
本ノートで **アンカー = spec、実装はそれに寄せる** を確定する。

## 1. 確定方針 (Anchor Decisions)

| 決定 | 内容 |
|---|---|
| **D1. アンカー** | `numadora-language-spec.md` を正とする。実装・サンプル・統合ドキュメントを spec に合わせる。 |
| **D2. ホスト呼び出しモデル** | `.numai` 型付きシグネチャ + C# 側のホスト登録機構 (Slasher 内コンテナ)。文字列 RPC は廃止。 |
| **D3. マクロ機構** | 採用しない。`DEF-SYNTAX` / `DEF-SYNTAX-CMD` は spec から削除する (= spec 改訂の対象)。 |
| **D4. 拡張性の確保** | 関数 (高階関数) + ブロック付き関数呼び出し (trailing block) で DSL 風表現を実現する。詳細は 5 章。 |

## 2. 表記/構文の正規化

D1 に従って、以下の 6 軸を spec 形に固定する。実装・サンプル・integration ドキュメント全てが対象。

| 軸 | 採用形 | 不採用 |
|---|---|---|
| A. 基本型 | `int`, `string`, `float`, `bool`, `unit`, `array[T]`, `Option[T]` | `Int`, `String`, `Bool`, `Array<T>` |
| B. LET 区切り | `LET x = expr` / `VAR x = expr` | `LET x := expr` |
| C. 関数戻り値 | `FUNC f(x: int): int` | `FUNC f(x: Int) -> Int` |
| D. 識別子命名 | kebab-case (関数・変数・モジュール内識別子) | PascalCase, snake_case |
| E. モジュールパス | `slasher/desktop`, `std/array` (slash 区切り) | `slasher_desktop` (snake) |
| F. EXPORT 文法 | 各宣言に `EXPORT FUNC ... END` / `EXPORT RECORD ...` | `EXPORT a, b, c` のリスト |

### 2.1 ホスト関数の命名

ホスト関数も kebab-case を採用する (spec 命名規約に統一)。

```numadora
IMPORT slasher/desktop AS desktop

LET app = desktop.start-app("notepad.exe")
LET win = app.wait-for-window("Notepad", 10000)
win.focus()
```

PascalCase (`StartApp`, `WaitForWindow`) は廃止。

### 2.2 MODULE / EXPORT の正規形

```numadora
MODULE slasher/window

EXPORT FUNC wait-for-title(title: string, timeout-ms: int): Option[WindowRef]
  ...
END

EXPORT FUNC focus(target: WindowRef): unit
  ...
END
```

`MODULE name ... END` のブロック形式は廃止。`MODULE` はファイル先頭のヘッダ単独宣言。

### 2.3 LET / VAR / 代入

```numadora
LET x = 1            # 不変
VAR y = 2            # 可変
y = 3                # 再代入は `=` 単独 (spec 通り)
```

`:=` は採用しない。

## 3. 型システムの実装スコープ

spec の型システムから、**最初の実装フェーズで必須** とするものを切り出す。

| 機能 | 実装スコープ | 備考 |
|---|---|---|
| 基本型 (`int`, `string`, `float`, `bool`, `unit`) | 必須 | |
| `Option[T]` + `Some` / `None` | 必須 | エラーモデルの根幹 |
| `array[T]` | 必須 | リテラル `[a, b, c]`、インデックス `a[i]`、`std/array` |
| `record` 型 (名前付き `RECORD`) | 必須 | ホストが返す観測値の表現に必要 |
| 構造的サブタイピング | 必須 | ホスト戻り値とユーザレコードの結合に必要 |
| 不透明 nominal 型 (`opaque`) | 新規追加 (spec 拡張) | リソース参照用。3.4 参照 |
| `string-literal union` | 推奨 | window state 等の列挙表現 |
| `TYPE` 別名 | 推奨 | |
| `CONST` | 推奨 | |
| 純粋性自動判定 | 後フェーズ | まずは「不純関数を式から呼ぶことを構文上禁止」程度から |
| ジェネリクス (型パラメータ) | 後フェーズ | 標準ライブラリのシグネチャでのみ使用 |
| フロー依存型 / `?.` / `??` | 不採用 (spec 通り) | |

### 3.1 Option[T] の取り扱い

spec の `OR FAIL` / `OR DEFAULT` / `IS SOME` / `MATCH` を全面採用する。

```numadora
LET win = window.wait-for-title("Notepad", 5000)
        OR FAIL "Notepad が見つからない"
        CODE "window_not_found"
        DETAILS { title: "Notepad" }

# あるいは
LET maybe = window.find("Notepad")
IF maybe IS SOME THEN
  ...
ELSE
  ...
END
```

`.value` で None を unwrap する操作は **静的検査で警告**、実行時は panic。

### 3.2 MATCH と網羅性検査

spec 第 4 章の「3 段階強度」をそのまま採用 (`Option[T]` / `bool` / string-literal union はエラー、その他は警告)。

### 3.3 RuntimeError と TRY / CATCH

spec 第 3 章の `RuntimeError` レコード、`TRY ... CATCH e: RuntimeError ... FINALLY ... END` をそのまま採用。

### 3.4 リソース参照型 (新規)

現状実装は `"window:last"` のような文字列ハンドルでウィンドウ/アプリ/ブラウザを表現している。これを **不透明 nominal 型** に置き換える:

```numadora
# slasher/window.numai
EXPORT OPAQUE TYPE WindowRef
EXPORT OPAQUE TYPE AppRef

EXPORT FUNC focus(target: WindowRef): unit
EXPORT FUNC wait-for-title(title: string, timeout-ms: int): Option[WindowRef]
```

- `WindowRef`/`AppRef` の中身は隠蔽 (実装上は internal handle ID)
- フィールドアクセス不可
- 構造的サブタイピングの対象外
- Numadora スクリプトからは「型付き不透明値」として扱う

これは spec への拡張である (spec には `opaque` 型がない)。spec 改訂の対象。

「観測値レコード」(タイトル・矩形・状態などフィールド一式) は別の型として返す:

```numadora
EXPORT RECORD WindowInfo {
  title: string,
  handle: int,
  state: "normal" | "minimized" | "maximized",
  rect: record { x: int, y: int, w: int, h: int },
}

EXPORT FUNC info(target: WindowRef): WindowInfo
```

つまり: **ハンドル = 不透明 `WindowRef` / 観測値 = `WindowInfo` レコード**。

## 4. ホストバインディング (`.numai` モデル)

D2 の確定事項を具体化する。

### 4.1 ファイル構成

```
scripts/numadora-host/
  slasher/desktop.numai
  slasher/window.numai
  slasher/input.numai
  slasher/screen.numai
  slasher/element.numai
  slasher/browser.numai
  slasher/io.numai
  slasher/dialog.numai
  slasher/test.numai
  std/array.numai
  std/string.numai
  std/io.numai
  std/error.numai
  std/test.numai
```

`.numai` は **シグネチャ宣言のみ**。本体は持たない。

```numadora
# slasher/desktop.numai
MODULE slasher/desktop

IMPORT slasher/window AS window

EXPORT OPAQUE TYPE AppRef

EXPORT FUNC start-app(file-name: string): AppRef
EXPORT FUNC start-app-with-args(file-name: string, args: array[string]): AppRef
EXPORT FUNC wait-for-window(app: AppRef, title: string, timeout-ms: int): Option[window.WindowRef]
EXPORT FUNC close(app: AppRef): unit
```

### 4.2 C# 側の登録

C# 側はホスト関数を **属性で宣言**、起動時に登録する。

```csharp
[NumadoraHostModule("slasher/desktop")]
public sealed class DesktopHostBindings
{
    [NumadoraHostFunc("start-app")]
    public AppRef StartApp(string fileName) => ...;

    [NumadoraHostFunc("wait-for-window")]
    public Option<WindowRef> WaitForWindow(AppRef app, string title, int timeoutMs) => ...;
}
```

ホスト登録時に:
1. `.numai` のシグネチャを読み込み
2. C# 側の登録メソッドのシグネチャと突き合わせ
3. 不整合があれば起動失敗 (型の食い違い・引数数の食い違いなど)

ランタイムは `Print("__SLASHER_HOST_CALL__ ...")` を完全廃止し、**型付き直接呼び出し** に置き換える。

### 4.3 ポリシーゲート

各ホスト関数にポリシー要件を宣言可能にする (現行 `NumadoraPolicyEvaluator` の延長線):

```csharp
[NumadoraHostFunc("focus", RequiresInteractive = true)]
public void Focus(WindowRef target) => ...;
```

`.numai` 側にも視覚的に明示する (将来):

```numadora
EXPORT INTERACTIVE FUNC focus(target: WindowRef): unit
```

`INTERACTIVE` は spec 改訂で導入する修飾子 (案)。run mode でユーザの明示承認 (`allowInteractiveInput`) が必要なホスト関数を示す。

### 4.4 メソッド呼び出し糖衣

`win.focus()` の表記は以下のいずれかにより成立させる:

- **(案 a) UFCS 風**: `win.focus()` は `focus(win)` の糖衣。ホスト関数の第 1 引数の型でメソッド呼び出しを解決する。
- **(案 b) record-method 表記の禁止**: 全部 `focus(win)` で書く。冗長だが透明性最大。

**推奨は (案 a)**。ただし spec に明文化されていないため、spec 改訂対象として明示する。

## 5. 拡張性: マクロなしでの DSL 風表現

D3 (マクロ廃止) と D4 (関数 + ブロック引数) の確定を、具体的にどう実現するか。

### 5.1 トレーリング ブロック構文

関数の最後の引数が **関数値** の場合、呼び出し側で `DO ... END` ブロックとして記述できる。

```numadora
# 標準ライブラリで定義
EXPORT FUNC retry(times: int, backoff-ms: int, body: function(): unit): unit
  ...
END

# 呼び出し
retry(3, 500) DO
  input.text("hello")
END
```

これは内部的には:

```numadora
retry(3, 500, FUNC() input.text("hello") END)
```

の糖衣。

### 5.2 ブロック内での値受け取り (案)

`with-window` のように、ブロック内へ値を渡したい場合の表現は **未決**。3 つの選択肢:

| 案 | 例 | コメント |
|---|---|---|
| (案 1) ラムダ復活 (限定形) | `with-window("Notepad") DO |w| ... END` | spec の「lambda 採用しない」を一部緩める |
| (案 2) 名前付き FUNC を呼び出し側で書く | `FUNC handle(w: WindowRef) ... END; with-window("Notepad", handle)` | spec 通りだが冗長 |
| (案 3) 暗黙束縛変数 (予約名 `it`) | `with-window("Notepad") DO io.print(it.title) END` | Kotlin 風。読みやすいが「暗黙」の理解負荷 |

**推奨は (案 1) のトレーリング ラムダ限定採用**。理由:
- 関数の引数位置だけで使え、独立式としては書けないため、副作用ありの式が散乱しない
- AI 生成可読性は (案 3) より高い (引数名が明示)
- DEF-SYNTAX-CMD ほど強力ではないが、`with-window`/`with-app`/`retry` 程度の DSL 風は素直に書ける

(案 1) を採用する場合、spec に「トレーリング ブロック専用ラムダ構文」を追加する必要がある。

### 5.3 失われる表現と許容理由

マクロを捨てたことで失う代表的な表現:

- `text "hello"` (括弧なしコマンド) → `input.text("hello")` で書く
- `wait-window "Notepad" 5000 AS win OR FAIL "..."` → `LET win = window.wait-for-title("Notepad", 5000) OR FAIL "..."` で書く
- 行指向 DSL の任意定義 → 関数 + トレーリング ブロック で間に合う範囲に留める

**容認可能** と判断する理由:
- AI 生成・AI 読解での認知負荷が下がる (構文が一貫する)
- 実装コストが大きく下がる (パーサ・展開器・トレーサ不要)
- マクロ展開バグ・衛生性バグの可能性がそもそも生じない

## 6. ホストモジュール再編

現状は `slasher_app` / `slasher_window` / `slasher_desktop` で重複・曖昧があり、`appRef.WaitForWindow` と `win.WaitForApp(appRef, ...)` が両立するなど、責務が交錯している。

D1 採用の機会に、以下のように責務を切り直す。

| モジュール | 責務 | 主な関数 |
|---|---|---|
| `slasher/app` | アプリ起動、プロセス/アプリ レベル操作、デスクトップ全体観測 (Q5 採用: 旧 `slasher/desktop` を統合) | `start-app`, `start-app-with-args`, `enumerate-windows`, `wait-for-window-of`, `close` |
| `slasher/window` | ウィンドウ単体への操作 | `wait-for-title`, `focus`, `set-state`, `close`, `info`, `move`, `capture` |
| `slasher/input` | キーボード・マウス | `text`, `keys`, `mouse`, `wheel`, `drag`, `context-menu` |
| `slasher/screen` | 画面キャプチャ | `capture-full`, `capture-monitor`, `capture-region` |
| `slasher/element` | UI 要素 (UIA / 子ウィンドウ) | `find`, `exists`, `read-text`, `tree`, `click` |
| `slasher/browser` | Selenium ブラウザ操作 | `open`, `find`, `click`, `type`, ... |
| `slasher/clipboard` | クリップボード | `read`, `write` |
| `slasher/files` | ファイル操作 (Slasher 拡張) | `read-text`, `write-text`, `delete`, `watch` |
| `slasher/data` | CSV/JSON/Excel | `csv-read`, `json-query`, `excel-read` |
| `slasher/io` | Slasher 固有のログ・ステップ・待機 | `step`, `log`, `wait` |
| `slasher/dialog` | ローカルメッセージボックス | `message`, `confirm` |
| `slasher/test` | アサート | `assert-foreground-title`, `assert-element-text` |
| `std/io` | 汎用 I/O (時刻・環境変数・標準出力) | `now`, `env`, `cwd`, `print` |
| `std/array`, `std/string`, ... | spec 第 8 章のとおり | |

**std と slasher の境界**:
- `std/*` は OS 非依存・純粋寄り (`std/array`, `std/string` 等は完全に純粋)
- `slasher/*` は Windows 自動化のホスト能力 (副作用あり)
- 例: `print` は `std/io` (汎用)、`step` は `slasher/io` (Slasher の証跡モデルに依存)

## 7. エラーコード体系

spec 付録 B のカテゴリ分類をそのまま採用。Slasher 固有のホストエラーは「動詞ベース」の小文字スネーク:

| カテゴリ | プレフィックス | 例 |
|---|---|---|
| 構文 | `syntax_` | `syntax_unexpected_token` |
| 型 | `type_` | `type_mismatch`, `type_unknown_field` |
| 名前 | `name_` | `name_undefined_variable` |
| 実行時 (操作) | (動詞ベース) | `window_not_found`, `element_not_found`, `file_not_found` |
| 実行時 (アサート) | `assertion_` | `assertion_failed` |
| 実行時 (システム) | `runtime_` | `runtime_option_unwrap_none`, `runtime_index_out_of_bounds` |
| ユーザ | `user_` | `user_fail` |

## 8. spec への必要な改訂

D1 で「spec を正」としたが、以下の改訂が必要になる。これらは別 PR で `numadora-language-spec.md` に反映する。

| 項目 | 改訂内容 | 由来 |
|---|---|---|
| マクロ章 (第 6 章) | **削除**。`DEF-SYNTAX` / `DEF-SYNTAX-CMD` を仕様から外す | D3 |
| `EXPORT` 対象から `DEF-SYNTAX` 系を削除 | 5.1 の export 対象を「FUNC, RECORD, TYPE, CONST, OPAQUE TYPE」の 5 つに整理 | D3 |
| `opaque` 型の追加 | 第 1 章に「不透明 nominal 型」を追加 (3.4 参照、`OPAQUE TYPE` 構文) | D3 + Q-L2 |
| **`EXPORT EFFECT FUNC` 文法** | 第 5 章 export に追加。副作用ありホスト関数の明示マーカ | Q-D3 + Q-P6 |
| トレーリング ブロック構文 | 第 2 章 / 第 7 章 に追加。`DO \|x\| ... END` のラムダ限定採用 (Q1 採用) | Q1 |
| `INTERACTIVE` 修飾子 | 第 5 章 export に追加 (4.3 参照)。`EXPORT INTERACTIVE EFFECT FUNC` の組み合わせ可 | **Q3 採用** |
| メソッド呼び出し糖衣 (UFCS) | 第 2 章に明文化。`win.focus()` ≡ `focus(win)` (4.4 参照) | **Q2 採用** |
| 数値リテラル詳細 / kebab-case と `-` の解決 | 字句構造章に追加 (`numadora-base-structure.md` 第 1 章準拠) | base-structure |
| **raw 文字列リテラル `r"..."`** | 字句構造章に追加 (`numadora-base-structure.md` 1.9.2 / 3.1) | **Q-A1 採用** |
| **行頭演算子接続** | 字句構造章に追加 (`numadora-base-structure.md` 1.6.3) | **Q-A3 採用** |
| 識別子は ASCII のみ | 字句構造章で明文化 | Q-A2 採用 |
| 不透明型・トレーリング ブロック・キーワード予約語 | `numadora-base-structure.md` 1.4 / 2.8 / 2.9 に従って明文化 | base-structure |
| 局所型推論 / ジェネリクス (ユーザ関数も可) | 第 1 章 / 第 7 章に明文化 (`numadora-core-systems.md` 1.3 / 1.6) | **Q4 採用** |
| トップレベル副作用禁止 | 第 5 章に追加 (`numadora-core-systems.md` 2.6.4) | core-systems |
| 関数値の `==` 比較禁止 | 第 1 章 / 等価性節に追加 (`type_function_value_eq`) | Q-D2 |
| **レコード等価性は O(構造的サイズ)** | 第 1 章 / 等価性節に追加。実装の CoW 共有は自由 | **Q-D1 採用** |
| クロージャ越し VAR 書き換え禁止 | 第 2 章に追加 (`name_closure_var_assign`) | Q-D4 |
| **シングル スレッド前提** | 第 2 章に明示。`ASYNC`/`AWAIT` は予約語のみ | **Q-D5 採用** |
| **GC + finalizer + 明示 close** | リソース管理を `numadora-core-systems.md` 3.6 に従って明文化 | **Q-D6 採用** |
| `IMPORT ... REQUIRES >= x.y` 余地 | 第 5 章 import に「将来余地として注記」(本体は v2) | Q-P5 |
| **モジュール再編: `slasher/desktop` を `slasher/app` に統合** | 6 章モジュール表参照 | **Q5 採用** |
| **旧表記の deprecation なし (ハードカット)** | PR-B (サンプル書き換え) と PR-C (パーサ更新) を 1 PR に統合 | **Q6 採用** (Q-L3 と整合) |
| 残タスク欄 (付録 C) | マクロ関連項目を削除 | D3 |

## 9. 移行影響と PR 分割案

実装フェーズに進むときの **PR 分割の見通し** (このノートでは計画のみ、実施しない)。

| PR | 内容 | 依存 |
|---|---|---|
| **PR-A** | spec 改訂: マクロ削除、opaque 追加、構文表記の確定を spec に反映 | (前提) |
| **PR-B** | サンプル `.numa` を新表記に書き換え (`Int`→`int`, `:=`→`=`, `->`→`:`, snake→kebab, slash モジュールパス) | PR-A |
| **PR-C** | C# パーサ/インタプリタの新表記対応 (古い表記を deprecation で残しつつ並行受理) | PR-A |
| **PR-D** | `.numai` ホスト登録機構の C# 側実装 (属性 + 起動時リンク) | PR-C |
| **PR-E** | 既存ホスト関数 (`slasher_window` 等) を `.numai` + 属性付き C# クラスに移行 | PR-D, PR-B |
| **PR-F** | `Option[T]` / `MATCH` / `OR FAIL` / `RuntimeError` のインタプリタ実装 | PR-C |
| **PR-G** | リソース参照を不透明型に切替 (`"window:last"` 文字列廃止) | PR-D, PR-F |
| **PR-H** | トレーリング ブロック構文の実装 | PR-C |
| **PR-I** | 古い表記の受理を停止 (deprecation 解除) | PR-B〜H 完了後 |
| **PR-J** | ドキュメント整備 (`slasher-script.md` / `slasher-numadora-integration.md` を新仕様に書き換え) | PR-A〜I |

## 10. Q1〜Q6 確定事項

すべて採用済 (一括採用)。8 章 spec 改訂リストに反映済。

| Q | 確定 | 反映先 |
|---|---|---|
| **Q1** トレーリング ブロックの値受け | **(案 1) ラムダ限定採用**: `DO \|x\| ... END` で関数値を渡す | 8 章 spec 改訂、`numadora-base-structure.md` 2.8 |
| **Q2** メソッド呼び出し糖衣 (UFCS) | **採用**: `win.focus()` ≡ `focus(win)` (第 1 引数の型でディスパッチ) | 8 章 spec 改訂 |
| **Q3** `INTERACTIVE` 修飾子 | **採用**: `EXPORT INTERACTIVE EFFECT FUNC` の組み合わせを許す。run mode で `allowInteractiveInput` 承認が必要なホスト関数の宣言 | 8 章 spec 改訂、ホスト登録 C# 属性 (`RequiresInteractive = true`) と二重宣言 |
| **Q4** ユーザ定義ジェネリクス | **採用**: ユーザ定義関数でも `[T]` を許可 (`numadora-core-systems.md` 1.6 確定済) | core-systems 1.6 |
| **Q5** `slasher/desktop` と `slasher/app` の統合 | **統合採用**: `slasher/desktop` を廃止、機能を `slasher/app` に集約 | 6 章モジュール表 |
| **Q6** 旧表記の deprecation 期間 | **ハードカット採用**: PR-B (サンプル書換) と PR-C (パーサ更新) を 1 PR に統合。`Slasher.csproj` は内部のみで外部消費者なし、Q-L3 と整合 | 8 章 spec 改訂、9 章 PR 計画修正 |

### 10.1 Q1 トレーリング ブロックの構文 (詳細)

```ebnf
trailing-block := "DO" ( "|" param-list "|" )? newline body "END"
param          := identifier (":" type)?
```

- 関数の **最後の引数が関数型** (`function(...)`) のとき、その引数を `DO ... END` で渡せる
- `|x, y|` でブロック パラメータを宣言、型注釈は推論可
- 内部の最終式 or `RETURN` で値を返す
- ラムダの独立式形 (`FUNC(x) ... END` を式の中で書く) は **不採用** (トレーリング位置のみ)

### 10.2 Q2 UFCS 解決規則 (詳細)

`expr.func(args...)` の解決:

1. `expr` の型 `T` を求める
2. スコープ内の `func` のうち、第 1 引数の型が `T` (または `T` の structural supertype) と一致するものを探す
3. 一致が 1 つなら `func(expr, args...)` に変換
4. 0 個または複数個なら型エラー (`name_method_not_found` / `name_method_ambiguous`)

- 不透明型 (`WindowRef` 等) は名目的に解決
- レコード型は構造的サブタイプを許容
- ジェネリクスは型推論の一部として解決

### 10.3 Q6 ハードカット PR 計画への反映

9 章 PR 計画を更新:

| PR | 旧 | 新 |
|---|---|---|
| PR-B + PR-C | サンプル書換 / パーサ並行受理 | **統合**: 1 PR で全サンプル書換 + パーサ新表記専用化 |
| PR-I | 旧表記受理停止 | **削除** (ハードカットで不要) |

## 11. 改訂履歴

- v0.1 — 初版起草。spec をアンカー、`.numai` ホスト、マクロなし、トレーリング ブロックでの拡張、を 4 つの確定方針として固定。
- v0.2 — Q-A1〜A3, Q-D1〜D6, Q1〜Q6 を一括採用。8 章 spec 改訂リスト拡充、`slasher/desktop` 統合 (Q5)、ハードカット採用 (Q6) を反映。
