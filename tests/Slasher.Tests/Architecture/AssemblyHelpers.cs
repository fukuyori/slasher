using System.Reflection;
using NetArchTest.Rules;

namespace Slasher.Tests.Architecture;

/// <summary>
/// Architecture tests の共有ヘルパ。
/// Slasher アセンブリは <see cref="Slasher.Api.SlasherEndpointExtensions"/> 等の型から取得する。
/// 旧 v0.1 namespace 体系下では一部の型が見つからない可能性もあるため、
/// 型プローブを使ってアセンブリを解決する。
/// </summary>
internal static class AssemblyHelpers
{
    public static Assembly SlasherAssembly => ResolveSlasherAssembly();

    private static Assembly? _cachedAssembly;

    private static Assembly ResolveSlasherAssembly()
    {
        if (_cachedAssembly is not null)
        {
            return _cachedAssembly;
        }

        // Try several known type names. v0.1 と v0.2.1 (5 層構成適用後) の両方をカバー。
        var probes = new[]
        {
            "Slasher.Api.SlasherEndpointExtensions",
            "Slasher.Automation.ScriptRunService",
            "Slasher.Peers.PeerRegistry",
            "Slasher.Network.Peers.PeerRegistry",   // 5 層構成適用後
            "Slasher.Core.Numadora.ScriptRunService",
            "Program",
        };

        foreach (var typeName in probes)
        {
            var t = Type.GetType($"{typeName}, Slasher", throwOnError: false);
            if (t is not null)
            {
                _cachedAssembly = t.Assembly;
                return _cachedAssembly;
            }
        }

        // Fallback: scan loaded assemblies for one with a Slasher-like name.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name;
            if (name == "Slasher")
            {
                _cachedAssembly = asm;
                return _cachedAssembly;
            }
        }

        throw new InvalidOperationException(
            "Could not locate Slasher assembly. Ensure tests/Slasher.Tests references src/Slasher.");
    }

    public static string FormatFailures(TestResult result, string contextLabel)
    {
        if (result.IsSuccessful)
        {
            return $"{contextLabel}: passed";
        }

        var failing = result.FailingTypeNames ?? Array.Empty<string>();
        if (failing.Count == 0)
        {
            return $"{contextLabel}: failed (no detail)";
        }

        return $"{contextLabel}: violations -> " + string.Join(", ", failing);
    }
}
