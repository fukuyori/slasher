# Numadora Host Interface Files (`.numai`)

Slasher が提供する Numadora ホスト モジュールのシグネチャ宣言。
v0.2 spec (`docs/numadora-language-spec.md`) の `OPAQUE TYPE` / `EXPORT EFFECT(class) FUNC` /
`EXPORT INTERACTIVE EFFECT(class) FUNC` 形式で記述 (能力クラス必須化はハードカット採用済)。

## 位置付け

これらは **参照用ファイル**。最終的には AppOps プラグイン (`docs/slasher-plugin-architecture.md`)
配下の埋め込みリソースに移動される:

```text
src/Slasher/AppOps/Plugins/
  WindowsNative/
    HostInterfaces/
      slasher/
        app.numai          ← 移動先
        window.numai
        ...
  Browser/
    HostInterfaces/
      slasher/
        browser.numai
```

移動は Lang PR-D / PR-E (`docs/numadora-migration-plan.md`) で実施される。
それまで本ディレクトリは:

- AI / 開発者がホスト関数のシグネチャを参照する場所
- v0.2 構文サンプルとして機能
- ホスト関数の最終決定の集積場所

## 構成

| ファイル | 提供プラグイン (将来) | 概要 |
|---|---|---|
| `slasher/app.numai` | WindowsNative | アプリ起動・プロセス操作・ウィンドウ列挙 |
| `slasher/window.numai` | WindowsNative | ウィンドウ単体操作 (focus, state, capture, close) |
| `slasher/input.numai` | WindowsNative | キーボード・マウス入力 |
| `slasher/screen.numai` | WindowsNative | 画面キャプチャ |
| `slasher/element.numai` | WindowsNative | UI 要素 (UIA / 子ウィンドウ) |
| `slasher/dialog.numai` | WindowsNative | ローカル メッセージ ボックス |
| `slasher/browser.numai` | Browser | Selenium ベースのブラウザ操作 |
| `slasher/io.numai` | (Slasher built-in) | step / log / wait |
| `slasher/test.numai` | (Slasher built-in) | アサート |
| `slasher/clipboard.numai` | (Io 層から公開、TBD) | クリップボード |
| `slasher/files.numai` | (Io 層から公開) | ファイル操作 |
| `slasher/data.numai` | (Io 層から公開) | CSV / JSON / Excel |
| `slasher/peer.numai` | (Network 層から公開) | ピア通信、namespace 読み取り、委譲 run |

## 命名規約

`docs/slasher-script.md` および `docs/slasher-plugin-architecture.md` 11.4 に従う:

- モジュール パス: `slasher/<name>` (kebab-case)
- 関数名: kebab-case (`start-app`, `wait-for-title`)
- 型名: UpperCamelCase (`AppRef`, `WindowInfo`, `WindowState`)
- 不透明型は `EXPORT OPAQUE TYPE Name` で `.numai` 内のみ宣言
- 副作用ありホスト関数は `EXPORT EFFECT(class) FUNC` (能力クラス必須)
- ユーザ承認必須 (`allowInteractiveInput`) は `EXPORT INTERACTIVE EFFECT(class) FUNC`
- 能力クラスは 13 種 (observe / file-read / file-write / destructive / user-input /
  browser-data / clipboard / process-app / network-out / network-in / peer-delegate /
  secrets / unattended / scheduling / system-info、ただし scheduling は将来予約) から選ぶ
