namespace Slasher.Core.AppOps;

/// <summary>
/// AppOps プラグインの契約。各プラグインはこの interface を実装し、
/// <see cref="PluginRegistry"/> 経由で起動時に登録される。
/// 詳細は docs/slasher-plugin-architecture.md 2.1。
/// </summary>
public interface IAppOpsPlugin
{
    /// <summary>プラグインの一意識別子。例: "WindowsNative", "Browser", "Excel"。</summary>
    string Name { get; }

    /// <summary>プラグインのバージョン。Slasher 本体と独立に変えてよい。</summary>
    Version Version { get; }

    /// <summary>
    /// このプラグインが提供する Numadora ホスト モジュール
    /// (例: ["slasher/window", "slasher/input"])。
    /// </summary>
    IReadOnlyList<string> HostModules { get; }

    /// <summary>動作要件 (OS, 必要ソフトウェア, 提供能力クラス)。</summary>
    PluginRequirements Requirements { get; }

    /// <summary>現環境で動作可能か判定。</summary>
    PluginAvailability CheckAvailability();

    /// <summary>
    /// プラグイン登録 (DI, ホスト バインディング, エンドポイント)。
    /// <see cref="CheckAvailability"/> が <see cref="PluginAvailability.Available"/> を返した場合のみ呼ばれる。
    /// </summary>
    void Register(IPluginRegistration registration);
}
