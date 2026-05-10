# Numadora 言語仕様 v0.2

> Status: 言語仕様。v0.2 は `numadora-language-redesign.md` 8 章の改訂方針を反映した正式版。
> 実装は Slasher 内蔵 C# インタプリタが提供する。
>
> 関連:
>
> - `numadora-language-redesign.md` — v0.1 → v0.2 の改訂方針と背景
> - `numadora-base-structure.md` — 字句/意味の詳細設計ノート
> - `numadora-core-systems.md` — 型/モジュール/実行モデルの詳細設計ノート
> - `slasher-numadora-integration.md` — Slasher ホスト統合 (改訂対象)
> - `slasher-script.md` — Slasher スクリプト プロファイル (改訂対象)

Numadora は汎用プログラミング言語。OS のアプリケーション、ウィンドウ、入力、ファイル、
ブラウザなどを統一的に扱えるホスト能力を持ちつつ、それ自体は特定 OS や特定ツールに
縛られない核を持つことを目標にする。

このドキュメントは Numadora の **言語仕様** (汎用部分) を定める。Slasher ホストとの
統合は `slasher-numadora-integration.md`、現在の Slasher プロファイルは
`slasher-script.md`、Slasher v1 (`.slasher`) からの移行は `migration-from-slasher-v1.md` を参照。

---

## 目次

- [第0章 設計の原則](#第0章-設計の原則)
- [第1章 字句構造](#第1章-字句構造)
- [第2章 型システム](#第2章-型システム)
- [第3章 式と文](#第3章-式と文)
- [第4章 エラーモデル](#第4章-エラーモデル)
- [第5章 match と網羅性検査](#第5章-match-と網羅性検査)
- [第6章 module と import](#第6章-module-と-import)
- [第7章 配列とコレクション](#第7章-配列とコレクション)
- [第8章 標準ライブラリ](#第8章-標準ライブラリ)
- [第9章 ホスト バインディング](#第9章-ホスト-バインディング)
- [付録A 文法 EBNF](#付録a-文法-ebnf)
- [付録B エラーメッセージ仕様](#付録b-エラーメッセージ仕様)
- [付録C 残タスク](#付録c-残タスク)

---

## 第0章 設計の原則

### 原則1: 静的検査を段階的に強化する (gradual typing)

型注釈を書かないコードは右辺式から推論される。書いた瞬間だけ静的検査が強くなる。
プロトタイピングは最小限の注釈で、本格運用は型付きで安全に、を両立する。

### 原則2: AI エージェント可読性は一級市民

Numadora は AI エージェントから生成・編集される頻度が高いことを前提に設計する。
エラーメッセージの構造化、AST の透明性、明示的なシグネチャ、単純な構文規則、
これらすべてが AI から扱いやすい形になっている必要がある。

### 原則3: 外部アプリケーション制御を一級の応用領域にする

Numadora は汎用言語でありながら、外部アプリケーション (OS native ウィンドウ、
ブラウザ、Excel、GIMP 等) を統一的に扱えるホスト能力を持つ。
これらはすべて Numadora の型・モジュール・エラーモデルに乗ったホスト関数として
提供され、特殊構文を持たない。

### 原則4: 拡張は関数とブロックで行い、マクロは持たない

Numadora は **マクロ機構を持たない**。新しい構文を導入する力は意図的に放棄し、
代わりに関数 + トレーリング ブロック (`DO |x| ... END`) で DSL 風表現を実現する。
これにより AI 生成・読解の予測性が安定する。

### 原則5: シンプルな核 + 強力なライブラリ

言語コアは小さく保つ (Pascal/ML 系の素直な構造)。複雑な機能は標準ライブラリと
ホスト バインディングで提供する。コア仕様の変更は慎重に、ライブラリは積極的に拡張する。

---

## 第1章 字句構造

### 1.1 文字集合とエンコーディング

- ソースファイル `.numa` / `.numai` は **UTF-8** でエンコードされる。BOM は許容するが推奨しない。
- 改行は LF / CRLF / CR のいずれも受理。**意味は同一**。
- タブは空白として扱う (4 列とは規定しない)。
- 制御文字 (タブ・改行を除く) はソース中に現れてはならない。

### 1.2 トークン分類

| カテゴリ | 例 |
|---|---|
| キーワード | `LET`, `VAR`, `CONST`, `FUNC`, `IF`, `THEN`, `ELSE`, ... |
| 識別子 | `wait-for-title`, `count`, `WindowRef` |
| リテラル | `42`, `3.14`, `"hello"`, `r"C:\path"`, `` `${x}` ``, `true`, `false`, `()` |
| 演算子 | `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=` |
| 区切り | `(`, `)`, `[`, `]`, `{`, `}`, `,`, `:`, `=`, `.`, `\|` |
| テンプレート区切り | `` ` ``, `${`, `}` |
| コメント | `# ...`, `--- ...` |

字句解析は **最長一致 (longest match)** で行う。

### 1.3 識別子と命名規則

#### 1.3.1 文法

```ebnf
identifier   := id-start id-continue*
id-start     := lowercase-letter | uppercase-letter | "_"
id-continue  := id-start | digit | "-"
```

ASCII 限定 (`a..z`, `A..Z`, `0..9`, `_`, `-`)。Unicode 識別子は採用しない。

#### 1.3.2 推奨命名規約

| 種類 | 規約 | 例 |
|---|---|---|
| 関数 | kebab-case | `wait-for-title`, `start-app` |
| 変数 (LET/VAR/パラメータ) | kebab-case | `wait-time`, `timeout-ms` |
| `CONST` | UPPER-KEBAB-CASE | `MAX-RETRIES` |
| `RECORD` 型 / `OPAQUE TYPE` / `TYPE` 別名 | UpperCamelCase | `Window`, `WindowRef`, `WindowState` |
| モジュール (パス要素) | kebab-case | `slasher/window`, `string-utils` |

#### 1.3.3 識別子と二項 `-` の曖昧性解決

kebab-case と二項マイナスは **空白の前後一致** で解決する:

| ソース表記 | 解釈 |
|---|---|
| `a-b` | 識別子 1 個 |
| `a - b` | 二項減算 |
| `a -b` | 字句エラー (`syntax_ambiguous_minus`) |
| `a- b` | 字句エラー (`syntax_ambiguous_minus`) |
| `-a` | 単項マイナス + 識別子 (前トークンが演算子・区切り・キーワードのとき) |

数値リテラル中のマイナスは別扱い: `1e-10` はリテラルの一部。

### 1.4 キーワード予約語

```text
LET     VAR     CONST   FUNC    RETURN
IMPORT  AS      MODULE  EXPORT  OPAQUE  TYPE
RECORD  WITH
IF      THEN    ELSE    END
WHILE   DO      FOR     IN
BREAK   CONTINUE
MATCH   OF      CASE
TRY     CATCH   FINALLY
AND     OR      NOT     IS      SOME    NONE
CONTAINS  STARTSWITH  ENDSWITH
EFFECT  INTERACTIVE  REQUIRES
true    false   unit
```

将来予約 (識別子として禁止):

```text
ASYNC   AWAIT   YIELD   DEFER
TRAIT   IMPL    HANDLE
```

### 1.4.1 能力クラス識別子 (capability namespace)

**コンテキスト認識**: `EFFECT(...)` と `REQUIRES(...)` の括弧内のみ能力クラスとして
解釈される。括弧外では通常の識別子として扱う (例: `LET observe = ...` は合法、Linter
警告対象)。

能力クラスは **閉集合** (13 種、ユーザ定義不可、`security-policy.md` 由来):

```text
observe          file-read         file-write       destructive
user-input       browser-data      clipboard        process-app
network-out      network-in        peer-delegate    secrets
unattended       scheduling        system-info
```

| クラス | 対象例 |
|---|---|
| `observe` | ウィンドウ列挙、画面キャプチャ、要素読み取り、ログ記録 |
| `file-read` | ファイル/ディレクトリ読み取り |
| `file-write` | ファイル書き込み (上書きは destructive 併記) |
| `destructive` | 削除、上書き、close-all |
| `user-input` | キーボード/マウス入力、ウィンドウ操作、ダイアログ表示 |
| `browser-data` | ブラウザ クッキー/ストレージ、アップロード、ダウンロード |
| `clipboard` | クリップボード読み書き |
| `process-app` | プロセス起動/終了 |
| `network-out` | アウトバウンドの HTTP / ピア呼び出し |
| `network-in` | 着信受付 (通常スクリプト側では使わない) |
| `peer-delegate` | 他ピアへの run 委譲 |
| `secrets` | 秘密値アクセス |
| `unattended` | 無人実行 |
| `scheduling` | 定時/繰り返し実行 |
| `system-info` | 時刻、CWD、環境変数の軽い読み取り |

詳細は `security-policy.md` および `numadora-security-network-design.md`。

予約語は **大文字小文字を区別** する。`let`, `if` はただの識別子。

### 1.5 コメント

| 形式 | 用途 |
|---|---|
| `# ...` 〜行末 | 通常のコメント |
| `--- ...` 〜行末 | ドキュメンテーション コメント (直後の宣言・フィールドに付与) |

ブロックコメントは採用しない。

### 1.6 空白と改行

#### 1.6.1 トークン区切り

空白文字 (空白・タブ) は **トークン分離以外の意味を持たない**。インデントは無意味。

#### 1.6.2 改行

改行は **文の区切り**。セミコロン (`;`) は採用しない。1 行 1 文を強制する。

#### 1.6.3 行継続

- **括弧内自動継続**: `(`, `[`, `{`, `${` の中では改行は意味を持たない
- **行頭演算子接続**: 行頭が二項演算子 (`+`, `-`, `*`, `/`, `OR`, `AND`, `OR FAIL`, `OR DEFAULT` 等) の場合、前行と接続する

```numadora
LET total = a
          + b
          + c

LET ok = window.find("OK")
       OR FAIL "OK が見つからない"
```

### 1.7 整数リテラル

```ebnf
int-lit  := dec-int | hex-int | bin-int
dec-int  := digit ("_"? digit)*
hex-int  := "0x" hex-digit ("_"? hex-digit)*
bin-int  := "0b" ("0" | "1") ("_"? ("0" | "1"))*
```

- 10/16/2 進、`_` 桁区切り
- 8 進 (`0o`) は採用しない
- 値域: 64-bit signed
- リテラル単独で値域超は `syntax_int_overflow`
- 単項マイナスは演算子 (`-42` はリテラル `42` + 単項 `-`)

### 1.8 浮動小数点リテラル

```ebnf
float-lit := dec-int "." dec-int (exp-part)?
           | dec-int exp-part
exp-part  := ("e" | "E") ("+" | "-")? dec-int
```

- IEEE 754 double precision
- `inf`/`nan` リテラルは採用しない (`std/math` 経由で得る)

### 1.9 文字列リテラル

#### 1.9.1 通常の文字列

```ebnf
string-lit := '"' char* '"'
char       := normal-char | escape
escape     := "\\" ("n" | "r" | "t" | "\\" | "\"" | "'" | "0" | "u{" hex+ "}")
```

- ダブルクォート区切り
- 改行 (素のまま) を含むことはできない (`\n` で表す)
- エスケープ: `\n`, `\r`, `\t`, `\\`, `\"`, `\'`, `\0`, `\u{XXXX}`
- 不正なエスケープは `syntax_invalid_escape`
- 文字型は存在せず、1 文字も `string` で扱う

#### 1.9.2 raw 文字列リテラル

```ebnf
raw-string-lit := "r" '"' raw-char* '"'
raw-char       := any-char-except-double-quote
```

- 接頭辞 `r` は **`"` 直前のみ** で意味を持つ
- 内側のエスケープは無効: `\n`, `\\`, `\"` はそのまま 2 文字
- 内側に `"` を含めるには raw 文字列を分割: `r"a" + r"b"`
- 改行を含むことはできない (1 行 raw のみ)
- 複数行 raw `r"""..."""` は将来検討

```numadora
LET path = r"C:\Users\foo\bar.txt"
LET regex = r"\d{4}-\d{2}-\d{2}"
```

### 1.10 テンプレート リテラル

```ebnf
template-lit     := "`" template-segment* "`"
template-segment := text-segment | "${" expr "}"
```

- バッククオート区切り
- `${expr}` 内は **純粋な式** のみ (副作用関数禁止)
- ネスト不可 (テンプレート内テンプレート禁止)
- 改行を含めてよい (複数行 OK)
- `` \` `` でバッククオート、`\$` で `$` をエスケープ

```numadora
LET msg = `title is ${win.title}`
LET multi = `line 1
line 2 with ${x}`
```

### 1.11 真偽値・unit リテラル

- 真偽値: `true`, `false` (lowercase, 予約語)
- unit: `()` または `unit`

### 1.12 配列リテラル

```ebnf
array-lit := "[" (expr ("," expr)* ","?)? "]"
```

空配列 `[]` は型注釈必須: `LET a: array[int] = []`。

### 1.13 字句エラーコード

| コード | 意味 |
|---|---|
| `syntax_invalid_character` | 不正な文字 |
| `syntax_unterminated_string` | 文字列が閉じていない |
| `syntax_invalid_raw_string` | raw 文字列が閉じていない or 改行を含む |
| `syntax_unterminated_template` | テンプレートが閉じていない |
| `syntax_invalid_escape` | 不正なエスケープ |
| `syntax_invalid_int_literal` | 不正な整数リテラル |
| `syntax_invalid_float_literal` | 不正な浮動小数点リテラル |
| `syntax_int_overflow` | 整数リテラル値域超 |
| `syntax_ambiguous_minus` | `-` の前後空白不一致 |
| `syntax_unexpected_comment` | コメント位置不正 |
| `syntax_reserved_word` | 予約語を識別子として使用 |

---

## 第2章 型システム

### 2.1 名前空間

Numadora は型・関数・変数の 3 つを独立した名前空間に分ける。同一空間内では衝突禁止。
構文上は共存可能だが、可読性のため Linter で警告。

### 2.2 型の階層

```text
type
├── primitive
│   ├── int       (64bit signed)
│   ├── float     (IEEE 754 double)
│   ├── bool
│   ├── string    (UTF-8)
│   └── unit
├── composite
│   ├── Option[T]
│   ├── array[T]
│   ├── record (匿名)
│   └── RECORD-name (名前付き、構造的サブタイピング)
├── opaque
│   └── OPAQUE-name (.numai でのみ宣言、名目的)
├── function
│   └── function(T1, T2, ..., Tn): R
└── string-literal-union
    └── "a" | "b" | "c"
```

### 2.3 基本型

| 型 | 説明 |
|---|---|
| `string` | UTF-8 文字列 |
| `int` | 64bit signed 整数 |
| `float` | 64bit 浮動小数点数 |
| `bool` | `true` / `false` |
| `unit` | 値を持たない |
| `Option[T]` | `Some(t)` / `None` |
| `array[T]` | 同種要素の動的配列 |
| `record` | 匿名レコード (構造的) |
| 名前付きレコード型 | `RECORD` で宣言 |
| 不透明型 | `OPAQUE TYPE` で宣言 (.numai のみ) |
| string-literal union | `"a" | "b" | "c"` 形式 |

### 2.4 レコード型

```numadora
RECORD Window {
  --- ウィンドウ タイトル。空文字列もありうる。
  title: string,
  handle: int,
  className: string,
  isVisible: bool,
}
```

- フィールド型は他の任意型 (含むレコード/不透明型)
- 自己/相互参照を許可 (`RECORD Element { children: array[Element] }`)
- フィールドのデフォルト値はなし
- ドキュメンテーション コメントは `---` で各フィールドに付けられる
- **フィールドは読み取り専用**: `w.title = "..."` は `name_record_field_readonly`

#### 2.4.1 構造的サブタイピング

```numadora
RECORD Window { title: string, handle: int }
RECORD TitledThing { title: string }

LET t: TitledThing = win    # OK: Window は TitledThing のフィールドをすべて持つ
```

`A` が `B` のサブタイプ ⇔ `A` のフィールド集合が `B` を包含し、対応フィールド型が
互いにサブタイプ関係。

#### 2.4.2 レコードの生成と更新

```numadora
LET w: Window = Window {
  title: "Notepad",
  handle: 0x123,
  className: "Notepad",
  isVisible: true,
}

LET w2 = w WITH { title: "Notepad - edited" }
```

すべてのフィールドを必須で書く。順不同。`WITH` は不変更新で新値を生成する。

### 2.5 不透明型 (`OPAQUE TYPE`)

ホスト リソース (Win32 handle、ブラウザ session 等) を表現する型。

```numadora
# slasher/window.numai
EXPORT OPAQUE TYPE WindowRef
```

| 性質 | 内容 |
|---|---|
| 宣言場所 | `.numai` ファイルのみ (`.numa` 本体では宣言不可) |
| 内部表現 | 隠蔽 (host 管理) |
| フィールドアクセス | 不可 (`win.handle` は `type_opaque_field_access`) |
| パターン マッチ | 不可 (`type_opaque_destructure`) |
| 構造的サブタイピング | 対象外 (名目的) |
| 等価性 (`==`) | ホスト定義 (典型的にはアイデンティティ) |
| 構築 | ホスト関数の戻り値経由のみ |

不透明型と観測値レコードはペアで使う:

```numadora
EXPORT OPAQUE TYPE WindowRef
EXPORT RECORD WindowInfo {
  title: string,
  handle: int,
  state: "normal" | "minimized" | "maximized",
}
EXPORT EFFECT(observe) FUNC info(target: WindowRef): WindowInfo
```

### 2.6 Option[T]

Numadora には `nil` がない。値の有無は `Option[T]` で表す。

```numadora
EXPORT EFFECT(observe) FUNC find-helper(title: string): Option[Element]
```

#### 2.6.1 Option の取り出し

```numadora
# match
MATCH ok OF
CASE Some(elem) THEN element.click(elem)
CASE None       THEN std/io.print("OK 未発見")
END

# 後置 OR FAIL
LET ok = element.find("OK") OR FAIL "OK ボタン未発見"
LET ok = element.find("OK") OR FAIL "OK 未発見" CODE "ok_missing" DETAILS { searchedTitle: "OK" }

# 後置 OR DEFAULT
LET cancel = element.find("Cancel") OR DEFAULT default-element

# IS SOME / IS NONE
IF ok IS SOME THEN
  element.click(ok.value)
ELSE
  std/io.print("見つからない")
END
```

`.value` で None を unwrap すると **panic** (`runtime_option_unwrap_none`)。
`.value` の使用は静的検査で警告 (`OR FAIL` を推奨)。

採用しない構文: `?.` (optional chaining), `??` (null coalescing)。

### 2.7 string-literal union

```numadora
RECORD Window { state: "normal" | "minimized" | "maximized" }

TYPE WindowState = "normal" | "minimized" | "maximized"
```

- 各リテラルは `string` のサブタイプ
- 集合の包含関係でサブタイプ判定
- `MATCH` の網羅性検査対象

### 2.8 関数型

```numadora
function(int, int): int
function(): unit
function(WindowRef): WindowInfo
```

- 引数型と戻り値型の組み合わせで決まる
- **不変 (invariant)** で扱う
- **関数値の `==` 比較は型エラー** (`type_function_value_eq`)

### 2.9 等価性 (`==`, `!=`)

| 型 | 等価性の定義 |
|---|---|
| `int`, `float`, `bool`, `unit`, `string` | 値そのもの |
| `Option[T]` | 同バリアント、内部値も `==` |
| `array[T]` | 同長、各要素が `==` |
| レコード型 | 同型、各フィールドが `==`。**計算量 O(構造的サイズ) 保証** (実装は CoW 共有 fast-path 可) |
| 不透明型 | アイデンティティ (ホスト定義) |
| 関数型 | 比較不可 (`type_function_value_eq`) |

異種型の `==` は **型エラー** (`type_eq_mismatch`)。

`float` の NaN は `nan == nan` が **false** (IEEE 754)。`std/math.is-nan(x)` を使う。

### 2.10 type 別名

```numadora
TYPE WindowState = "normal" | "minimized" | "maximized"

RECORD Window { state: WindowState }
```

`TYPE` でレコード型や string-literal union への別名を定義可能。`EXPORT TYPE ...` で公開可。

### 2.11 const 宣言

```numadora
CONST MAX-RETRIES: int = 3
EXPORT CONST KNOWN-CODES: array[string] = ["element_not_found", "window_not_found"]
```

トップレベルの不変定数。**右辺は純粋でコンパイル時 (= モジュール初期化時) 評価可能**
でなければならない。

### 2.12 unit 型

```numadora
FUNC print-hello(): unit
  std/io.print("hello")
END
```

戻り値が無意味な関数で使う。値は `()` または `unit`。

### 2.13 型推論

Numadora は **関数境界で閉じた局所型推論** を採用する。

- 関数のシグネチャ (引数・戻り値) は完全注釈、または `unit` のみ省略可
- 関数本体内では `LET`/`VAR` の右辺型から左辺型を推論
- 関数呼び出しの結果型はシグネチャから直接決まる

全プログラム型推論 (Hindley-Milner) は採用しない。エラーが局所化され、AI 生成
コードでシグネチャが読み取れる。

### 2.14 ジェネリクス

ユーザ定義関数でも型パラメータを許可する (初版は **不変 (invariant)** のみ)。

```numadora
FUNC first-or-default[T](items: array[T], fallback: T): T
  IF std/array.is-empty(items) THEN
    RETURN fallback
  END
  RETURN items[0]
END
```

- `[T]` または `[T, U]` の角括弧
- 慣習: 大文字 1〜数文字 (`T`, `U`, `K`, `V`)
- 制約 (`T: Comparable`) は採用しない
- 呼び出し時に引数型から推論 (明示指定構文なし)
- 推論失敗は `type_inference_failure`

### 2.15 型エラー コード

| コード | 意味 |
|---|---|
| `type_mismatch` | 一般の型不一致 |
| `type_unknown_field` | レコードに存在しないフィールド |
| `type_missing_field` | レコード生成でフィールド漏れ |
| `type_field_type_mismatch` | フィールド型不一致 |
| `type_inference_failure` | 型推論不能 |
| `type_arity_mismatch` | 引数の数が合わない |
| `type_function_value_eq` | 関数値の `==` 比較 |
| `type_opaque_field_access` | 不透明型のフィールドアクセス |
| `type_opaque_destructure` | 不透明型のパターン分解 |
| `type_eq_mismatch` | `==` 両辺の型不一致 |
| `type_assignment_mismatch` | 代入時の型不一致 |
| `type_impure_in_expression` | 式中で不純関数呼び出し |
| `type_subtype_mismatch` | 構造的サブタイプの不適合 |

---

## 第3章 式と文

### 3.1 文と式の区別

Numadora は **式指向言語** だが、トップレベルでは文として実行される。

文 (statement):

- `LET name = expr` / `VAR name = expr` / `CONST name = expr` (CONST はトップレベルのみ)
- `name = expr` (再代入、`VAR` のみ)
- 関数呼び出し
- `IF ... END`, `WHILE ... END`, `FOR ... END`, `MATCH ... END`, `TRY ... END`
- `RETURN expr`
- `BREAK`, `CONTINUE`

式 (expression):

- リテラル、変数参照、フィールドアクセス、配列インデックス
- 算術・比較・論理演算
- 関数呼び出し (戻り値を持つ)
- `Some(expr)` / `None`
- レコード生成 / `WITH` 更新
- 配列リテラル、テンプレート リテラル

### 3.2 演算子と優先順位

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

- 比較は連鎖しない (`a < b < c` は文法エラー)
- 論理演算子は `AND`, `OR`, `NOT` (キーワード)
- 文字列連結は `+`
- 配列リテラルは `[ ... ]`

### 3.3 数値演算

| 演算 | 結果型 |
|---|---|
| `int OP int` (`+`, `-`, `*`, `/`, `%`) | `int` |
| `int OP float` または逆 | `float` (int を float に昇格) |
| `float OP float` | `float` |
| `int / int` | `int` (整数除算) |
| `int / 0` または `int % 0` | `runtime_division_by_zero` |
| `int OP int` がオーバーフロー | `runtime_int_overflow` |

### 3.4 文字列連結とテンプレート

```numadora
std/io.print("title is " + win.title)
std/io.print(`title is ${win.title}`)
```

`${expr}` 内は **純粋な式** のみ (副作用なし)。

### 3.5 関数とプロシージャ

```numadora
FUNC double(x: int): int
  RETURN x * 2
END

FUNC say-hello()                # 戻り値型省略 ⇒ unit
  std/io.print("hello")
END
```

戻り値型を省略すると `unit` を返す関数とみなす。

#### 3.5.1 純粋性の自動判定

関数 `f` が **純粋 (pure)** であるとは:

- ホスト関数 (`.numai` で `EFFECT` 修飾) を呼ばない
- 不純関数を呼ばない
- クロージャ越しに VAR を書き換えない

純粋関数のみ式の文脈で呼べる。不純関数は **文として** のみ呼べる。

```numadora
FUNC double(x: int): int      # 純粋
  RETURN x * 2
END

FUNC focus-and-id(w: WindowRef): int     # 不純 (window.focus はホスト呼び出し)
  window.focus(w)
  RETURN 1
END

LET d = double(5)              # OK
LET f = focus-and-id(w)        # type_impure_in_expression
focus-and-id(w)                # OK (文として)
```

純粋性は静的検査で固定点計算 (`O(n × max-call-depth)` で収束)。

#### 3.5.2 副作用なしの保証

- 不純な関数は式から呼べない
- 代入は式ではない (`x := y` のような代入式は不採用)
- インクリメント・デクリメント `++`/`--` は不採用

### 3.6 メソッド呼び出し糖衣 (UFCS)

`expr.func(args...)` は次のように解決される:

1. `expr` の型 `T` を求める
2. スコープ内の `func` のうち、第 1 引数の型が `T` (またはその構造的スーパータイプ) と一致するものを探す
3. 一致が 1 つなら `func(expr, args...)` に変換
4. 0 個または複数個なら `name_method_not_found` / `name_method_ambiguous`

```numadora
win.focus()                # ≡ focus(win) → window.focus(win)
app.wait-for-window("Notepad", 10000)
```

不透明型は名目的解決、レコードは構造的サブタイプを許容、ジェネリクスは型推論の一部。

### 3.7 制御構造

```numadora
IF condition THEN body ELSE body END
WHILE condition DO body END
FOR i IN 0..10 DO body END         # 半開区間 [0, 10)
FOR (i, item) IN items DO body END
BREAK
CONTINUE
```

- `WHILE` の最大反復数は 1000 (超過で `runtime_max_iteration`)
- `FOR i IN a..b` は半開区間 (`a > b` なら反復なし)
- 反復中の配列変更は `runtime_array_modified_during_iteration`

### 3.8 LET / VAR

```numadora
LET x = 1            # 不変
VAR y = 2            # 可変
y = 3                # OK
x = 5                # name_let_reassign
```

型注釈は省略可 (右辺から推論)。空配列は注釈必須。

### 3.9 トレーリング ブロック (関数値)

関数の最後の引数が **関数型** のとき、`DO ... END` で渡せる:

```ebnf
trailing-block := "DO" ("|" param-list "|")? newline body "END"
```

```numadora
# 標準ライブラリ例
EXPORT FUNC retry(times: int, backoff-ms: int, body: function(): unit): unit

# 呼び出し
retry(3, 500) DO
  input.text("hello")
END

# パラメータ付き
std/array.map(nums) DO |x|
  RETURN x * 2
END
```

ラムダの独立式形 (`FUNC(x) ... END` を式の中で書く) は採用しない。
トレーリング位置のみ匿名関数を許す。

### 3.10 クロージャ捕獲規則

トレーリング ブロックは字句スコープに閉じている = クロージャ。

| 外側変数 | 読み取り | 書き換え |
|---|---|---|
| `LET` | 可 | 元々不可 |
| `VAR` | 可 | **不可** (`name_closure_var_assign`) |
| 関数引数 | 可 | 不可 |

VAR 書き換えはクロージャ越しでは禁止。代替: ブロック戻り値で値を返して呼び出し側で更新、
または `std/array.reduce` 等の純粋集約。

### 3.11 評価順

- 関数引数: **左から右**
- 二項演算: **左から右**
- 短絡: `AND`, `OR`, `OR FAIL`, `OR DEFAULT` で右辺は条件次第
- 文の列: 書かれた順を保証
- 純粋な式の評価順は観測不可能 (実装は再順序化可)

### 3.12 スコープ

字句スコープ + ブロック スコープ。各 `IF`/`WHILE`/`FOR`/`MATCH` ケース/`TRY` 節/
トレーリング ブロックは新スコープを導入する。

`LET`/`VAR` の有効範囲は **宣言文の後** からブロック末尾まで。前方参照不可。
ただし `FUNC`, `RECORD`, `TYPE`, `CONST`, `IMPORT`, `OPAQUE TYPE` はモジュール全体で有効
(相互再帰のため)。

同一スコープ内の同名再宣言は禁止 (`name_duplicate_definition`)。別スコープではシャドウ可。

### 3.13 シングル スレッド前提

Numadora の実行モデルは **単一スレッド**。並行構造 (async/await/spawn) は採用しない。
`ASYNC`, `AWAIT` は将来予約語。

### 3.14 意味エラー コード (3 章で追加)

| コード | 意味 |
|---|---|
| `name_undefined_variable` | 未定義変数 |
| `name_undefined_function` | 未定義関数 |
| `name_undefined_module` | 未定義モジュール |
| `name_duplicate_definition` | 同一スコープでの重複宣言 |
| `name_let_reassign` | LET 変数への再代入 |
| `name_record_field_readonly` | レコードフィールドへの代入 |
| `name_let_array_immutable` | LET 配列の可変操作 |
| `name_closure_var_assign` | クロージャからの VAR 書き換え |
| `name_method_not_found` | UFCS 解決失敗 |
| `name_method_ambiguous` | UFCS 解決曖昧 |
| `runtime_division_by_zero` | ゼロ除算 |
| `runtime_int_overflow` | int オーバーフロー |
| `runtime_index_out_of_bounds` | 配列範囲外 |
| `runtime_option_unwrap_none` | `.value` で None |
| `runtime_max_iteration` | WHILE 反復数超過 |
| `runtime_array_modified_during_iteration` | 反復中の配列変更 |

---

## 第4章 エラーモデル

### 4.1 失敗の 3 分類

| 種別 | 表現方法 | catch 可否 |
|---|---|---|
| 期待される失敗 | `Option[T]` | (catch しない、値で表現) |
| 操作の失敗 | `RuntimeError` (例外) | TRY/CATCH 可 |
| プログラムの誤り | panic | **catch 不可、即終了** |

panic の対象: `runtime_option_unwrap_none`, `runtime_index_out_of_bounds`,
`runtime_division_by_zero`, `runtime_int_overflow`, `runtime_max_iteration`,
`runtime_array_modified_during_iteration` 等。

### 4.2 RuntimeError 型

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

### 4.3 try / catch / finally

```numadora
TRY
  window.focus(win)
  input.text("hello")
CATCH e: RuntimeError
  std/io.print("失敗: " + e.code + " - " + e.message)
FINALLY
  std/io.print("クリーンアップ")
END
```

- `CATCH e: Type` の型注釈は任意 (注釈ありで `e.code` 等のフィールドアクセスが静的検査される)
- `CATCH` 句は **1 つだけ**。エラー種別での分岐は内側で `MATCH e.code OF`
- `CATCH` 句なしの `TRY` は禁止

### 4.4 fail コマンド

```numadora
fail("OK ボタン未発見")
fail("OK ボタン未発見", "ok_not_found")
fail("OK ボタン未発見", "ok_not_found", { expected: "OK", actual: "Cancel" })
```

`fail` は標準ライブラリ関数 (`std/error.fail` または top-level shortcut)。
第 2 引数 `code` (省略時 `"user_fail"`)、第 3 引数 `details` (省略時 `{}`)。

### 4.5 例外の伝播

例外伝播時に `e.source.stack` に呼び出し元フレームが積まれる。
ホスト呼び出し境界は `function = Some("<host:slasher/window.focus>")` のように示す。

### 4.6 エラー詳細へのアクセス

```numadora
IMPORT std/error AS err

MATCH e.code OF
CASE "assertion_failed" THEN
  LET expected = err.detail-string(e, "expected")
  LET actual = err.detail-string(e, "actual")
END
```

`err.detail-string`, `detail-int`, `detail-float`, `detail-bool` は `Option[T]` を返す。

---

## 第5章 match と網羅性検査

### 5.1 値空間の分類

| 種別 | 例 | 網羅性可能か |
|---|---|---|
| 有限な型 | `bool`, `Option[T]`, string-literal union | 可能 |
| 無限な型 | `int`, `float`, `string` (素), `array[T]` | 不能 |
| 準有限な型 | レコード型 (フィールド分解) | 構造的には可能 |
| 不透明型 | 分解不可 (`type_opaque_destructure`) | 対象外 |

### 5.2 検査の 3 段階強度

| 被検査値の型 | 網羅性なしの扱い |
|---|---|
| `Option[T]` | **エラー** (`Some` / `None` 両方必須) |
| `bool` | **エラー** (`true` / `false` 両方必須) |
| string-literal union | **エラー** (列挙されたすべての値が必須) |
| `string` (素) | 警告 (`_` がないと警告) |
| `int`, `float` | 検査なし |
| レコード (構造分解のみ) | 検査なし |
| レコード (値分岐) | 警告 |
| `array[T]` | 検査なし |

到達不能ケースは別途 **警告**。

### 5.3 パターンの種類

```ebnf
pattern :=
  | literal-pattern         # "hello", 42, true, false
  | binding-pattern         # x
  | wildcard-pattern        # _
  | some-pattern            # Some(p)
  | none-pattern            # None
  | record-pattern          # Window { f1: p1, f2: p2 }
```

`record-pattern` ではフィールド省略を許す。

### 5.4 採用しない構文

- ガード句 (when 節) — 網羅性検査と相性が悪い、`IF` で書けばよい
- ADT (代数的データ型) — string-literal union とレコードで代替

`Option[T]` は特例として組み込み済み。

### 5.5 case の評価順

上から順に評価して最初にマッチした case を実行。**到達不能な case は警告**。

### 5.6 if と match の使い分け

| 場面 | 推奨 |
|---|---|
| bool 値の単純な分岐 | `IF` |
| 比較演算子による分岐 | `IF` |
| 1〜2 分岐の単純なロジック | `IF` |
| Option の `Some` / `None` 分岐 | `MATCH` |
| string-literal union の値分岐 | `MATCH` |
| 3 つ以上のケース | `MATCH` |
| フィールド分解で複数パターン | `MATCH` |

---

## 第6章 module と import

### 6.1 ファイルとモジュール

- 1 ファイル = 1 モジュール
- ファイル先頭で `MODULE path/to/name` を宣言する
- ファイルパスとモジュール名の不一致は **警告** (`module_path_mismatch`)、エラーではない
- `MODULE` 宣言は **ファイル先頭の唯一行** (コメントを除く先頭)

### 6.2 ファイル拡張子

| 拡張子 | 役割 |
|---|---|
| `.numa` | 通常の実装ファイル |
| `.numai` | インターフェイス宣言ファイル (本体なし、ホスト バインディング用) |

### 6.3 解決順序

`IMPORT path/to/m AS m` のとき:

1. プラグイン埋め込みリソース (Slasher の AppOps プラグイン由来) を最優先
2. ワークスペース ローカル `.numai`
3. ワークスペース ローカル `.numa`
4. `std/` プレフィックス → 標準ライブラリ
5. `slasher/` プレフィックス → Slasher ホスト バインディング (1. 経由)

両方存在する場合 (`.numai` と `.numa`):

- `.numai` のシグネチャを **公的インターフェイス** として採用
- `.numa` の宣言と `.numai` のシグネチャが一致するか検査
- 不一致は `module_interface_mismatch`

### 6.4 alias 必須

```numadora
IMPORT std/array          # name_import_alias_required
IMPORT std/array AS arr   # OK
```

理由: 名前衝突を呼び出し側で完全に制御、AI 生成時に呼び出し場所からモジュール識別可能。

### 6.5 EXPORT の規則

```text
EXPORT FUNC ...
EXPORT EFFECT(class) FUNC ...                 # 副作用ありホスト関数 (能力クラス必須)
EXPORT INTERACTIVE EFFECT(class) FUNC ...     # 対話的承認 + 能力クラス必須
EXPORT RECORD ...
EXPORT TYPE ...
EXPORT CONST ...
EXPORT OPAQUE TYPE ...           # .numai のみ
```

`MODULE`, `IMPORT` は EXPORT の対象ではない。

**プライベート型を EXPORT FUNC のシグネチャに含めることは禁止**
(`name_private_type_in_export_signature`)。

#### 6.5.1 EFFECT(class) 必須化

`EFFECT` には能力クラス (1.4.1 の 13 種から 1 つ以上) を必ず指定する。引数なしの
`EXPORT EFFECT FUNC` は構文エラー (`effect_class_required`)。複数能力は `,` 区切り:

```numadora
EXPORT EFFECT(observe) FUNC info(target: WindowRef): WindowInfo
EXPORT EFFECT(file-write, destructive) FUNC delete(path: string, allow-destructive: bool, dry-run: bool): array[string]
EXPORT EFFECT(network-out, peer-delegate) FUNC delegate-run(...)
```

未知のクラス名は `effect_class_unknown`。

#### 6.5.2 INTERACTIVE は EFFECT(class) 併記必須

INTERACTIVE は能力クラスに直交するメタ修飾 (ユーザ承認 = `allowInteractiveInput`)。
EFFECT(class) との併用が必須:

```numadora
EXPORT INTERACTIVE EFFECT(user-input) FUNC text(content: string): unit          # OK
EXPORT INTERACTIVE EFFECT(destructive) FUNC delete-window(target: WindowRef): unit  # OK
EXPORT INTERACTIVE EFFECT FUNC focus(target: WindowRef): unit                   # 構文エラー (能力クラスなし)
EXPORT INTERACTIVE FUNC something(): unit                                       # 構文エラー (`interactive_without_effect`)
```

### 6.5.3 script-requires 宣言 (REQUIRES)

実行可能スクリプト (main を持つモジュール) は使用する能力クラスを宣言する:

```ebnf
script-requires := "REQUIRES" "(" capability-list ")" newline
```

```numadora
MODULE notepad-check
REQUIRES (process-app, user-input, observe)

IMPORT slasher/app AS app
...

EXPORT FUNC main()
  ...
END
```

#### 配置と検査

- `MODULE` 宣言の **直後** に置く (任意。`IMPORT` より前)
- check 段階: スクリプト内および import 先で実際に使われている `EFFECT(class)` の集合を
  推移的に計算 → `REQUIRES` 集合に **含まれる** ことを検証
- 不足: `requires_missing_capability` (詳細は `details.missing` に列挙、起源情報も付与)
- 過多: `requires_unused_capability` (warning)
- ライブラリ モジュール (main を持たない) は `REQUIRES` を **持てない** (`requires_in_library`)

#### ランタイム連携

run 時に `REQUIRES` 集合と現在の **能力プロファイル** (`security-policy.md` の `observe` /
`interactive` / `destructive` 等) を突合し、含まれない能力があれば `policy_denied` で run 拒否。
これにより「実行前に静的に判断」が可能になる。

### 6.6 IMPORT のセマンティクス

- `IMPORT` 文は **モジュール初期化時** に評価される (一度だけ)
- 関数本体内に書くことはできない (ファイル先頭セクションのみ)

#### 6.6.1 将来余地: バージョン要件

将来構文 (本体は将来検討):

```numadora
IMPORT slasher/excel AS excel REQUIRES-VERSION >= 2.0
```

ホスト プラグインのバージョン要件を宣言する余地として残す (v1 では構文として受理せず)。
スクリプト レベルの `REQUIRES` (能力宣言) とは別物 (6.5.3 参照)。

### 6.7 モジュール初期化順

#### 6.7.1 グラフ構築

各モジュールの `IMPORT` 関係から有向グラフを作る。**循環は禁止** (`module_circular_import`)。

#### 6.7.2 トポロジカル順

依存先から順に初期化。同階層は import 順。

#### 6.7.3 初期化フェーズ

1. すべての `RECORD`, `TYPE`, `OPAQUE TYPE`, `EXPORT` 宣言を型空間に登録
2. `FUNC` 宣言を関数空間に登録 (本体は未評価)
3. `CONST` 宣言の式を上から順に評価
4. ホスト登録 (`.numai` の場合) を解決し、シグネチャ整合を検査

### 6.8 トップレベル副作用の禁止

`.numa` モジュールの **トップレベルでの副作用ありの式・文を禁止**。許される top-level 要素:

- `MODULE` 宣言
- `IMPORT` 文
- `RECORD`, `TYPE`, `CONST`, `FUNC`, `OPAQUE TYPE` 宣言
- `EXPORT` 修飾

`CONST` の右辺式は **純粋** で、コンパイル時 (= モジュール初期化時) に評価される。

### 6.9 モジュール エラーコード

| コード | 意味 |
|---|---|
| `module_not_found` | パスが解決できない |
| `module_circular_import` | 循環 import |
| `module_path_mismatch` (warning) | ファイルパスと `MODULE` 宣言の不一致 |
| `module_interface_mismatch` | `.numai` と `.numa` の不整合 |
| `module_host_not_registered` | `.numai` だけあってホスト登録なし |
| `name_import_alias_required` | `AS alias` 省略 |
| `name_import_alias_duplicate` | alias 重複 |
| `name_private_type_in_export_signature` | プライベート型を EXPORT 関数で使用 |
| `effect_class_required` | `EFFECT` に能力クラス指定なし |
| `effect_class_unknown` | 未知の能力クラス名 |
| `interactive_without_effect` | `INTERACTIVE` 単独使用 (EFFECT 併記必須) |
| `requires_missing_capability` | スクリプトが使う能力が REQUIRES に未宣言 |
| `requires_unused_capability` (warning) | REQUIRES に宣言したが未使用 |
| `requires_in_library` | ライブラリ モジュール (main なし) で REQUIRES 使用 |

---

## 第7章 配列とコレクション

### 7.1 array[T] 型

```numadora
LET nums: array[int] = [1, 2, 3]
LET titles: array[string] = ["a", "b"]
LET nested: array[array[int]] = [[1, 2], [3, 4]]
LET opts: array[Option[string]] = [Some("a"), None]
```

要素型 `T` は任意。要素型は単一 (異種要素配列はなし)。

### 7.2 構築

```numadora
LET items: array[string] = ["alpha", "beta", "gamma"]
LET empty: array[int] = []                   # 型注釈必須
LET doubled = std/array.map(nums, double)
```

### 7.3 読み取り

```numadora
LET first: string = items[0]              # 範囲外なら panic
LET safe = std/array.get(items, 0)        # Option[T] を返す
LET n: int = std/array.length(items)
```

| 構文 | 範囲外時 |
|---|---|
| `items[i]` | panic (`runtime_index_out_of_bounds`) |
| `std/array.get(items, i)` | `None` |

### 7.4 変更操作

```numadora
VAR items: array[string] = ["a", "b"]
std/array.push(items, "c")
LET last = std/array.pop(items)
std/array.insert(items, 0, "z")
LET removed = std/array.remove(items, 1)
std/array.clear(items)
```

`VAR` で宣言された配列のみ変更可。`LET` 配列の可変操作は `name_let_array_immutable`。

### 7.5 反復

```numadora
FOR item IN items DO ... END
FOR (i, item) IN items DO ... END
```

### 7.6 関数値とトレーリング ブロック

```numadora
FUNC double(x: int): int
  RETURN x * 2
END

LET d1 = std/array.map(nums, double)         # 名前付き関数を渡す
LET d2 = std/array.map(nums) DO |x|          # トレーリング ブロック
  RETURN x * 2
END
```

純粋関数のみ渡せる (副作用ありの関数引数は型エラー)。

### 7.7 文字列と配列

文字列は配列ではない別の型。変換は `std/string.split`, `std/string.chars` 等。

### 7.8 辞書と集合

辞書 (map) と集合 (set) は当面非対応。
必要なら `array[record { key: string, value: T }]` で代替。

---

## 第8章 標準ライブラリ

`std/` プレフィックスで提供。OS 非依存・純粋寄りに保つ (副作用ありホスト機能は
`slasher/` プラグイン側に置く)。

### 8.1 モジュール一覧

```text
std/array       - 配列操作 (純粋関数)
std/string      - 文字列操作 (純粋関数)
std/error       - RuntimeError、エラー詳細アクセス
std/io          - now、env、cwd、print 等の汎用 I/O
std/test        - assert、テスト記録
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
EXPORT EFFECT(observe) FUNC push[T](a: array[T], value: T): unit
EXPORT EFFECT(observe) FUNC pop[T](a: array[T]): Option[T]
EXPORT EFFECT(observe) FUNC insert[T](a: array[T], index: int, value: T): unit
EXPORT EFFECT(observe) FUNC remove[T](a: array[T], index: int): Option[T]
EXPORT EFFECT(observe) FUNC clear[T](a: array[T]): unit
```

VAR 配列の in-place 変更は副作用 → `EFFECT` 修飾。

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
EXPORT RECORD RuntimeError { ... }                 # 4.2 参照
EXPORT RECORD ErrorSource { ... }
EXPORT RECORD ErrorSourceFrame { ... }

EXPORT FUNC detail-string(e: RuntimeError, field: string): Option[string]
EXPORT FUNC detail-int(e: RuntimeError, field: string): Option[int]
EXPORT FUNC detail-float(e: RuntimeError, field: string): Option[float]
EXPORT FUNC detail-bool(e: RuntimeError, field: string): Option[bool]
EXPORT FUNC format(e: RuntimeError): string
EXPORT EFFECT(observe) FUNC fail(message: string): unit
EXPORT EFFECT(observe) FUNC fail-with(message: string, code: string, details: record): unit
```

### 8.5 std/io

```numadora
EXPORT EFFECT(system-info) FUNC now(): int                          # Unix ms
EXPORT EFFECT(system-info) FUNC env(name: string): Option[string]
EXPORT EFFECT(system-info) FUNC cwd(): string
EXPORT EFFECT(observe) FUNC print(s: string): unit
EXPORT EFFECT(observe) FUNC eprint(s: string): unit
```

### 8.6 std/test

```numadora
EXPORT RECORD AssertResult {
  passed: bool,
  expected: string,
  actual: string,
  message: Option[string],
}

EXPORT EFFECT(observe) FUNC equal[T](actual: T, expected: T): unit
EXPORT EFFECT(observe) FUNC not-equal[T](actual: T, expected: T): unit
EXPORT EFFECT(observe) FUNC is-true(condition: bool): unit
EXPORT EFFECT(observe) FUNC is-false(condition: bool): unit
EXPORT EFFECT(observe) FUNC is-some[T](opt: Option[T]): unit
EXPORT EFFECT(observe) FUNC is-none[T](opt: Option[T]): unit
EXPORT EFFECT(observe) FUNC contains(haystack: string, needle: string): unit
EXPORT EFFECT(observe) FUNC array-contains[T](a: array[T], value: T): unit

# ソフト アサート
EXPORT EFFECT(observe) FUNC soft-equal[T](actual: T, expected: T): unit
EXPORT EFFECT(observe) FUNC soft-is-true(condition: bool): unit

# テスト記録
EXPORT EFFECT(observe) FUNC note(message: string): unit
EXPORT EFFECT(observe) FUNC attach(path: string, role: string): unit
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

EXPORT EFFECT(network-out) FUNC get(url: string): Option[HttpResponse]
EXPORT EFFECT(network-out) FUNC post(url: string, body: string): Option[HttpResponse]
EXPORT EFFECT(network-out) FUNC post-json(url: string, body: any): Option[HttpResponse]
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

EXPORT EFFECT(file-read) FUNC info(path: string): Option[FileInfo]
EXPORT EFFECT(file-read) FUNC exists(path: string): bool
EXPORT EFFECT(file-read) FUNC read-text(path: string): string
EXPORT EFFECT(file-write) FUNC write-text(path: string, content: string): unit
EXPORT EFFECT(file-write) FUNC append-text(path: string, content: string): unit
```

### 8.10 std/process

```numadora
EXPORT RECORD ProcessResult {
  exitCode: int,
  stdout: string,
  stderr: string,
}

EXPORT EFFECT(process-app) FUNC run(command: string, args: array[string]): ProcessResult
EXPORT EFFECT(process-app) FUNC run-with-timeout(command: string, args: array[string], timeoutMs: int): Option[ProcessResult]
```

---

## 第9章 ホスト バインディング

ホスト バインディングは Numadora から外部世界 (OS, 外部アプリ, ブラウザ等) を呼ぶための
仕組み。Slasher の場合は AppOps プラグインが提供する。

### 9.1 .numai インターフェイス ファイル

`.numai` は **シグネチャ宣言のみ**。本体は持たず、関数の実装はホスト側
(C# / Rust など、言語仕様外) に登録される。

```numadora
# slasher/window.numai
MODULE slasher/window

EXPORT OPAQUE TYPE WindowRef
EXPORT TYPE WindowState = "normal" | "minimized" | "maximized"

EXPORT RECORD WindowInfo {
  title: string,
  handle: int,
  state: WindowState,
}

EXPORT EFFECT(observe) FUNC find(title: string): Option[WindowRef]
EXPORT EFFECT(observe) FUNC info(target: WindowRef): WindowInfo
EXPORT INTERACTIVE EFFECT(user-input) FUNC focus(target: WindowRef): unit
EXPORT INTERACTIVE EFFECT(user-input) FUNC set-state(target: WindowRef, state: WindowState): unit
EXPORT EFFECT(observe) FUNC capture(target: WindowRef, max-w: int, max-h: int): string
EXPORT INTERACTIVE EFFECT(user-input) FUNC close(target: WindowRef): unit
```

### 9.2 EFFECT(class) 修飾子

`EXPORT EFFECT(class) FUNC` は **副作用あり** (ホスト世界に対して観測可能な変更を加える、
または環境状態に依存して結果が変わる) のホスト関数を示す。能力クラス (1.4.1 の 13 種から
1 つ以上) を **必ず指定** する:

- `EFFECT` 修飾された関数は **不純** とみなされ、純粋関数の式中から呼べない (`type_impure_in_expression`)
- 能力クラスの省略は構文エラー (`effect_class_required`)
- 未知のクラス名は `effect_class_unknown`
- 複数能力は `,` 区切り: `EFFECT(file-write, destructive)`
- `EFFECT` のないホスト関数は純粋ヘルパとして許容 (例: 純粋計算のみのフォーマッタ、座標計算)

#### 9.2.1 利用側への波及

スクリプト (main を持つモジュール) は `REQUIRES (...)` で使用能力を宣言する (6.5.3)。
ホスト関数の `EFFECT(class)` 集合が `REQUIRES` の集合に含まれている必要がある。

#### 9.2.2 主要な能力クラス対応 (典型例)

| 関数のカテゴリ | 典型的な EFFECT(class) |
|---|---|
| ウィンドウ/要素/画面の **読み取り** | `EFFECT(observe)` |
| キーボード/マウス/ウィンドウ操作 | `INTERACTIVE EFFECT(user-input)` |
| ファイル読み取り | `EFFECT(file-read)` |
| ファイル書き込み (上書きなし) | `EFFECT(file-write)` |
| ファイル削除/上書き | `EFFECT(file-write, destructive)` |
| プロセス起動/終了 | `INTERACTIVE EFFECT(process-app)` |
| クリップボード読み | `EFFECT(clipboard)` |
| クリップボード書き/クリア | `INTERACTIVE EFFECT(clipboard)` |
| HTTP 送信 | `EFFECT(network-out)` |
| ピア委譲 | `INTERACTIVE EFFECT(network-out, peer-delegate)` |
| 時刻/CWD/環境変数 | `EFFECT(system-info)` |

### 9.3 INTERACTIVE 修飾子

`INTERACTIVE` は **ユーザの明示承認が必要** であることを示すメタ修飾。
能力クラスとは **直交** し、必ず `EFFECT(class)` と併記する:

- `INTERACTIVE EFFECT(class) FUNC ...` ✅ 推奨形
- `INTERACTIVE EFFECT FUNC ...` ❌ 構文エラー (`effect_class_required`)
- `INTERACTIVE FUNC ...` ❌ 構文エラー (`interactive_without_effect`)

Slasher 統合での挙動:

- run 時に `allowInteractiveInput` フラグなしで呼ばれた場合、ポリシー判定で
  `policy_denied` の `RuntimeError` を投げる
- 承認済でも入力送信直前にフォアグラウンド ターゲットを再検証し、変わっていれば
  fail closed (`policy_target_changed`)
- check モードでは構文・型のみ検査し、実行はしない

### 9.4 不透明型の扱い

`OPAQUE TYPE` は `.numai` でのみ宣言可能。利用側 `.numa` 本体ファイルでは:

- 不透明値はホスト関数の戻り値として得る
- フィールドアクセス禁止 (`type_opaque_field_access`)
- パターン分解禁止 (`type_opaque_destructure`)
- `==` はホスト定義 (典型的にはアイデンティティ等価)

リソース解放は GC finalizer + 明示 close 関数の併用 (host 側の責任)。

### 9.5 メソッド呼び出し糖衣との整合

UFCS (3.6) はホスト関数にも適用される:

```numadora
LET win = window.find("Notepad") OR FAIL "no window"
win.focus()                # ≡ focus(win) → window.focus(win)
LET info = win.info()      # ≡ info(win) → window.info(win)
```

第 1 引数の不透明型でディスパッチが解決される。

### 9.6 ホスト例外の正規化

ホスト側 (C# 等) で発生した例外は Numadora `RuntimeError` に正規化される。

| ホスト側 | Numadora `code` |
|---|---|
| 引数バリデーション失敗 | `host_invalid_argument` |
| タイムアウト | `host_timeout` |
| アクセス拒否 | `host_access_denied` |
| I/O エラー | `host_io_error` |
| キャンセル | `host_cancelled` |
| プラットフォーム未対応 | `platform_not_supported` |
| 未分類 | `host_unknown_error` (`details.exceptionType` に格納) |

ポリシー関連:

| ケース | code |
|---|---|
| 能力プロファイル不適合 | `policy_denied` |
| INTERACTIVE 関数が未承認 | `policy_denied` (`details.reason = "interactive_unapproved"`) |
| INTERACTIVE 入力送信前のターゲット変化 | `policy_target_changed` |
| 委譲経由 run からの再帰委譲試行 | `policy_recursive_delegation` |

### 9.6.1 ピア委譲 (slasher/peer)

ピア間通信は `slasher/peer` モジュールを通じて言語の一級概念として扱う。
`PeerRef` 不透明型、`TrustProfile` 列挙型、`namespace-list` / `delegate-run` 等の
関数を提供 (詳細は `numadora-security-network-design.md` 2.4 参照)。

委譲経由で実行された run は **再帰的な `delegate-run` を禁止**:

- 各 run コンテキストに `delegation-depth: int` を記録 (初回 = 0、委譲経由 = 1)
- `delegation-depth >= 1` の run が `delegate-run` を呼ぶと `policy_recursive_delegation`
- run artifact に経路 (`delegated-from: peer1 -> peer2`) を記録、監査可能

### 9.7 ホスト呼び出しの意味論

- すべて **同期 ブロッキング**
- タイムアウトは関数引数で渡す (言語レベルのタイムアウト機構なし)
- キャンセル機構なし
- Numadora 側スレッドは応答を待つ (シングル スレッド)

### 9.8 エントリ ポイント

実行時の主モジュールは `EXPORT FUNC main(): unit` を持たなければならない:

```numadora
MODULE my-script
IMPORT slasher/app AS app
IMPORT slasher/io AS io

EXPORT FUNC main()
  io.step("hello")
  LET a = app.start-app("notepad.exe")
  ...
END
```

- `main` の引数は不可 (将来的に `args: array[string]` 検討)
- 戻り値は `unit`

エラー: `runtime_no_main`, `runtime_main_invalid_signature`。

### 9.9 ホスト関連エラー コード

| コード | 意味 |
|---|---|
| `policy_denied` | ポリシーで拒否 (能力不適合 / INTERACTIVE 未承認 等) |
| `policy_target_changed` | INTERACTIVE 入力送信前にフォアグラウンド ターゲットが変化 |
| `policy_recursive_delegation` | 委譲経由 run からの再帰委譲試行 |
| `host_invalid_argument` | ホスト引数不正 |
| `host_timeout` | ホスト タイムアウト |
| `host_access_denied` | ホスト アクセス拒否 |
| `host_io_error` | ホスト I/O エラー |
| `host_cancelled` | ホスト キャンセル |
| `host_unknown_error` | 未分類 |
| `platform_not_supported` | プラットフォーム未対応 |
| `runtime_no_main` | main 関数なし |
| `runtime_main_invalid_signature` | main のシグネチャ不正 |

---

## 付録A 文法 EBNF

```ebnf
program       := top-level*

top-level     := module-decl
               | script-requires       # main を持つモジュールのみ
               | import-decl
               | record-decl
               | type-decl
               | opaque-type-decl
               | const-decl
               | func-decl

module-decl     := "MODULE" path newline
script-requires := "REQUIRES" "(" capability-list ")" newline
import-decl     := "IMPORT" path "AS" Ident newline
path            := Ident ("/" Ident)*

capability-list := capability ("," capability)*
capability      := "observe" | "file-read" | "file-write" | "destructive"
                 | "user-input" | "browser-data" | "clipboard" | "process-app"
                 | "network-out" | "network-in" | "peer-delegate" | "secrets"
                 | "unattended" | "scheduling" | "system-info"
# 能力クラスはコンテキスト認識: EFFECT(...) と REQUIRES(...) の括弧内のみ予約。

record-decl       := "EXPORT"? "RECORD" Ident "{" field-list "}"
type-decl         := "EXPORT"? "TYPE" Ident "=" type
opaque-type-decl  := "EXPORT" "OPAQUE" "TYPE" Ident                 # .numai のみ
const-decl        := "EXPORT"? "CONST" Ident ":" type "=" expr

field         := doc-comment? Ident ":" type

type          := "string" | "int" | "float" | "bool" | "unit"
               | "array" "[" type "]"
               | "Option" "[" type "]"
               | "record"
               | "function" "(" type-list? ")" ":" type
               | string-literal-union
               | Ident type-args?
type-args     := "[" type ("," type)* "]"
string-literal-union := string-lit ("|" string-lit)+

func-decl     := "EXPORT"? interactive? effect-clause? "FUNC" Ident type-params? "(" param-list? ")" (":" type)? newline
                 body
                 "END"
interactive   := "INTERACTIVE"           # EFFECT 併記必須 (interactive_without_effect)
effect-clause := "EFFECT" "(" capability-list ")"  # 能力クラス必須 (effect_class_required)
type-params   := "[" Ident ("," Ident)* "]"
param         := Ident (":" type)?

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

let-stmt      := "LET" Ident (":" type)? "=" expr
var-stmt      := "VAR" Ident (":" type)? "=" expr
assign-stmt   := Ident "=" expr

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
                              | "(" arg-list? ")" trailing-block? )*
primary-expr  := literal
               | Ident
               | "(" expr ")"
               | "Some" "(" expr ")"
               | "None"
               | record-construct
               | array-literal
               | template-literal
               | or-clause-expr

record-construct := Ident "{" field-init ("," field-init)* ","? "}"
                  | expr "WITH" "{" field-init ("," field-init)* ","? "}"
field-init    := Ident ":" expr
array-literal := "[" (expr ("," expr)*)? "]"
template-literal := "`" (text-segment | "${" expr "}")* "`"

trailing-block := "DO" ("|" param-list "|")? newline body "END"

or-clause-expr := primary-expr "OR" or-clause-tail
or-clause-tail := "FAIL" string-expr code-clause? details-clause?
                | "DEFAULT" expr

literal       := int-lit | float-lit | string-lit | raw-string-lit | bool-lit
bool-lit      := "true" | "false"

# 字句 (lexer 層)
int-lit       := dec-int | hex-int | bin-int
dec-int       := digit ("_"? digit)*
hex-int       := "0x" hex-digit ("_"? hex-digit)*
bin-int       := "0b" ("0" | "1") ("_"? ("0" | "1"))*
float-lit     := dec-int "." dec-int (exp-part)? | dec-int exp-part
exp-part      := ("e" | "E") ("+" | "-")? dec-int
string-lit    := '"' char* '"'
raw-string-lit := "r" '"' raw-char* '"'
template-literal := "`" template-segment* "`"

identifier    := id-start id-continue*
id-start      := lowercase-letter | uppercase-letter | "_"
id-continue   := id-start | digit | "-"
```

---

## 付録B エラーメッセージ仕様

エラーメッセージは 2 チャンネルで出力する:

- **チャンネル A**: 構造化 JSON (AI 向け / プログラマブル)
- **チャンネル B**: テキスト整形 (人間向け)

詳細は `slasher-numadora-integration.md` 付録 B (Slasher と Numadora で共通)。

エラーコード分類:

| カテゴリ | プレフィックス | 例 |
|---|---|---|
| 字句 | `syntax_` | `syntax_unexpected_token`, `syntax_ambiguous_minus`, `syntax_invalid_raw_string` |
| 型 | `type_` | `type_mismatch`, `type_function_value_eq`, `type_opaque_field_access` |
| 名前解決 | `name_` | `name_undefined_variable`, `name_closure_var_assign`, `name_method_ambiguous` |
| モジュール | `module_` | `module_not_found`, `module_circular_import` |
| 検査警告 | (warning として) | `unreachable_case`, `unused_import`, `module_path_mismatch` |
| 実行時 (操作系) | (動詞)_ | `element_not_found` (Slasher), `file_not_found` |
| 実行時 (アサート) | `assertion_` | `assertion_failed` |
| 実行時 (システム) | `runtime_` | `runtime_option_unwrap_none`, `runtime_index_out_of_bounds` |
| ホスト | `host_` | `host_invalid_argument`, `host_timeout` |
| プラットフォーム | `platform_` | `platform_not_supported` |
| ポリシー | `policy_` | `policy_denied` |
| ユーザ定義 | `user_` | `user_fail`, `user_<custom>` |

---

## 付録C 残タスク

### v1 で確定、実装フェーズで詰める

1. **トレーリング ブロックのキャプチャ詳細仕様** — 字句スコープ捕獲のテストケース整備
2. **UFCS 解決の曖昧性ルール詳細** — 構造的サブタイプとジェネリクスの相互作用
3. **`numac fmt` のフォーマット規則** (将来コマンド)
4. **`numac doc` の Markdown 出力** (将来コマンド)
5. **ベンチマーク基準** — パース・型検査・実行の時間目標

### v2 以降で検討

1. **`IMPORT ... REQUIRES >= x.y`** — プラグイン バージョン要件 (予約語のみ済)
2. **複数行 raw 文字列 `r"""..."""`**
3. **`Cell[T]`** 等の明示的可変ボックス型 (クロージャ越しの可変参照代替)
4. **async/await** — `ASYNC`/`AWAIT` 予約語のみ済
5. **linear types** (不透明型の close 後使用検出)
6. **REPL の言語拡張対応** — 型推論を REPL でどう見せるか
7. **国際化** — エラーメッセージ多言語対応の基盤
8. **`PURE` 修飾子の明示** — 自動判定との併用

---

## 改訂履歴

- v0.1 (2026-04-29) — 初版起草。Slasher v2 設計書 v1.3 をベースに Numadora 言語仕様として再編成。
- v0.2 (2026-05-10) — 全面改訂。`numadora-language-redesign.md` 8 章の方針を反映:
  - マクロ章 (旧 6 章) を削除
  - 字句構造章 (新 1 章) を追加
  - 不透明型 (`OPAQUE TYPE`) を追加
  - `EFFECT` / `INTERACTIVE` 修飾子を追加
  - メソッド呼び出し糖衣 (UFCS) を明文化
  - トレーリング ブロック構文を追加
  - raw 文字列リテラル `r"..."` を追加
  - 行頭演算子接続を明文化
  - 関数値 `==` 比較禁止
  - クロージャ越し VAR 書き換え禁止
  - レコード等価性の O(構造的サイズ) 性能契約
  - トップレベル副作用禁止
  - ジェネリクスをユーザ定義関数で許可
  - シングル スレッド前提を明示
  - ホスト バインディング章 (新 9 章) を追加
  - 付録 A EBNF を新文法に更新
  - 付録 C 残タスクを v1 確定/v2 検討に整理
- v0.2.1 (2026-05-10) — セキュリティ・ネットワーク統合 (Q-S1〜S6 一括採用、ハードカット)。
  `numadora-security-network-design.md` の方針を spec に反映:
  - 1.4.1 能力クラス識別子 (13 種、コンテキスト認識方式) を追加
  - 6.5 EXPORT 規則: `EFFECT(class)` 必須化、`INTERACTIVE EFFECT(class)` 併記必須
  - 6.5.3 `script-requires` (REQUIRES) 宣言を main 持ちモジュールに追加
  - 6.9 モジュール エラー コード追加 (`effect_class_required`, `effect_class_unknown`,
    `interactive_without_effect`, `requires_missing_capability`,
    `requires_unused_capability`, `requires_in_library`)
  - 9.2 EFFECT(class) 必須化を明文化、典型対応表を追加
  - 9.3 INTERACTIVE 修飾子の規則を明確化
  - 9.6 ホスト例外正規化テーブルにポリシー関連 code (`policy_target_changed`,
    `policy_recursive_delegation`) を追加
  - 9.6.1 ピア委譲 (slasher/peer) と再帰委譲禁止を追加
  - 9.9 ホスト関連エラー コード拡充
  - 付録 A EBNF: top-level に `script-requires` を追加、`func-modifier` を
    `interactive` + `effect-clause` に分離、`capability` non-terminal 追加
  - 標準ライブラリ (8 章) の全 `EXPORT EFFECT FUNC` を `EXPORT EFFECT(class) FUNC` に書き換え
