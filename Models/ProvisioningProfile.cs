using System.Text.Json.Serialization;

namespace WinPilot.Models;

public sealed class ProvisioningProfile
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("name")] public string Name { get; set; } = "我的电脑开荒方案";
    [JsonPropertyName("enableTweaks")] public List<string> EnableTweaks { get; set; } = [];
    [JsonPropertyName("disableServices")] public List<string> DisableServices { get; set; } = [];
    [JsonPropertyName("software")] public List<string> Software { get; set; } = [];
}
