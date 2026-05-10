namespace Slasher.Core.AppOps;

/// <summary>
/// プラグインの動作要件 (OS / 必要ソフトウェア / 提供能力クラス)。
/// 詳細は docs/slasher-plugin-architecture.md 2.1, 2.4.1。
/// </summary>
/// <param name="SupportedOS">
/// 対応 OS の識別子配列 (例: ["windows"], ["windows", "macos", "linux"])。
/// <see cref="System.OperatingSystem.IsWindows"/> 等と突き合わせる。
/// </param>
/// <param name="RequiredSoftware">
/// 必要ソフトウェアの識別子配列 (例: ["microsoft.office.excel"])。空配列で必須なし。
/// 実体検出は <see cref="IAppOpsPlugin.CheckAvailability"/> の責務。
/// </param>
/// <param name="Capabilities">
/// このプラグインが提供する Numadora 能力クラスの集合
/// (numadora-language-spec.md 1.4.1 の 15 種から選ぶ)。
/// 起動時に <c>.numai</c> の <c>EXPORT EFFECT(class) FUNC</c> 集合との整合検査
/// (<c>plugin_capabilities_mismatch</c>) に使う。
/// </param>
public sealed record PluginRequirements(
    IReadOnlyList<string> SupportedOS,
    IReadOnlyList<string> RequiredSoftware,
    IReadOnlyList<string> Capabilities);
