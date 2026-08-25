using System.Diagnostics;
using System.IO;
using WinPilot.Models;

namespace WinPilot.Services;

public static class InstallerLauncher
{
    public static void Launch(SoftwareEntry entry, string downloadedFile)
    {
        if (entry.Installer is null) throw new InvalidOperationException("配置中没有安装方式。");
        if (!File.Exists(downloadedFile)) throw new FileNotFoundException("下载文件已不存在。", downloadedFile);
        var kind = entry.Installer.Kind.Trim().ToLowerInvariant();
        if (kind is not ("exe" or "msi")) throw new NotSupportedException("安装类型只支持 exe 或 msi。");

        var info = new ProcessStartInfo
        {
            FileName = kind == "msi" ? Path.Combine(Environment.SystemDirectory, "msiexec.exe") : downloadedFile,
            UseShellExecute = true,
            Verb = entry.Installer.RequiresAdmin ? "runas" : "open"
        };
        if (kind == "msi")
        {
            info.ArgumentList.Add("/i");
            info.ArgumentList.Add(downloadedFile);
        }
        foreach (var argument in entry.Installer.Arguments)
        {
            if (argument.Contains('\0') || argument.Contains('\r') || argument.Contains('\n'))
                throw new InvalidDataException("安装参数包含无效控制字符。");
            info.ArgumentList.Add(argument);
        }
        _ = Process.Start(info) ?? throw new InvalidOperationException("无法启动安装程序。");
    }

    public static string Describe(SoftwareEntry entry)
    {
        if (entry.Installer is null) return "未配置自动安装";
        var args = entry.Installer.Arguments.Count == 0 ? "（无参数）" : string.Join(" ", entry.Installer.Arguments);
        return $"{entry.Installer.Kind.ToUpperInvariant()} · {args} · {(entry.Installer.RequiresAdmin ? "管理员" : "当前用户")}";
    }
}
