using Microsoft.Win32;

namespace WinPilot.Models;

public enum RiskLevel { Low, Medium, High }

public sealed record RegistryChange(RegistryHive Hive, string KeyPath, string ValueName,
    object? EnabledValue, RegistryValueKind ValueKind, object? DisabledValue = null,
    bool DeleteWhenDisabled = true);

public sealed record TweakDefinition(string Id, string Category, string Title, string Description,
    RiskLevel Risk, bool RequiresAdmin, bool RequiresExplorerRestart,
    IReadOnlyList<RegistryChange> Changes);

public enum TweakState { Enabled, Disabled, Mixed, Unknown }
