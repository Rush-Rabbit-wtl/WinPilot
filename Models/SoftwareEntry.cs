namespace WinPilot.Models;

public sealed class SoftwareEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public bool IsBuiltIn { get; set; }
}

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double Percentage => TotalBytes is > 0 ? BytesReceived * 100d / TotalBytes.Value : 0;
}

public sealed record DownloadResult(string FilePath, string Sha256, long Size);
