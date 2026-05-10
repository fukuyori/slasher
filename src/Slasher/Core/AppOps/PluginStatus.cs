namespace Slasher.Core.AppOps;

/// <summary>
/// プラグイン登録結果のスナップショット (<see cref="PluginRegistry"/> および
/// <c>/plugins</c> エンドポイントで使用)。
/// </summary>
public sealed record PluginStatus(
    string Name,
    string Version,
    PluginAvailability Status,
    string? Reason,
    IReadOnlyList<string> HostModules,
    IReadOnlyList<string> Capabilities);
