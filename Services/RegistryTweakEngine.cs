using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class RegistryTweakEngine
{
    private readonly SnapshotStore _snapshots;
    public RegistryTweakEngine(SnapshotStore snapshots) => _snapshots = snapshots;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public TweakState GetState(TweakDefinition tweak)
    {
        try
        {
            var matches = tweak.Changes.Select(change => ValuesEqual(ReadValue(change), change.EnabledValue)).ToArray();
            if (matches.All(x => x)) return TweakState.Enabled;
            if (matches.All(x => !x)) return TweakState.Disabled;
            return TweakState.Mixed;
        }
        catch { return TweakState.Unknown; }
    }

    public string Apply(TweakDefinition tweak, bool enabled)
    {
        if (tweak.RequiresAdmin && !IsAdministrator())
            throw new UnauthorizedAccessException("此设置写入系统范围，需要以管理员身份运行 WinPilot。");

        var snapshot = Capture(tweak);
        var snapshotPath = _snapshots.Save(snapshot);
        try
        {
            foreach (var change in tweak.Changes) Write(change, enabled);
            return snapshotPath;
        }
        catch
        {
            Restore(snapshot);
            throw;
        }
    }

    public Snapshot Capture(TweakDefinition tweak)
    {
        var snapshot = new Snapshot();
        foreach (var change in tweak.Changes)
        {
            using var baseKey = RegistryKey.OpenBaseKey(change.Hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(change.KeyPath, false);
            var names = key?.GetValueNames() ?? [];
            var exists = names.Contains(change.ValueName, StringComparer.OrdinalIgnoreCase);
            var value = exists ? key!.GetValue(change.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;
            var kind = exists ? key!.GetValueKind(change.ValueName) : RegistryValueKind.Unknown;
            snapshot.Registry.Add(new RegistrySnapshotEntry
            {
                Hive = change.Hive.ToString(), KeyPath = change.KeyPath, ValueName = change.ValueName,
                Exists = exists, Kind = kind.ToString(), JsonValue = value is null ? null : JsonSerializer.Serialize(value)
            });
        }
        return snapshot;
    }

    public void Restore(Snapshot snapshot)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive>(entry.Hive);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            if (!entry.Exists)
            {
                using var key = baseKey.OpenSubKey(entry.KeyPath, true);
                key?.DeleteValue(entry.ValueName, false);
                key?.Close();
                DeleteKeyIfEmpty(baseKey, entry.KeyPath);
                continue;
            }
            using var writable = baseKey.CreateSubKey(entry.KeyPath, true);
            var kind = Enum.Parse<RegistryValueKind>(entry.Kind);
            writable.SetValue(entry.ValueName, DeserializeValue(entry.JsonValue, kind), kind);
        }
    }

    private static object? ReadValue(RegistryChange change)
    {
        using var baseKey = RegistryKey.OpenBaseKey(change.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(change.KeyPath, false);
        return key?.GetValue(change.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    private static void Write(RegistryChange change, bool enabled)
    {
        using var baseKey = RegistryKey.OpenBaseKey(change.Hive, RegistryView.Registry64);
        if (!enabled && change.DeleteWhenDisabled)
        {
            using var existing = baseKey.OpenSubKey(change.KeyPath, true);
            existing?.DeleteValue(change.ValueName, false);
            existing?.Close();
            DeleteKeyIfEmpty(baseKey, change.KeyPath);
            return;
        }
        var value = enabled ? change.EnabledValue : change.DisabledValue;
        if (value is null) throw new InvalidOperationException("该设置没有可写入的恢复值。");
        using var key = baseKey.CreateSubKey(change.KeyPath, true);
        key.SetValue(change.ValueName, value, change.ValueKind);
    }

    private static object DeserializeValue(string? json, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => JsonSerializer.Deserialize<int>(json!),
        RegistryValueKind.QWord => JsonSerializer.Deserialize<long>(json!),
        RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(json!) ?? [],
        RegistryValueKind.Binary => JsonSerializer.Deserialize<byte[]>(json!) ?? [],
        _ => JsonSerializer.Deserialize<string>(json!) ?? ""
    };

    private static bool ValuesEqual(object? current, object? expected)
    {
        if (current is null || expected is null) return current is null && expected is null;
        if (current is int i && expected is int j) return i == j;
        return string.Equals(Convert.ToString(current), Convert.ToString(expected), StringComparison.Ordinal);
    }

    private static void DeleteKeyIfEmpty(RegistryKey baseKey, string path)
    {
        using var key = baseKey.OpenSubKey(path, false);
        if (key is null || key.ValueCount != 0 || key.SubKeyCount != 0) return;
        key.Close();
        baseKey.DeleteSubKey(path, false);
    }
}
