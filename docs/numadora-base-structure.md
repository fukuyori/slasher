# Numadora 基本構造 設計ノート

> Status: 設計ドキュメント。`numadora-language-redesign.md` の確定方針 (D1〜D4) を
> 前提に、**字句構造 (Lexical)** と **意味構造 (Semantic)** を詳細化する。
>
> 関連:
> - `numadora-language-redesign.md` — 再構成方針 (アンカー、ホストモデル、マクロなし、トレーリング ブロック)
> - `numadora-language-spec.md` — 元仕様 (改訂対象)
> - `slasher-numadora-integration.md` — Slasher 統合 (別途改訂対象)
>
> このノートでの命名・表記は redesign ノートの D1 採用形 (lowercase 型、kebab-case、`=` 区切り、`: T` 戻り値) に従う。

---

## 第1章 字句構造 (Lexical Structure)

### 1.1 文字集合とエンコーディング

- ソースファイル `.numa` / `.numai` は **UTF-8** でエンコードされる。BOM は許容するが推奨しない。
- 改行コードは LF / CRLF / CR のいずれも受理。**意味は同一** (改行 1 個として扱う)。
- 制御文字 (タブ・改行を除く) はソース中に現れてはならない。

### 1.2 トークン分類

| カテゴリ | 例 |
|---|---|
| キーワード | `LET`, `VAR`, `CONST`, `FUNC`, `IF`, `THEN`, `ELSE`, ... |
| 識別子 | `wait-for-title`, `count`, `WindowRef` |
| リテラル | `42`, `3.14`, `"hello"`, `true`, `false`, `()` |
| 演算子 | `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=` |
| 区切り | `(`, `)`, `[`, `]`, `{`, `}`, `,`, `:`, `=`, `.` |
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

- `lowercase-letter` = `a..z`
- `uppercase-letter` = `A..Z`
- `digit` = `0..9`

ASCII のみを採用する (Unicode 識別子は採用しない)。AI 生成可読性とエラーメッセージ整形性を優先する。

#### 1.3.2 推奨命名規約

| 種類 | 規約 | 例 |
|---|---|---|
| 関数 | kebab-case | `wait-for-title`, `start-app` |
| 変数 (LET/VAR) | kebab-case | `wait-time`, `window-handle` |
| パラメータ | kebab-case | `timeout-ms` |
| `CONST` | UPPER-KEBAB-CASE | `MAX-RETRIES`, `KNOWN-CODES` |
| `RECORD` 型 | UpperCamelCase | `Window`, `WindowRef`, `RuntimeError` |
| `TYPE` 別名 | UpperCamelCase | `WindowState`, `BrowserName` |
| モジュール (パス要素) | kebab-case | `slasher/window`, `string-utils` |

これは **規約** であり、字句解析は名前形を強制しない。Linter で警告とする。

#### 1.3.3 識別子と二項 `-` の曖昧性解決 (重要)

kebab-case 識別子と二項マイナスは衝突する。Numadora は **空白による分離** で解決する。

| ソース表記 | 解釈 |
|---|---|
| `a-b` | 識別子 1 個 |
| `a - b` | 二項減算 (`a` と `b` の引き算) |
| `a -b` | **構文エラー** (混在) |
| `a- b` | **構文エラー** (混在) |
| `-a` | 単項マイナス + 識別子 (前のトークンが演算子・区切り・キーワードの場合) |
| `f(-1)` | 関数 `f` に `-1` を渡す |
| `n - 1` | 二項減算 |
| `n-1` | 識別子 (定義されていなければ `name_undefined_variable`) |

**規則の本体**: `-` の前後の空白の有無が一致するかで決める。

- 前後とも空白あり → 二項演算子
- 前後とも空白なし → 識別子の一部
- 不一致 → 字句エラー (`syntax_ambiguous_minus`)

数値リテラル内のマイナスは別扱い: `1e-10` は浮動小数リテラルの一部。`1-10` は数値リテラル `1` の後に「不一致」エラー。

#### 1.3.4 予約語と識別子の使い回し禁止

予約語 (1.4) は識別子として使えない。**部分一致は許容**: `LETTER` は `LET` を含むが識別子として OK (キーワードは完全一致)。

### 1.4 キーワード予約語

```
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
true    false   unit
```

将来予約 (現時点では未使用、識別子として禁止):

```
ASYNC   AWAIT   YIELD   DEFER
TRAIT   IMPL    EFFECT  HANDLE
INTERACTIVE
```

`Some`, `None` はキーワードであり、コンストラクタ/パターンとして使う。識別子としては使えない。

予約語は **大文字小文字を区別する** (= 大文字専用キーワード)。`let`, `if` はただの識別子。

### 1.5 コメント

| 形式 | 用途 |
|---|---|
| `# ...` 〜行末 | 通常のコメント |
| `--- ...` 〜行末 | ドキュメンテーション コメント。直後の宣言・フィールドに付与される |

ブロックコメントは **採用しない** (簡潔性優先)。複数行は各行に `#` を付ける。

`---` コメントは AST に保持され、`numac doc` (将来) や IDE ホバーに使われる。

#### コメントの位置規則

- 行頭または式・文の後ろの空白に置ける
- トークンの中には置けない (`a-b`# foo` は `syntax_unexpected_comment`)
- `---` は宣言の **直前の行** または **フィールド宣言の直前の行** にのみ意味を持つ

### 1.6 空白と改行

#### 1.6.1 トークン区切り

空白文字 (空白・タブ) は **トークン分離以外の意味を持たない**。インデント (Python 風) は採用しない。

#### 1.6.2 改行の意味

改行は **文の区切り** として有効。具体的には以下の文末に必須:

- `LET ... = expr` の終わり
- `VAR ... = expr` の終わり
- `name = expr` (再代入) の終わり
- `RETURN expr` の終わり
- `BREAK`, `CONTINUE`, `IMPORT`, `MODULE` の終わり
- `END` の後ろ
- 式文 (関数呼び出しを含む) の終わり

セミコロン (`;`) は **採用しない**。1 行 1 文を強制する。

#### 1.6.3 行継続

長い式は以下のいずれかで継続できる:

- **括弧内自動継続**: `(`, `[`, `{`, `${` の中では改行は意味を持たない
- **行末バックスラッシュ**: 行末 `\` + 改行 で次行に継続 (推奨度低、`OR FAIL` の前後など限定)

```numadora
LET ok = window.find("OK")
       OR FAIL "OK が見つからない"          # 括弧内ではないが、OR FAIL は二項演算扱いで継続可
```

`OR FAIL` / `OR DEFAULT` および後置二項演算子 (`+`, `-`, ...) は **行頭に来た場合に前行と接続** する規則を採用する (Go/Swift 風)。

```numadora
LET total = a
          + b              # OK: 行頭 + は前行と接続
          + c
```

### 1.7 整数リテラル

```ebnf
int-lit  := dec-int | hex-int | bin-int
dec-int  := digit ("_"? digit)*
hex-int  := "0x" hex-digit ("_"? hex-digit)*
bin-int  := "0b" ("0" | "1") ("_"? ("0" | "1"))*
```

- 10 進: `0`, `42`, `1_000_000`
- 16 進: `0x123`, `0xDEAD_BEEF` (大文字小文字混在可)
- 2 進: `0b1010`
- 区切り `_` は **桁の間** にのみ置ける。先頭/末尾/連続 `__` は不可
- 8 進 (`0o`) は採用しない (混乱回避)
- 値域: 64-bit signed (`-2^63 .. 2^63 - 1`)
- リテラル単独で値域超えは `syntax_int_overflow`
- 単項マイナスは演算子として扱う (`-42` はリテラル `42` に単項 `-` を適用)

### 1.8 浮動小数点リテラル

```ebnf
float-lit := dec-int "." dec-int (exp-part)?
           | dec-int exp-part
exp-part  := ("e" | "E") ("+" | "-")? dec-int
```

- 例: `1.0`, `3.14`, `1e10`, `1.5e-3`, `1_000.5`
- IEEE 754 double precision (`float64` 相当)
- `inf` / `nan` リテラルは **採用しない** (実行時に `std/math` 関数経由で得る)
- 16 進浮動小数 (`0x1.8p3`) は採用しない

### 1.9 文字列リテラル

#### 1.9.1 通常の文字列

```ebnf
string-lit := '"' char* '"'
char       := normal-char | escape
escape     := "\\" ("n" | "r" | "t" | "\\" | "\"" | "'" | "0" | "u{" hex+ "}")
```

- ダブルクォート区切り
- 改行 (素のまま) を含むことはできない (`\n` で表す)
- エスケープ:
  - `\n` 改行
  - `\r` 復帰
  - `\t` タブ
  - `\\` バックスラッシュ
  - `\"` ダブルクォート
  - `\'` シングルクォート
  - `\0` ヌル文字
  - `\u{XXXX}` Unicode コードポイント (1〜6 桁の 16 進)
- 不正なエスケープシーケンスは `syntax_invalid_escape`

シングルクォートでの文字列リテラルは **採用しない** (`'a'` は使えない)。文字型は存在せず、1 文字も `string` で扱う (length 1)。

#### 1.9.2 raw 文字列 (検討、初版見送り)

3 連クォート `"""..."""` などの raw 文字列形式は **初版では採用しない**。エスケープ重複が問題になるパス文字列等は配列 join やテンプレートで回避する。

### 1.10 テンプレート リテラル

```ebnf
template-lit := "`" template-segment* "`"
template-segment := text-segment | "${" expr "}"
```

- バッククオート区切り
- `${expr}` 内には任意の式を書ける (ただし副作用なしの式のみ → 純粋性)
- `${...}` のネストは禁止 (テンプレート内テンプレート不可)
- 非埋め込み部分の改行は **そのまま含まれる** (複数行テンプレート OK)

```numadora
LET msg = `title is ${win.title}`
LET multi = `line 1
line 2 with ${x}`
```

エスケープは通常文字列と同じ。`` \` `` でバッククオートをエスケープ、`\$` で `$` をエスケープ。

### 1.11 真偽値リテラルと unit リテラル

- 真偽値: `true`, `false` (lowercase, 予約語)
- unit: `()` または `unit` の 2 形式を許容
  - 戻り値型注釈やパターンでは `unit` を推奨
  - 値式としては `()` を推奨

### 1.12 配列リテラル

```ebnf
array-lit := "[" (expr ("," expr)* ","?)? "]"
```

- 空配列 `[]` は **型注釈必須**: `LET a: array[int] = []`
- 末尾カンマ許容
- 改行は括弧内なので自由

### 1.13 レコード生成と WITH 句

```ebnf
record-construct := type-name "{" field-init ("," field-init)* ","? "}"
                  | expr "WITH" "{" field-init ("," field-init)* ","? "}"
field-init       := identifier ":" expr
```

`WITH` 句の左辺は識別子に限定せず、レコード値を返す任意の式を許す。ただし副作用なしの式のみ (純粋性により式中で不純な関数呼び出しは禁止)。

### 1.14 字句エラーコード一覧

| コード | 意味 |
|---|---|
| `syntax_invalid_character` | 不正な文字 |
| `syntax_unterminated_string` | 文字列リテラルが閉じていない |
| `syntax_unterminated_template` | テンプレート リテラルが閉じていない |
| `syntax_invalid_escape` | 不正なエスケープシーケンス |
| `syntax_invalid_int_literal` | 不正な整数リテラル形式 |
| `syntax_invalid_float_literal` | 不正な浮動小数点リテラル形式 |
| `syntax_int_overflow` | 整数リテラルが値域超 |
| `syntax_ambiguous_minus` | `-` の前後空白が不一致 |
| `syntax_unexpected_comment` | コメントの位置が不正 |
| `syntax_reserved_word` | 予約語を識別子として使用 |

---

## 第2章 意味構造 (Semantic Structure)

### 2.1 値の世界

Numadora の **値 (value)** は以下のカテゴリに分類される。

| カテゴリ | 例 | ストレージ意味論 | 等価性 |
|---|---|---|---|
| プリミティブ | `int`, `float`, `bool`, `unit`, `string` | 値型 | 構造的 |
| `Option[T]` | `Some(x)`, `None` | 値型 | 構造的 |
| 配列 `array[T]` | `[1,2,3]` | 値型だが内部は共有可 | 構造的 (要素ごと) |
| レコード | `Window { ... }` | 値型 (immutable) | 構造的 (フィールドごと) |
| 不透明型 (`opaque`) | `WindowRef` | ホスト管理 | アイデンティティ |
| 関数値 | `FUNC ... END`, トレーリング ブロック | 不変参照 | アイデンティティ (既定) |

「値型」とは **コピー意味論** を持つこと。「構造的等価性」とは内部表現を再帰的に比較すること。

### 2.2 LET / VAR と不変性

- `LET name: T = expr` — **束縛**。`name` を `expr` の値に固定。後の再代入禁止。
- `VAR name: T = expr` — **可変束縛**。`name = ...` で再代入できる (型は変更不可)。
- `CONST name: T = expr` — **トップレベル定数**。`expr` は純粋・コンパイル時評価可能でなければならない。

```numadora
LET x = 1
x = 2          # エラー: name_let_reassign

VAR y = 1
y = 2          # OK
y = "hello"   # エラー: type_assignment_mismatch
```

`LET`/`VAR` 宣言時に **型注釈は省略可能**。省略時は右辺式から型推論される。

#### 不変な値の更新

レコードは **すべて immutable**。フィールド書き換えは構文上不可。

```numadora
LET w = Window { title: "A", handle: 1, ... }
w.title = "B"  # エラー: name_record_field_readonly
LET w2 = w WITH { title: "B" }   # OK: 新しい値
```

配列は `VAR` 宣言された場合のみ in-place 変更可:

```numadora
VAR items: array[int] = [1, 2, 3]
std/array.push(items, 4)   # OK: items は [1, 2, 3, 4] に
LET frozen: array[int] = [1, 2, 3]
std/array.push(frozen, 4)  # エラー: name_let_array_immutable
```

`VAR` 配列でも、配列リテラルやレコード値そのものは値型 (代入はコピー意味論)。`std/array.push` 等の **可変操作 API は VAR 配列のみ受理** する。

### 2.3 等価性 (`==`, `!=`)

| 型 | 等価性の定義 |
|---|---|
| `int`, `float`, `bool`, `unit`, `string` | 値そのものの一致 |
| `Option[T]` | 同じバリアント、内部値も `==` |
| `array[T]` | 同長、各要素が `==` |
| レコード型 | 同じ型、各フィールドが `==` |
| 不透明型 (`opaque`) | アイデンティティ (ホストが定義) |
| 関数値 | アイデンティティ (生成元の同一性) |

#### 異種型の `==`

異なる型の値同士の `==` は **型エラー** (`type_eq_mismatch`)。実行時に `false` を返す挙動は採用しない。

```numadora
1 == "1"      # type_eq_mismatch (実行時 false ではなく検査エラー)
None == None  # OK (両辺 Option[T] なら型 T が一致する場合に限る)
```

`Option[T]` の比較は型パラメータ T の一致が必要。

#### NaN

`float` の NaN は `nan == nan` が **false** (IEEE 754 準拠)。等価性検査ヘルパ `std/math.is-nan(x)` を使うこと。

### 2.4 スコープ規則

#### 2.4.1 字句スコープ + ブロックスコープ

Numadora は **字句スコープ (lexical scope)** を採用する。さらに、以下のブロック構造はそれぞれ **新しいスコープ** を導入する:

- 関数本体 `FUNC ... END`
- `IF ... THEN body END`, `ELSE body END`
- `WHILE ... DO body END`
- `FOR ... DO body END`
- `MATCH ... OF CASE ... THEN body` (各 CASE)
- `TRY body CATCH e body FINALLY body END` (各節)
- `DO ... END` トレーリング ブロック (関数値の本体)

```numadora
LET x = 1
IF cond THEN
  LET x = 2     # 内側 x は外側 x をシャドウ
  io.print(x)   # → 2
END
io.print(x)     # → 1
```

#### 2.4.2 宣言の有効範囲

`LET`/`VAR` で宣言された変数は、**宣言文の後** から、それが書かれたブロックの末尾までで有効。前方参照は不可。

```numadora
io.print(x)      # エラー: name_undefined_variable
LET x = 1
```

`FUNC` トップレベル宣言は **モジュール全体で有効** (前方参照可能)。これは相互再帰のため。

```numadora
FUNC even(n: int): bool
  IF n == 0 THEN RETURN true END
  RETURN odd(n - 1)              # OK: odd は後ろで宣言されているが見える
END

FUNC odd(n: int): bool
  IF n == 0 THEN RETURN false END
  RETURN even(n - 1)
END
```

`RECORD`, `TYPE`, `CONST`, `IMPORT` も同様にモジュール全体で有効。

#### 2.4.3 シャドーイング

同じスコープ内での同名再宣言は **禁止** (`name_duplicate_definition`)。

別スコープでの同名宣言は **シャドウ可能** (上の例)。Linter で警告するか議論あり (案: 同型なら警告なし、異型なら警告)。

#### 2.4.4 名前空間の独立 (spec 1.1 を継承)

型名・関数名・変数名は **3 つの独立した名前空間** に分かれる。

```numadora
RECORD Window { title: string, handle: int }
LET Window = "string"      # OK (別名前空間)
FUNC Window(t: string)     # 関数空間として OK (ただし混乱を招くため Linter で警告)
  ...
END
```

ただし、**読みやすさを優先** して同名衝突は実用上避けるべき。Linter で警告。

### 2.5 評価順

#### 2.5.1 引数評価

関数呼び出し `f(a, b, c)` の引数は **左から右** に評価される。

```numadora
LET r = f(g(1), h(2), k(3))
# 評価順: g(1) → h(2) → k(3) → f(...)
```

#### 2.5.2 二項演算

`a OP b` は `a` を先に、次に `b` を評価する (左から右)。

`a + b * c` の優先順位 (`*` が優先) によって評価順は `a` → `b` → `c` → `b * c` → `a + ...` となる。

#### 2.5.3 短絡評価

`AND`, `OR` は **短絡評価** する:

- `a AND b`: `a` が false なら `b` は評価されない
- `a OR b`: `a` が true なら `b` は評価されない

`OR FAIL` / `OR DEFAULT` も同様: 左辺が `Some(...)` なら右辺は評価されない。

#### 2.5.4 純粋性との関係

評価順が観測可能になるのは **副作用** が生じる場合のみ。Numadora は **不純な関数を式から呼ぶことを禁止** するため、純粋な式の評価順は観測不可能 (実装は最適化のため再順序化してよい)。

文の列の評価順は **書かれた順** に保証される。

### 2.6 数値の暗黙昇格

| 演算 | 結果型 |
|---|---|
| `int OP int` (`+`, `-`, `*`, `/`, `%`) | `int` |
| `int OP float` または `float OP int` | `float` (int を先に float に昇格) |
| `float OP float` | `float` |
| `int / int` | `int` (整数除算、切り捨て) |
| `int / 0` | `runtime_division_by_zero` |
| `int % 0` | `runtime_division_by_zero` |
| `int OP int` でオーバーフロー | `runtime_int_overflow` (silent wrap-around しない) |
| `int % float` | `type_mismatch` (`%` は int のみ) |

比較演算子 (`<`, `<=`, `>`, `>=`) も同じ昇格規則。`int == float` は型不一致エラー (1.0 と 1 を等価扱いしない)。

### 2.7 文字列演算

- `+` で連結
- 比較 (`<`, `<=`, `>`, `>=`) は **UTF-8 バイト辞書順**、case-sensitive
- `string CONTAINS string`, `STARTSWITH`, `ENDSWITH` (spec 2.2)
- `length` の意味は **コードポイント数** (`std/string.length`)、バイト数は `std/string.byte-length`

### 2.8 関数値

Numadora の関数値は以下のいずれかで生成される:

1. 名前付き `FUNC` 宣言 → 関数値が変数に束縛される
2. **トレーリング ブロック** (`DO ... END`) → 引数位置で関数値を生成する糖衣

ラムダ式の独立形は **採用しない** (`FUNC(x) ... END` を式の中で書くことは不可)。トレーリング ブロックでのみ匿名関数を許可する。

```numadora
# 名前付き
FUNC double(x: int): int
  RETURN x * 2
END
LET doubled = std/array.map(nums, double)

# トレーリング ブロック
std/array.map(nums) DO |x|
  RETURN x * 2
END
```

#### トレーリング ブロックの構文

```ebnf
trailing-block := "DO" ("|" param-list "|")? newline body "END"
```

- 関数の **最後の引数が関数型** であるとき、その引数を `DO ... END` で渡せる
- パラメータは `|x, y|` 形式で書く (spec 改訂対象)
- パラメータ型は省略可 (推論)

#### 戻り値

トレーリング ブロックの戻り値は最終式 or 明示的 `RETURN`。`unit` を返す関数引数 (`function(): unit`) では `RETURN` 不要。

### 2.9 クロージャ捕獲規則

トレーリング ブロックは **字句スコープに閉じている** = クロージャである。捕獲規則:

| 外側変数 | ブロック内での読み取り | ブロック内での書き換え |
|---|---|---|
| `LET` | 可 | 不可 (元々不可) |
| `VAR` | 可 | **不可** (Numadora 規則) |
| 関数引数 | 可 | 不可 |

**VAR の書き換えはクロージャ越しでは禁止**。これは:

- 純粋性判定をトラクタブルに保つ
- AI 生成コードでの「見えない副作用」を排除
- 並行化や最適化の余地を残す

書き換えが必要な場合は、ブロックの戻り値で値を返して呼び出し側で更新する。

```numadora
VAR count = 0
retry(3, 500) DO
  count = count + 1   # エラー: name_closure_var_assign
END

# 正しい書き方
VAR count = 0
count = retry(3, 500) DO
  RETURN count + 1
END
# (※ retry のシグネチャ次第。実際は std/control に高階関数を用意する必要)
```

実用上は VAR 累積を `std/array.reduce` 等の純粋関数に置き換えるのを推奨する。

### 2.10 純粋性判定アルゴリズム

#### 2.10.1 純粋関数の定義

関数 `f` が **純粋 (pure)** であるとは:

- `f` の本体に以下が **一切現れない** こと:
  - ホスト関数呼び出し (`.numai` で `EFFECT` 修飾された関数。詳細は別ノート)
  - 不純な関数の呼び出し
  - クロージャ越しの VAR 書き換え (規則上禁止だが念のため)
  - `IMPORT` した不純なモジュールの top-level 副作用への依存

純粋関数は以下を行ってよい:

- 自分の引数の読み取り
- 内部の LET/VAR の宣言と内部での書き換え
- 純粋関数の呼び出し
- `RETURN`, `IF`, `WHILE`, `FOR`, `MATCH`, `TRY` 制御フロー
- レコード生成と `WITH` 更新

#### 2.10.2 判定アルゴリズム

1. すべての関数を初期状態 **「純粋候補」** とする
2. 各関数本体を走査し、ホスト関数呼び出しを直接含むものを **「不純」** にマーク
3. 不純関数を呼ぶ関数を **「不純」** にマーク (固定点が落ち着くまで反復)
4. 残った関数が純粋

このアルゴリズムは型検査の一部として実行される (`O(n × max-call-depth)` で収束)。

#### 2.10.3 純粋性違反の取り扱い

不純な関数を **式の文脈で呼ぶ** ことは静的検査エラー (`type_impure_in_expression`)。

```numadora
FUNC focus-and-id(w: WindowRef): int   # 不純 (focus がホスト呼び出し)
  window.focus(w)
  RETURN 1
END

LET id = focus-and-id(w)    # エラー: 不純関数を式から呼べない
focus-and-id(w)             # OK: 文として呼ぶ
```

#### 2.10.4 型注釈による明示

将来的に、関数シグネチャに `PURE` / `EFFECT` 修飾子を追加することを検討する (spec 改訂対象)。第一段階は自動判定のみ。

### 2.11 ライフタイム / メモリ

- ガベージコレクションあり (実装はトレース GC または ARC のいずれでもよい)
- 値型レコードは内部で **Copy-on-Write 共有** してよい (immutable なので等価)
- 配列の `VAR` 変更操作は **論理的に in-place** だが、実装は共有検出して必要時にコピーしてもよい
- 不透明型 (`opaque`) の寿命は **ホストが管理** する。Numadora 側からは値型のように見える
- `WindowRef`/`AppRef` のような不透明値が破棄されたとき、ホストは登録解除イベントを受け取れる (将来検討)

### 2.12 イテレーション意味論

#### 2.12.1 範囲式

```numadora
FOR i IN 0..10 DO
  ...
END
```

- `a..b` は **半開区間** `[a, b)`。`i` は `a, a+1, ..., b-1` を順に取る
- `a > b` の場合は反復なし
- `a..=b` (閉区間) は採用しない (簡潔性優先、`0..b+1` で書く)

#### 2.12.2 配列イテレーション

```numadora
FOR item IN items DO ... END
FOR (i, item) IN items DO ... END
```

- 順序は配列の格納順 (0 から)
- イテレーション中の配列変更は **未定義動作** ではなく **ランタイム エラー** (`runtime_array_modified_during_iteration`)
- 安全には新配列を作る (`std/array.map` 等)

#### 2.12.3 無限ループ防止

`WHILE` の最大反復数は spec 2.7 で 1000。`FOR i IN 0..N` は N 不問。

将来検討: `WHILE` に上限引数 (`WHILE cond LIMIT 10000 DO`) を導入する案。

### 2.13 式と文の境界 (副作用とのコントラクト)

| 場所 | 許可される呼び出し |
|---|---|
| 式 (RHS, 引数, ...) | 純粋関数のみ |
| 文 (top-level, ブロック内行) | 純粋・不純の両方 |
| `IF` 条件、`WHILE` 条件、`MATCH` 被検査値 | 純粋のみ |
| `FOR` の被反復式 | 純粋のみ |
| `RETURN` の引数 | 関数自身が不純なら不純 OK |

つまり「式は副作用なし」「文だけが副作用を起こせる」が貫かれる。これは AI 生成コードの読み下しを安定させる。

### 2.14 セマンティクスのエラーコード一覧

| コード | 意味 |
|---|---|
| `type_mismatch` | 型不一致 |
| `type_eq_mismatch` | `==`/`!=` の両辺型不一致 |
| `type_assignment_mismatch` | 代入時の型不一致 |
| `type_impure_in_expression` | 式中で不純関数を呼んだ |
| `name_undefined_variable` | 未定義変数 |
| `name_undefined_function` | 未定義関数 |
| `name_undefined_module` | 未定義モジュール |
| `name_duplicate_definition` | 同一スコープでの重複宣言 |
| `name_let_reassign` | LET 変数への再代入 |
| `name_record_field_readonly` | レコードフィールドへの代入 |
| `name_let_array_immutable` | LET 配列の可変操作 |
| `name_closure_var_assign` | クロージャからの VAR 書き換え |
| `runtime_division_by_zero` | ゼロ除算 |
| `runtime_int_overflow` | int オーバーフロー |
| `runtime_index_out_of_bounds` | 配列範囲外 |
| `runtime_option_unwrap_none` | `.value` で None を unwrap |
| `runtime_max_iteration` | WHILE 反復数超過 |
| `runtime_array_modified_during_iteration` | 反復中の配列変更 |

---

## 第3章 Q-A* / Q-D* 確定事項

すべての Q-A* / Q-D* は採用済 (一括採用)。以下に確定内容を記載する。

| Q | 確定 | 反映先 |
|---|---|---|
| **A1** raw 文字列 | **`r"..."` 採用** (Rust 風)。複数行 raw `r"""..."""` は v2 検討 | 1.9.2 で `r"..."` を正式採用形に変更、1.14 エラーコードに `syntax_invalid_raw_string` 追加 |
| **A2** Unicode 識別子 | **ASCII 維持** (現方針継続) | 1.3.1 確定、変更なし |
| **A3** 行頭演算子接続 | **採用** (Go/Swift 風)。lexer 後段または parser 段階で実装 | 1.6.3 確定、変更なし |
| **D1** レコード CoW | **仕様は構造的等価 (O(構造的サイズ))**、実装は CoW 自由 | 2.3 等価性節に「O(構造的サイズ)」明記、`numadora-language-spec.md` 改訂対象 |
| **D2** 関数値の `==` | **全面禁止** (`type_function_value_eq`) | 2.3 等価性テーブル「採用しない」、2.14 エラーコード追加済 |
| **D3** 純粋性明示 | **`.numai` で `EXPORT EFFECT FUNC` 必須** (Q-P6 整合) | spec 改訂対象 (`numadora-language-redesign.md` 8 章) |
| **D4** クロージャ VAR | **書換禁止維持** (`name_closure_var_assign`)、将来 `Cell[T]` 検討 | 2.9 確定、エラーコード追加済 |
| **D5** 並行性 | **v1 シングルスレッド維持**、ホスト側並行化で対応 | `numadora-core-systems.md` 3.5 確定済 |
| **D6** メモリ モデル | **C# GC + finalizer + 明示 close 併用**。`std/dispose` 等の言語側 API は不採用 (host 関数として `app.close()` を提供) | `numadora-core-systems.md` 3.6 確定済 |

### 3.1 raw 文字列 `r"..."` の確定 (Q-A1)

Slasher の Windows パスを書く際の二重エスケープを回避する。

```ebnf
raw-string-lit := "r" '"' raw-char* '"'
raw-char       := any-char-except-double-quote
```

- 接頭辞 `r` は識別子と紛らわしくないよう **`"` 直前のみ** で意味を持つ
- raw 文字列内では **エスケープ無効**: `\n`, `\\`, `\"` などはそのまま 2 文字として扱う
- 内側に `"` を含めるには raw 文字列を分割: `r"path1" + r"path2"`
- 改行 (素の) を含めることは不可 (1 行 raw 文字列のみ)
- 複数行 raw `r"""..."""` は v2 で検討

```numadora
LET path = r"C:\Users\foo\bar.txt"
LET regex = r"\d{4}-\d{2}-\d{2}"
```

字句エラー追加: `syntax_invalid_raw_string` (raw 文字列が閉じていない、改行を含む等)。

### 3.2 レコード等価性の性能契約 (Q-D1)

- レコード `==` の計算量は **O(構造的サイズ)** (フィールド数 × 各フィールド再帰深度) が仕様
- 実装は内部参照同一性での fast-path (O(1)) を採用してよい (オプション)
- 実装の差で計算量保証は変わらない: `O(構造的サイズ)` を上限と保証

### 3.3 関連する spec 改訂事項

Q-A1, A3, D1, D2, D3, D4, D5, D6 から派生する spec 改訂は `numadora-language-redesign.md` 8 章のリストに統合済。

---

## 改訂履歴

- v0.1 — 初版起草。字句構造と意味構造の確定方針を提示。Q-A1〜Q-D6 を未決事項として明示。
