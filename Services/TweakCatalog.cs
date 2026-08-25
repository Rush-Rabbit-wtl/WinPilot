using Microsoft.Win32;
using WinPilot.Models;

namespace WinPilot.Services;

public static class TweakCatalog
{
    private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public static IReadOnlyList<TweakDefinition> All { get; } =
    [
        One("show-extensions", "资源管理器", "显示文件扩展名", "让可执行文件与伪装文件更容易识别。", RiskLevel.Low,
            RegistryHive.CurrentUser, ExplorerAdvanced, "HideFileExt", 0, RegistryValueKind.DWord, 1, false),
        One("show-hidden", "资源管理器", "显示隐藏文件", "在资源管理器中显示隐藏的文件与文件夹。", RiskLevel.Low,
            RegistryHive.CurrentUser, ExplorerAdvanced, "Hidden", 1, RegistryValueKind.DWord, 2, false),
        One("show-seconds", "任务栏", "任务栏时钟显示秒", "在系统托盘时钟中显示秒数；部分版本需重启资源管理器。", RiskLevel.Low,
            RegistryHive.CurrentUser, ExplorerAdvanced, "ShowSecondsInSystemClock", 1, RegistryValueKind.DWord, 0, true, restartExplorer: true),
        One("left-taskbar", "任务栏", "任务栏左对齐", "将 Windows 11 任务栏图标改为左对齐。", RiskLevel.Low,
            RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarAl", 0, RegistryValueKind.DWord, 1, false, restartExplorer: true),
        One("hide-widgets", "任务栏", "隐藏小组件", "隐藏任务栏的小组件按钮，不卸载系统组件。", RiskLevel.Low,
            RegistryHive.CurrentUser, ExplorerAdvanced, "TaskbarDa", 0, RegistryValueKind.DWord, 1, false, restartExplorer: true),
        One("hide-search", "任务栏", "隐藏任务栏搜索框", "保留开始菜单搜索，只隐藏任务栏入口。", RiskLevel.Low,
            RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, RegistryValueKind.DWord, 1, false, restartExplorer: true),
        One("classic-menu", "资源管理器", "使用经典右键菜单", "在支持的 Windows 11 版本上恢复紧凑的经典右键菜单。", RiskLevel.Medium,
            RegistryHive.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "", RegistryValueKind.String, null, true, restartExplorer: true),
        One("disable-suggestions", "隐私", "关闭个性化推荐", "关闭设置建议和部分开始菜单推荐内容。", RiskLevel.Low,
            RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0, RegistryValueKind.DWord, 1, false),
        One("disable-ad-id", "隐私", "关闭广告 ID", "禁止应用使用广告 ID 提供跨应用个性化体验。", RiskLevel.Low,
            RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord, 1, false),
        One("disable-web-search", "隐私", "关闭开始菜单 Web 搜索", "减少开始菜单搜索中的联网内容，不影响浏览器。", RiskLevel.Medium,
            RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord, 0, true, restartExplorer: true),
        One("disable-copilot", "隐私", "关闭 Windows Copilot", "通过当前用户策略关闭 Windows Copilot；可随时撤销。", RiskLevel.Medium,
            RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord, 0, true, restartExplorer: true),
        One("disable-edge-boost", "应用", "关闭 Edge 启动增强", "禁止 Edge 登录时预启动；不会卸载 Edge 或 WebView2。", RiskLevel.Low,
            RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Edge", "StartupBoostEnabled", 0, RegistryValueKind.DWord, 1, true),
        One("disable-edge-background", "应用", "关闭 Edge 后台运行", "关闭浏览器后不再保留 Edge 后台应用。", RiskLevel.Low,
            RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0, RegistryValueKind.DWord, 1, true),
        One("disable-delivery-opt", "更新", "限制更新传递优化", "仅使用 HTTP 下载更新，避免向其他设备上传更新片段。", RiskLevel.Medium,
            RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 0, RegistryValueKind.DWord, null, true, admin: true),
        One("disable-consumer-features", "应用", "关闭消费者体验", "阻止系统为新用户自动推荐部分消费类应用。", RiskLevel.Medium,
            RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1, RegistryValueKind.DWord, null, true, admin: true),
        new TweakDefinition("disable-mouse-accel", "性能", "关闭鼠标加速", "关闭 Enhance Pointer Precision 的三个相关注册表值。", RiskLevel.Low, false, false,
        [
            new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String, "1", false),
            new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String, "6", false),
            new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String, "10", false)
        ])
    ];

    private static TweakDefinition One(string id, string category, string title, string description, RiskLevel risk,
        RegistryHive hive, string path, string name, object enabled, RegistryValueKind kind, object? disabled,
        bool deleteWhenDisabled, bool admin = false, bool restartExplorer = false) =>
        new(id, category, title, description, risk, admin, restartExplorer,
            [new RegistryChange(hive, path, name, enabled, kind, disabled, deleteWhenDisabled)]);
}
