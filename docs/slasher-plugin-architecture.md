# Slasher AppOps プラグイン アーキテクチャ 設計ノート

> Status: 設計ドキュメント。
> `slasher-layer-architecture.md` の AppOps 層を **プラグイン ホスト** として詳細化する。
>
> 確定方針:
> - AppOps は単なる「Windows 自動化」ではなく、**外部アプリケーション / GUI を制御するプラグイン群** をホストする層
> - ブラウザ (Selenium) / Windows native だけでなく、Excel / GIMP / Photoshop / AutoCAD など、各種アプリ統合をプラグインとして追加可能
> - v1 は **静的プラグイン** (csproj に同梱)、v2 以降で動的ロード (`AssemblyLoadContext`) を検討

---

## 第1章 プラグイン モデルの全体像

### 1.1 概念

```
┌─────────────────────────────────────────────────────┐
│ AppOps Layer                                        │
│ ┌─────────────────────────────────────────────────┐ │
│ │ PluginHost                                      │ │
│ │  - Discovery / Registration / Lifecycle         │ │
│ │  - Plugin contract (IAppOpsPlugin)              │ │
│ │  - Capability/availability check                │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────┬─────────────┬──────────┬──────────┐ │
│ │ Windows     │ Browser     │ Excel    │ Gimp     │ │
│ │ Native      │ (Selenium)  │ (COM/    │ (Script  │ │
│ │ (Win32)     │             │  OOXML)  │  -Fu)    │ │
│ │             │             │          │          │ │
│ │ slasher/    │ slasher/    │ slasher/ │ slasher/ │ │
│ │  window     │  browser    │  excel   │  gimp    │ │
│ │  input      │             │          │          │ │
│ │  screen     │             │          │          │ │
│ │  element    │             │          │          │ │
│ └─────────────┴─────────────┴──────────┴──────────┘ │
│        ↑              ↑          ↑          ↑       │
│   built-in v1    built-in v1   future    future    │
└─────────────────────────────────────────────────────┘
```

### 1.2 プラグインの 2 形態

AppOps プラグインは **2 つのパターン** に分類される。

#### (A) OS ネイティブ プラグイン

OS 標準の GUI/プロセス制御を提供する。同じ Numadora モジュール (`slasher/window` 等) を **OS ごとに別実装** で提供する。

| プラグイン | OS | 提供する Numadora モジュール |
|---|---|---|
| WindowsNative | Windows | `slasher/window`, `slasher/input`, `slasher/screen`, `slasher/element`, `slasher/dialog` |
| MacOSNative (将来) | macOS | (同上) |
| LinuxNative (将来) | Linux | (同上) |

ランタイムで OS 検出により **1 つだけがアクティブ** になる。

#### (B) アプリ固有プラグイン

特定アプリの操作を提供する。各プラグインは **独自の Numadora モジュール** を提供する。

| プラグイン | 提供する Numadora モジュール | 前提条件 |
|---|---|---|
| Browser | `slasher/browser` | (なし、Selenium 同梱) |
| Excel (将来) | `slasher/excel` | Office 導入済 (COM) または OOXML 直接処理 |
| Gimp (将来) | `slasher/gimp` | GIMP 導入済 + Script-Fu サーバ起動 |
| Photoshop (将来) | `slasher/photoshop` | Photoshop 導入済 |
| AutoCAD (将来) | `slasher/autocad` | AutoCAD 導入済 |

各プラグインは前提条件をチェックし、**満たさなければモジュールを公開しない**。スクリプト側は `IMPORT slasher/excel AS excel` が `module_not_found` で失敗する。

### 1.3 v1 のスコープ

v1 で同梱するプラグイン:

| プラグイン | 形態 | 状態 |
|---|---|---|
| **WindowsNative** | OS ネイティブ | 実装済 (現 `Windows/`) |
| **Browser** | アプリ固有 | 実装済 (現 `BrowserAutomationService`) |
| **MacOSNative** | OS ネイティブ | interface のみ、実装は `UnsupportedAppLauncher` |
| **LinuxNative** | OS ネイティブ | interface のみ、実装は `UnsupportedAppLauncher` |

v1 では他のアプリ固有プラグインは含めない。Excel/GIMP は別 PR / 別フェーズ。

---

## 第2章 プラグイン契約 (Plugin Contract)

### 2.1 IAppOpsPlugin

```csharp
namespace Slasher.Core.AppOps;

public interface IAppOpsPlugin
{
    /// <summary>プラグインの一意識別子。例: "WindowsNative", "Browser", "Excel"</summary>
    string Name { get; }

    /// <summary>プラグインのバージョン。Slasher 本体と独立に変えてよい</summary>
    Version Version { get; }

    /// <summary>このプラグインが提供する Numadora ホスト モジュール (例: ["slasher/window", "slasher/input"])</summary>
    IReadOnlyList<string> HostModules { get; }

    /// <summary>動作要件 (OS, 必要ソフトウェア)</summary>
    PluginRequirements Requirements { get; }

    /// <summary>現環境で動作可能か判定</summary>
    PluginAvailability CheckAvailability();

    /// <summary>プラグイン登録 (DI, ホスト バインディング, エンドポイント)</summary>
    void Register(IPluginRegistration registration);
}

public sealed record PluginRequirements(
    IReadOnlyList<string> SupportedOS,           // ["windows"], ["windows","macos","linux"]
    IReadOnlyList<string> RequiredSoftware,      // ["microsoft.office.excel"]
    IReadOnlyList<string> Capabilities           // ["process-app","observe","user-input"]
);

public enum PluginAvailability
{
    Available,                  // 動作可能、登録に進む
    NotApplicable,              // この OS 等では適用外 (例: WindowsNative on Mac)
    MissingPrerequisites,       // 必要ソフトウェア未導入 (例: Excel 未インストール)
    Disabled                    // 設定で無効化されている
}
```

### 2.2 IPluginRegistration

```csharp
public interface IPluginRegistration
{
    /// <summary>このプラグイン用の設定 (Plugins:&lt;Name&gt; セクションにスコープ済) — Q-P2 採用</summary>
    IConfiguration Configuration { get; }

    /// <summary>Numadora ホスト バインディング クラスを登録</summary>
    void RegisterHostBindings<T>() where T : class;

    /// <summary>プラグイン同梱の .numai 埋め込みリソースを開く (Stream 返却で動的ロード対応 — Q-P1 採用)</summary>
    Stream OpenEmbeddedNumaiResource(string logicalPath);

    /// <summary>HTTP エンドポイント グループを登録</summary>
    void RegisterEndpointGroup(string prefix, Action<RouteGroupBuilder> configure);

    /// <summary>DI サービスを登録 (プラグイン内部使用)</summary>
    void RegisterService<TInterface, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TInterface : class
        where TImpl : class, TInterface;

    /// <summary>OS ネイティブ プラグイン: 共通 interface に対する OS 固有実装を登録</summary>
    void RegisterOSNativeImpl<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface;

    /// <summary>プラグイン用ロガー</summary>
    ILogger Logger { get; }
}
```

#### 2.2.1 設定の渡し方 (Q-P2 採用)

`appsettings.json` の `Plugins:<Name>` セクションが各プラグインにスコープして渡される。
環境変数 `Plugins__<Name>__<Key>` で上書き可能 (ASP.NET Core 標準)。

```json
// appsettings.json
{
  "Plugins": {
    "Browser": {
      "DefaultBrowser": "edge",
      "WebDriverPath": null,
      "DownloadDir": "C:\\Slasher\\downloads"
    },
    "Excel": {
      "ComTimeoutMs": 30000,
      "Enabled": true
    }
  }
}
```

`Plugins:<Name>:Enabled = false` で `CheckAvailability()` が `Disabled` を返す規約とする。

HTTP API 経由の動的設定変更 (`POST /plugins/<name>/config`) は **不採用**。設定変更は再起動。

### 2.3 OS ネイティブの interface 群 (Q-L1 適用)

`Core/AppOps/Abstractions/` に定義。OS ネイティブ プラグインだけが実装する。

```csharp
public interface IAppLauncher        { ... }   // start-app, enumerate-processes
public interface IWindowControl      { ... }   // focus, move, resize, state
public interface IInputSimulator     { ... }   // text, keys, mouse, wheel, drag
public interface IScreenCapture      { ... }   // capture-full, capture-monitor
public interface IElementInspector   { ... }   // find, exists, read-text, tree
public interface IDialogHost         { ... }   // message, confirm
```

WindowsNative プラグイン:
```csharp
public sealed class WindowsNativePlugin : IAppOpsPlugin
{
    public string Name => "WindowsNative";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<string> HostModules => new[] {
        "slasher/window", "slasher/input", "slasher/screen",
        "slasher/element", "slasher/dialog",
    };
    public PluginRequirements Requirements => new(
        SupportedOS: new[] { "windows" },
        RequiredSoftware: Array.Empty<string>(),
        Capabilities: new[] { "process-app", "user-input", "observe", "system-info" });

    public PluginAvailability CheckAvailability()
        => OperatingSystem.IsWindows() ? PluginAvailability.Available : PluginAvailability.NotApplicable;

    public void Register(IPluginRegistration r)
    {
        r.RegisterOSNativeImpl<IAppLauncher, WindowsAppLauncher>();
        r.RegisterOSNativeImpl<IWindowControl, WindowsWindowControl>();
        r.RegisterOSNativeImpl<IInputSimulator, WindowsInputSimulator>();
        r.RegisterOSNativeImpl<IScreenCapture, WindowsScreenCapture>();
        r.RegisterOSNativeImpl<IElementInspector, WindowsElementInspector>();
        r.RegisterOSNativeImpl<IDialogHost, WindowsDialogHost>();
        r.RegisterHostBindings<WindowsHostBindings>();   // Numadora ホスト関数集約
    }
}
```

### 2.4 アプリ固有プラグインの構造 (interface 不要)

アプリ固有プラグインは横展開がない (実装が 1 つだけ) ので、共通 interface を持たない。Numadora ホスト バインディング クラスを直接登録する。

```csharp
public sealed class BrowserPlugin : IAppOpsPlugin
{
    public string Name => "Browser";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<string> HostModules => new[] { "slasher/browser" };
    public PluginRequirements Requirements => new(
        SupportedOS: new[] { "windows", "macos", "linux" },
        RequiredSoftware: Array.Empty<string>(),
        Capabilities: new[] { "process-app", "user-input", "observe" });

    public PluginAvailability CheckAvailability() => PluginAvailability.Available;

    public void Register(IPluginRegistration r)
    {
        r.RegisterService<BrowserAutomationService, BrowserAutomationService>();
        r.RegisterHostBindings<BrowserHostBindings>();
        r.RegisterEndpointGroup("/browser", routes => routes.MapBrowserEndpoints());
    }
}
```

#### 2.4.1 Capabilities フィールドの意味 (Q-S* 整合)

`PluginRequirements.Capabilities` はプラグインが提供する **能力クラスの自己宣言**:

- `numadora-language-spec.md` 1.4.1 の 13 種から選ぶ
- プラグインの `.numai` の `EXPORT EFFECT(class) FUNC` で使用される能力クラスの **集合** と一致する必要あり (起動時整合検査: `plugin_capabilities_mismatch`)
- `/plugins` エンドポイントに表示
- `slasher-numadora-integration.md` の能力テーブルと突き合わせて運用ドキュメント生成可

---

## 第3章 プラグイン ライフサイクル

### 3.1 起動時シーケンス

```
1. Slasher 起動
   ↓
2. PluginHost: 全 IAppOpsPlugin インスタンスを集める (コンストラクタ DI 経由)
   ↓
3. 各プラグインに CheckAvailability() を呼ぶ
   ↓
4. Available なプラグインだけを残す
   ├── 重複 HostModule の検出 (2 つの Available プラグインが同じ slasher/xxx を提供 → 起動失敗)
   ↓
5. 各 Available プラグインに Register() を呼ぶ
   ↓
6. PluginHost: 登録された .numai リソースを Numadora モジュール解決に提供
   ↓
7. PluginHost: 登録された HTTP エンドポイント グループを Api 層にマップ
   ↓
8. プラグイン状態を /plugins エンドポイントで参照可能に
```

### 3.2 不可用プラグインの可視化

`/plugins` エンドポイントで全プラグインの状態を返す:

```json
GET /plugins
{
  "plugins": [
    { "name": "WindowsNative",  "version": "1.0.0", "status": "available",
      "hostModules": ["slasher/window","slasher/input","slasher/screen",...] },
    { "name": "MacOSNative",    "version": "1.0.0", "status": "not_applicable",
      "reason": "OS=Windows (requires macos)" },
    { "name": "Browser",        "version": "1.0.0", "status": "available",
      "hostModules": ["slasher/browser"] },
    { "name": "Excel",          "version": "0.1.0", "status": "missing_prerequisites",
      "reason": "Office not installed", "hostModules": ["slasher/excel"] }
  ]
}
```

これで AI/人間が「なぜこの操作が動かないのか」を即座に確認できる。

### 3.3 Numadora モジュール解決との接続

`numadora-core-systems.md` 2.2.1 (`.numai`/`.numa` 解決順序) に **プラグイン埋め込み** を最優先で追加:

```
1. プラグイン埋め込みリソース (Available プラグイン由来)
2. ワークスペース ローカル .numai
3. ワークスペース ローカル .numa
4. std/ プレフィックス → 標準ライブラリ
```

`IMPORT slasher/excel AS excel` のとき:
- Excel プラグインが Available なら .numai を提供 → 解決
- 不可用なら `module_not_found` で `details: { "reason": "plugin_not_available", "plugin": "Excel" }` を返す

---

## 第4章 .numai リソース管理

### 4.1 配置 (Q-L6 反映)

各プラグインが自分の `.numai` を **埋め込みリソース** として持つ。

```
src/Slasher/AppOps/Plugins/
  WindowsNative/
    HostInterfaces/
      slasher/
        window.numai
        input.numai
        screen.numai
        element.numai
        dialog.numai
    WindowsNativePlugin.cs
    WindowsAppLauncher.cs
    WindowsWindowControl.cs
    ...
  Browser/
    HostInterfaces/
      slasher/
        browser.numai
    BrowserPlugin.cs
    BrowserAutomationService.cs
    BrowserHostBindings.cs
```

csproj:
```xml
<ItemGroup>
  <EmbeddedResource Include="AppOps/Plugins/**/HostInterfaces/**/*.numai">
    <LogicalName>numai/%(RecursiveDir)%(Filename)%(Extension)</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

論理名は `numai/slasher/window.numai` のような統一形式。`PluginRegistration.OpenEmbeddedNumaiResource("slasher/window.numai")` で各プラグインが自分のリソースにアクセスする。

### 4.2 衝突の検出

複数プラグインが同じ論理名 `.numai` を持つことは **起動時エラー** (`plugin_numai_conflict`)。

OS ネイティブ プラグイン群 (Windows/Mac/Linux) は同じ `slasher/window.numai` を持つが、`CheckAvailability()` で 1 つだけ Available になるので運用上は衝突しない。

---

## 第5章 ディレクトリ レイアウト最終形

```
src/Slasher/
  Api/                        — HTTP server
    Endpoints/
    Contracts/
  Core/                       — Numadora interpreter, runs, models, plugin abstractions
    Numadora/                 — interpreter, parser, runtime
    Runs/
    Models/
    AppOps/
      Abstractions/           — IAppLauncher, IWindowControl, IInputSimulator, etc.
      IAppOpsPlugin.cs
      PluginRequirements.cs
      IPluginRegistration.cs
  Io/
    Files/
    Data/
    Clipboard/
  Network/
    Peers/
    Discovery/                — (将来 mDNS)
  AppOps/                     — プラグイン群と PluginHost
    PluginHost/               — 起動時の discovery, registration, lifecycle
      PluginRegistry.cs
      PluginRegistrationImpl.cs
      PluginHostExtensions.cs
    Plugins/
      WindowsNative/          — built-in v1
        HostInterfaces/
          slasher/
            window.numai
            input.numai
            screen.numai
            element.numai
            dialog.numai
        WindowsNativePlugin.cs
        WindowsAppLauncher.cs
        WindowsWindowControl.cs
        WindowsInputSimulator.cs
        WindowsScreenCapture.cs
        WindowsElementInspector.cs
        WindowsDialogHost.cs
        WindowsHostBindings.cs
        Native/                — P/Invoke
          NativeMethods.cs
          NativeStructs.cs
      Browser/                — built-in v1
        HostInterfaces/
          slasher/
            browser.numai
        BrowserPlugin.cs
        BrowserAutomationService.cs
        BrowserHostBindings.cs
      MacOSNative/            — interface のみ、実装空 (将来)
        MacOSNativePlugin.cs   ← CheckAvailability で NotApplicable を返す
      LinuxNative/            — interface のみ、実装空 (将来)
        LinuxNativePlugin.cs
  wwwroot/
  Program.cs
```

---

## 第6章 依存方向と規律 (NetArchTest)

### 6.1 追加ルール

`tests/Slasher.Tests/Architecture/DependencyTests.cs` に追加:

```csharp
[Fact]
public void Plugins_must_only_depend_on_Core_and_their_own_namespace()
{
    // 各プラグインは Core と自分の namespace 内だけに依存可
    // 他プラグインを直接参照するのは禁止 (プラグイン間の独立性)
    foreach (var plugin in EnumeratePluginNamespaces())
    {
        var result = Types.InAssembly(SlasherAssembly)
            .That().ResideInNamespaceStartingWith(plugin)
            .ShouldNot().HaveDependencyOnAny(OtherPluginNamespaces(plugin))
            .GetResult();
        Assert.True(result.IsSuccessful);
    }
}

[Fact]
public void PluginHost_must_not_depend_on_specific_plugins()
{
    var result = Types.InAssembly(SlasherAssembly)
        .That().ResideInNamespaceStartingWith("Slasher.AppOps.PluginHost")
        .ShouldNot().HaveDependencyOnAny(
            "Slasher.AppOps.Plugins.WindowsNative",
            "Slasher.AppOps.Plugins.Browser",
            "Slasher.AppOps.Plugins.MacOSNative",
            "Slasher.AppOps.Plugins.LinuxNative")
        .GetResult();
    Assert.True(result.IsSuccessful);
}
```

これにより:
- プラグイン間の意図しない結合を防ぐ
- PluginHost が新プラグインを「知らないまま」扱える設計が強制される

### 6.2 命名規則

- プラグイン namespace: `Slasher.AppOps.Plugins.<PluginName>`
- 各プラグインのエントリ: `<PluginName>Plugin` クラス
- ホスト バインディング集約: `<PluginName>HostBindings` クラス

---

## 第7章 配布戦略への影響

### 7.1 v1: 全プラグイン同梱

v1 では built-in プラグイン (WindowsNative, Browser, Mac/Linux スタブ) を全て同梱。`dotnet publish` の単一ファイル化で 1 EXE に収まる。

### 7.2 v2 以降: オプショナル プラグイン

将来、Excel/GIMP/Photoshop のような重い依存を持つプラグインを **オプショナル パッケージ** にする可能性:

選択肢:
- (i) NuGet パッケージとして提供、ユーザが csproj に追加してビルド
- (ii) `AssemblyLoadContext` で実行時にディレクトリから DLL ロード
- (iii) 別プロセスとして起動、IPC で連携

これは v1 のスコープ外。設計だけ視野に入れる。

### 7.3 Selenium 同梱の判断

Selenium WebDriver + 各 Driver (Chrome/Firefox/Edge) は数 MB〜10 MB 級。これを Browser プラグインの依存として全 Slasher ビルドに含めるのが現状。

将来 Browser プラグインを別パッケージ化する余地があるが、Browser はかなり頻用されるので **v1 では同梱維持**。

---

## 第8章 既存コードの移行マッピング (Q-L3 ハードカット適用)

| 現在 | 移行先 |
|---|---|
| `src/Slasher/Windows/WindowsAutomationService.*.cs` | `AppOps/Plugins/WindowsNative/Windows*.cs` (各 interface 実装に分割) |
| `src/Slasher/Windows/NativeMethods.*.cs` | `AppOps/Plugins/WindowsNative/Native/NativeMethods.cs` |
| `src/Slasher/Windows/NativeStructs.cs` | `AppOps/Plugins/WindowsNative/Native/NativeStructs.cs` |
| `src/Slasher/Windows/BrowserAutomationService.cs` | `AppOps/Plugins/Browser/BrowserAutomationService.cs` |
| `src/Slasher/Windows/ClipboardService.cs` | `Io/Clipboard/ClipboardService.cs` |
| `src/Slasher/WindowHandle.cs` | `Core/AppOps/Abstractions/WindowHandle.cs` (共通モデル) |

**`src/Slasher/Windows/` ディレクトリは消滅**。Windows 専用コードは `AppOps/Plugins/WindowsNative/` に移動。

`WindowsAutomationService` の partial 群 (`Apps`, `Input`, `Windows`, `Capture`, `Elements`, `ImageMatching`, `Dialogs`, `WindowActions`, `WindowHelpers`, `InputHelpers`, `CaptureHelpers`) は対応する **interface 実装クラス** に分割される:
- `Apps.cs` → `WindowsAppLauncher.cs`
- `Input.cs` + `InputHelpers.cs` → `WindowsInputSimulator.cs`
- `Windows.cs` + `WindowActions.cs` + `WindowHelpers.cs` → `WindowsWindowControl.cs`
- `Capture.cs` + `CaptureHelpers.cs` → `WindowsScreenCapture.cs`
- `Elements.cs` → `WindowsElementInspector.cs`
- `Dialogs.cs` → `WindowsDialogHost.cs`
- `ImageMatching.cs` → `WindowsScreenCapture.cs` の一部 (画像マッチは画面キャプチャの延長)

これは PR-1 (フォルダ移動) と PR-3 (interface 切り出し) を **1 PR にまとめる** ことになる (Q-L3 ハードカット方針)。

---

## 第9章 PR 分割案 (改訂)

[slasher-layer-architecture.md 6.1](docs/slasher-layer-architecture.md) の PR-1〜PR-3 を統合し、以下に変更:

| PR | 内容 | 依存 |
|---|---|---|
| **PR-1** | フォルダ移動 + namespace 一括書き換え + WindowsAutomationService の interface 分割 + IAppOpsPlugin 契約導入 + WindowsNativePlugin/BrowserPlugin の作成 (ハードカット) | (前提) |
| **PR-2** | NetArchTest 導入 + 依存方向テスト + プラグイン独立性テスト | PR-1 |
| **PR-3** | DI 登録を PluginHost ベースに書き換え、OS 検出と CheckAvailability 実装 | PR-1 |
| **PR-4** | `[SupportedOSPlatform("windows")]` 属性付与、Mac/Linux 警告対応、UnsupportedXxx スタブ実装 | PR-3 |
| **PR-5** | self-contained + single-file publish CI ジョブ追加 (Windows 用) | PR-1 |
| **PR-6** | Mac/Linux 用 publish ジョブ追加 (Browser のみ動作する状態) | PR-4, PR-5 |
| **PR-7** | trimming 検証 + トリマー設定 | PR-5 |
| **PR-8** | `/plugins` エンドポイントと プラグイン状態の HTTP 公開 | PR-3 |

PR-1 のサイズが大きくなるが、**機械的な移動 + interface 分割** が大半なので diff レビューは可能。

---

## 第10章 採用済み Q-L*

このノートで採用された Q-L* の答え:

| Q | 採用 | 反映 |
|---|---|---|
| L1 | interface 命名: 短い動詞-対象、モジュール毎 | 2.3 (`IAppLauncher` 等) |
| L2 | **AppOps はプラグイン ホスト、Excel/GIMP 等を将来追加** | **本ノート全体** |
| L3 | ハードカット (1 PR で全部) | 8 章、9 章 PR-1 |
| L4 | `PlatformNotSupportedException` → 正規化 | 3.2 (`/plugins` で可視化、ホスト呼び出し時は `RuntimeError("platform_not_supported")`) |
| L5 | AppOps 定義を「外部 GUI 制御」に変更 | 1.1 (プラグイン ホスト) |
| L6 | `.numai` は埋め込みリソース、各プラグイン所有 | 4 章 |

---

## 第11章 Q-P* 確定事項

すべての Q-P* は採用済 (一括採用)。以下に確定内容を記載する。

| Q | 確定内容 | 反映先 |
|---|---|---|
| **P1** 動的ロード | v1 静的、v2 で `AssemblyLoadContext` 検討。**契約は動的対応前提で設計** (interface 経由、`Stream` でリソース取得) | 2.1, 2.2 (`OpenEmbeddedNumaiResource` の `Stream` 返却) |
| **P2** 設定 | `appsettings.json` の `Plugins:<Name>` + 環境変数上書き。HTTP API 不採用 | 2.2 (`IConfiguration Configuration`)、2.2.1 |
| **P3** 共有依存 | 汎用 → Core/Utilities/、AppOps 横断 → Core/AppOps/Abstractions/ interface。OS 固有 P/Invoke は OS ネイティブ プラグイン内に閉じ、interface だけ公開 | 6.1 NetArchTest で plugin 間直接依存禁止 |
| **P4** ID 体系 | name/module の双方向辞書 (`StringComparer.Ordinal`)、衝突は起動エラー (`plugin_module_conflict`)。precedence 設定は将来 | 11.4 命名規約 (下記) |
| **P5** バージョニング | v1 は `/plugins` 表示のみ。将来 `IMPORT REQUIRES >= 2.0` 余地を `numadora-core-systems.md` 2.5.3 に注記 | `numadora-core-systems.md` 2.5.3 |
| **P6** EFFECT 修飾 | 明示必須を維持 (std と plugin で規則一致)。プラグイン作者は副作用ありホスト関数に `EXPORT EFFECT FUNC` を付ける | spec 改訂対象 |

### 11.1 動的ロード対応の契約設計 (Q-P1)

v1 は静的だが、契約は以下の制約で動的対応の余地を残す:

- `IAppOpsPlugin` 等のすべての契約は interface
- `OpenEmbeddedNumaiResource` の戻りは `Stream` (ファイル/埋め込みどちらも実装可能)
- DI 登録は `Type` パラメータ経由 (`RegisterHostBindings<T>`) → AssemblyLoadContext から型参照しやすい
- プラグイン名・モジュール名は文字列ベースで管理 (型名に縛られない)

これにより v2 で `AssemblyLoadContext` 経由ロードを追加しても契約変更なし。

### 11.2 プラグイン設定の検証 (Q-P2)

各プラグインは `Register()` 内で `Configuration` を読み、不正な値は **起動時に例外を投げる** (fail-fast):

```csharp
public void Register(IPluginRegistration r)
{
    var config = r.Configuration.Get<BrowserPluginConfig>()
        ?? throw new InvalidOperationException("Browser plugin config missing");
    if (config.DefaultBrowser is null)
        throw new InvalidOperationException("Plugins:Browser:DefaultBrowser is required");
    // ...
}
```

Slasher は最初の起動失敗で全プラグイン状態を `/plugins` 互換エラー レスポンスとして返し、ログに記録する。

### 11.3 共有ヘルパの配置 (Q-P3)

```
Core/
  Utilities/                    — 汎用 (色変換、文字列処理など、副作用なし)
  AppOps/
    Abstractions/               — クロスプラグイン interface (IWindowControl 等)
    PluginHost/                 — PluginRegistry, IAppOpsPlugin 契約
AppOps/
  Plugins/
    WindowsNative/
      Native/                   — Win32 P/Invoke (このプラグイン内に閉じる)
```

NetArchTest 追加ルール (`slasher-plugin-architecture.md` 6.1 既存):

```csharp
[Fact]
public void Plugins_must_only_depend_on_Core_and_their_own_namespace() { ... }
```

Excel プラグインが `IWindowControl` を使うときは Core interface 経由 (DI で WindowsNative 由来の実装が注入される)。Excel から WindowsNative に **直接** 依存することは禁止。

### 11.4 命名規約の確定 (Q-P4)

| 概念 | 規約 | 例 |
|---|---|---|
| プラグイン名 | PascalCase, ASCII | `WindowsNative`, `Browser`, `Excel` |
| Numadora モジュール パス | kebab-case + `slasher/` | `slasher/window`, `slasher/browser` |
| C# namespace | `Slasher.AppOps.Plugins.<PluginName>` | `Slasher.AppOps.Plugins.WindowsNative` |
| プラグイン エントリ クラス | `<PluginName>Plugin` | `WindowsNativePlugin` |
| ホスト バインディング集約 | `<PluginName>HostBindings` | `WindowsHostBindings` |
| 設定型 | `<PluginName>PluginConfig` | `BrowserPluginConfig` |

`PluginRegistry` 実装:

```csharp
public sealed class PluginRegistry
{
    private readonly Dictionary<string, IAppOpsPlugin> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IAppOpsPlugin> _byModule = new(StringComparer.Ordinal);

    public IAppOpsPlugin? FindByName(string name) => _byName.GetValueOrDefault(name);
    public IAppOpsPlugin? FindByModule(string module) => _byModule.GetValueOrDefault(module);
    public IReadOnlyCollection<IAppOpsPlugin> All => _byName.Values;
}
```

### 11.5 PR-1 への取り込み

これらの確定事項を反映するため、9 章 PR-1 に以下を追加:

- `appsettings.json` に `Plugins:` セクションのスキーマ例を追加
- `Plugins:Browser`, `Plugins:WindowsNative` の最小設定を含める
- `IPluginRegistration.Configuration` を契約に含める
- `BrowserPluginConfig` / `WindowsNativePluginConfig` を実装

### 11.6 spec 改訂タスク (別 PR)

Q-P5 と P6 は Numadora 言語 spec (`numadora-language-spec.md`) の改訂を伴う:

- `EXPORT EFFECT FUNC` 文法の追加 (Q-D3 + P6)
- `IMPORT ... REQUIRES >= x.y` の将来余地として明記 (Q-P5、本体導入は v2)

これは `numadora-language-redesign.md` 8 章「spec への必要な改訂」リストに追加する。

---

## 改訂履歴

- v0.1 — 初版起草。AppOps をプラグイン ホスト化、Q-L1〜L6 を採用、Q-P1〜P6 を新たに提示。
