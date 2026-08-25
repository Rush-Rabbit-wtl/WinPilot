using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class SoftwareDownloader
{
    private const long MaxDownloadBytes = 2L * 1024 * 1024 * 1024;
    private readonly HttpClient _client;
    public string DownloadDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "WinPilot");

    public SoftwareDownloader()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 8, AutomaticDecompression = DecompressionMethods.All };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("WinPilot/0.4 (+https://github.com/Rush-Rabbit-wtl/WinPilot)");
    }

    public async Task<DownloadResult> DownloadAsync(SoftwareEntry entry, IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!SoftwareCatalogStore.IsValid(entry.Name, entry.Source, out var error)) throw new ArgumentException(error);
        if (entry.Source.StartsWith(@"\\", StringComparison.Ordinal))
            return await CopyFromShareAsync(entry, progress, cancellationToken);

        using var response = await _client.GetAsync(entry.Source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri ?? new Uri(entry.Source);
        if (!SoftwareCatalogStore.IsValid(entry.Name, finalUri.AbsoluteUri, out _))
            throw new InvalidOperationException("重定向后的地址不符合网络来源安全规则，已拒绝下载。");
        var total = response.Content.Headers.ContentLength;
        if (total > MaxDownloadBytes) throw new InvalidOperationException("文件超过 2 GB 安全限制。");

        Directory.CreateDirectory(DownloadDirectory);
        var fileName = GetSafeFileName(entry.FileName, response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName ?? Path.GetFileName(finalUri.LocalPath), entry.Name);
        var destination = GetUniquePath(Path.Combine(DownloadDirectory, fileName));
        var partial = destination + ".part";
        long received = 0;
        var completed = false;
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                received += read;
                if (received > MaxDownloadBytes) throw new InvalidOperationException("下载内容超过 2 GB 安全限制。");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report(new DownloadProgress(received, total));
            }
            await output.FlushAsync(cancellationToken);
            File.Move(partial, destination);
            await using var verify = File.OpenRead(destination);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken));
            ValidateExpectedHash(entry, hash);
            completed = true;
            return new DownloadResult(destination, hash, received);
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            if (!completed && File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    private async Task<DownloadResult> CopyFromShareAsync(SoftwareEntry entry, IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var source = new FileInfo(entry.Source);
        if (!source.Exists) throw new FileNotFoundException("局域网共享文件不存在或当前账户无权访问。", entry.Source);
        if (source.Length > MaxDownloadBytes) throw new InvalidOperationException("文件超过 2 GB 安全限制。");
        Directory.CreateDirectory(DownloadDirectory);
        var fileName = GetSafeFileName(entry.FileName, source.Name, entry.Name);
        var destination = GetUniquePath(Path.Combine(DownloadDirectory, fileName));
        var partial = destination + ".part";
        var completed = false;
        long received = 0;
        try
        {
            await using var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                received += read;
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report(new DownloadProgress(received, source.Length));
            }
            await output.FlushAsync(cancellationToken);
            File.Move(partial, destination);
            await using var verify = File.OpenRead(destination);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken));
            ValidateExpectedHash(entry, hash);
            completed = true;
            return new DownloadResult(destination, hash, received);
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            if (!completed && File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    private static string GetSafeFileName(string? configuredName, string? detectedName, string fallbackName)
    {
        var proposed = !string.IsNullOrWhiteSpace(configuredName) ? configuredName : detectedName;
        proposed = proposed?.Trim('"');
        if (string.IsNullOrWhiteSpace(proposed) || !Path.HasExtension(proposed)) proposed = fallbackName + ".download";
        proposed = Path.GetFileName(proposed);
        foreach (var invalid in Path.GetInvalidFileNameChars()) proposed = proposed.Replace(invalid, '_');
        var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (reserved.Contains(Path.GetFileNameWithoutExtension(proposed), StringComparer.OrdinalIgnoreCase)) proposed = "download-" + proposed;
        return proposed.Length > 120 ? proposed[..120] : proposed;
    }

    private static void ValidateExpectedHash(SoftwareEntry entry, string actual)
    {
        if (string.IsNullOrWhiteSpace(entry.Sha256)) return;
        var expected = entry.Sha256.Replace(" ", "").Trim();
        if (expected.Length != 64 || !expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA-256 校验失败。期望 {expected}，实际 {actual}。文件已删除。");
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path) && !File.Exists(path + ".part")) return path;
        var directory = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{stem}-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
    }
}
