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
