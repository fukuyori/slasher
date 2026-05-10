# Numadora 型システム/モジュール/実行モデル 設計ノート

> Status: 設計ドキュメント。`numadora-language-redesign.md` の確定方針 (D1〜D4) と
> `numadora-base-structure.md` の字句/意味確定を前提に、**C. 型システム**、
> **E. モジュール システム**、**F. 実行モデル** を詳細化する。
>
> 関連:
> - `numadora-language-redesign.md` — 再構成方針
> - `numadora-base-structure.md` — 字句構造と意味構造
> - `numadora-language-spec.md` — 元仕様 (改訂対象)

---

## 第1章 型システム (Type System)

### 1.1 型の階層

Numadora の型は以下のカテゴリに分類される。

```
type
├── primitive
│   ├── int
│   ├── float
│   ├── bool
│   ├── string
│   └── unit
├── composite
│   ├── Option[T]
│   ├── array[T]
│   ├── record (匿名)
│   └── RECORD-name (名前付き)
├── opaque
│   └── OPAQUE-name (ホスト管理)
├── function
│   └── function(T1, T2, ..., Tn): R
└── string-literal-union
    └── "a" | "b" | "c"
```

`TYPE` 別名はこれらに名前を付けた **同型** であり、新たなカテゴリを作らない。

### 1.2 型注釈の必須/省略

| 場所 | 型注釈 |
|---|---|
| `FUNC` の引数 | **必須** |
| `FUNC` の戻り値 | 省略時は `unit` を返す関数とみなす (spec 2.5 通り) |
| `RECORD` のフィールド | **必須** |
| `CONST` | **必須** |
| `LET` | 省略可 (右辺から推論) |
| `VAR` | 省略可 (右辺から推論) |
| 空配列 `[]` の `LET`/`VAR` | **必須** (要素型推論不能のため) |
| トレーリング ブロックのパラメータ | 省略可 (呼び出し関数のシグネチャから推論) |

「省略可」の基本方針: **右辺から一意に決まるなら省略可、そうでなければ必須**。

### 1.3 型推論

#### 1.3.1 推論の範囲 (Local Type Inference)

Numadora は **関数境界で閉じた局所型推論** を採用する。

- 関数のシグネチャ (引数型・戻り値型) は完全注釈、または `unit` のみ省略可
- 関数本体内では `LET`/`VAR` の右辺型から左辺型を推論
- 関数呼び出しの結果型はシグネチャから直接決まる

**全プログラム型推論 (Hindley-Milner) は採用しない**。理由:

- AI 生成コードでシグネチャが読み取れることが第一
- エラーメッセージが局所化される (推論失敗が遠くから来ない)
- インクリメンタル コンパイルが容易

#### 1.3.2 推論アルゴリズム

`LET x = expr` の場合:

1. `expr` の型を求める (型空間で完全に決まる)
2. その型を `x` に割り当てる

`LET x: T = expr` の場合:

1. `expr` の期待型を `T` として走査
2. `expr` の実型と `T` の整合を検査

トレーリング ブロックの場合:

```numadora
std/array.map(nums) DO |x|
  RETURN x * 2
END
```

- `std/array.map[T, U]` のシグネチャから `T = element-type-of(nums)` が決まる
- `x` の型は `T`
- ブロック本体から戻り値型 `U` を推論
- `map` の戻り型は `array[U]`

### 1.4 構造的サブタイピング (レコード)

レコード型は **フィールド形状で適合性を判定** する (spec 1.4 を継承)。

```numadora
RECORD Window { title: string, handle: int, state: WindowState }
RECORD TitledThing { title: string }

LET t: TitledThing = win    # OK: Window は TitledThing のフィールドをすべて持つ
```

#### 1.4.1 ルール

`A` が `B` のサブタイプであるとは、`A` のフィールド集合が `B` のフィールド集合を **包含** し、対応するフィールド型が一致または互いにサブタイプであること。

- フィールドの **追加** はサブタイプ方向 (拡張)
- フィールドの **省略** はスーパータイプ方向 (縮小)
- フィールドの **型変更** は不可 (再帰的に同じサブタイプ規則)

#### 1.4.2 構造的サブタイプの境界

構造的サブタイピングは **レコード型に限る**。以下は名目的:

- `OPAQUE TYPE` (1.6 参照)
- `RECORD` 同士でも、同名フィールドの型が異なる場合は無関係

### 1.5 不透明型 (Opaque Types) — 新規導入

`numadora-language-redesign.md` 3.4 で導入した不透明型の正式仕様。

#### 1.5.1 宣言

`.numai` ファイルでのみ宣言可能:

```numadora
MODULE slasher/window

EXPORT OPAQUE TYPE WindowRef
```

`.numa` 本体ファイルでは宣言できない (ユーザは新しい不透明型を定義できない)。

#### 1.5.2 性質

| 性質 | 内容 |
|---|---|
| 内部表現 | 隠蔽 (host 管理 handle ID 等) |
| フィールドアクセス | 不可 (`win.handle` 等は型エラー) |
| パターン マッチ | 不可 (`MATCH` の対象として分解できない) |
| 構造的サブタイピング | 対象外 (名目的 nominal) |
| 等価性 (`==`) | ホストが定義 (典型的にはアイデンティティ等価) |
| 構築 | ホスト関数の戻り値経由のみ |
| 文字列化 | `std/io.print(WindowRef)` は型エラー (明示的に変換が必要) |

#### 1.5.3 観測値レコードとのペアリング

不透明型はホスト リソースの **不透明な参照**。観測値 (タイトル・矩形等) は別のレコード型で返す。

```numadora
# slasher/window.numai
MODULE slasher/window

EXPORT OPAQUE TYPE WindowRef

EXPORT RECORD WindowInfo {
  title: string,
  handle: int,
  state: "normal" | "minimized" | "maximized",
}

EXPORT FUNC info(target: WindowRef): WindowInfo
```

```numadora
# 利用側
LET win = window.find("Notepad") OR FAIL "no window"
LET wi = window.info(win)
io.print(wi.title)              # OK: WindowInfo はレコード
io.print(win)                   # 型エラー: WindowRef は print できない
```

#### 1.5.4 不透明型の宣言修飾子 (将来検討)

```numadora
EXPORT OPAQUE TYPE WindowRef WITH-DISPOSAL
```

`WITH-DISPOSAL` を付けた不透明型は、最後の参照が消えたときにホストに通知される (cleanup callback 起動)。詳細は 3.6 参照、spec 改訂対象。

### 1.6 型パラメータ (ジェネリクス)

#### 1.6.1 採用範囲

**ユーザ定義関数でも型パラメータを許可する** (spec を緩和)。

理由:

- `array[Window]` 等を扱うユーザ関数を書きたい場面は実際多い
- `std/array.map` 等を呼び出す結果に対する後続処理がジェネリックでないと書きづらい
- 型推論の局所性は型パラメータがあっても保てる

ただし、初版は **不変 (invariant)** のみ。共変・反変は導入しない。

#### 1.6.2 構文

```numadora
FUNC first-or-default[T](items: array[T], fallback: T): T
  IF std/array.is-empty(items) THEN
    RETURN fallback
  END
  RETURN items[0]
END
```

- 型パラメータは `[T]` または `[T, U]` のような角括弧
- 大文字始まりの 1〜数文字を慣習とする (`T`, `U`, `K`, `V`)
- 制約 (`T: Comparable` 等) は **採用しない** (構造的サブタイピングが弱い形で代替)

#### 1.6.3 型推論との関係

呼び出し時に型パラメータは **引数型から推論** される。明示指定は不可 (Numadora には `f[int](arg)` 構文がない)。

推論失敗 (例: 引数なしで戻り値型のみで決まる場合) は型エラー (`type_inference_failure`)。

### 1.7 string-literal union

```numadora
TYPE WindowState = "normal" | "minimized" | "maximized"
```

- 各リテラルは `string` のサブタイプ (`WindowState` は `string` に代入可)
- 集合の包含関係でサブタイプ判定: `"normal" | "minimized"` ⊂ `"normal" | "minimized" | "maximized"`
- `MATCH` で網羅性検査の対象 (spec 4.2)

### 1.8 関数型

```numadora
function(int, int): int
function(): unit
function(WindowRef): WindowInfo
```

- 引数型と戻り値型の組み合わせで決まる
- **不変 (invariant)** で扱う (variance を持たない)
- 関数値の等価性は **採用しない** (Q-D2 参照)

### 1.9 型変換

#### 1.9.1 暗黙変換

- `int` → `float` (二項演算の混合時のみ、`numadora-base-structure.md` 2.6 参照)
- それ以外の暗黙変換なし

#### 1.9.2 明示変換

`std/string.from-int`, `std/string.to-int`, `std/math.float-to-int` 等の関数経由のみ。`Option[T]` を返す変換 (失敗の可能性あり) と panic 変換 (常に成功または panic) を区別する。

```numadora
LET n: Option[int] = std/string.to-int("42")        # 失敗時 None
LET m: int = std/math.float-to-int(3.14)            # 失敗時 runtime panic
```

### 1.10 型エラーコード

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

## 第2章 モジュール システム

### 2.1 ファイルとモジュールの関係

- 1 ファイル = 1 モジュール
- ファイル先頭で `MODULE path/to/name` を宣言する
- ファイルパスとモジュール名は **規約上一致させる** (例: `slasher/window.numa` の `MODULE` は `slasher/window`)。不一致は **警告** (`module_path_mismatch`)、エラーではない

`MODULE` 宣言は **ファイル先頭の唯一行** (コメントを除く先頭) に置かれる。途中での宣言は構文エラー。

### 2.2 ファイル拡張子

| 拡張子 | 役割 |
|---|---|
| `.numa` | 通常の実装ファイル |
| `.numai` | インターフェイス宣言ファイル (本体なし) |

#### 2.2.1 解決順序

`IMPORT slasher/window AS window` のとき:

1. `<root>/slasher/window.numai` を探す
2. 同時に `<root>/slasher/window.numa` を探す
3. 両方存在する場合:
   - `.numai` のシグネチャを **公的インターフェイス** として採用
   - `.numa` の宣言と `.numai` のシグネチャが一致するか検査
   - 不一致は `module_interface_mismatch` エラー
4. `.numai` のみの場合:
   - 本体は **ホスト登録から提供される** ことを期待 (詳細は第 3 章)
   - ホスト登録がなければ起動時エラー (`module_host_not_registered`)
5. `.numa` のみの場合:
   - その `.numa` の `EXPORT` 宣言群が公的インターフェイス

### 2.3 パス解決

#### 2.3.1 パスの種類

| プレフィックス | 解決先 |
|---|---|
| `std/...` | 標準ライブラリ ルート (Slasher 同梱) |
| `slasher/...` | Slasher ホスト バインディング (`.numai` のみ) |
| `./...`, `../...` | 現ファイル相対 |
| その他 | ワークスペース ルート相対 |

ワークスペース ルートは Slasher のスクリプト ベース ディレクトリ (`scripts/numadora-host/` 等)。

#### 2.3.2 alias 必須

```numadora
IMPORT std/array          # エラー: name_import_alias_required
IMPORT std/array AS arr   # OK
```

理由:

- 名前衝突を呼び出し側で完全に制御できる
- AI 生成時に「どのモジュールから来た関数か」が呼び出し場所で読み取れる

#### 2.3.3 alias の重複

同一ファイル内での alias 重複は `name_import_alias_duplicate`。

#### 2.3.4 再 export

現状は **再 export を採用しない**。あるモジュールが import した名前を、別モジュールから見えるようには **しない**。

将来 `EXPORT IMPORT` のような再 export 構文を導入する余地はあるが、初版は範囲外。

### 2.4 EXPORT の規則

#### 2.4.1 EXPORT 対象

```
EXPORT FUNC ...
EXPORT RECORD ...
EXPORT TYPE ...
EXPORT CONST ...
EXPORT OPAQUE TYPE ...    # .numai のみ
```

`MODULE`, `IMPORT` は EXPORT の対象ではない。

#### 2.4.2 可視性整合

**プライベート型を EXPORT FUNC のシグネチャに含めることは禁止**。

```numadora
RECORD Internal { x: int }                # 非 EXPORT

EXPORT FUNC build(): Internal             # エラー: name_private_type_in_export_signature
  RETURN Internal { x: 0 }
END
```

これは外部から戻り値を受け取った後、その型名で扱えなくなる事を防ぐ。

### 2.5 IMPORT のセマンティクス

#### 2.5.1 タイミング

`IMPORT` 文は **モジュール初期化時** に評価される (一度だけ)。`IMPORT` を関数本体内に書くことはできない (ファイル先頭セクションのみ)。

#### 2.5.2 順序

ファイル内では `MODULE` の直後、トップレベル宣言の前に `IMPORT` をまとめて書く慣習とする。Linter で警告 (構文上はトップレベル宣言の前後どこでも書ける、ただし `MODULE` の前は不可)。

#### 2.5.3 将来の REQUIRES 余地 (Q-P5 採用)

プラグイン由来モジュール (`slasher/excel` 等) のバージョン要件を将来宣言可能にする余地を残す:

```numadora
# 将来構文 (v2 以降の検討対象、現時点では未採用)
IMPORT slasher/excel AS excel REQUIRES >= 2.0
```

v1 では `REQUIRES` 句なし。プラグインのバージョンは `/plugins` で表示のみ。breaking change 発生時に Numadora spec へ追加検討。

### 2.6 モジュール初期化順

#### 2.6.1 グラフ構築

各モジュールの `IMPORT` 関係から有向グラフを作る。**循環は禁止** (`module_circular_import`)。

#### 2.6.2 トポロジカル順

依存先から順に初期化する。同階層は import 順。

#### 2.6.3 初期化フェーズ

各モジュールの初期化は以下の順:

1. すべての `RECORD`, `TYPE`, `OPAQUE TYPE`, `EXPORT` 宣言を型空間に登録
2. `FUNC` 宣言を関数空間に登録 (本体は未評価)
3. `CONST` 宣言の式を **左から下** の順で評価
4. ホスト登録 (`.numai` の場合) を解決し、シグネチャ整合を検査

#### 2.6.4 トップレベル副作用の禁止

`.numa` モジュールの **トップレベルでの副作用ありの式・文を禁止**。許される top-level 要素は:

- `MODULE` 宣言
- `IMPORT` 文
- `RECORD`, `TYPE`, `CONST`, `FUNC`, `OPAQUE TYPE` 宣言
- `EXPORT` 修飾

`CONST` の右辺式は **純粋** で、コンパイル時 (= モジュール初期化時) に評価される。

これは:

- モジュール ロード順による副作用の予測不可性を排除
- AI 生成コードでの「ファイルを読んだだけで何かが起きる」を排除
- テスト・キャッシュ・並行ロードを容易にする

### 2.7 標準ライブラリの位置づけ

| モジュール群 | 提供形態 |
|---|---|
| `std/*` | Slasher 同梱の `.numa` 実装 + 一部 `.numai` (組み込み関数) |
| `slasher/*` | Slasher ホストの `.numai` のみ。本体は C# 側 |

`std/array`, `std/string` の純粋関数群は `.numa` で書かれていてもよいし、性能上 `.numai` + ホスト実装でもよい。設計上はどちらでも構わない。

### 2.8 モジュール エラーコード

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

---

## 第3章 実行モデル (Execution Model)

### 3.1 エントリ ポイント

#### 3.1.1 主モジュール

実行時に 1 つの **主モジュール** が指定される (Slasher の場合は `POST /scripts/run` の `path` パラメータが指すファイル)。

#### 3.1.2 main 関数

主モジュールは `EXPORT FUNC main(): unit` を持たなければならない。

```numadora
MODULE my-script
IMPORT slasher/desktop AS desktop
IMPORT slasher/io AS io

EXPORT FUNC main()
  io.step("hello")
  LET app = desktop.start-app("notepad.exe")
  ...
END
```

`main` の引数は **不可** (将来的にコマンドライン引数を取るなら `args: array[string]`)。

#### 3.1.3 main の検査

- 存在しない: `runtime_no_main`
- 引数あり: `runtime_main_invalid_signature`
- 戻り値が `unit` 以外: `runtime_main_invalid_signature`

#### 3.1.4 直接スクリプト モード (検討、初版では不採用)

`main` がないスクリプトを「上から順に実行」する直接モードは現状採用しない (2.6.4 のトップレベル副作用禁止と整合)。

### 3.2 プログラム ライフサイクル

```
1. パース      — 全ファイルを読み込み、AST を構築
2. 名前解決    — 識別子・モジュール・型を解決
3. 型検査      — 型システムを通す
4. 純粋性検査  — 関数の純粋性を固定点計算
5. 初期化      — モジュールを依存順にロード、CONST を評価、ホスト登録を解決
6. main 起動   — 主モジュールの main() を呼び出す
7. 実行        — 文を順に実行 (シングルスレッド)
8. 終了        — main() リターン or uncaught error
9. クリーンアップ — 不透明型のホスト リソースを解放 (3.6 参照)
```

### 3.3 ホスト呼び出しの意味論

#### 3.3.1 同期/ブロッキング

すべてのホスト呼び出しは **同期 ブロッキング**。Numadora 側スレッドは応答を待つ。

#### 3.3.2 タイムアウト

タイムアウトは **個々のホスト関数の引数で表現** する。言語レベルのタイムアウト機構はない。

```numadora
LET win = window.wait-for-title("Notepad", 10000)   # 10 秒
```

タイムアウト超過は `Option[WindowRef]` の `None` で表現する関数と、`RuntimeError` を投げる関数の両方を許容。`.numai` のシグネチャで明確化:

```numadora
EXPORT FUNC wait-for-title(title: string, timeout-ms: int): Option[WindowRef]
EXPORT FUNC focus(target: WindowRef): unit         # 失敗時 RuntimeError
```

#### 3.3.3 キャンセル

初版は **キャンセル不可**。ホスト関数はタイムアウトで自己終了する。

#### 3.3.4 ポリシー判定

各ホスト呼び出しの前に Slasher の `NumadoraPolicyEvaluator` がチェック:

| 判定 | 結果 |
|---|---|
| `allow` | 通常呼び出し |
| `deny` | `RuntimeError` を投げる (`code = "policy_denied"`) |
| `require_approval` | 未承認なら `policy_denied`、承認済みなら通常呼び出し |

ポリシー拒否は通常の `RuntimeError` として `TRY/CATCH` で捕捉可能。

#### 3.3.5 ホスト例外の正規化

C# 側で発生した例外は以下に正規化される:

| C# 例外 | Numadora `code` |
|---|---|
| `ArgumentException` | `host_invalid_argument` |
| `TimeoutException` | `host_timeout` |
| `UnauthorizedAccessException` | `host_access_denied` |
| `IOException` | `host_io_error` |
| `OperationCanceledException` | `host_cancelled` |
| `PlatformNotSupportedException` | `platform_not_supported` (Q-L4 採用) |
| `Win32Exception` | `host_win32_error` (詳細を `details` に格納) |
| その他 | `host_unknown_error` (詳細を `details.exceptionType` に格納) |

`platform_not_supported` の `details` には実行中の OS 情報を含める:

```json
{
  "code": "platform_not_supported",
  "message": "AppOps not implemented for this OS",
  "details": {
    "os": "macos",
    "plugin": "WindowsNative",
    "module": "slasher/window"
  }
}
```

これは `slasher-plugin-architecture.md` 3.2 の `/plugins` レスポンスと整合する。

各ホスト関数 (`.numai` 修飾) で **正規化マッピングを上書き可能** にする (将来検討):

```numadora
EXPORT FUNC focus(target: WindowRef): unit
  ERRORS { "Win32Exception": "window_focus_failed" }
```

### 3.4 エラー伝播

#### 3.4.1 失敗の 3 分類 (spec 3.1 を継承)

| 種別 | 表現 | catch 可否 |
|---|---|---|
| 期待される失敗 | `Option[T]` | (catch しない、値で表現) |
| 操作の失敗 | `RuntimeError` | TRY/CATCH 可 |
| プログラムの誤り | panic | **catch 不可、即終了** |

#### 3.4.2 panic の対象

- `runtime_option_unwrap_none` (`.value` で None)
- `runtime_index_out_of_bounds` (配列範囲外、文字列範囲外)
- `runtime_division_by_zero`
- `runtime_int_overflow`
- `runtime_max_iteration` (WHILE 上限超え)
- `runtime_array_modified_during_iteration`
- 型システムを欺く host 戻り値 (本来あり得ないが、防御的に panic)

panic は `TRY/CATCH` で捕捉できない。プロセス (Numadora インタプリタ) は致命終了し、Slasher は run artifact に記録する。

#### 3.4.3 RuntimeError のスタック フレーム

`RuntimeError.source.stack` には、エラーが投げられた位置から `main` までの **呼び出しフレーム** が積まれる。各フレームは:

```numadora
RECORD ErrorSourceFrame {
  file: string,
  line: int,
  function: Option[string],
}
```

ホスト呼び出し境界は `function = Some("<host:slasher/window.focus>")` のように示す。

#### 3.4.4 TRY/CATCH の範囲

`TRY` ブロック内で投げられた `RuntimeError` のみ捕捉。ブロック内で起動した別関数からの伝播も対象。`FINALLY` は常に実行 (例外あり/なし両方)。

`CATCH` 句は **1 つだけ**。エラー種別での分岐は内側で `MATCH e.code OF`。

### 3.5 並行性

#### 3.5.1 v1 はシングルスレッド

`main` は単一スレッドで実行される。並行構造 (async/await/spawn) は **採用しない**。

#### 3.5.2 ホスト並行性

ホスト関数 (Slasher 側 C#) は内部で並行処理してよいが、Numadora から見ると同期呼び出し。

#### 3.5.3 将来の async

予約語 `ASYNC`, `AWAIT` を識別子として禁止 (1.4)。将来 async 拡張するときの構文は別途設計。

### 3.6 リソース管理 / 不透明型のクリーンアップ

#### 3.6.1 GC モデル

Numadora 値は GC 管理。実装は C# 側のヒープ上に置く想定なので、C# の GC が回収する。

#### 3.6.2 不透明型のリソース解放

不透明型 (`WindowRef` 等) は外部リソース (Win32 handle 等) を握る。解放戦略:

| 戦略 | 内容 | 採用 |
|---|---|---|
| (i) finalizer | GC 回収時にホスト callback | 標準採用 |
| (ii) 明示 close | スクリプトが `app.close()` を呼ぶ | 必要なら併用 (推奨) |
| (iii) スコープ自動 (RAII) | 変数のスコープ離脱で自動解放 | 採用しない |

(i) と (ii) を併用する。`app.close()` のような明示 close 関数を `.numai` で公開し、推奨運用とする。GC fallback は安全網。

#### 3.6.3 二重 close の安全性

ホスト側で **idempotent** に実装する責任。同じ handle を 2 回 close しても OK。Numadora 言語は close 後の使用を防げない (型レベルで「閉じた後は無効」を表現できないため)。これは将来の言語拡張 (linear types) で改善余地あり。

### 3.7 出力ストリーム

#### 3.7.1 標準出力

`std/io.print(s: string)` は標準出力に出す。Slasher 統合では `numadora.log` イベントとして run timeline に記録される。

#### 3.7.2 標準エラー

別途 `std/io.eprint(s: string)` を提供する。Slasher は `numadora.log` の severity = `error` として記録。

#### 3.7.3 ステップ ロギング (Slasher 拡張)

```numadora
slasher/io.step("open notepad")
```

`slasher/io.step` は人間/AI 可読な 1 行ステップを run timeline の `step` イベントとして記録。証跡モデルの一級メンバー。

### 3.8 プログラム終了

#### 3.8.1 正常終了

`main()` がリターン → exit code 0 相当 (Slasher は `run.outcome = "succeeded"`)

#### 3.8.2 uncaught RuntimeError

`main` の外まで `RuntimeError` が伝播 → exit code 1 相当 (Slasher は `run.outcome = "failed"` と error code を記録)

#### 3.8.3 panic

panic → exit code 2 相当 (Slasher は `run.outcome = "failed"` と panic 種別を記録)

#### 3.8.4 タイムアウト (Slasher 制御)

Slasher の run-level タイムアウト (将来) は Numadora プロセスを強制終了し、`run.outcome = "timed_out"` を記録。Numadora 言語自身はタイムアウトを管理しない。

### 3.9 実行時エラーコード一覧 (3 章で追加分)

| コード | 種別 |
|---|---|
| `runtime_no_main` | main 関数なし |
| `runtime_main_invalid_signature` | main のシグネチャ不正 |
| `policy_denied` | ポリシーで拒否 |
| `host_invalid_argument` | ホスト引数不正 |
| `host_timeout` | ホスト タイムアウト |
| `host_access_denied` | ホスト アクセス拒否 |
| `host_io_error` | ホスト I/O エラー |
| `host_cancelled` | ホスト キャンセル |
| `host_win32_error` | Win32 エラー |
| `host_unknown_error` | 未分類 ホスト エラー |

---

## 第4章 全体図 (Cross-Cutting Diagram)

各システムの相互関係:

```
ファイル群 (.numa, .numai)
    │
    ▼
[パース] → AST
    │
    ▼
[名前解決] ── モジュール解決 (E章)
    │       └── パス解決 (std/, slasher/, 相対)
    │       └── .numai/.numa 統合
    ▼
[型検査] ── 型推論 (C章 1.3)
    │      └── 構造的サブタイピング
    │      └── ジェネリクス (C章 1.6)
    │      └── 不透明型 (C章 1.5)
    ▼
[純粋性検査] (base-structure.md 2.10)
    │
    ▼
[モジュール初期化] (E章 2.6)
    │      └── トポロジカル順
    │      └── CONST 評価
    │      └── ホスト登録解決
    ▼
[main 起動] (F章 3.1)
    │
    ▼
[実行ループ] ── ホスト呼び出し (F章 3.3)
    │         └── ポリシー判定
    │         └── 例外正規化
    │         └── RuntimeError 投出
    │         └── TRY/CATCH 捕捉
    ▼
[終了 + クリーンアップ] (F章 3.6, 3.8)
    └── GC + finalizer
    └── 不透明型解放
```

---

## 改訂履歴

- v0.1 — 初版起草。型システム/モジュール/実行モデルの確定方針を提示。ジェネリクスはユーザ定義関数でも許可 (spec を緩和)。トップレベル副作用禁止を確定。
