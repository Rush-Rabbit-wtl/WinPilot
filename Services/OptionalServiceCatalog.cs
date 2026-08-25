using WinPilot.Models;

namespace WinPilot.Services;

public static class OptionalServiceCatalog
{
    public static IReadOnlyList<ServiceDefinition> All { get; } =
    [
        new("DiagTrack", "连接用户体验和遥测", "诊断", "禁用后会减少 Windows 诊断数据收集，可能影响部分故障诊断。", RiskLevel.Medium),
        new("MapsBroker", "下载的地图管理器", "可选功能", "不使用 Windows 离线地图时通常可以禁用。", RiskLevel.Low),
        new("Fax", "传真", "可选功能", "不使用传真设备或 Windows 传真和扫描时可以禁用。", RiskLevel.Low),
        new("RetailDemo", "零售演示服务", "可选功能", "普通个人电脑通常不需要零售演示模式。", RiskLevel.Low),
        new("XboxGipSvc", "Xbox 配件管理", "Xbox", "不使用 Xbox 手柄及相关配件时可禁用。", RiskLevel.Medium),
        new("XblAuthManager", "Xbox Live 身份验证", "Xbox", "禁用后可能无法登录 Xbox 服务或运行部分商店游戏。", RiskLevel.Medium),
        new("XblGameSave", "Xbox Live 游戏保存", "Xbox", "禁用后 Xbox 云存档和部分游戏同步可能失效。", RiskLevel.Medium),
        new("XboxNetApiSvc", "Xbox Live 网络服务", "Xbox", "禁用后 Xbox 联机和部分商店游戏网络能力可能失效。", RiskLevel.Medium),
        new("WSearch", "Windows Search", "搜索", "禁用会停止索引，可降低后台磁盘活动，但搜索会明显变慢。", RiskLevel.Medium),
        new("SysMain", "SysMain", "性能", "禁用可能降低部分设备后台预读活动，也可能让应用冷启动变慢。", RiskLevel.Medium),
        new("Spooler", "打印后台处理程序", "打印", "仅在确定不使用打印机、虚拟打印和打印到 PDF 工作流时禁用。", RiskLevel.Medium)
    ];
}
