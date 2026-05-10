# Slasher 5 層アーキテクチャ 設計ノート

> Status: 設計ドキュメント。
>
> 確定方針:
> - 言語ホスト: **C# (.NET 8) で統一**。Rust 移行は採用しない。
> - 動機: **実装規律と配布容易さが本命**。Mac/Linux 動作は副次的だが、層分割で素地を残す。
> - csproj 構成: 単一 `Slasher.csproj` を維持。namespace/フォルダ + アーキテクチャ テスト で規律強制。
> - 5 層分割: API / Core / IO / Network / AppOps。
>
> 関連:
> - `numadora-language-redesign.md` — 言語再構成 (Numadora は Core 配下)
> - `numadora-base-structure.md` — 字句/意味
> - `numadora-core-systems.md` — 型/モジュール/実行モデル
> - `slasher-plugin-architecture.md` — **AppOps はプラグイン ホスト** (Q-L2 拡張版)

---

## 第1章 5 層構成

### 1.1 全体図

```
┌────────────────────────────────────────────────────────┐
│  Api                                                   │
│  HTTP server, endpoints, request/response contracts    │
└─────┬──────────────┬──────────────┬───────────────────┘
      │              │              │
      ▼              ▼              ▼
┌──────────┐ ┌──────────┐ ┌──────────────────────────────┐
│  Io      │ │ Network  │ │  AppOps                      │
│  Files / │ │ Peers /  │ │  ┌──────────────────────────┐│
│  Data /  │ │ HTTP /   │ │  │ Common (interfaces)      ││
│  Clip-   │ │ Discov-  │ │  ├──────────────────────────┤│
│  board   │ │ ery      │ │  │ Windows  │ MacOS │ Linux ││
└──┬───────┘ └────┬─────┘ │  │ (impl)   │ (TBD) │ (TBD) ││
   │              │       │  └──────────────────────────┘│
   │              │       └────────────┬─────────────────┘
   │              │                    │
   └──────────────┴──── ▶ ─────────────┘
                  │
                  ▼
         ┌─────────────────┐
         │ Core            │
         │ Numadora /      │
         │ Runs / Policy / │
         │ Models          │
         └─────────────────┘
```

### 1.2 各層の責務

| 層 | 責務 | C# namespace |
|---|---|---|
| **Api** | HTTP エンドポイント、リクエスト/レスポンス DTO、認証、ルーティング | `Slasher.Api` |
| **Core** | Numadora インタプリタ、ポリシー評価、run artifact、共通モデル、抽象 interface | `Slasher.Core` |
| **Io** | ファイル/フォルダ操作、CSV/JSON/Excel 読み書き、クリップボード | `Slasher.Io` |
| **Network** | ピア間通信、HTTP クライアント、ディスカバリ (mDNS 等) | `Slasher.Network` |
| **AppOps** | 外部アプリケーション / GUI を制御する **プラグイン ホスト**。Windows native, Browser, Excel, GIMP 等を各々プラグインとして収容 (詳細は `slasher-plugin-architecture.md`) | `Slasher.AppOps` |

### 1.3 依存方向 (Dependency Direction)

許される依存方向:

```
Api  ──▶ Core, Io, Network, AppOps
Io   ──▶ Core
Network ──▶ Core
AppOps  ──▶ Core
Core ──▶ (none)
```

**Core はどの層にも依存しない**。Numadora インタプリタや run artifact は副作用層 (Io/Network/AppOps) を **interface 経由でしか触らない**。実装は外部 (DI) から注入される。

---

## 第2章 既存コードのマッピング

### 2.1 移動先

| 現在 | 新しい場所 |
|---|---|
| `src/Slasher/Api/SlasherEndpointExtensions.*.cs` | `Api/Endpoints/` |
| `src/Slasher/Api/Requests.cs` | `Api/Contracts/` |
| `src/Slasher/Automation/ScriptRunService.*.cs` (Numadora interpreter 一式) | `Core/Numadora/` |
| `src/Slasher/Automation/NumadoraPolicyEvaluator.cs` | `Core/Numadora/` |
| `src/Slasher/Automation/AutomationRunArtifactStore.*.cs` | `Core/Runs/` |
| `src/Slasher/Automation/AutomationRunModels.cs` 等 | `Core/Models/` |
| `src/Slasher/Files/` 一式 | `Io/Files/` |
| `src/Slasher/Data/` 一式 | `Io/Data/` |
| `src/Slasher/Windows/ClipboardService.cs` | `Io/Clipboard/` |
| `src/Slasher/Peers/` 一式 | `Network/Peers/` |
| `src/Slasher/Windows/WindowsAutomationService.*.cs` | `AppOps/Windows/` |
| `src/Slasher/Windows/NativeMethods.*.cs` / `NativeStructs.cs` | `AppOps/Windows/` |
| `src/Slasher/Windows/BrowserAutomationService.cs` | `AppOps/Browser/` (※ Selenium は cross-platform) |
| `src/Slasher/WindowHandle.cs` | `AppOps/Windows/` |

### 2.2 ブラウザ自動化の位置

Selenium WebDriver は **クロスプラットフォーム** (Mac/Linux でも動く)。OS 固有ではないので、`AppOps/Browser/` に独立サブフォルダで配置する。

```
AppOps/
  Common/         # interfaces (IAppLauncher, IWindowControl, ...)
  Browser/        # Selenium-based (cross-platform)
  Windows/        # Win32 P/Invoke implementations
  MacOS/          # (将来)
  Linux/          # (将来)
```

### 2.3 wwwroot

`src/Slasher/wwwroot/` は変更なし。Api 層の静的リソースとして扱う。

---

## 第3章 OS 別コードの分離 (プラグイン化として再定義)

> **注**: 当初「OS 別コード」として設計していた仕組みは、Q-L2 採用により
> **プラグイン アーキテクチャ** に発展した。詳細は `slasher-plugin-architecture.md` を参照。
> 本章はプラグイン アーキテクチャの中で「OS ネイティブ プラグイン」として再構成された。

### 3.1 抽象化方針

OS 固有のコードは **Core 配下の interface** に対する実装として配置する。具体的には:

```
Core/
  AppOps/
    Abstractions/
      IAppLauncher.cs
      IWindowControl.cs
      IInputSimulator.cs
      IScreenCapture.cs
      IElementInspector.cs
      IDialogHost.cs
```

これらは **Core が定義する interface**。Numadora ホスト呼び出しはこの interface を経由する。

実装は AppOps 層:

```
AppOps/
  Windows/
    WindowsAppLauncher.cs       implements IAppLauncher
    WindowsWindowControl.cs     implements IWindowControl
    WindowsInputSimulator.cs    implements IInputSimulator
    ...
```

### 3.2 実装の選択 (DI 登録)

Program.cs で OS 検出して適切な実装を登録:

```csharp
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IAppLauncher, WindowsAppLauncher>();
    builder.Services.AddSingleton<IWindowControl, WindowsWindowControl>();
    // ...
}
else if (OperatingSystem.IsMacOS())
{
    builder.Services.AddSingleton<IAppLauncher, MacOSAppLauncher>();
    // ...
}
else if (OperatingSystem.IsLinux())
{
    // ...
}
else
{
    // 未対応 OS では unsupported 実装を登録 (起動はするが各操作で OperationNotSupported)
    builder.Services.AddSingleton<IAppLauncher, UnsupportedAppLauncher>();
    // ...
}
```

### 3.3 OS 属性によるリンカ最適化

Windows 専用クラスには `[SupportedOSPlatform("windows")]` を付与。これにより:
- ビルド時に Mac/Linux で誤呼び出しすれば警告 (`CA1416`)
- linker / trimmer が他 OS では落とせる (将来の AOT/単一ファイル化に有利)

```csharp
[SupportedOSPlatform("windows")]
public sealed class WindowsAppLauncher : IAppLauncher { ... }
```

### 3.4 当面の実装スコープ

- Windows 実装のみ提供。`UnsupportedAppLauncher` は Mac/Linux で 501-相当を返す (`platform_not_supported`)
- Mac/Linux 実装は本ノートの範囲外 (interface だけ整える)
- ブラウザ自動化は最初から Mac/Linux で動くため、`UnsupportedAppLauncher` の対象外

---

## 第4章 規律強制 (Architecture Tests)

### 4.1 アプローチ選定

| 方針 | 強度 | コスト | 採用 |
|---|---|---|---|
| csproj 分割 | 最高 (コンパイル時失敗) | 高 (現コード再構成) | × (1 csproj 維持) |
| Roslyn analyzer (custom) | 高 (リアルタイム警告) | 高 (analyzer 自作) | △ (将来検討) |
| **NetArchTest 等によるアーキテクチャ テスト** | 中 (CI で失敗) | 低 (ユニットテストとして書く) | **○ 採用** |
| コードレビューのみ | 低 | 0 | × (人手依存) |

### 4.2 NetArchTest の導入

`tests/Slasher.Tests/Architecture/DependencyTests.cs` を新設:

```csharp
public class DependencyTests
{
    private static readonly Assembly SlasherAssembly = typeof(Program).Assembly;

    [Fact]
    public void Core_must_not_depend_on_other_layers()
    {
        var result = Types.InAssembly(SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Core")
            .ShouldNot().HaveDependencyOnAny(
                "Slasher.Api",
                "Slasher.Io",
                "Slasher.Network",
                "Slasher.AppOps")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Io_must_not_depend_on_Api_or_AppOps_or_Network() { ... }

    [Fact]
    public void Network_must_not_depend_on_Api_or_AppOps_or_Io() { ... }

    [Fact]
    public void AppOps_must_not_depend_on_Api_or_Io_or_Network() { ... }

    [Fact]
    public void OS_specific_namespaces_must_be_isolated()
    {
        // Slasher.AppOps.Windows は Slasher.AppOps.MacOS / .Linux に依存してはならない
        // Slasher.Core は OS 固有 namespace に依存してはならない
    }
}
```

これにより `dotnet test` で依存違反が即検出される。

### 4.3 NuGet 依存の追加

```xml
<PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
```

`tests/Slasher.Tests.csproj` に追加。

---

## 第5章 配布戦略

### 5.1 段階

| 段階 | 内容 | コスト | 効果 |
|---|---|---|---|
| **段階 1** | self-contained + single-file publish | 低 | runtime 不要、配布 1 ファイル (~70-80MB) |
| **段階 2** | trimmed publish | 中 | サイズ削減 (~30-50MB)、Selenium 互換性検証必要 |
| **段階 3** | AOT compilation | 高 | 起動最速、サイズ最小、リフレクション制約 |

### 5.2 段階 1: 単一ファイル配布

```powershell
dotnet publish src/Slasher/Slasher.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true
```

成果物: `Slasher.exe` 単体 (約 70-80MB)。`.NET runtime` 不要。

将来的な Mac/Linux 用 RID:
- `osx-x64`, `osx-arm64`
- `linux-x64`, `linux-arm64`

CI に各 RID 用 publish ジョブを追加する想定。

### 5.3 段階 2: Trimming の検証点

`<PublishTrimmed>true</PublishTrimmed>` を有効化したときに動作確認すべき:

- Selenium WebDriver のリフレクション依存箇所
- ASP.NET Core の DI コンテナ
- System.Text.Json のソース ジェネレータ移行
- Numadora インタプリタが内部でリフレクションを使っているか (現状未調査)

トリミング不可なケースは `<TrimmerRootDescriptor>` で除外指定。

### 5.4 段階 3: AOT は将来

`PublishAot=true` は Selenium と相性が悪い可能性が高い。AOT 化を本気で目指すなら:
- Selenium を別プロセスに分離 (子プロセスとして起動、stdio/IPC 経由)
- または Selenium 依存箇所のみ JIT モードで分離アセンブリ化

これは本ノートの範囲外。

### 5.5 wwwroot の扱い

`PublishSingleFile=true` でも wwwroot は外に出る (静的ファイルとしてアクセスする必要があるため)。配布物は `Slasher.exe` + `wwwroot/` ディレクトリの 2 つ。

将来的には埋め込みリソース化も検討。

---

## 第6章 実施ステップ

### 6.1 PR 分割案 (実装フェーズで進める順序)

| PR | 内容 | 依存 |
|---|---|---|
| **PR-1** | フォルダ移動 + namespace 一括書き換え (5 層配置への移行) | (前提) |
| **PR-2** | NetArchTest 導入 + 依存方向テスト追加 | PR-1 |
| **PR-3** | `Core/AppOps/Abstractions/` に interface 切り出し、`WindowsAutomationService` 等を実装側に移行 | PR-1 |
| **PR-4** | DI 登録を OS 検出ベースに書き換え、`UnsupportedXxx` 実装を追加 | PR-3 |
| **PR-5** | `[SupportedOSPlatform("windows")]` 属性付与、Mac/Linux 警告対応 | PR-3 |
| **PR-6** | self-contained + single-file publish CI ジョブ追加 (Windows のみ) | PR-1 |
| **PR-7** | Mac/Linux 用 publish ジョブ追加 (バイナリは作るが操作は unsupported) | PR-4, PR-5 |
| **PR-8** | trimming の検証 + 必要ならトリマー設定 | PR-6 |

PR-1〜2 が「層分割と規律強制」、PR-3〜5 が「OS 抽象化」、PR-6〜8 が「配布」。

### 6.2 言語再構成との順序

`numadora-language-redesign.md` の PR 群と本ノートの PR 群は **直交** している。同時並行可能だが、衝突を避けるなら以下の順序:

1. 本ノート PR-1, 2 (5 層フォルダ再編)
2. Numadora 言語 PR-A, B (spec 改訂、サンプル書き換え)
3. 本ノート PR-3, 4, 5 (OS 抽象化)
4. Numadora 言語 PR-C, D, E... (パーサ/ホスト登録)
5. 本ノート PR-6〜 (配布)

---

## 第7章 Q-L* の確定状況

すべての Q-L* は確定済み。`slasher-plugin-architecture.md` 第 10 章に反映。
新たに発生した Q-P1〜P6 はそちらに記載。

| Q | 確定 | 主な反映先 |
|---|---|---|
| L1 | interface 命名: 短い動詞-対象、モジュール毎 | `slasher-plugin-architecture.md` 2.3 |
| L2 | **AppOps をプラグイン ホスト化** (Excel/GIMP 等を将来追加) | `slasher-plugin-architecture.md` 全体 |
| L3 | ハードカット (1 PR で全部) | `slasher-plugin-architecture.md` 9 章 PR-1 |
| L4 | `PlatformNotSupportedException` → `platform_not_supported` 正規化 | `slasher-plugin-architecture.md` 3.2 |
| L5 | AppOps 定義を「外部 GUI 制御」に変更 | 本ノート 1.2 |
| L6 | `.numai` は埋め込みリソース、各プラグイン所有 | `slasher-plugin-architecture.md` 4 章 |

以下は履歴用の元記述 (詳細は採用済の各反映先を参照)。

### Q-L1. interface 命名

`AppOps/Abstractions/IAppLauncher.cs` のような命名で十分か。Slasher の既存命名規約 (`WindowsAutomationService`) との整合をどう取るか。

候補:
- `IAppLauncher` (シンプル)
- `IAppOps` (層名そのまま) → ただしこれだと粒度が荒すぎ
- `IWindowsCompatibleAppLauncher` (互換性意図を示す)

推奨: `IAppLauncher`, `IWindowControl` 等の **動詞 + 対象** ベース。短く意図が伝わる。

### Q-L2. Browser を独立層にすべきか

ブラウザ自動化は OS に依存しないが、Selenium 起動・WebDriver 管理など独自関心ごとが多い。`AppOps/Browser/` に置くか、独立層 `Slasher.Browser` を立てるか。

推奨: **AppOps 配下のサブ領域** とする。新層を増やすコスト > 整理の利益。Selenium ドライバ管理などは AppOps 内のインフラとして扱う。

### Q-L3. 旧 namespace の移行猶予

PR-1 で一括 namespace 変更すると、外部ツール (`plugins/slasher` MCP, `scripts/*.ps1`) は影響を受けないが、内部の test や documentation で旧 namespace を参照している箇所がある。

選択肢:
- 一括変更 (1 PR で全部)
- 旧 namespace を `[Obsolete]` で残し並行運用 → 移行完了後に削除

推奨: **一括変更** (1 csproj なので外部依存なし、移行コスト最小)。

### Q-L4. unsupported 実装の挙動

`UnsupportedAppLauncher` が呼ばれたとき:
- (a) `NotSupportedException` を投げる
- (b) `RuntimeError("platform_not_supported", ...)` を Numadora に伝播
- (c) DI 登録自体を拒否し、起動時に「この OS では使えない API があります」と警告

推奨: **(b)**。Numadora スクリプトの `TRY/CATCH` で扱える。`details` に OS 情報を入れる。

### Q-L5. 5 層境界の Browser 例外扱い

Selenium はブラウザを別プロセスで起動するため、ある意味「OS 依存だが OS API を直接叩かない」。AppOps に入れると、Mac/Linux でも動くのに「AppOps は OS-specific」というルールに矛盾感が出る。

整理案: AppOps の責務を「**外部アプリケーション・GUI 制御**」と再定義。OS API を直接叩くか否かは無関係。Selenium も「外部ブラウザの制御」だから AppOps が正しい場所。

### Q-L6. 配布物に Numadora ホスト .numai を含めるか

`scripts/numadora-host/slasher/*.numai` (将来作成予定) は配布物に同梱する必要がある。`Content` として csproj に登録 → publish 時に出力ディレクトリへコピー、を想定。

ファイル探索パスを `AppContext.BaseDirectory` 起点で解決するロジックを Core に置く。

---

## 改訂履歴

- v0.1 — 初版起草。5 層構成 + C# 統一 + NetArchTest による規律 + 段階的 self-contained 配布の方針を提示。
