namespace Slasher.Core.AppOps;

/// <summary>
/// プラグインの可用性状態。<see cref="IAppOpsPlugin.CheckAvailability"/> が返す。
/// 詳細は docs/slasher-plugin-architecture.md 2.1。
/// </summary>
public enum PluginAvailability
{
    /// <summary>動作可能。<see cref="IAppOpsPlugin.Register"/> に進む。</summary>
    Available,

    /// <summary>この OS 等では適用外 (例: WindowsNative on macOS)。</summary>
    NotApplicable,

    /// <summary>必要ソフトウェア未導入 (例: Excel 未インストール)。</summary>
    MissingPrerequisites,

    /// <summary>設定 (Plugins:&lt;Name&gt;:enabled = false) で無効化されている。</summary>
    Disabled,
}
