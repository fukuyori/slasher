using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Slasher.Automation;
using Slasher.Peers;
using Slasher.Windows;
using Xunit;

namespace Slasher.Tests;

public sealed class PeerModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slasher-peer-tests", Guid.NewGuid().ToString("N"));

    public PeerModelTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData("/", 0)]
    [InlineData("/windows/0x123", 2)]
    [InlineData("/runs/run-1/events", 3)]
    public void ResourceAddress_Parse_AcceptsAbsoluteResourcePaths(string path, int segmentCount)
    {
        var address = ResourceAddress.Parse(path);

        Assert.Equal(segmentCount, address.Segments.Count);
        Assert.StartsWith("/", address.Path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("/windows\\bad")]
    [InlineData("/../windows")]
    public void ResourceAddress_Parse_RejectsUnsafePaths(string path)
    {
        Assert.Throws<ArgumentException>(() => ResourceAddress.Parse(path));
    }

    [Fact]
    public void PeerEndpointService_DeniesObserveCapabilitiesForUnknownPeer()
    {
        var service = CreateService();

        var response = service.GetCapabilities("missing-peer");

        Assert.Equal(PeerTrustProfile.Unknown, response.TrustProfile);
        Assert.Contains(response.Capabilities, capability =>
            capability.Name == PeerCapabilities.ObserveWindowList
            && capability.Status == "denied"
            && capability.Reason == "trust_profile");
    }

    [Fact]
    public void PeerEndpointService_AllowsObserveCapabilitiesForObservedPeer()
    {
        var registryPath = Path.Combine(_root, "peers.json");
        File.WriteAllText(registryPath,
            """
            {
              "schemaVersion": 1,
              "peers": [
                {
                  "peerId": "peer_known",
                  "displayName": "known",
                  "trustProfile": "observed",
                  "enabled": true
                }
              ]
            }
            """);
        var service = CreateService(registryPath: registryPath);

        var response = service.GetCapabilities("peer_known");

        Assert.Equal(PeerTrustProfile.Observed, response.TrustProfile);
        Assert.Contains(response.Capabilities, capability =>
            capability.Name == PeerCapabilities.ObserveWindowList
            && capability.Status == "allowed");
        Assert.Contains(response.Capabilities, capability =>
            capability.Name == PeerCapabilities.InputText
            && capability.Status == "denied");
    }

    [Fact]
    public void NamespaceService_FiltersRootEntriesByTrustProfile()
    {
        var service = new NamespaceService(CreateRegistry());

        var known = service.List("/", requestingPeerId: null);
        var observed = service.List("/", "peer_known");

        Assert.DoesNotContain(known.Entries, entry => entry.Name == "windows");
        Assert.Contains(observed.Entries, entry => entry.Name == "windows");
        Assert.Contains(observed.Entries, entry => entry.Name == "runs");
    }

    [Fact]
    public void ResourceReadService_AllowsIdentityForKnownProfile()
    {
        var service = CreateResourceReadService();

        var response = service.Read("/identity", requestingPeerId: null);

        Assert.Equal("peer.identity", response.Kind);
        Assert.Contains(PeerCapabilities.Hello, response.CapabilitiesUsed);
    }

    [Fact]
    public void ResourceReadService_DeniesWindowsForKnownProfile()
    {
        var service = CreateResourceReadService();

        var ex = Assert.Throws<ResourceReadException>(() => service.Read("/windows", requestingPeerId: null));

        Assert.Equal("peer_capability_denied", ex.Code);
    }

    private PeerEndpointService CreateService(string? registryPath = null)
    {
        var environment = new TestEnvironment(_root);
        var options = Options.Create(new PeerOptions
        {
            DisplayName = "test-peer",
            IdentityPath = Path.Combine(_root, "identity.json"),
            RegistryPath = registryPath
        });
        var identity = new PeerIdentityStore(environment, options);
        var registry = new PeerRegistry(environment, options);
        return new PeerEndpointService(identity, registry, options);
    }

    private PeerRegistry CreateRegistry()
    {
        var registryPath = Path.Combine(_root, "peers.json");
        File.WriteAllText(registryPath,
            """
            {
              "schemaVersion": 1,
              "peers": [
                {
                  "peerId": "peer_known",
                  "displayName": "known",
                  "trustProfile": "observed",
                  "enabled": true
                }
              ]
            }
            """);
        var environment = new TestEnvironment(_root);
        var options = Options.Create(new PeerOptions
        {
            DisplayName = "test-peer",
            IdentityPath = Path.Combine(_root, "identity.json"),
            RegistryPath = registryPath
        });
        return new PeerRegistry(environment, options);
    }

    private ResourceReadService CreateResourceReadService()
    {
        var environment = new TestEnvironment(_root);
        var options = Options.Create(new PeerOptions
        {
            DisplayName = "test-peer",
            IdentityPath = Path.Combine(_root, "identity.json")
        });
        var identity = new PeerIdentityStore(environment, options);
        var registry = new PeerRegistry(environment, options);
        var endpoint = new PeerEndpointService(identity, registry, options);
        var artifactStore = new AutomationRunArtifactStore(environment);
        return new ResourceReadService(endpoint, identity, registry, new WindowsAutomationService(), artifactStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
            ContentRootFileProvider = new PhysicalFileProvider(root);
            WebRootFileProvider = new PhysicalFileProvider(root);
        }

        public string ApplicationName { get; set; } = "Slasher.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = Environments.Development;

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }
    }
}
