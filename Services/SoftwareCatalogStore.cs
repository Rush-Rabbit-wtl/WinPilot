using System.IO;
using System.Text.Json;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class SoftwareCatalogStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public string CatalogPath { get; }

    public SoftwareCatalogStore(string? catalogPath = null) => CatalogPath = catalogPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinPilot", "software-catalog.json");

    public static IReadOnlyList<SoftwareEntry> BuiltIns { get; } =
    [
        new() { Id = "builtin-steam", Name = "Steam", Description = "Valve 官方 Windows 客户端安装程序。", DownloadUrl = "https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe", IsBuiltIn = true },
        new() { Id = "builtin-vscode", Name = "Visual Studio Code", Description = "微软官方最新稳定版 64 位用户安装程序。", DownloadUrl = "https://update.code.visualstudio.com/latest/win32-x64-user/stable", IsBuiltIn = true },
        new() { Id = "builtin-chrome", Name = "Google Chrome", Description = "Google 官方最新版在线安装程序。", DownloadUrl = "https://dl.google.com/chrome/install/latest/chrome_installer.exe", IsBuiltIn = true }
    ];

    public IReadOnlyList<SoftwareEntry> LoadAll()
    {
        var custom = LoadCustom();
        return BuiltIns.Concat(custom).ToArray();
    }

    public IReadOnlyList<SoftwareEntry> LoadCustom()
    {
        try
        {
            if (!File.Exists(CatalogPath)) return [];
            var entries = JsonSerializer.Deserialize<List<SoftwareEntry>>(File.ReadAllText(CatalogPath), _options) ?? [];
            return entries.Where(x => !x.IsBuiltIn && IsValid(x.Name, x.DownloadUrl, out _)).ToArray();
        }
        catch (JsonException) { return []; }
    }

    public void Add(string name, string downloadUrl, string description)
    {
        name = name.Trim();
        downloadUrl = downloadUrl.Trim();
        description = description.Trim();
        if (!IsValid(name, downloadUrl, out var error)) throw new ArgumentException(error);
        var custom = LoadCustom().ToList();
        if (custom.Any(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            throw new InvalidOperationException("自定义目录中已经存在同名软件。");
        custom.Add(new SoftwareEntry { Name = name, DownloadUrl = downloadUrl, Description = description, IsBuiltIn = false });
        Save(custom);
    }

    public void Remove(string id)
    {
        var custom = LoadCustom().Where(x => x.Id != id).ToList();
        Save(custom);
    }

    public static bool IsValid(string name, string downloadUrl, out string error)
    {
        if (string.IsNullOrWhiteSpace(name)) { error = "请输入软件名称。"; return false; }
        if (name.Trim().Length > 80) { error = "软件名称不能超过 80 个字符。"; return false; }
        if (!Uri.TryCreate(downloadUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "下载地址必须是有效的 HTTPS 地址。";
            return false;
        }
        if (uri.IsLoopback) { error = "不允许使用本机回环地址。"; return false; }
        if (!string.IsNullOrEmpty(uri.UserInfo)) { error = "下载地址不能包含用户名或密码。"; return false; }
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
}
