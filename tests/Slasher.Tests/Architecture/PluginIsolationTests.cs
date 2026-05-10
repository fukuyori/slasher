using NetArchTest.Rules;
using Xunit;

namespace Slasher.Tests.Architecture;

/// <summary>
/// AppOps プラグイン間の独立性ルール。詳細は <c>docs/slasher-plugin-architecture.md</c> 6.1。
///
/// PR-1 完了前は <c>Slasher.AppOps.Plugins.*</c> が存在しないため vacuous truth として通過。
/// PR-1 完了後に実質的な検証として機能する。
/// </summary>
public sealed class PluginIsolationTests
{
    private const string PluginHostNamespace = "Slasher.AppOps.PluginHost";
    private const string WindowsNativeNamespace = "Slasher.AppOps.Plugins.WindowsNative";
    private const string BrowserNamespace = "Slasher.AppOps.Plugins.Browser";
    private const string MacOSNativeNamespace = "Slasher.AppOps.Plugins.MacOSNative";
    private const string LinuxNativeNamespace = "Slasher.AppOps.Plugins.LinuxNative";

    [Fact]
    public void PluginHost_must_not_depend_on_specific_plugins()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith(PluginHostNamespace)
            .ShouldNot().HaveDependencyOnAny(
                WindowsNativeNamespace,
                BrowserNamespace,
                MacOSNativeNamespace,
                LinuxNativeNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "PluginHost -> 具体プラグイン"));
    }

    [Fact]
    public void WindowsNative_must_not_depend_on_other_plugins()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith(WindowsNativeNamespace)
            .ShouldNot().HaveDependencyOnAny(
                BrowserNamespace,
                MacOSNativeNamespace,
                LinuxNativeNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "WindowsNative -> 他プラグイン"));
    }

    [Fact]
    public void Browser_must_not_depend_on_other_plugins()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith(BrowserNamespace)
            .ShouldNot().HaveDependencyOnAny(
                WindowsNativeNamespace,
                MacOSNativeNamespace,
                LinuxNativeNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "Browser -> 他プラグイン"));
    }

    /// <summary>
    /// プラグインから Slasher.Api への依存を禁止 (プラグインは API 層を意識しない)。
    /// 必要なら Core 経由 (interface または共通モデル) で連携する。
    /// </summary>
    [Fact]
    public void Plugins_must_not_depend_on_Api()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.AppOps.Plugins")
            .ShouldNot().HaveDependencyOn("Slasher.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "AppOps Plugins -> Api"));
    }

    /// <summary>
    /// プラグインから Io 層への直接依存を禁止 (interface 経由のみ許可)。
    /// </summary>
    [Fact]
    public void Plugins_must_not_directly_depend_on_Io_implementations()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.AppOps.Plugins")
            .ShouldNot().HaveDependencyOnAny(
                "Slasher.Io.Files",
                "Slasher.Io.Data",
                "Slasher.Io.Clipboard")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "AppOps Plugins -> Io 実装"));
    }

    /// <summary>
    /// プラグインから Network 層への直接依存を禁止 (interface 経由のみ許可)。
    /// </summary>
    [Fact]
    public void Plugins_must_not_directly_depend_on_Network_implementations()
    {
        var result = Types.InAssembly(AssemblyHelpers.SlasherAssembly)
            .That().ResideInNamespaceStartingWith("Slasher.AppOps.Plugins")
            .ShouldNot().HaveDependencyOn("Slasher.Network")
            .GetResult();

        Assert.True(result.IsSuccessful,
            AssemblyHelpers.FormatFailures(result, "AppOps Plugins -> Network 実装"));
    }
}
