using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Slasher.Core.AppOps;

/// <summary>
/// プラグインが <see cref="IAppOpsPlugin.Register"/> で受け取る登録 API。
/// 詳細は docs/slasher-plugin-architecture.md 2.2。
///
/// 動的ロード対応 (Q-P1) の余地を残すため、契約は具象型ではなく interface のみで記述する。
/// </summary>
public interface IPluginRegistration
{
    /// <summary>
    /// このプラグイン用の設定 (appsettings.json の Plugins:&lt;PluginName&gt; セクションに
    /// スコープ済) — Q-P2 採用。環境変数 <c>Plugins__&lt;Name&gt;__&lt;Key&gt;</c> で上書き可。
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>プラグイン用ロガー。</summary>
    ILogger Logger { get; }

    /// <summary>
    /// Numadora ホスト バインディング クラスを登録する。
    /// 通常は <c>[NumadoraHostBindings]</c> 属性付きクラスを指定。
    /// </summary>
    void RegisterHostBindings<T>() where T : class;

    /// <summary>
    /// プラグイン同梱の <c>.numai</c> 埋め込みリソースを開く (Stream 返却で動的ロード対応 — Q-P1)。
    /// 論理パス例: <c>"slasher/window.numai"</c>。
    /// </summary>
    Stream OpenEmbeddedNumaiResource(string logicalPath);

    /// <summary>HTTP エンドポイント グループを登録 (例: <c>/browser</c>)。</summary>
    void RegisterEndpointGroup(string prefix, Action<IEndpointRouteBuilder> configure);

    /// <summary>
    /// 内部 DI サービスを登録 (プラグイン内部で利用、外部からは見えない)。
    /// </summary>
    void RegisterService<TInterface, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TInterface : class
        where TImpl : class, TInterface;

    /// <summary>
    /// OS ネイティブ プラグイン用: <c>Core/AppOps/Abstractions</c> の共通 interface に対する
    /// OS 固有実装を登録する。同 interface に対する複数登録は最後勝ちではなく
    /// <c>plugin_module_conflict</c> エラーで起動失敗させる責務がある。
    /// </summary>
    void RegisterOSNativeImpl<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface;
}
