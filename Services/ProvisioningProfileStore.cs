using System.IO;
using System.Text.Json;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class ProvisioningProfileStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string ProfilePath { get; }

    public ProvisioningProfileStore(string? profilePath = null) => ProfilePath = profilePath ??
        Path.Combine(AppContext.BaseDirectory, "config", "profile.json");

    public ProvisioningProfile Load()
    {
        EnsureProfile();
        var profile = JsonSerializer.Deserialize<ProvisioningProfile>(File.ReadAllText(ProfilePath), _options)
            ?? throw new InvalidDataException("开荒方案为空。");
        if (profile.SchemaVersion != 1) throw new InvalidDataException($"不支持方案版本 {profile.SchemaVersion}。");
        profile.EnableTweaks = Unique(profile.EnableTweaks);
        profile.DisableServices = Unique(profile.DisableServices);
        profile.Software = Unique(profile.Software);
        return profile;
    }

    public void Save(ProvisioningProfile profile)
    {
        var directory = Path.GetDirectoryName(ProfilePath)!;
        Directory.CreateDirectory(directory);
        var temp = ProfilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _options));
        File.Move(temp, ProfilePath, true);
    }

    public void EnsureProfile()
    {
        if (!File.Exists(ProfilePath)) Save(Default());
    }

    public static ProvisioningProfile Default() => new()
    {
        EnableTweaks = ["show-extensions", "show-hidden", "disable-suggestions", "disable-ad-id"],
        Software = ["vscode", "chrome"]
    };

    private static List<string> Unique(IEnumerable<string>? values) => (values ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
