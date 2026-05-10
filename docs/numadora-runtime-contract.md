# Numadora Runtime Contract

Slasher と Numadora v0.2 ランタイムの統合境界。

`numadora-language-spec.md` の v0.2 言語仕様、`slasher-plugin-architecture.md` の
プラグイン アーキテクチャ、`slasher-numadora-integration.md` の統合契約を前提とする。

## Runtime Strategy

Slasher は `.numa` の check / run 全パスを所有する:

- Numadora 言語は **Slasher 内蔵 C# インタプリタ** で解釈・実行
- Slasher が parse / 検査 / run artifact 生成 / HTTP / MCP レスポンス整形を所有
- ホスト関数は AppOps プラグイン (`.numai` + C# `[NumadoraHostBindings]` クラス) として登録
- ポリシー判定は `NumadoraPolicyEvaluator` がホスト呼び出しごとに実行

外部ランタイム (旧 Rust プロトタイプ) は **実行時に呼ばれない**。設計参照のみ。
`cargo`, `NUMADORA_HOME`, 隣接 Rust checkout への依存はない。

## Runtime Discovery

外部ランタイム探索なし。ランタイムは Slasher プロセスの一部。

手動 check ヘルパー:

```powershell
.\scripts\check-numadora.ps1 -Path scripts\numadora-samples\notepad-check.numa
```

このヘルパーは Slasher HTTP サーバを必要に応じて起動し、`POST /scripts/check` に
スクリプトを送る。

## Repository Layout (v0.2)

```text
src/Slasher/
  AppOps/Plugins/
    WindowsNative/
      HostInterfaces/
        slasher/
          window.numai
          input.numai
          screen.numai
          element.numai
          dialog.numai
          app.numai
      WindowsNativePlugin.cs
      WindowsHostBindings.cs
      ...
    Browser/
      HostInterfaces/
        slasher/
          browser.numai
      BrowserPlugin.cs
      BrowserHostBindings.cs

scripts/
  numadora-samples/
    notepad-check.numa            # PR-B+C で v0.2 表記に書き換え予定
    excel-showcase.numa
    ...
```

`.numai` は **埋め込みリソース** として csproj 同梱 (`slasher-plugin-architecture.md`
4 章)。ランタイムは embedded resource を最優先で解決。

## Module Resolution Order

`IMPORT path AS alias` の解決順 (`numadora-language-spec.md` 6.3):

1. プラグイン埋め込みリソース (Available なプラグイン由来)
2. ワークスペース ローカル `.numai`
3. ワークスペース ローカル `.numa`
4. `std/` プレフィックス → 標準ライブラリ

不可用プラグイン (`not_applicable` / `missing_prerequisites` / `disabled`) のモジュールは
解決されず、`module_not_found` で失敗 (`details.reason = "plugin_not_available"`)。

## Check Contract

入力:

```json
{
  "language": "numadora",
  "script": "MODULE x\nIMPORT slasher/io AS io\nEXPORT FUNC main()\n  io.log(\"hello\")\nEND\n",
  "path": null,
  "workspaceRoot": "D:\\home\\source\\csharp\\slasher",
  "entryPoint": "<inline>"
}
```

成功出力:

```json
{
  "ok": true,
  "language": "numadora",
  "diagnostics": [],
  "files": [
    { "path": "<inline>", "lineCount": 5 }
  ],
  "requiredCapabilities": [
    { "module": "slasher/io", "function": "log", "class": "Observe", "profile": "observe" }
  ]
}
```

診断出力:

```json
{
  "ok": false,
  "language": "numadora",
  "diagnostics": [
    {
      "code": "module_not_found",
      "message": "module 'slasher/foo' was not found",
      "file": "scripts/example.numa",
      "line": 3,
      "column": 8,
      "severity": "error",
      "details": {
        "module": "slasher/foo",
        "reason": "plugin_not_available",
        "plugin": null
      }
    }
  ]
}
```

マッピング規則:

- 全 `code` は `numadora-language-spec.md` 付録 B のカテゴリに従う (`syntax_*`, `type_*`,
  `name_*`, `module_*`, `runtime_*`, `host_*`, `platform_*`, `policy_*`, `user_*`)
- 行・列は `.numa` 上の位置 (1-based)
- `details` には機械可読な追加情報
- check モードは GUI アクションを実行しない (INTERACTIVE 関数も呼ばない)

## Run Contract

入力:

```json
{
  "language": "numadora",
  "script": null,
  "path": "scripts/numadora-samples/notepad-check.numa",
  "workspaceRoot": "D:\\home\\source\\csharp\\slasher",
  "runId": "run-...",
  "purpose": "local-test",
  "allowInteractiveInput": false,
  "capturePolicy": {
    "captureAfterEachStep": false,
    "captureBeforeEachStep": false,
    "captureTarget": "selected"
  }
}
```

成功出力:

```json
{
  "ok": true,
  "language": "numadora",
  "exitCode": 0,
  "events": [],
  "diagnostics": [],
  "runId": "run-..."
}
```

run 中の各ホスト呼び出しは `events.jsonl` に `numadora.hostCall` イベントとして
記録される。詳細は `slasher-numadora-integration.md` の Error And Evidence Contract。

### Behavioural Notes

- `.slasher` パスと `language=slasher` は `slasher_language_removed` で拒否
- Slasher は run 前に check を preflight として実行
- `purpose` 省略時は `local-test`
- run は lineage メタデータを記録 (purpose, actor surface, entry point, script SHA-256,
  local classification, redaction mode)
- INTERACTIVE 関数は `allowInteractiveInput=true` がないと `policy_denied` で fail closed
- 承認済 INTERACTIVE 入力は送信直前にフォアグラウンド ターゲットを再検証

## Host Call Contract (in-process)

v0.2 ではテキスト RPC (`__SLASHER_HOST_CALL__` 文字列) を **廃止**。
ホスト関数は **C# 直接呼び出し** で実行される。

C# 側の登録:

```csharp
[NumadoraHostBindings("slasher/window")]
public sealed class WindowsHostBindings
{
    [NumadoraHostFunc("focus", RequiresInteractive = true)]
    public void Focus(WindowRef target) => /* ... */;

    [NumadoraHostFunc("wait-for-title")]
    public Option<WindowRef> WaitForTitle(string title, int timeoutMs) => /* ... */;
}
```

Numadora 値 ↔ C# 値の変換は登録時にチェック:

| Numadora | C# |
|---|---|
| `int` | `long` (.NET) |
| `float` | `double` |
| `bool` | `bool` |
| `string` | `string` |
| `unit` | `void` 戻り or `Unit` 値 |
| `Option[T]` | `Option<T>` (Slasher 提供型) または `T?` |
| `array[T]` | `IReadOnlyList<T>` または `List<T>` |
| `record {...}` | C# `record` 型 (シグネチャ整合チェック) |
| `OPAQUE TYPE Name` | C# `Name` クラス (内部 ID 隠蔽) |

シグネチャ不整合は起動時 `module_interface_mismatch` で失敗。

### Host Exception Normalization

C# 例外 → Numadora `RuntimeError.code` (`numadora-language-spec.md` 9.6):

| C# 例外 | code |
|---|---|
| `ArgumentException` | `host_invalid_argument` |
| `TimeoutException` | `host_timeout` |
| `UnauthorizedAccessException` | `host_access_denied` |
| `IOException` | `host_io_error` |
| `OperationCanceledException` | `host_cancelled` |
| `PlatformNotSupportedException` | `platform_not_supported` |
| `Win32Exception` | `host_win32_error` |
| その他 | `host_unknown_error` |

ポリシー拒否は `code = "policy_denied"`。

## Verification

```powershell
.\scripts\verify-numadora-n0.ps1
```

このスクリプトは Slasher の C# ランタイムが現行サンプル `.numa` を check できることを
検証する。v0.2 サンプルへの書き換え完了後に同スクリプトを再検証する。

## v0.1 → v0.2 の主な変更

| 項目 | v0.1 | v0.2 |
|---|---|---|
| ランタイム | Slasher 内蔵 C# (テキスト RPC ブリッジ並走) | Slasher 内蔵 C# (型付き直接呼び出し) |
| ホスト呼び出し | `Print("__SLASHER_HOST_CALL__ ...")` 文字列 | C# 属性 + `.numai` シグネチャ |
| リソース参照 | `"window:last"` 等の文字列ハンドル | `OPAQUE TYPE WindowRef` |
| モジュール パス | `slasher_window` (snake) | `slasher/window` (slash) |
| 関数名 | PascalCase (`StartApp`) | kebab-case (`start-app`) |
| 型表記 | `Int`, `String`, `Array<T>` | `int`, `string`, `array[T]` |
| LET 区切り | `:=` | `=` |
