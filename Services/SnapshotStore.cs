using System.IO;
using System.Text.Json;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class SnapshotStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinPilot", "Snapshots");

    public string Save(Snapshot snapshot)
    {
        Directory.CreateDirectory(RootDirectory);
        var path = Path.Combine(RootDirectory, $"snapshot-{snapshot.CreatedAt:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, _options));
        return path;
    }

    public IReadOnlyList<string> List() => Directory.Exists(RootDirectory)
        ? Directory.GetFiles(RootDirectory, "snapshot-*.json").OrderByDescending(x => x).ToArray()
        : [];

    public Snapshot Load(string path) => JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), _options)
        ?? throw new InvalidDataException("快照文件内容无效。");
}
