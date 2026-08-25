using System.ServiceProcess;

namespace WinPilot.Models;

public sealed record ServiceDefinition(string ServiceName, string DisplayName, string Group,
    string Description, RiskLevel Risk);

public sealed record ServiceInfo(bool Available, ServiceStartMode StartMode,
    ServiceControllerStatus Status, bool CanStop, bool DelayedAutoStart)
{
    public static ServiceInfo Missing { get; } = new(false, ServiceStartMode.Manual,
        ServiceControllerStatus.Stopped, false, false);
}
