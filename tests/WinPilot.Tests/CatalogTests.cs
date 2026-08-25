using WinPilot.Models;
using WinPilot.Services;

namespace WinPilot.Tests;

public class CatalogTests
{
    [Fact]
    public void CatalogIdsAreUniqueAndDefinitionsAreComplete()
    {
        Assert.NotEmpty(TweakCatalog.All);
        Assert.Equal(TweakCatalog.All.Count, TweakCatalog.All.Select(x => x.Id).Distinct().Count());
        Assert.All(TweakCatalog.All, tweak =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Title));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Description));
            Assert.NotEmpty(tweak.Changes);
            Assert.All(tweak.Changes, change => Assert.False(string.IsNullOrWhiteSpace(change.KeyPath)));
        });
    }

    [Fact]
    public void CatalogContainsNoHighRiskTweaks()
        => Assert.DoesNotContain(TweakCatalog.All, x => x.Risk == RiskLevel.High);

    [Fact]
    public void OptionalServicesAreUniqueAndExcludeCriticalServices()
    {
        var critical = new[] { "RpcSs", "EventLog", "Dnscache", "Dhcp", "LanmanWorkstation", "Winmgmt" };
        Assert.Equal(OptionalServiceCatalog.All.Count,
            OptionalServiceCatalog.All.Select(x => x.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(OptionalServiceCatalog.All, x => critical.Contains(x.ServiceName, StringComparer.OrdinalIgnoreCase));
        Assert.All(OptionalServiceCatalog.All, x => Assert.NotEqual(RiskLevel.High, x.Risk));
    }

    [Fact]
    public void OptionalServiceStatusQueriesAreReadOnlyAndDoNotThrow()
    {
        var engine = new ServiceTweakEngine(new SnapshotStore());
        foreach (var definition in OptionalServiceCatalog.All)
        {
            var info = engine.GetInfo(definition.ServiceName);
            Assert.NotNull(info);
        }
    }

    [Fact]
    public void BuiltInSoftwareEntriesUseUniqueHttpsUrls()
    {
        Assert.Equal(SoftwareCatalogStore.BuiltIns.Count,
            SoftwareCatalogStore.BuiltIns.Select(x => x.Id).Distinct().Count());
        Assert.All(SoftwareCatalogStore.BuiltIns, entry =>
        {
            Assert.True(SoftwareCatalogStore.IsValid(entry.Name, entry.DownloadUrl, out var error), error);
            Assert.True(entry.IsBuiltIn);
        });
        Assert.False(SoftwareCatalogStore.IsValid("Unsafe", "http://example.com/app.exe", out _));
        Assert.False(SoftwareCatalogStore.IsValid("Local", "https://localhost/app.exe", out _));
        Assert.False(SoftwareCatalogStore.IsValid("Credential", "https://user:password@example.com/app.exe", out _));
    }

    [Fact]
    public void CustomSoftwareCatalogRoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinPilotTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "catalog.json");
        try
        {
            var store = new SoftwareCatalogStore(path);
            store.Add("Example Tool", "https://example.com/tool.exe", "test entry");
            var entry = Assert.Single(store.LoadCustom());
            Assert.Equal("Example Tool", entry.Name);
            Assert.False(entry.IsBuiltIn);
            store.Remove(entry.Id);
            Assert.Empty(store.LoadCustom());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SnapshotRoundTrips()
    {
        var store = new SnapshotStore();
        var original = new Snapshot
        {
            ComputerName = "test-machine",
            Registry = [new RegistrySnapshotEntry
            {
                Hive = "CurrentUser", KeyPath = @"Software\WinPilotTest", ValueName = "Example",
                Exists = true, Kind = "DWord", JsonValue = "1"
            }],
            Services = [new ServiceSnapshotEntry { ServiceName = "ExampleSvc", StartMode = "Manual", Status = "Stopped" }]
        };
        var path = store.Save(original);
        try
        {
            var restored = store.Load(path);
            Assert.Equal(original.ComputerName, restored.ComputerName);
            Assert.Single(restored.Registry);
            Assert.Equal("Example", restored.Registry[0].ValueName);
            Assert.Single(restored.Services);
            Assert.Equal("ExampleSvc", restored.Services[0].ServiceName);
        }
        finally { File.Delete(path); }
    }
}
