using System.Runtime.Versioning;
using Slasher.Core.AppOps;

namespace Slasher.AppOps.Plugins.WindowsNative;

/// <summary>
/// WindowsNative プラグイン エントリ。
/// Windows 上で Win32 ネイティブ自動化 (window/input/screen/element/dialog/app) を提供。
///
/// 詳細は docs/slasher-plugin-architecture.md 1.3 (built-in v1)。
/// 現状は登録シェル — PR-E で <c>WindowsAutomationService</c> 等の既存実装を
/// このプラグイン配下の interface 実装に分割移行する。
/// </summary>
public sealed class WindowsNativePlugin : IAppOpsPlugin
{
    public string Name => "WindowsNative";

    public Version Version { get; } = new(1, 0, 0);

    public IReadOnlyList<string> HostModules { get; } = new[]
    {
        "slasher/window",
        "slasher/input",
        "slasher/screen",
        "slasher/element",
        "slasher/dialog",
        "slasher/app",
    };

    public PluginRequirements Requirements { get; } = new(
        SupportedOS: new[] { "windows" },
        RequiredSoftware: Array.Empty<string>(),
        Capabilities: new[] { "process-app", "user-input", "observe", "system-info" });

    public PluginAvailability CheckAvailability()
        => OperatingSystem.IsWindows()
            ? PluginAvailability.Available
            : PluginAvailability.NotApplicable;

    [SupportedOSPlatform("windows")]
    public void Register(IPluginRegistration registration)
    {
        // PR-E でここに interface 実装の登録 (WindowsAppLauncher, WindowsWindowControl 等) を追加する。
        // 現時点では何も登録しない (既存の直接 DI 登録が引き続き動作)。
        registration.Logger.LogInformation(
            "WindowsNativePlugin registered (shell only — PR-E pending for full interface migration)");
    }
}
