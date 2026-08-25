namespace WinPilot.Models;

public sealed class Snapshot
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string ComputerName { get; set; } = Environment.MachineName;
    public List<RegistrySnapshotEntry> Registry { get; set; } = [];
    public List<ServiceSnapshotEntry> Services { get; set; } = [];
}

public sealed class ServiceSnapshotEntry
{
    public string ServiceName { get; set; } = "";
    public string StartMode { get; set; } = "Manual";
    public string Status { get; set; } = "Stopped";
    public bool DelayedAutoStart { get; set; }
}

public sealed class RegistrySnapshotEntry
{
    public string Hive { get; set; } = "";
    public string KeyPath { get; set; } = "";
    public string ValueName { get; set; } = "";
    public bool Exists { get; set; }
    public string Kind { get; set; } = "Unknown";
    public string? JsonValue { get; set; }
}
