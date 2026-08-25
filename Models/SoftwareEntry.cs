using System.Text.Json.Serialization;

namespace WinPilot.Models;

public sealed class SoftwareEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public bool SelectedByDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public InstallerSpec? Installer { get; set; }
}

public sealed class InstallerSpec
{
    public string Kind { get; set; } = "exe";
    public List<string> Arguments { get; set; } = [];
    public bool RequiresAdmin { get; set; } = true;
}

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double Percentage => TotalBytes is > 0 ? BytesReceived * 100d / TotalBytes.Value : 0;
}

public sealed record DownloadResult(string FilePath, string Sha256, long Size);
