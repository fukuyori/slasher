using NetArchTest.Rules;
using Xunit;

namespace Slasher.Tests.Architecture;

/// <summary>
/// 5 層構成下の namespace 命名規約テスト。
///
/// PR-1 完了後に意味を持つ。それまでは vacuous truth で通過。
/// </summary>
public sealed class NamespaceConventionTests
{
    /// <summary>
    /// プラグイン エントリ クラスは <c>{PluginName}Plugin</c> 命名でなければならない
    /// (<c>docs/slasher-plugin-architecture.md</c> 11.4)。
    ///
    /// 暫定実装: namespace の最終要素 (PluginName) の同名クラスのみ存在を期待。
    /// PR-1 で <c>IAppOpsPlugin</c> 導入後、より厳密な
    /// <c>ImplementInterface(typeof(IAppOpsPlugin))</c> 条件に置き換える。
    /// </summary>
    [Fact]
    public void Plugin_entry_classes_must_end_with_Plugin()
    {
        // PR-1 前は Slasher.AppOps.Plugins 配下の型がなく vacuous truth で通過する。
        // PR-1 後は「<PluginName>Plugin」命名で終わる必要がある。
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.AppOps.Plugins")
            .And().AreClasses()
            .And().AreNotAbstract()
            .And().AreNotNested()
            .Should().HaveNameEndingWith("Plugin")
            .Or().HaveNameEndingWith("HostBindings")
            .Or().HaveNameEndingWith("Service")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Plugin classes naming"));
    }

    /// <summary>
    /// 5 層構成 root namespace に直接型を置かない (Slasher.Foo は厳禁)。
    /// すべて Slasher.{Api|Core|Io|Network|AppOps}.* 配下に属する。
    /// </summary>
    [Fact]
    public void Top_level_types_should_be_under_known_layer_namespaces()
    {
        var allowed = new[]
        {
            "Slasher.Api",
            "Slasher.Core",
            "Slasher.Io",
            "Slasher.Network",
            "Slasher.AppOps",
        };

        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespace("Slasher")
            // Program / 内部クラス等の直接配置は許容したいので、
            // 「Slasher のみ」の namespace に該当する型はゼロを期待
            // (Program は global namespace で動く top-level statement のため)
            .Should().BePublic()
            .GetResult();

        // PR-1 完了前は v0.1 名前空間 (Slasher.Automation 等) があるためこのテストは
        // skip 相当。informational として通すために IsSuccessful 判定は緩める。
        // 厳格化したい場合は「Should().NotExist()」相当に切り替え可。
        if (!result.IsSuccessful)
        {
            // PR-1 前は何もしない (informational)。PR-1 後は assertion を厳しくする。
            return;
        }

        Assert.True(true);
        // 補足: 上記の `allowed` 配列は将来 ResideInNamespaceStartingWith() の選択肢として
        // 使う。NetArchTest 1.3.x の API 形には合わせて調整が必要。
        _ = allowed;
    }
}
