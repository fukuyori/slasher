using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Slasher.Core.AppOps;

/// <summary>
/// AppOps プラグインの discovery / availability check / 登録ライフサイクル。
/// 詳細は docs/slasher-plugin-architecture.md 3.1。
///
/// 起動時に <see cref="DiscoverAndRegister"/> が呼ばれ、Available なプラグインを
/// <see cref="IPluginRegistration"/> 経由で DI / ホスト バインディング / エンドポイントに登録する。
/// </summary>
public sealed class PluginRegistry
{
    private readonly Dictionary<string, IAppOpsPlugin> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IAppOpsPlugin> _byModule = new(StringComparer.Ordinal);
    private readonly List<PluginStatus> _statuses = new();

    public IReadOnlyList<PluginStatus> Statuses => _statuses;

    public IAppOpsPlugin? FindByName(string name)
        => _byName.TryGetValue(name, out var plugin) ? plugin : null;

    public IAppOpsPlugin? FindByModule(string moduleName)
        => _byModule.TryGetValue(moduleName, out var plugin) ? plugin : null;

    public IReadOnlyCollection<IAppOpsPlugin> AvailablePlugins => _byName.Values;

    /// <summary>
    /// 指定された候補プラグインに対して CheckAvailability を呼び、Available なものを登録する。
    /// </summary>
    public void DiscoverAndRegister(
        IEnumerable<IAppOpsPlugin> candidates,
        IServiceCollection services,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        IEndpointRouteBuilder? endpointRouteBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        foreach (var plugin in candidates)
        {
            if (_byName.ContainsKey(plugin.Name))
            {
                throw new InvalidOperationException($"Duplicate plugin name '{plugin.Name}'");
            }

            // appsettings.json の Plugins:<Name>:enabled = false なら Disabled として扱う
            var section = configuration.GetSection($"Plugins:{plugin.Name}");
            var enabled = section.GetValue("enabled", true);

            PluginAvailability status;
            string? reason = null;

            if (!enabled)
            {
                status = PluginAvailability.Disabled;
                reason = "Plugins:" + plugin.Name + ":enabled = false";
            }
            else
            {
                status = plugin.CheckAvailability();
                reason = status switch
                {
                    PluginAvailability.NotApplicable => "OS or environment mismatch",
                    PluginAvailability.MissingPrerequisites => "required software not installed",
                    PluginAvailability.Disabled => "disabled by plugin",
                    _ => null,
                };
            }

            _statuses.Add(new PluginStatus(
                Name: plugin.Name,
                Version: plugin.Version.ToString(),
                Status: status,
                Reason: reason,
                HostModules: plugin.HostModules,
                Capabilities: plugin.Requirements.Capabilities));

            if (status != PluginAvailability.Available)
            {
                continue;
            }

            // モジュール衝突検出
            foreach (var moduleName in plugin.HostModules)
            {
                if (_byModule.ContainsKey(moduleName))
                {
                    throw new InvalidOperationException(
                        $"plugin_module_conflict: module '{moduleName}' provided by both " +
                        $"'{_byModule[moduleName].Name}' and '{plugin.Name}'");
                }
            }

            // 登録
            var pluginLogger = loggerFactory.CreateLogger($"Slasher.AppOps.Plugins.{plugin.Name}");
            var pluginConfig = section;
            var registration = new PluginRegistrationImpl(
                plugin: plugin,
                services: services,
                configuration: pluginConfig,
                logger: pluginLogger,
                endpointRouteBuilder: endpointRouteBuilder);

            try
            {
                plugin.Register(registration);
            }
            catch (Exception ex)
            {
                pluginLogger.LogError(ex, "Plugin '{Name}' Register failed", plugin.Name);
                throw;
            }

            _byName.Add(plugin.Name, plugin);
            foreach (var moduleName in plugin.HostModules)
            {
                _byModule.Add(moduleName, plugin);
            }

            pluginLogger.LogInformation(
                "Plugin {Name} v{Version} registered ({ModuleCount} modules)",
                plugin.Name, plugin.Version, plugin.HostModules.Count);
        }
    }

    /// <summary>
    /// <see cref="IPluginRegistration"/> の標準実装。
    /// プラグインから DI / ホスト バインディング / エンドポイント登録を受ける。
    /// </summary>
    private sealed class PluginRegistrationImpl : IPluginRegistration
    {
        private readonly IAppOpsPlugin _plugin;
        private readonly IServiceCollection _services;
        private readonly IEndpointRouteBuilder? _endpointRouteBuilder;

        public PluginRegistrationImpl(
            IAppOpsPlugin plugin,
            IServiceCollection services,
            IConfiguration configuration,
            ILogger logger,
            IEndpointRouteBuilder? endpointRouteBuilder)
        {
            _plugin = plugin;
            _services = services;
            Configuration = configuration;
            Logger = logger;
            _endpointRouteBuilder = endpointRouteBuilder;
        }

        public IConfiguration Configuration { get; }
        public ILogger Logger { get; }

        public void RegisterHostBindings<T>() where T : class
        {
            // PR-D で Numadora ホスト bindings 登録機構と連携する。
            // 現状は DI 登録のみ (将来 ScriptRunService がここから取り出す)。
            _services.AddSingleton<T>();
        }

        public Stream OpenEmbeddedNumaiResource(string logicalPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
            var asm = _plugin.GetType().Assembly;
            var resourceName = $"numai/{logicalPath}";
            var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new FileNotFoundException(
                    $"Embedded .numai resource not found: '{resourceName}' in plugin '{_plugin.Name}'");
            }
            return stream;
        }

        public void RegisterEndpointGroup(string prefix, Action<IEndpointRouteBuilder> configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            ArgumentNullException.ThrowIfNull(configure);
            if (_endpointRouteBuilder is null)
            {
                throw new InvalidOperationException(
                    "Endpoint registration is not available at this lifecycle stage");
            }
            // RouteGroupBuilder を作って渡す代わりに、prefix 付きの IEndpointRouteBuilder を渡す。
            var group = _endpointRouteBuilder.MapGroup(prefix);
            configure(group);
        }

        public void RegisterService<TInterface, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
            where TInterface : class
            where TImpl : class, TInterface
        {
            var descriptor = new ServiceDescriptor(typeof(TInterface), typeof(TImpl), lifetime);
            _services.Add(descriptor);
        }

        public void RegisterOSNativeImpl<TInterface, TImpl>()
            where TInterface : class
            where TImpl : class, TInterface
        {
            // 同 interface に対する複数登録は plugin_module_conflict として扱う。
            // 現状は DI コンテナの最後勝ちを許容するが、将来は事前チェックで弾く。
            _services.AddSingleton<TInterface, TImpl>();
        }
    }
}
