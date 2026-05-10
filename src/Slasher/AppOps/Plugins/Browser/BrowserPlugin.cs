using Slasher.Core.AppOps;

namespace Slasher.AppOps.Plugins.Browser;

/// <summary>
/// Browser プラグイン エントリ。
/// Selenium WebDriver ベースのブラウザ自動化を提供 (Edge/Chrome/Firefox)。
/// クロスプラットフォーム (Windows / macOS / Linux 全対応)。
///
/// 詳細は docs/slasher-plugin-architecture.md 1.3 (built-in v1)。
/// 現状は登録シェル — PR-E で <c>BrowserAutomationService</c> をこのプラグイン
/// 配下のホスト バインディングに分割移行する。
/// </summary>
public sealed class BrowserPlugin : IAppOpsPlugin
{
    public string Name => "Browser";

    public Version Version { get; } = new(1, 0, 0);

    public IReadOnlyList<string> HostModules { get; } = new[]
    {
        "slasher/browser",
    };

    public PluginRequirements Requirements { get; } = new(
        SupportedOS: new[] { "windows", "macos", "linux" },
        RequiredSoftware: Array.Empty<string>(),
        Capabilities: new[] { "process-app", "user-input", "observe" });

    public PluginAvailability CheckAvailability() => PluginAvailability.Available;

    public void Register(IPluginRegistration registration)
    {
        // PR-E で BrowserHostBindings の登録と /browser エンドポイント グループ移行を追加。
        registration.Logger.LogInformation(
            "BrowserPlugin registered (shell only — PR-E pending for host bindings migration)");
    }
}
