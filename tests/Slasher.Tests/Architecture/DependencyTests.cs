using NetArchTest.Rules;
using Xunit;

namespace Slasher.Tests.Architecture;

/// <summary>
/// 5 層構成 (Api / Core / Io / Network / AppOps) の依存方向ルールを強制するテスト。
/// 詳細は <c>docs/slasher-layer-architecture.md</c> 1.3 と
/// <c>docs/slasher-plugin-architecture.md</c> 6.1 を参照。
///
/// PR-1 (フォルダ移動 + namespace 一括書き換え) 完了前は、
/// <c>Slasher.Core</c> 等の namespace に該当する型が存在しないため、
/// 多くのテストが空集合に対する vacuous truth として通過する。
/// PR-1 完了後にこれらは実質的な検証として機能し始める。
/// </summary>
public sealed class DependencyTests
{
    // ---------- Core が他層に依存しないこと (中核ルール) ----------

    [Fact]
    public void Core_must_not_depend_on_Api()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Core")
            .ShouldNot().HaveDependencyOn("Slasher.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Core -> Slasher.Api"));
    }

    [Fact]
    public void Core_must_not_depend_on_Io()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Core")
            .ShouldNot().HaveDependencyOn("Slasher.Io")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Core -> Slasher.Io"));
    }

    [Fact]
    public void Core_must_not_depend_on_Network()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Core")
            .ShouldNot().HaveDependencyOn("Slasher.Network")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Core -> Slasher.Network"));
    }

    [Fact]
    public void Core_must_not_depend_on_AppOps()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Core")
            .ShouldNot().HaveDependencyOn("Slasher.AppOps")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Core -> Slasher.AppOps"));
    }

    // ---------- Io / Network / AppOps の相互独立 ----------

    [Fact]
    public void Io_must_not_depend_on_Api()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Io")
            .ShouldNot().HaveDependencyOn("Slasher.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Io -> Slasher.Api"));
    }

    [Fact]
    public void Io_must_not_depend_on_Network_or_AppOps()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Io")
            .ShouldNot().HaveDependencyOnAny("Slasher.Network", "Slasher.AppOps")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Io -> Network/AppOps"));
    }

    [Fact]
    public void Network_must_not_depend_on_Api_or_Io_or_AppOps()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.Network")
            .ShouldNot().HaveDependencyOnAny(
                "Slasher.Api",
                "Slasher.Io",
                "Slasher.AppOps")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.Network -> Api/Io/AppOps"));
    }

    [Fact]
    public void AppOps_must_not_depend_on_Api_or_Io_or_Network()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.AppOps")
            .ShouldNot().HaveDependencyOnAny(
                "Slasher.Api",
                "Slasher.Io",
                "Slasher.Network")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Slasher.AppOps -> Api/Io/Network"));
    }

    // ---------- Api が下位層を呼ぶ向きは許可 (テストせず) ----------
    // Api → Core, Io, Network, AppOps はすべて OK。
}
