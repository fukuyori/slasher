# Numadora 言語仕様

> Status: this is a language design/reference document. The active Slasher N0
> implementation follows the Numadora runtime that exists today. Slasher
> examples and bindings therefore use current syntax such as
> `IMPORT slasher_app AS app` and `app.Start("notepad.exe")`. Features in this
> document such as `.numai`, slash-separated module paths, and
> `DEF-SYNTAX-CMD` are not prerequisites for the current Slasher migration.

Numadora は汎用プログラミング言語。Rust で実装された静的検査付きの軽量インタプリタ言語であり、
Windows を幅広く制御するためのホスト機能を統一的に扱えることを目標にする。
Slasher は Numadora に Windows automation 能力を提供するホスト実装のひとつであり、
Numadora 自体は Slasher v1 の後継 DSL ではない。

このドキュメントは Numadora の**言語仕様** (汎用部分) を定める。
Slasher ホストとの統合は `slasher-numadora-integration.md` を参照。
現在の Slasher 側プロファイルについては `slasher-script.md` も参照。
既存の `.slasher` ファイルからの移行は `migration-from-slasher-v1.md` を参照。

---

## 目次

- [第0章 設計の原則](#第0章-設計の原則)
- [第1章 型システム](#第1章-型システム)
- [第2章 式と文](#第2章-式と文)
- [第3章 エラーモデル](#第3章-エラーモデル)
- [第4章 match と網羅性検査](#第4章-match-と網羅性検査)
- [第5章 module と import](#第5章-module-と-import)
- [第6章 マクロ](#第6章-マクロ)
- [第7章 配列とコレクション](#第7章-配列とコレクション)
- [第8章 標準ライブラリ](#第8章-標準ライブラリ)
- [付録A 文法 EBNF](#付録a-文法-ebnf)
- [付録B エラーメッセージ仕様](#付録b-エラーメッセージ仕様)
- [付録C 残タスク](#付録c-残タスク)

---

## 第0章 設計の原則

### 原則1: 静的検査を段階的に強化する (gradual typing)

型注釈を書かないコードは動的型付けで動作する。書いた瞬間だけ静的検査が有効になる。
これにより、プロトタイピングは型なしで素早く、本格運用は型付きで安全に、を両立する。

### 原則2: AI エージェント可読性は一級市民

Numadora は AI エージェントから生成・編集される頻度が高いことを前提に設計する。
エラーメッセージ、構造化情報、ASTの透明性、マクロ展開のトレース、これらすべてが
AI から扱いやすい形になっている必要がある。

### 原則3: Windows 制御を一級の応用領域にする

Numadora は汎用言語でありながら、Windows アプリケーション、ウィンドウ、
入力、ファイル、ブラウザ、プロセス、クリップボード、証跡を統一的に扱える
ホスト能力を持つことを目標にする。

これらは旧 Slasher v1 コマンドの再現ではなく、Numadora の型・モジュール・
エラーモデルに乗った通常のライブラリとして設計する。

### 原則4: 派生 DSL は後から作れるが、核にしない

Numadora はマクロ機構で DSL を構築できる。ただし、旧仕様の見た目を再現するために
言語設計を曲げない。まず通常の関数、型、モジュールとして表現し、必要な表現力が
Numadora 全体に有益な場合だけマクロや糖衣を追加する。

### 原則5: シンプルな核 + 強力な拡張

言語コアは小さく保つ (Pascal/ML系の素直な構造)。複雑な機能は標準ライブラリと
マクロで提供する。コア仕様の変更は慎重に、ライブラリは積極的に拡張する。

---

## 第1章 型システム

### 1.1 名前空間

Numadora は型・関数・変数の3つを独立した名前空間に分ける。

```numadora
RECORD Window { title: string, handle: int }    # 型空間
FUNC Window(title: string): Window               # 関数空間 — 共存OK
  ...
END

LET Window = "string value"                      # 変数空間 — これも共存OK
```

同一空間内では衝突禁止。同名 `RECORD` の二重宣言や、同名関数のオーバーロードは不可。

### 1.2 基本型

| 型 | 説明 |
|---|---|
| `string` | UTF-8 文字列 |
| `int` | 64bit signed 整数 |
| `float` | 64bit 浮動小数点数 |
| `bool` | `true` / `false` |
| `unit` | 値を持たないことを示す |
| `Option[T]` | T 型または値なし (`Some(t)` / `None`) |
| `array[T]` | 同種要素の動的配列 |
| `record` | 匿名レコード (構造的) |
| 名前付きレコード型 | `RECORD` で宣言 |
| string-literal union | `"a" | "b" | "c"` 形式 |

### 1.3 レコード型

```numadora
RECORD Window {
  title: string,
  handle: int,
  className: string,
  isVisible: bool,
}
```

決定事項:

- **フィールド型**は基本型8種類 (string, int, float, bool, レコード型, array[T], Option[T], record)
- **自己参照と相互参照を許可**: `RECORD Element { children: array[Element] }` は OK
  - 構築不能な循環は静的検査でエラー
- **フィールドのデフォルト値はなし** (将来検討)
- **ドキュメンテーションコメントは `---`** で各フィールドに付けられる

```numadora
RECORD Window {
  --- ウィンドウタイトル。空文字列もありうる。
  title: string,
  --- Win32 の HWND 相当。0 は無効値を意味する。
  handle: int,
}
```

### 1.4 構造的サブタイピング

レコード型は名前ではなくフィールド形状で適合性を判定する。

```numadora
RECORD Window { title: string, handle: int }
RECORD TitledThing { title: string }

LET t: TitledThing = win    # OK: Window は TitledThing のフィールドをすべて持つ
```

これにより、組み込みコマンドの戻り値とユーザー定義レコードを名前で結ぶ必要がない。

### 1.5 レコードの生成と更新

```numadora
LET w: Window = Window {
  title: "Notepad",
  handle: 0x123,
  className: "Notepad",
  isVisible: true,
}
```

すべてのフィールドを必須で書く。順不同。

不変更新は `WITH` 句:

```numadora
LET w2 = w WITH { title: "Notepad - edited" }
```

`w` は変更されず、`w2` が新しい値。

**フィールドは読み取り専用**。`w.title := "..."` は禁止。
可変な状態は `LET` / `VAR` 変数だけが持てる、という二分法。

### 1.6 Option[T] と nullability

Numadora には `nil` がない。値が「ある / ない」の表現は `Option[T]` 型を使う。

```numadora
FUNC findHelper(title: string): Option[Element]
  ...
END
```

#### Option の取り出し: match

```numadora
MATCH ok OF
CASE Some(elem) THEN
  Element.Click(elem)
CASE None THEN
  Fail("OKボタン未発見")
END
```

#### Option の取り出し: 後置 OR 構文

```numadora
LET ok: Element = elementFind("OK") OR FAIL "OK ボタン未発見"
LET cancel: Element = elementFind("Cancel") OR DEFAULT someDefault
```

`OR FAIL` は内部的には `MATCH` の糖衣。

`OR FAIL` は code/details を取れる:

```numadora
LET ok = elementFind("OK") OR FAIL "OK 未発見" CODE "ok_missing" DETAILS { searchedTitle: "OK" }
```

#### Option の判定と展開

```numadora
LET ok = elementFind("OK")
IF ok IS SOME THEN
  ElementClick(ok.value)
ELSE
  Print("見つからない")
END
```

**フロー依存型は採用しない**。`.value` で明示的にアンラップする。
`.value` は危険な操作なので静的検査で**警告**を出す (エラーではなく)。
`OR FAIL` を使うことを提案する。

#### 採用しない構文

- `?.` (optional chaining)
- `??` (null coalescing)

これらは式指向言語では便利だが、明示的な `MATCH` か `OR FAIL` を使う方針を貫く。

### 1.7 string-literal union

```numadora
RECORD Window {
  state: "normal" | "minimized" | "maximized",
  ...
}
```

文字列リテラルを `|` で繋いだ集合を型として使える。
静的検査で `w.state == "minimised"` を typo として検出できる。

### 1.8 type 別名

```numadora
TYPE WindowState = "normal" | "minimized" | "maximized"

RECORD Window {
  state: WindowState,
}
```

`TYPE` でレコード型や string-literal union への別名を定義できる。
`TYPE` は型空間の名前として export 可能。

### 1.9 const 宣言

```numadora
CONST MAX_RETRIES: int = 3
EXPORT CONST KNOWN_CODES: array[string] = ["element_not_found", "window_not_found"]
```

トップレベルの不変定数。`EXPORT` で公開可能。

### 1.10 unit 型

```numadora
FUNC printHello(): unit
  Print("hello")
END
```

`unit` 型は値を持たない。関数の戻り値が無意味な場合に使う。
`unit` リテラルは `()` または `unit` キーワード。

---

## 第2章 式と文

### 2.1 文と式の区別

Numadora は **式指向言語** だが、トップレベルでは文として実行される。

文 (statement):
- `LET name = expr`、`VAR name = expr`、`CONST name = expr`
- `name = expr` (再代入、`VAR` 宣言された変数のみ)
- 関数呼び出し / マクロ呼び出し
- `IF ... END`、`WHILE ... END`、`FOR ... END`
- `RETURN expr`
- `MATCH ... END`
- `TRY ... END`

式 (expression):
- リテラル、変数参照、フィールドアクセス、配列インデックス
- 算術・比較・論理演算
- 関数呼び出し (戻り値を持つ)
- `Some(expr)` / `None`
- レコード生成 / `WITH` 更新
- 配列リテラル `[a, b, c]`

### 2.2 演算子と優先順位

```ebnf
expr        := or-expr
or-expr     := and-expr ("OR" and-expr)*
and-expr    := not-expr ("AND" not-expr)*
not-expr    := "NOT" not-expr | compare-expr
compare-expr:= add-expr (compare-op add-expr)?
compare-op  := "==" | "!=" | "<" | "<=" | ">" | ">="
             | "CONTAINS" | "STARTSWITH" | "ENDSWITH"
             | "IS" ("SOME" | "NONE")
add-expr    := mul-expr (("+" | "-") mul-expr)*
mul-expr    := unary-expr (("*" | "/" | "%") unary-expr)*
```

決定:

- 通常の優先順位
- 比較は連鎖しない (`a < b < c` は文法エラー)
- 論理演算子は `AND`, `OR`, `NOT` (キーワード)
- 文字列連結は `+`
- 配列リテラルは `[ ... ]`

### 2.3 数値型の振る舞い

- `1 + 2` → `int`
- `1.0 + 2` / `1 + 2.0` → `float` (暗黙昇格)
- `1 / 2` → `int` (整数除算 = 0)
- `1.0 / 2` / `1 / 2.0` → `float`
- `%` (剰余) は `int % int` のみ
- `int` は 64bit signed、オーバーフローは実行時エラー (silent wrap-around しない)

### 2.4 文字列補間と式埋め込み

文字列内で値を埋め込む:

```numadora
Print("title is " + win.title)              # 連結
Print(`title is ${win.title}`)              # テンプレートリテラル (補間)
```

決定:
- **`+` による連結**: 標準の文字列結合
- **テンプレートリテラル ` `` ` (バッククオート)**: 中で `${expr}` を埋め込み

`${expr}` の中身は完全な式が書ける。複雑な式は外で計算してから `${var}` で埋めるほうが読みやすい。

### 2.5 関数とプロシージャ

```numadora
FUNC double(x: int): int
  RETURN x * 2
END

FUNC sayHello()                # 戻り値なし
  Print("hello")
END
```

戻り値型を省略すると `unit` を返す関数とみなされる。

#### 関数の純粋性

Numadora は関数の純粋性を**自動判定**する。

> 関数の本体に副作用 (コマンド呼び出し、I/O、可変状態への書き込み) が1つでもあれば不純、
> なければ純粋。

純粋関数のみが式の中から呼べる:

```numadora
FUNC double(x: int): int      # 純粋
  RETURN x * 2
END

FUNC focusAndReturn(w: Window): int     # 不純 (Focus は副作用)
  Slasher.Focus(w.handle)
  RETURN w.handle
END

LET d = double(5)              # OK
LET f = focusAndReturn(w)      # 検査エラー: 不純な関数は式から呼べない
focusAndReturn(w)              # 文として呼ぶ: OK
```

純粋性判定は静的検査の段階で全関数の不動点計算で求める。

### 2.6 副作用なしの保証

式が副作用を起こさない、という原則を強制する仕組み:

1. 不純な関数は式から呼べない
2. 代入は式ではない: `x := y` のような代入式は不採用
3. インクリメント・デクリメント `++`, `--` は不採用

これにより、式の評価順序が観測可能な副作用を起こさないことが保証される。

### 2.7 制御構造

```numadora
IF condition THEN
  ...
ELSE
  ...
END

WHILE condition DO
  ...
END

FOR i IN 0..10 DO
  ...
END

FOR (i, item) IN items DO    # タプル形式 (インデックス + 要素)
  ...
END

BREAK
CONTINUE
```

`WHILE` ループは最大反復数 1000 を超えると `runtime_max_iteration` で停止 (無限ループ防止)。

---

## 第3章 エラーモデル

### 3.1 失敗の3分類

| 種別 | 表現方法 | 例 |
|---|---|---|
| 期待される失敗 (expected) | `Option[T]` | 検索結果なし、待機タイムアウト |
| 操作の失敗 (operational) | 例外 (`RuntimeError`) | 操作系コマンドの拒否、I/O エラー |
| プログラム上の誤り (programming) | catch 不能な panic | `.value` で None 展開、配列範囲外、ゼロ除算 |

### 3.2 RuntimeError 型

```numadora
EXPORT RECORD RuntimeError {
  --- エラー種別を示す機械可読な識別子。
  code: string,
  --- 人間向けエラーメッセージ。
  message: string,
  --- 追加情報。
  details: record,
  --- エラー発生位置。
  source: ErrorSource,
  --- エラーが発生したコマンド名 (該当する場合)。
  command: Option[string],
  --- 失敗イベントに紐づく証跡パス。
  evidence: array[string],
}

EXPORT RECORD ErrorSource {
  file: Option[string],
  line: Option[int],
  function: Option[string],
  stack: array[ErrorSourceFrame],
}

EXPORT RECORD ErrorSourceFrame {
  file: string,
  line: int,
  function: Option[string],
}
```

### 3.3 try / catch / finally

```numadora
TRY
  Slasher.Focus(win.handle)
  Slasher.SendText("hello")
CATCH e: RuntimeError
  Print("失敗: " + e.code + " - " + e.message)
FINALLY
  Print("クリーンアップ")
END
```

決定事項:

- `CATCH e: Type` の型注釈は任意
- 注釈ありで `e.code` 等のフィールドアクセスが静的検査される
- `CATCH` 句は1つだけ
- エラー種別での分岐は内部の `e.code` を `MATCH` で見る
- `CATCH` 句なしの `TRY` は禁止

### 3.4 fail コマンド

```numadora
Fail("OKボタン未発見")
Fail("OKボタン未発見", "ok_not_found")
Fail("OKボタン未発見", "ok_not_found", { expected: "OK", actual: "Cancel" })
```

`Fail` は標準ライブラリ関数として提供。第2引数は `code` (省略時 `"user_fail"`)、
第3引数は `details` (省略時 `{}`)。

### 3.5 例外の伝播

例外が伝播するときに `e.source.stack` に呼び出し元のフレームが積まれていく。

### 3.6 エラー詳細へのアクセス

`e.details` は不透明な値として扱う。フィールドアクセスは型付き関数経由:

```numadora
IMPORT std/error AS err

MATCH e.code OF
CASE "assertion_failed" THEN
  LET expected = err.DetailString(e, "expected")
  LET actual = err.DetailString(e, "actual")
  ...
END
```

`err.DetailString` は `Option[string]` を返す (フィールドがない、または型不一致なら `None`)。
`DetailInt`, `DetailFloat`, `DetailBool` も同様。

---

## 第4章 match と網羅性検査

### 4.1 値空間の分類

| 種別 | 例 | 網羅性可能か |
|---|---|---|
| 有限な型 | `bool`, `Option[T]`, string-literal union | 可能 |
| 無限な型 | `int`, `float`, `string` (素), `array[T]` | 不能 |
| 準有限な型 | レコード型 (フィールド分解) | 構造的には可能 |

### 4.2 検査の3段階強度

| 被検査値の型 | 網羅性なしの扱い | 備考 |
|---|---|---|
| `Option[T]` | エラー | `Some` / `None` 両方必須 |
| `bool` | エラー | `true` / `false` 両方必須 |
| string-literal union | エラー | 列挙されたすべての値が必須 |
| `string` (素) | 警告 | `_` がないと警告 |
| `int`, `float` | 検査なし | 値空間無限 |
| レコード (構造分解のみ) | 検査なし | 構造的に網羅 |
| レコード (値分岐) | 警告 | `_` か束縛-only ケースがないと警告 |
| `array[T]` | 検査なし | パターン分解なし |

到達不能ケースは別途**警告**として常時検出。

### 4.3 パターンの種類

```ebnf
pattern :=
  | literal-pattern         # "hello", 42, true, false
  | binding-pattern         # x (任意の名前で値を束縛)
  | wildcard-pattern        # _
  | some-pattern            # Some(p)
  | none-pattern            # None
  | record-pattern          # Window { f1: p1, f2: p2 }
```

`record-pattern` ではフィールド省略を許す:

```numadora
MATCH win OF
CASE Window { title: "Notepad" } THEN     # 他フィールドは無視
  Print("Notepad")
END
```

### 4.4 ガード句と ADT

- **ガード句 (when 節) は不採用**: 網羅性検査と相性が悪い、`IF` で書けばよい
- **ADT (代数的データ型) は不採用 (現時点)**: string-literal union とレコードで代替

`Option[T]` だけは特例として組み込み済み。

### 4.5 case の評価順

上から順に評価して最初にマッチした case を実行。
**到達不能な case は警告**。

### 4.6 if と match の使い分け

| 場面 | 推奨 |
|---|---|
| bool 値の単純な分岐 | `IF` |
| 比較演算子による分岐 | `IF` |
| 1〜2分岐の単純なロジック | `IF` |
| Option の `Some` / `None` 分岐 | `MATCH` |
| string-literal union の値分岐 | `MATCH` |
| 3つ以上のケースに分岐 | `MATCH` |
| フィールド分解で複数パターン | `MATCH` |

---

## 第5章 module と import

### 5.1 module 宣言と export

```numadora
MODULE string-utils

EXPORT FUNC trim-and-upper(s: string): string
  RETURN StringUpper(StringTrim(s))
END

# EXPORT がないので外部からは呼べない
FUNC internal-helper(s: string): string
  ...
END

EXPORT RECORD Pair {
  left: string,
  right: string,
}
```

決定:

- `EXPORT` の対象は `FUNC`, `RECORD`, `TYPE`, `CONST`, `DEF-SYNTAX`, `DEF-SYNTAX-CMD` の6つ
- プライベート `RECORD` 型を `EXPORT` 関数のシグネチャに含めるのは禁止

### 5.2 import — alias 必須

```numadora
IMPORT lib/string-utils                # エラー: AS 句が必要
IMPORT lib/string-utils AS su          # OK
```

呼び出し:

```numadora
LET result = su.trim-and-upper("  hello  ")
LET p = su.Pair { left: "a", right: "b" }
```

### 5.3 import パスの解決

- 現在ファイル相対 / ワークスペースルート相対
- **`std/` で始まるパスは標準ライブラリ**
- ファイル名規約: `IMPORT path/foo` は `path/foo.numa` または `path/foo.numai` を探す
  - 両方ある場合は `.numa` 優先 (実装ファイルがある場合は型情報をソースから抽出)

### 5.4 .numai インターフェイスファイル

```numadora
# std/array.numai
MODULE array

EXPORT FUNC length[T](a: array[T]): int
EXPORT FUNC get[T](a: array[T], index: int): Option[T]
...
```

`.numai` は実装を含まない。`FUNC` 本体を持たず、シグネチャだけで終わる。
組み込み関数や、別言語で実装された関数のシグネチャ宣言に使う。
Slasher の組み込みコマンドはこの形式で公開される。

### 5.5 import の検査

静的検査で検証:

1. パスの存在確認
2. module 宣言の整合
3. alias の重複
4. 循環 import
5. エクスポート可視性
6. 型の整合性

---

## 第6章 マクロ

中心命題: **マクロは新しい構文を導入する力を持つが、AI エージェントにとって
読めなくなった瞬間に負債になる**。Numadora のマクロは強力さよりも**透明性**を優先する。

### 6.1 マクロの2形態

Numadora には2種類のマクロがある。

**`DEF-SYNTAX` (関数風マクロ)**: 通常の関数呼び出し構文で呼ぶマクロ。

```numadora
DEF-SYNTAX retry(times: int, backoffMs: int, body) DO
  ...
END

retry(3, 500) DO
  ...
END
```

**`DEF-SYNTAX-CMD` (コマンド風マクロ)**: 行指向 DSL を作るための拡張マクロ。
括弧なしで呼べる。

```numadora
DEF-SYNTAX-CMD text(content: string) DO
  Slasher.SendText(${content})
END

text "hello"     # 括弧なしで呼べる
```

これは Numadora 上に行指向 DSL を構築するためのコア機構。Slasher の自動化スクリプトは
この機構の上に作られる。

### 6.2 設計原則

1. 展開後は通常コード列に必ず還元される (新しいセマンティクスは導入しない)
2. 衛生的 (hygienic) — 内部変数と呼び出し側変数は衝突しない
3. パターンベース、計算的でない (procedural macro は不採用)
4. 展開はコンパイル時 (検査時)
5. 展開結果がトレース可能

### 6.3 DEF-SYNTAX の構文

```numadora
DEF-SYNTAX macro-name(param1: type1, param2: type2, body) DO
  command1(${param1})
  command2()
  ${body}
  command3()
END
```

呼び出し:

```numadora
macro-name(arg1, arg2) DO
  body-content
END
```

決定:

- マクロ名は通常識別子と同じ命名規則 (英数字とハイフン)
- 値パラメータとブロックパラメータの2種類
- ブロックパラメータは1つまで
- 戻り値はない

### 6.4 DEF-SYNTAX-CMD の構文

これは Numadora の拡張マクロ機能で、行指向 DSL の構築に使う。

```numadora
DEF-SYNTAX-CMD command-name(param1: type1, param2: type2) DO
  ...
END
```

#### コマンド呼び出しの形式

`DEF-SYNTAX-CMD` で定義されたマクロは、**括弧なし、引数空白区切り**で呼べる:

```numadora
DEF-SYNTAX-CMD text(content: string) DO
  Slasher.SendText(${content})
END

text "hello"                                    # 1引数
```

```numadora
DEF-SYNTAX-CMD mouse-click(x: int, y: int, button: string) DO
  Slasher.MouseClick(${x}, ${y}, ${button})
END

mouse-click 400 300 "left"                       # 3引数
```

#### AS 句のサポート

戻り値を変数に束縛する `AS` 句:

```numadora
DEF-SYNTAX-CMD foreground() AS-BINDING(target: Window) DO
  LET ${target} = Slasher.GetForeground()
END

foreground AS win                               # win に束縛
```

`AS-BINDING(target: Type)` は「呼び出し時の `AS varName` で受ける変数」と
「マクロ内で `${target}` として参照する名前」を結ぶ。

#### OR 句のサポート (Option を返すコマンド)

```numadora
DEF-SYNTAX-CMD wait-window(title: string, timeoutMs: int)
              AS-OPTION-BINDING(target: Window)
              OR-CLAUSE
DO
  LET ${target} = Slasher.WaitWindow(${title}, ${timeoutMs})
END

# 呼び出し
wait-window "Notepad" 5000 AS win OR FAIL "Notepadなし"
```

`AS-OPTION-BINDING` を指定すると、マクロは内部的に `Option[T]` を生成し、
`OR FAIL` / `OR DEFAULT` 句が後置可能になる。

#### ブロック引数のサポート

```numadora
DEF-SYNTAX-CMD with-window(title: string, timeoutMs: int)
              BLOCK(body)
              INTRODUCES(window: Window)
DO
  LET window = Slasher.WaitWindow(${title}, ${timeoutMs})
  IF window IS NONE THEN
    Fail("ウィンドウなし: " + ${title})
  END
  ${body}
END

# 呼び出し
with-window "Notepad" 10000 DO
  text "hello"
  Print(window.title)         # マクロが導入した変数
END
```

`BLOCK(body)` でブロック引数を宣言、`INTRODUCES(name: type)` でブロック内に導入される
変数を明示する。

### 6.5 衛生性

マクロ内の `LET` で導入された変数は自動的にユニークな名前にリネームされる。
`INTRODUCES` 句で明示された変数のみ、呼び出し側に見える。

衛生性は AI エージェントが「マクロが何を外部に公開するか」を読み取るための
重要な仕組み。

### 6.6 制約

- マクロは式の中では呼べない (`DEF-SYNTAX-CMD` は文として呼ぶ)
- マクロは組み込みコマンドと同名にできない
- マクロ内では同じマクロを再帰呼び出しできない

### 6.7 マクロ展開のトレース

実行時のイベントログに展開情報を載せる:

```json
{
  "type": "macro-expansion",
  "macroName": "with-window",
  "module": "slasher/control",
  "callSite": { "file": "test.numa", "line": 42 },
  "expandedTo": { "startLine": 42, "endLine": 58 },
  "arguments": { "title": "\"Notepad\"", "timeoutMs": "10000" }
}
```

これにより、AI エージェントが「失敗箇所がマクロ展開後の何行目か」と
「マクロ呼び出しは元のどこか」を両方知れる。

---

## 第7章 配列とコレクション

### 7.1 array[T] 型

```numadora
LET nums: array[int] = [1, 2, 3]
LET titles: array[string] = ["a", "b"]
LET windows: array[Window] = ...
LET nested: array[array[int]] = [[1, 2], [3, 4]]
LET opts: array[Option[string]] = [Some("a"), None]
```

要素型 `T` には任意の型を使える。要素型は単一 (異種要素配列はなし)。

### 7.2 構築方法

```numadora
# リテラル
LET items: array[string] = ["alpha", "beta", "gamma"]

# 空配列 (型注釈必須)
LET empty: array[int] = []

# 関数の戻り値
LET doubled = array.Map(nums, double)
```

### 7.3 読み取り

```numadora
LET first: string = items[0]              # 直接アクセス、範囲外なら panic
LET safe = array.Get(items, 0)            # Option[T] を返す
LET n: int = items.length                 # フィールドアクセス糖衣
```

| 構文 | 範囲外時 |
|---|---|
| `items[i]` | panic (programming error) |
| `array.Get(items, i)` | `None` を返す |

### 7.4 変更操作

可変配列の操作:

```numadora
VAR items: array[string] = ["a", "b"]
array.Push(items, "c")                # 末尾追加
LET last = array.Pop(items)           # 末尾取り出し、Option[T]
array.Insert(items, 0, "z")           # 指定位置に挿入
LET removed = array.Remove(items, 1)  # 削除、Option[T]
array.Clear(items)                    # 全削除
```

`VAR` で宣言された配列のみ変更可能。`LET` で宣言された配列は不変。

### 7.5 反復

```numadora
FOR item IN items DO
  Print(item)
END

# インデックス + 要素
FOR (i, item) IN items DO
  Print(ToString(i) + ": " + item)
END
```

### 7.6 ジェネリクス

標準ライブラリ関数のシグネチャでのみ型パラメータを使える。
ユーザー定義関数では型パラメータを使えない (将来検討)。

```numadora
FUNC map[T, U](a: array[T], f: function(T): U): array[U]
```

呼び出し時の型推論で型パラメータが決まる。

### 7.7 関数値

```numadora
FUNC double(x: int): int
  RETURN x * 2
END

LET doubled = array.Map(nums, double)
```

関数値の型: `function(T): U`。
**lambda は採用しない** — 名前付き関数のみ。
純粋関数のみ渡せる。

### 7.8 コレクション系の型

辞書 (map) と集合 (set) は当面非対応。
必要なら `array[record { key: string, value: T }]` で代替。

### 7.9 文字列と配列

文字列は配列ではない別の型。変換は `std/string.Split()`, `std/string.Chars()` などで。

---

## 第8章 標準ライブラリ

Numadora の標準ライブラリ。`std/` プレフィックスで提供。

### 8.1 モジュール一覧

```
std/array       - 配列操作 (純粋関数)
std/string      - 文字列操作 (純粋関数)
std/error       - RuntimeError、エラー詳細アクセス
std/io          - now、env、cwd 等の I/O 系
std/test        - Assert、テスト記録
std/math        - 数値計算
std/json        - JSON エンコード/デコード
std/http        - HTTP クライアント
std/file        - ファイル読み書き
std/process     - 外部プロセス起動
```

### 8.2 std/array

```numadora
EXPORT FUNC length[T](a: array[T]): int
EXPORT FUNC get[T](a: array[T], index: int): Option[T]
EXPORT FUNC is-empty[T](a: array[T]): bool
EXPORT FUNC first[T](a: array[T]): Option[T]
EXPORT FUNC last[T](a: array[T]): Option[T]
EXPORT FUNC contains[T](a: array[T], value: T): bool
EXPORT FUNC index-of[T](a: array[T], value: T): Option[int]
EXPORT FUNC join(a: array[string], sep: string): string
EXPORT FUNC map[T, U](a: array[T], f: function(T): U): array[U]
EXPORT FUNC filter[T](a: array[T], predicate: function(T): bool): array[T]
EXPORT FUNC reduce[T, U](a: array[T], initial: U, f: function(U, T): U): U
EXPORT FUNC reverse[T](a: array[T]): array[T]
EXPORT FUNC sort-strings(a: array[string]): array[string]
EXPORT FUNC sort-ints(a: array[int]): array[int]
EXPORT FUNC slice[T](a: array[T], from: int, to: int): array[T]
EXPORT FUNC concat[T](a: array[T], b: array[T]): array[T]
EXPORT FUNC distinct[T](a: array[T]): array[T]
EXPORT FUNC find[T](a: array[T], predicate: function(T): bool): Option[T]
EXPORT FUNC count[T](a: array[T], predicate: function(T): bool): int
EXPORT FUNC all[T](a: array[T], predicate: function(T): bool): bool
EXPORT FUNC any[T](a: array[T], predicate: function(T): bool): bool
EXPORT FUNC max-int(a: array[int]): Option[int]
EXPORT FUNC min-int(a: array[int]): Option[int]
EXPORT FUNC sum-int(a: array[int]): int

# 可変操作 (VAR 配列のみ)
EXPORT FUNC push[T](a: array[T], value: T): unit
EXPORT FUNC pop[T](a: array[T]): Option[T]
EXPORT FUNC insert[T](a: array[T], index: int, value: T): unit
EXPORT FUNC remove[T](a: array[T], index: int): Option[T]
EXPORT FUNC clear[T](a: array[T]): unit
```

### 8.3 std/string

```numadora
EXPORT FUNC length(s: string): int
EXPORT FUNC byte-length(s: string): int
EXPORT FUNC upper(s: string): string
EXPORT FUNC lower(s: string): string
EXPORT FUNC trim(s: string): string
EXPORT FUNC trim-left(s: string): string
EXPORT FUNC trim-right(s: string): string
EXPORT FUNC contains(s: string, sub: string): bool
EXPORT FUNC starts-with(s: string, prefix: string): bool
EXPORT FUNC ends-with(s: string, suffix: string): bool
EXPORT FUNC replace(s: string, old: string, new: string): string
EXPORT FUNC replace-first(s: string, old: string, new: string): string
EXPORT FUNC split(s: string, sep: string): array[string]
EXPORT FUNC substring(s: string, from: int, to: int): string
EXPORT FUNC index-of(s: string, sub: string): Option[int]
EXPORT FUNC last-index-of(s: string, sub: string): Option[int]
EXPORT FUNC from-int(n: int): string
EXPORT FUNC from-float(n: float): string
EXPORT FUNC to-int(s: string): Option[int]
EXPORT FUNC to-float(s: string): Option[float]
EXPORT FUNC chars(s: string): array[string]
EXPORT FUNC is-empty(s: string): bool
EXPORT FUNC pad-left(s: string, width: int, fill: string): string
EXPORT FUNC pad-right(s: string, width: int, fill: string): string
```

### 8.4 std/error

```numadora
EXPORT RECORD RuntimeError {
  code: string,
  message: string,
  details: record,
  source: ErrorSource,
  command: Option[string],
  evidence: array[string],
}

EXPORT RECORD ErrorSource {
  file: Option[string],
  line: Option[int],
  function: Option[string],
  stack: array[ErrorSourceFrame],
}

EXPORT RECORD ErrorSourceFrame {
  file: string,
  line: int,
  function: Option[string],
}

EXPORT FUNC detail-string(e: RuntimeError, field: string): Option[string]
EXPORT FUNC detail-int(e: RuntimeError, field: string): Option[int]
EXPORT FUNC detail-float(e: RuntimeError, field: string): Option[float]
EXPORT FUNC detail-bool(e: RuntimeError, field: string): Option[bool]
EXPORT FUNC format(e: RuntimeError): string
```

### 8.5 std/io

```numadora
EXPORT FUNC now(): int                             # Unix ms
EXPORT FUNC env(name: string): Option[string]
EXPORT FUNC cwd(): string
EXPORT FUNC print(s: string): unit
```

### 8.6 std/test

```numadora
EXPORT RECORD AssertResult {
  passed: bool,
  expected: string,
  actual: string,
  message: Option[string],
}

EXPORT FUNC equal[T](actual: T, expected: T): unit
EXPORT FUNC not-equal[T](actual: T, expected: T): unit
EXPORT FUNC is-true(condition: bool): unit
EXPORT FUNC is-false(condition: bool): unit
EXPORT FUNC is-some[T](opt: Option[T]): unit
EXPORT FUNC is-none[T](opt: Option[T]): unit
EXPORT FUNC contains(haystack: string, needle: string): unit
EXPORT FUNC array-contains[T](a: array[T], value: T): unit

# ソフトアサート
EXPORT FUNC soft-equal[T](actual: T, expected: T): unit
EXPORT FUNC soft-is-true(condition: bool): unit

# テスト記録
EXPORT FUNC note(message: string): unit
EXPORT FUNC attach(path: string, role: string): unit
EXPORT FUNC soft-failure-count(): int
```

### 8.7 std/json

```numadora
EXPORT FUNC encode(value: any): string
EXPORT FUNC decode(s: string): Option[any]
EXPORT FUNC decode-record[T](s: string): Option[T]
```

### 8.8 std/http

```numadora
EXPORT RECORD HttpResponse {
  status: int,
  body: string,
  headers: array[record { name: string, value: string }],
}

EXPORT FUNC get(url: string): Option[HttpResponse]
EXPORT FUNC post(url: string, body: string): Option[HttpResponse]
EXPORT FUNC post-json(url: string, body: any): Option[HttpResponse]
```

### 8.9 std/file

```numadora
EXPORT RECORD FileInfo {
  path: string,
  name: string,
  extension: string,
  size: int,
  modifiedMs: int,
}

EXPORT FUNC info(path: string): Option[FileInfo]
EXPORT FUNC exists(path: string): bool
EXPORT FUNC read-text(path: string): string
EXPORT FUNC write-text(path: string, content: string): unit
EXPORT FUNC append-text(path: string, content: string): unit
```

### 8.10 std/process

```numadora
EXPORT RECORD ProcessResult {
  exitCode: int,
  stdout: string,
  stderr: string,
}

EXPORT FUNC run(command: string, args: array[string]): ProcessResult
EXPORT FUNC run-with-timeout(command: string, args: array[string], timeoutMs: int): Option[ProcessResult]
```

---

## 付録A 文法 EBNF

```ebnf
program       := top-level*

top-level     := module-decl
               | import-decl
               | record-decl
               | type-decl
               | const-decl
               | func-decl
               | macro-decl
               | cmd-macro-decl
               | statement

module-decl   := "MODULE" Ident newline
import-decl   := "IMPORT" path "AS" Ident newline

record-decl   := "RECORD" Ident "{" field-list "}"
type-decl     := "TYPE" Ident "=" type
const-decl    := "CONST" Ident ":" type "=" expr

field         := doc-comment? Ident ":" type

type          := "string" | "int" | "float" | "bool" | "unit"
               | "array" "[" type "]"
               | "Option" "[" type "]"
               | "record"
               | "function" "(" type-list? ")" ":" type
               | string-literal-union
               | Ident

string-literal-union := string-lit ("|" string-lit)+

func-decl     := "FUNC" Ident type-params? "(" param-list? ")" (":" type)? newline
                 body
                 "END"
type-params   := "[" Ident ("," Ident)* "]"
param         := Ident (":" type)?

macro-decl    := "DEF-SYNTAX" Ident "(" macro-param-list ")" "DO" newline
                 body
                 "END"

cmd-macro-decl := "DEF-SYNTAX-CMD" Ident "(" macro-param-list ")"
                  cmd-macro-modifiers
                  "DO" newline
                  body
                  "END"
cmd-macro-modifiers := ("AS-BINDING" "(" param ")"
                      | "AS-OPTION-BINDING" "(" param ")"
                      | "OR-CLAUSE"
                      | "BLOCK" "(" Ident ")"
                      | "INTRODUCES" "(" param-list ")")*

statement     := let-stmt
               | var-stmt
               | assign-stmt
               | if-stmt
               | while-stmt
               | for-stmt
               | match-stmt
               | try-stmt
               | return-stmt
               | break-stmt
               | continue-stmt
               | expr-stmt
               | cmd-call

let-stmt      := "LET" Ident (":" type)? "=" expr
var-stmt      := "VAR" Ident (":" type)? "=" expr
assign-stmt   := Ident "=" expr

cmd-call      := Ident cmd-args as-binding? or-clause? block-arg?
cmd-args      := arg-token+
arg-token     := literal | Ident | "${" expr "}"
as-binding    := "AS" Ident (":" type)?
or-clause     := "OR" ("FAIL" string-expr code-clause? details-clause? | "DEFAULT" expr)
block-arg     := "DO" newline body "END"

if-stmt       := "IF" expr "THEN" newline body ("ELSE" newline body)? "END"
match-stmt    := "MATCH" expr "OF" newline match-case+ "END"
match-case    := "CASE" pattern "THEN" newline body
pattern       := literal | Ident | "_"
               | "Some" "(" pattern ")"
               | "None"
               | Ident "{" field-pat ("," field-pat)* "}"

while-stmt    := "WHILE" expr "DO" newline body "END"
for-stmt      := "FOR" (Ident | tuple-bind) "IN" expr "DO" newline body "END"
tuple-bind    := "(" Ident "," Ident ")"

try-stmt      := "TRY" newline body
                 "CATCH" Ident (":" type)? newline body
                 ("FINALLY" newline body)?
                 "END"

expr          := or-expr
or-expr       := and-expr ("OR" and-expr)*
and-expr      := not-expr ("AND" not-expr)*
not-expr      := "NOT" not-expr | compare-expr
compare-expr  := add-expr (compare-op add-expr)?
compare-op    := "==" | "!=" | "<" | "<=" | ">" | ">="
               | "CONTAINS" | "STARTSWITH" | "ENDSWITH"
               | "IS" ("SOME" | "NONE")
add-expr      := mul-expr (("+" | "-") mul-expr)*
mul-expr      := unary-expr (("*" | "/" | "%") unary-expr)*
unary-expr    := "-" unary-expr | postfix-expr
postfix-expr  := primary-expr ( "." Ident
                              | "[" expr "]"
                              | "(" arg-list? ")" )*
primary-expr  := literal
               | Ident
               | "(" expr ")"
               | "Some" "(" expr ")"
               | "None"
               | record-construct
               | array-literal
               | template-literal

record-construct := Ident "{" field-init ("," field-init)* ","? "}"
                  | Ident "WITH" "{" field-init ("," field-init)* ","? "}"
field-init    := Ident ":" expr
array-literal := "[" (expr ("," expr)*)? "]"
template-literal := "`" (text-segment | "${" expr "}")* "`"

literal       := int-lit | float-lit | string-lit | bool-lit
bool-lit      := "true" | "false"
```

---

## 付録B エラーメッセージ仕様

エラーメッセージは2チャンネルで出力する:

- **チャンネルA**: 構造化 JSON (AI 向け / プログラマブル)
- **チャンネルB**: テキスト整形 (人間向け)

詳細は `slasher-numadora-integration.md` の付録 B を参照
(エラーメッセージ仕様は Slasher と Numadora で共通)。

エラーコード分類:

| カテゴリ | プレフィックス | 例 |
|---|---|---|
| 構文エラー | `syntax_` | `syntax_unexpected_token`, `syntax_block_not_closed` |
| 型エラー | `type_` | `type_mismatch`, `type_unknown_field` |
| 名前解決 | `name_` | `name_undefined_variable`, `name_duplicate_definition` |
| 検査警告 | (warning として) | `unreachable_case`, `unused_import` |
| 実行時エラー (操作系) | (動詞)_ | `element_not_found` (Slasher), `file_not_found` |
| 実行時エラー (アサート) | `assertion_` | `assertion_failed` |
| 実行時エラー (システム) | `runtime_` | `runtime_option_unwrap_none`, `runtime_index_out_of_bounds` |
| ユーザー定義 | `user_` | `user_fail`, `user_<custom>` |

完全なエラーカタログは統合仕様書を参照。

---

## 付録C 残タスク

1. **REPL の言語拡張対応** — 型推論を REPL でどう見せるか
2. **`numac fmt` のフォーマット規則**
3. **`numac doc` の Markdown 出力** (`---` から型情報込み)
4. **ベンチマーク基準** — 各検査の時間目標
5. **国際化** — エラーメッセージ多言語対応の基盤

---

## 改訂履歴

- v1.0 (2026-04-29) — 初版起草。Slasher v2 設計書 v1.3 をベースに、Numadora 言語仕様として再編成。
