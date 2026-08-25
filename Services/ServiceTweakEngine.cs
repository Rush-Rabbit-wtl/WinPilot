using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;
using WinPilot.Models;

namespace WinPilot.Services;

public sealed class ServiceTweakEngine
{
    private readonly SnapshotStore _snapshots;
    public ServiceTweakEngine(SnapshotStore snapshots) => _snapshots = snapshots;

    public ServiceInfo GetInfo(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _ = service.DisplayName;
            var delayed = false;
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("DelayedAutoStart") is int value) delayed = value == 1;
            return new ServiceInfo(true, service.StartType, service.Status, service.CanStop, delayed);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return ServiceInfo.Missing;
        }
    }

    public string Disable(ServiceDefinition definition)
    {
        if (!RegistryTweakEngine.IsAdministrator())
            throw new UnauthorizedAccessException("修改 Windows 服务需要以管理员身份运行 WinPilot。");
        var info = GetInfo(definition.ServiceName);
        if (!info.Available) throw new InvalidOperationException("当前系统不存在该服务。");

        var snapshot = new Snapshot
        {
            Services = [new ServiceSnapshotEntry
            {
                ServiceName = definition.ServiceName, StartMode = info.StartMode.ToString(),
                Status = info.Status.ToString(), DelayedAutoStart = info.DelayedAutoStart
            }]
        };
        var path = _snapshots.Save(snapshot);
        try
        {
            using var service = new ServiceController(definition.ServiceName);
            if (service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.Paused && service.CanStop)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
            Configure(definition.ServiceName, ServiceStartMode.Disabled, false);
            return path;
        }
        catch
        {
            Restore(snapshot);
            throw;
        }
    }

    public string RestoreLatest(string serviceName)
    {
        if (!RegistryTweakEngine.IsAdministrator())
            throw new UnauthorizedAccessException("恢复 Windows 服务需要以管理员身份运行 WinPilot。");
        foreach (var path in _snapshots.List())
        {
            var snapshot = _snapshots.Load(path);
            if (snapshot.Services.Any(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)))
            {
                Restore(snapshot, serviceName);
                return path;
            }
        }
        throw new InvalidOperationException("没有找到该服务的原始状态快照，WinPilot 不会猜测默认值。");
    }

    public bool HasSnapshot(string serviceName) => _snapshots.List().Any(path =>
        _snapshots.Load(path).Services.Any(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)));

    public void Restore(Snapshot snapshot, string? onlyService = null)
    {
        foreach (var entry in snapshot.Services.Where(x => onlyService is null || x.ServiceName.Equals(onlyService, StringComparison.OrdinalIgnoreCase)))
        {
            var startMode = Enum.Parse<ServiceStartMode>(entry.StartMode);
            Configure(entry.ServiceName, startMode, entry.DelayedAutoStart);
            using var service = new ServiceController(entry.ServiceName);
            if (entry.Status == ServiceControllerStatus.Running.ToString() && service.Status == ServiceControllerStatus.Stopped)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            }
            else if (entry.Status == ServiceControllerStatus.Stopped.ToString() && service.Status == ServiceControllerStatus.Running && service.CanStop)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
    }

    private static void Configure(string serviceName, ServiceStartMode mode, bool delayed)
    {
        var token = mode switch
        {
            ServiceStartMode.Automatic => "auto",
            ServiceStartMode.Disabled => "disabled",
            ServiceStartMode.Manual => "demand",
            ServiceStartMode.Boot => "boot",
            ServiceStartMode.System => "system",
            _ => throw new NotSupportedException($"不支持的启动类型：{mode}")
        };
        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        var startInfo = new ProcessStartInfo(sc) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        startInfo.ArgumentList.Add("config");
        startInfo.ArgumentList.Add(serviceName);
        startInfo.ArgumentList.Add("start=");
        startInfo.ArgumentList.Add(token);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动服务配置程序。");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"服务配置失败（{process.ExitCode}）：{stderr}{stdout}".Trim());

        if (mode == ServiceStartMode.Automatic)
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", true);
            key?.SetValue("DelayedAutoStart", delayed ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
