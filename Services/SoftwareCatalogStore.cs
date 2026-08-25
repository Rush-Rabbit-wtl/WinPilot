using System.IO;
using System.Net;
using System.Text.Json;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class SoftwareCatalogStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string CatalogPath { get; }

    public SoftwareCatalogStore(string? catalogPath = null) => CatalogPath = catalogPath ?? Path.Combine(
        AppContext.BaseDirectory, "config", "software.json");

    public static IReadOnlyList<SoftwareEntry> BuiltIns { get; } =
    [
        new() { Id = "steam", Name = "Steam", Description = "Valve 官方 Windows 客户端安装程序。", Source = "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe", FileName = "SteamSetup.exe", IsBuiltIn = true, Installer = new() { Kind = "exe" } },
        new() { Id = "vscode", Name = "Visual Studio Code", Description = "微软官方最新稳定版 64 位用户安装程序。", Source = "https://update.code.visualstudio.com/latest/win32-x64-user/stable", FileName = "VSCodeUserSetup-x64.exe", IsBuiltIn = true, Installer = new() { Kind = "exe", RequiresAdmin = false, Arguments = ["/VERYSILENT", "/NORESTART"] } },
        new() { Id = "chrome", Name = "Google Chrome", Description = "Google 官方最新版在线安装程序。", Source = "https://dl.google.com/chrome/install/latest/chrome_installer.exe", FileName = "ChromeInstaller.exe", IsBuiltIn = true, Installer = new() { Kind = "exe" } }
    ];

    public IReadOnlyList<SoftwareEntry> LoadAll(bool strict = false)
    {
        if (!File.Exists(CatalogPath)) Save(BuiltIns.Select(Clone).ToList());
        try
        {
            var entries = JsonSerializer.Deserialize<List<SoftwareEntry>>(File.ReadAllText(CatalogPath), _options) ?? [];
            if (strict)
            {
                var invalid = entries.FirstOrDefault(x => !IsValid(x.Name, x.Source, out _));
                if (invalid is not null) throw new InvalidDataException($"软件“{invalid.Name}”的来源地址无效。");
            }
            return entries.Where(x => IsValid(x.Name, x.Source, out _))
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
        }
        catch (JsonException ex) when (strict) { throw new InvalidDataException("software.json 不是有效的 JSON。", ex); }
        catch (JsonException) { return BuiltIns.Select(Clone).ToArray(); }
    }

    public void EnsureCatalog()
    {
        if (!File.Exists(CatalogPath)) Save(BuiltIns.Select(Clone).ToList());
    }

    public IReadOnlyList<SoftwareEntry> LoadCustom()
    {
        try
        {
            if (!File.Exists(CatalogPath)) return [];
            var entries = JsonSerializer.Deserialize<List<SoftwareEntry>>(File.ReadAllText(CatalogPath), _options) ?? [];
            return entries.Where(x => !x.IsBuiltIn && IsValid(x.Name, x.Source, out _)).ToArray();
        }
        catch (JsonException) { return []; }
    }

    public void Add(string name, string source, string description)
    {
        name = name.Trim();
        source = source.Trim();
        description = description.Trim();
        if (!IsValid(name, source, out var error)) throw new ArgumentException(error);
        var entries = LoadAll().Select(Clone).ToList();
        if (entries.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            throw new InvalidOperationException("自定义目录中已经存在同名软件。");
        entries.Add(new SoftwareEntry { Name = name, Source = source, Description = description, IsBuiltIn = false });
        Save(entries);
    }

    public void Remove(string id)
    {
        var entries = LoadAll().Where(x => x.Id != id).Select(Clone).ToList();
        Save(entries);
    }

    public static bool IsValid(string name, string source, out string error)
    {
        if (string.IsNullOrWhiteSpace(name)) { error = "请输入软件名称。"; return false; }
        if (name.Trim().Length > 80) { error = "软件名称不能超过 80 个字符。"; return false; }
        source = source.Trim();
        if (source.StartsWith(@"\\", StringComparison.Ordinal))
        {
            if (source.Length < 5 || string.IsNullOrWhiteSpace(Path.GetFileName(source))) { error = "UNC 共享路径必须指向具体文件。"; return false; }
            error = "";
            return true;
        }
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || !(uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            error = "来源必须是 HTTPS、受信任的局域网 HTTP 地址或 UNC 共享文件路径。";
            return false;
        }
        if (uri.IsLoopback) { error = "不允许使用本机回环地址。"; return false; }
        if (!string.IsNullOrEmpty(uri.UserInfo)) { error = "下载地址不能包含用户名或密码。"; return false; }
        if (uri.Scheme == Uri.UriSchemeHttp && !IsLanHost(uri.Host))
        {
            error = "HTTP 仅允许私有 IP、单标签主机名或 .local/.lan/.internal 局域网主机；互联网地址必须使用 HTTPS。";
            return false;
        }
        error = "";
        return true;
    }

    private void Save(List<SoftwareEntry> custom)
    {
        var directory = Path.GetDirectoryName(CatalogPath)!;
        Directory.CreateDirectory(directory);
        var temp = CatalogPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(custom, _options));
        File.Move(temp, CatalogPath, true);
    }

    private static bool IsLanHost(string host)
    {
        if (!host.Contains('.')) return true;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".home", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out var address)) return false;
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 169 && bytes[1] == 254);
        }
        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
    }

    private static SoftwareEntry Clone(SoftwareEntry entry) => new()
    {
        Id = entry.Id, Name = entry.Name, Description = entry.Description, Source = entry.Source,
        FileName = entry.FileName, Sha256 = entry.Sha256, SelectedByDefault = entry.SelectedByDefault,
        IsBuiltIn = entry.IsBuiltIn,
        Installer = entry.Installer is null ? null : new InstallerSpec
        {
            Kind = entry.Installer.Kind, RequiresAdmin = entry.Installer.RequiresAdmin,
            Arguments = [.. entry.Installer.Arguments]
        }
    };
}
