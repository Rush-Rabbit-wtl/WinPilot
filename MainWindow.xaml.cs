using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinPilot.Models;
using WinPilot.Services;

namespace WinPilot;

public partial class MainWindow : Window
{
    private readonly SnapshotStore _snapshots = new();
    private readonly RegistryTweakEngine _engine;
    private readonly ServiceTweakEngine _serviceEngine;
    private readonly SoftwareCatalogStore _softwareStore = new();
    private readonly SoftwareDownloader _softwareDownloader = new();
    private readonly ProvisioningProfileStore _profileStore = new();
    private ProvisioningProfile _profile = ProvisioningProfileStore.Default();
    private readonly ObservableCollection<TweakItem> _items;
    private readonly ObservableCollection<ServiceItem> _serviceItems;
    private readonly ObservableCollection<SoftwareItem> _softwareItems;
    private readonly ICollectionView _view;
    private readonly ICollectionView _serviceView;
    private readonly ICollectionView _softwareView;
    private string _category = "全部";

    public MainWindow()
    {
        InitializeComponent();
        _engine = new RegistryTweakEngine(_snapshots);
        _serviceEngine = new ServiceTweakEngine(_snapshots);
        _items = new(TweakCatalog.All.Select(x => new TweakItem(x)));
        _serviceItems = new(OptionalServiceCatalog.All.Select(x => new ServiceItem(x)));
        _softwareItems = new(_softwareStore.LoadAll().Select(x => new SoftwareItem(x)));
        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItem;
        _serviceView = CollectionViewSource.GetDefaultView(_serviceItems);
        _serviceView.Filter = FilterService;
        _softwareView = CollectionViewSource.GetDefaultView(_softwareItems);
        _softwareView.Filter = FilterSoftware;
        TweakList.ItemsSource = _view;
        ServiceList.ItemsSource = _serviceView;
        SoftwareList.ItemsSource = _softwareView;
        SoftwareConfigPathText.Text = _softwareStore.CatalogPath;
        ProfilePathText.Text = _profileStore.ProfilePath;
        try { ReloadProfileView(); }
        catch (Exception ex)
        {
            ProfileNameText.Text = "方案配置无法读取";
            ProfileTweaksText.Text = ProfileServicesText.Text = ProfileSoftwareText.Text = "（无）";
            ProfileWarningsText.Text = ex.Message + " 请编辑 profile.json 后重新加载。";
        }
        Loaded += async (_, _) =>
        {
            await RefreshStatesAsync();
            var screenshotCategory = Environment.GetEnvironmentVariable("WINPILOT_SCREENSHOT_CATEGORY");
            if (!string.IsNullOrWhiteSpace(screenshotCategory)) SelectCategory(screenshotCategory);
            var screenshotPath = Environment.GetEnvironmentVariable("WINPILOT_SCREENSHOT_PATH");
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                SaveVisual(screenshotPath);
                Close();
            }
        };
        var admin = RegistryTweakEngine.IsAdministrator();
        AdminText.Text = admin ? "管理员" : "标准用户";
        AdminText.Foreground = admin ? (Brush)FindResource("Accent") : Brushes.Gold;
        ElevateButton.Visibility = admin ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool FilterItem(object value)
    {
        var item = (TweakItem)value;
        var categoryMatch = _category == "全部" || item.Definition.Category == _category;
        var query = SearchBox?.Text?.Trim() ?? "";
        return categoryMatch && (query.Length == 0 || item.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private bool FilterService(object value)
    {
        var item = (ServiceItem)value;
        var query = SearchBox?.Text?.Trim() ?? "";
        return query.Length == 0 || item.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool FilterSoftware(object value)
    {
        var item = (SoftwareItem)value;
        var query = SearchBox?.Text?.Trim() ?? "";
        return query.Length == 0 || item.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private async Task RefreshStatesAsync()
    {
        StatusText.Text = "正在检测系统状态…";
        foreach (var item in _items)
            item.State = await Task.Run(() => _engine.GetState(item.Definition));
        foreach (var item in _serviceItems)
        {
            item.Info = await Task.Run(() => _serviceEngine.GetInfo(item.Definition.ServiceName));
            item.HasSnapshot = await Task.Run(() => _serviceEngine.HasSnapshot(item.Definition.ServiceName));
        }
        UpdateSummary();
        StatusText.Text = $"状态已刷新 · Windows build {Environment.OSVersion.Version.Build}";
    }

    private void UpdateSummary()
    {
        var services = ServiceScroll.Visibility == Visibility.Visible;
        var software = SoftwareScroll.Visibility == Visibility.Visible;
        var provisioning = ProvisioningScroll.Visibility == Visibility.Visible;
        CountText.Text = services ? _serviceItems.Count(x => x.Info.Available).ToString()
            : software ? _softwareItems.Count.ToString() : provisioning ? _profile.EnableTweaks.Count.ToString() : _items.Count.ToString();
        EnabledLabel.Text = services ? "已禁用" : software ? "自定义" : provisioning ? "方案软件" : "已启用";
        EnabledText.Text = services
            ? _serviceItems.Count(x => x.Info.Available && x.Info.StartMode == System.ServiceProcess.ServiceStartMode.Disabled).ToString()
            : software ? _softwareItems.Count(x => !x.Entry.IsBuiltIn).ToString()
            : provisioning ? _profile.Software.Count.ToString()
            : _items.Count(x => x.State == TweakState.Enabled).ToString();
        SnapshotText.Text = _snapshots.List().Count.ToString();
    }

    private void Category_Click(object sender, RoutedEventArgs e)
        => SelectCategory((string)((Button)sender).Tag);

    private void SelectCategory(string category)
    {
        _category = category;
        var services = _category == "服务管理";
        var software = _category == "软件下载";
        var provisioning = _category == "开荒方案";
        TweakScroll.Visibility = services || software || provisioning ? Visibility.Collapsed : Visibility.Visible;
        ServiceScroll.Visibility = services ? Visibility.Visible : Visibility.Collapsed;
        SoftwareScroll.Visibility = software ? Visibility.Visible : Visibility.Collapsed;
        ProvisioningScroll.Visibility = provisioning ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = _category == "全部" ? "系统调优" : _category;
        _view.Refresh();
        _serviceView.Refresh();
        _softwareView.Refresh();
        UpdateSummary();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view?.Refresh();
        _serviceView?.Refresh();
        _softwareView?.Refresh();
    }
    private async void Apply_Click(object sender, RoutedEventArgs e) => await ChangeAsync((TweakItem)((Button)sender).Tag, true);
    private async void RestoreDefault_Click(object sender, RoutedEventArgs e) => await ChangeAsync((TweakItem)((Button)sender).Tag, false);

    private async Task ChangeAsync(TweakItem item, bool enabled)
    {
        var action = enabled ? "应用" : "恢复";
        var impact = item.Definition.Changes.Count == 1
            ? $"{item.Definition.Changes[0].Hive}\\{item.Definition.Changes[0].KeyPath}"
            : $"将修改 {item.Definition.Changes.Count} 个注册表值";
        var warning = item.Definition.Risk == RiskLevel.Medium ? "\n\n这是中风险设置，请确认你理解其影响。" : "";
        if (MessageBox.Show($"{action}“{item.Definition.Title}”？\n\n影响：{impact}\n操作前会自动保存原始值。{warning}",
            "变更预览", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        try
        {
            StatusText.Text = $"正在{action}：{item.Definition.Title}…";
            var path = await Task.Run(() => _engine.Apply(item.Definition, enabled));
            item.State = await Task.Run(() => _engine.GetState(item.Definition));
            UpdateSummary();
            StatusText.Text = $"已{action} · 快照：{Path.GetFileName(path)}" + (item.Definition.RequiresExplorerRestart ? " · 建议重启资源管理器" : "");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{action}失败：{ex.Message}";
            MessageBox.Show(ex.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RollbackLatest_Click(object sender, RoutedEventArgs e)
    {
        var latest = _snapshots.List().FirstOrDefault();
        if (latest is null) { MessageBox.Show("还没有可用快照。", "WinPilot"); return; }
        if (MessageBox.Show($"恢复最近快照？\n\n{Path.GetFileName(latest)}", "确认回滚", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            await Task.Run(() => _engine.Restore(_snapshots.Load(latest)));
            await Task.Run(() => _serviceEngine.Restore(_snapshots.Load(latest)));
            await RefreshStatesAsync();
            StatusText.Text = $"已回滚：{Path.GetFileName(latest)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "回滚失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenSnapshots_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_snapshots.RootDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _snapshots.RootDirectory) { UseShellExecute = true });
    }

    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" }); Close(); }
        catch (Win32Exception) { StatusText.Text = "已取消管理员授权。"; }
    }

    private void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("资源管理器和任务栏会短暂关闭并自动恢复。继续吗？", "重启资源管理器", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        foreach (var process in Process.GetProcessesByName("explorer")) process.Kill();
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        StatusText.Text = "资源管理器已重新启动。";
    }

    private async void DisableService_Click(object sender, RoutedEventArgs e)
    {
        var item = (ServiceItem)((Button)sender).Tag;
        if (!item.Info.Available) { MessageBox.Show("当前系统不存在该服务。", "WinPilot"); return; }
        if (MessageBox.Show($"禁用“{item.Definition.DisplayName}”？\n\n当前启动类型：{item.StartModeText}\n当前状态：{item.StatusText}\n\nWinPilot 会先保存精确状态。",
            "服务变更预览", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            StatusText.Text = $"正在禁用服务：{item.Definition.ServiceName}…";
            var path = await Task.Run(() => _serviceEngine.Disable(item.Definition));
            item.Info = await Task.Run(() => _serviceEngine.GetInfo(item.Definition.ServiceName));
            item.HasSnapshot = true;
            UpdateSummary();
            StatusText.Text = $"服务已禁用 · 快照：{Path.GetFileName(path)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "服务操作失败", MessageBoxButton.OK, MessageBoxImage.Warning); StatusText.Text = ex.Message; }
    }

    private async void RestoreService_Click(object sender, RoutedEventArgs e)
    {
        var item = (ServiceItem)((Button)sender).Tag;
        if (MessageBox.Show($"按最近快照恢复“{item.Definition.DisplayName}”的启动类型和运行状态？", "恢复服务", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            var path = await Task.Run(() => _serviceEngine.RestoreLatest(item.Definition.ServiceName));
            item.Info = await Task.Run(() => _serviceEngine.GetInfo(item.Definition.ServiceName));
            UpdateSummary();
            StatusText.Text = $"服务已恢复 · 来源：{Path.GetFileName(path)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "服务恢复失败", MessageBoxButton.OK, MessageBoxImage.Warning); StatusText.Text = ex.Message; }
    }

    private void AddSoftware_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _softwareStore.Add(SoftwareNameBox.Text, SoftwareUrlBox.Text, SoftwareDescriptionBox.Text);
            ReloadSoftwareCatalog();
            SoftwareNameBox.Clear();
            SoftwareUrlBox.Clear();
            SoftwareDescriptionBox.Clear();
            StatusText.Text = "自定义软件已保存到本地目录。";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "无法保存软件", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void DownloadSoftware_Click(object sender, RoutedEventArgs e)
    {
        var item = (SoftwareItem)((Button)sender).Tag;
        if (MessageBox.Show($"下载“{item.Entry.Name}”？\n\n来源：{item.Entry.Source}\n保存到：{_softwareDownloader.DownloadDirectory}\n\n下载完成后仍需单独确认才能安装。",
            "确认下载", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        item.IsBusy = true;
        item.StatusText = "正在连接…";
        item.StatusForeground = Brushes.LightSkyBlue;
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                item.ProgressValue = p.Percentage;
                item.StatusText = p.TotalBytes is > 0
                    ? $"已下载 {FormatBytes(p.BytesReceived)} / {FormatBytes(p.TotalBytes.Value)}"
                    : $"已下载 {FormatBytes(p.BytesReceived)}";
            });
            var result = await _softwareDownloader.DownloadAsync(item.Entry, progress);
            item.DownloadedFilePath = result.FilePath;
            item.ProgressValue = 100;
            item.StatusForeground = (Brush)FindResource("Accent");
            item.StatusText = $"下载完成 · {Path.GetFileName(result.FilePath)} · SHA-256 {result.Sha256[..12]}…";
            StatusText.Text = $"{item.Entry.Name} 已下载到 {result.FilePath}";
            MessageBox.Show($"下载完成。\n\n文件：{result.FilePath}\n大小：{FormatBytes(result.Size)}\nSHA-256：{result.Sha256}\n\n运行前请核对来源和数字签名。",
                "下载完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            item.StatusForeground = Brushes.OrangeRed;
            item.StatusText = "下载失败：" + ex.Message;
        }
        finally { item.IsBusy = false; }
    }

    private void InstallSoftware_Click(object sender, RoutedEventArgs e)
    {
        var item = (SoftwareItem)((Button)sender).Tag;
        if (string.IsNullOrWhiteSpace(item.DownloadedFilePath) || !File.Exists(item.DownloadedFilePath))
        {
            MessageBox.Show("请先下载该软件。", "WinPilot", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var expectedHash = string.IsNullOrWhiteSpace(item.Entry.Sha256) ? "未配置（建议在 JSON 中填写）" : item.Entry.Sha256;
        if (MessageBox.Show($"运行安装程序？\n\n软件：{item.Entry.Name}\n文件：{item.DownloadedFilePath}\n方式：{InstallerLauncher.Describe(item.Entry)}\n预期 SHA-256：{expectedHash}\n\n即将启动外部安装程序。",
            "安装确认", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            InstallerLauncher.Launch(item.Entry, item.DownloadedFilePath);
            StatusText.Text = $"已启动 {item.Entry.Name} 安装程序。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "无法启动安装程序", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = ex.Message;
        }
    }

    private void EditSoftwareConfig_Click(object sender, RoutedEventArgs e)
    {
        _softwareStore.EnsureCatalog();
        Process.Start(new ProcessStartInfo("notepad.exe", _softwareStore.CatalogPath) { UseShellExecute = true });
        StatusText.Text = "配置已在记事本中打开；保存后点击“重新加载配置”。";
    }

    private void ReloadSoftwareConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ReloadSoftwareCatalog();
            StatusText.Text = "软件配置已重新加载。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "配置格式有误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteSoftware_Click(object sender, RoutedEventArgs e)
    {
        var item = (SoftwareItem)((Button)sender).Tag;
        if (item.Entry.IsBuiltIn) return;
        if (MessageBox.Show($"从自定义目录删除“{item.Entry.Name}”？\n不会删除已经下载的文件。", "删除目录项",
            MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _softwareStore.Remove(item.Entry.Id);
        ReloadSoftwareCatalog();
        StatusText.Text = "自定义目录项已删除。";
    }

    private void OpenDownloads_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_softwareDownloader.DownloadDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", _softwareDownloader.DownloadDirectory) { UseShellExecute = true });
    }

    private void ReloadSoftwareCatalog()
    {
        _softwareItems.Clear();
        foreach (var entry in _softwareStore.LoadAll(true)) _softwareItems.Add(new SoftwareItem(entry));
        _softwareView.Refresh();
        ReloadProfileView();
        UpdateSummary();
    }

    private void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        _profileStore.EnsureProfile();
        Process.Start(new ProcessStartInfo("notepad.exe", _profileStore.ProfilePath) { UseShellExecute = true });
        StatusText.Text = "方案已在记事本中打开；保存后点击“重新加载”。";
    }

    private void ReloadProfile_Click(object sender, RoutedEventArgs e)
    {
        try { ReloadProfileView(); StatusText.Text = "开荒方案已重新加载。"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "方案格式有误", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ReloadProfileView()
    {
        _profile = _profileStore.Load();
        var tweaks = _profile.EnableTweaks.Select(id => TweakCatalog.All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Title ?? $"未知 ID：{id}");
        var services = _profile.DisableServices.Select(id => OptionalServiceCatalog.All.FirstOrDefault(x => x.ServiceName.Equals(id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? $"未知 ID：{id}");
        var software = _profile.Software.Select(id => _softwareItems.FirstOrDefault(x => x.Entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Entry.Name ?? $"未知 ID：{id}");
        ProfileNameText.Text = _profile.Name;
        ProfileTweaksText.Text = JoinPreview(tweaks);
        ProfileServicesText.Text = JoinPreview(services);
        ProfileSoftwareText.Text = JoinPreview(software);
        var unknown = _profile.EnableTweaks.Count(id => !TweakCatalog.All.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            + _profile.DisableServices.Count(id => !OptionalServiceCatalog.All.Any(x => x.ServiceName.Equals(id, StringComparison.OrdinalIgnoreCase)))
            + _profile.Software.Count(id => !_softwareItems.Any(x => x.Entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
        ProfileWarningsText.Text = unknown == 0 ? "方案检查通过。系统配置会创建回滚快照；软件下载后不会自动安装。" : $"发现 {unknown} 个未知 ID，这些项目会被跳过。";
        UpdateSummary();
    }

    private void CaptureProfile_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("用当前检测到的已启用设置和已禁用服务覆盖 profile.json？\n\n软件清单会保留现有方案；程序不会尝试猜测电脑中已安装的软件。",
            "捕获本机状态", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _profile.EnableTweaks = _items.Where(x => x.State == TweakState.Enabled).Select(x => x.Definition.Id).ToList();
        _profile.DisableServices = _serviceItems.Where(x => x.Info.Available && x.Info.StartMode == System.ServiceProcess.ServiceStartMode.Disabled)
            .Select(x => x.Definition.ServiceName).ToList();
        _profileStore.Save(_profile);
        ReloadProfileView();
        StatusText.Text = "本机系统状态已保存到便携方案。";
    }

    private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        var tweaks = _profile.EnableTweaks.Select(id => TweakCatalog.All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).Where(x => x is not null).Cast<TweakDefinition>().ToArray();
        var services = _profile.DisableServices.Select(id => OptionalServiceCatalog.All.FirstOrDefault(x => x.ServiceName.Equals(id, StringComparison.OrdinalIgnoreCase))).Where(x => x is not null).Cast<ServiceDefinition>().ToArray();
        if ((services.Length > 0 || tweaks.Any(x => x.RequiresAdmin)) && !RegistryTweakEngine.IsAdministrator())
        {
            MessageBox.Show("此方案包含管理员级设置或服务变更，请先点击右上角“管理员运行”。", "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show($"应用“{_profile.Name}”的系统配置？\n\n启用设置：{tweaks.Length} 项\n禁用服务：{services.Length} 项\n\n每项变更前都会保存快照；软件不会在此步骤安装。",
            "方案执行预览", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        var failures = new List<string>();
        foreach (var tweak in tweaks)
        {
            try { StatusText.Text = $"正在应用：{tweak.Title}…"; await Task.Run(() => _engine.Apply(tweak, true)); }
            catch (Exception ex) { failures.Add($"{tweak.Title}：{ex.Message}"); }
        }
        foreach (var service in services)
        {
            try { StatusText.Text = $"正在禁用服务：{service.DisplayName}…"; await Task.Run(() => _serviceEngine.Disable(service)); }
            catch (Exception ex) { failures.Add($"{service.DisplayName}：{ex.Message}"); }
        }
        await RefreshStatesAsync();
        StatusText.Text = failures.Count == 0 ? "方案中的系统配置已全部应用。" : $"方案执行完成，{failures.Count} 项失败。";
        if (failures.Count > 0) MessageBox.Show(string.Join("\n", failures), "部分项目未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void DownloadProfileSoftware_Click(object sender, RoutedEventArgs e)
    {
        var entries = _profile.Software.Select(id => _softwareItems.FirstOrDefault(x => x.Entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x is not null).Cast<SoftwareItem>().ToArray();
        if (entries.Length == 0) { MessageBox.Show("方案中没有可识别的软件。", "WinPilot"); return; }
        if (MessageBox.Show($"依次下载方案中的 {entries.Length} 个软件？\n\n保存到：{_softwareDownloader.DownloadDirectory}\n下载完成后不会自动安装。",
            "软件下载预览", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        var failures = new List<string>();
        foreach (var item in entries)
        {
            item.IsBusy = true;
            try
            {
                StatusText.Text = $"正在下载：{item.Entry.Name}…";
                var progress = new Progress<DownloadProgress>(p => { item.ProgressValue = p.Percentage; item.StatusText = $"已下载 {FormatBytes(p.BytesReceived)}"; });
                var result = await _softwareDownloader.DownloadAsync(item.Entry, progress);
                item.DownloadedFilePath = result.FilePath;
                item.ProgressValue = 100;
                item.StatusForeground = (Brush)FindResource("Accent");
                item.StatusText = $"下载完成 · SHA-256 {result.Sha256[..12]}…";
            }
            catch (Exception ex) { failures.Add($"{item.Entry.Name}：{ex.Message}"); item.StatusForeground = Brushes.OrangeRed; item.StatusText = "下载失败：" + ex.Message; }
            finally { item.IsBusy = false; }
        }
        StatusText.Text = failures.Count == 0 ? "方案软件已全部下载，可到软件下载页逐个确认安装。" : $"软件下载完成，{failures.Count} 项失败。";
        if (failures.Count > 0) MessageBox.Show(string.Join("\n", failures), "部分下载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string JoinPreview(IEnumerable<string> values)
    {
        var list = values.ToArray();
        return list.Length == 0 ? "（无）" : string.Join("  ·  ", list);
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)value;
        var index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return $"{size:0.##} {units[index]}";
    }

    private void SaveVisual(string path)
    {
        var width = Math.Max(1, (int)ActualWidth);
        var height = Math.Max(1, (int)ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

public sealed class SoftwareItem : INotifyPropertyChanged
{
    private bool _isBusy;
    private double _progressValue;
    private string _statusText = "等待下载";
    private Brush _statusForeground = new SolidColorBrush(Color.FromRgb(101, 117, 141));
    private string? _downloadedFilePath;
    public SoftwareEntry Entry { get; }
    public string Initial => string.IsNullOrWhiteSpace(Entry.Name) ? "?" : Entry.Name[..1].ToUpperInvariant();
    public string SourceLabel => Entry.IsBuiltIn ? "示例目录" : "自定义";
    public string HostText => Entry.Source.StartsWith(@"\\", StringComparison.Ordinal)
        ? "LAN 共享 · " + Entry.Source
        : Uri.TryCreate(Entry.Source, UriKind.Absolute, out var uri) ? $"{uri.Scheme.ToUpperInvariant()} · {uri.Host}" : "地址无效";
    public string SearchText => $"{Entry.Name} {Entry.Description} {Entry.Source}";
    public string InstallText => InstallerLauncher.Describe(Entry);
    public string? DownloadedFilePath { get => _downloadedFilePath; set { _downloadedFilePath = value; Changed(); Changed(nameof(CanInstall)); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; Changed(); Changed(nameof(CanDownload)); Changed(nameof(CanInstall)); Changed(nameof(DownloadButtonText)); Changed(nameof(ProgressVisibility)); } }
    public bool CanDownload => !IsBusy;
    public bool CanInstall => !IsBusy && Entry.Installer is not null && !string.IsNullOrWhiteSpace(DownloadedFilePath) && File.Exists(DownloadedFilePath);
    public Visibility InstallVisibility => Entry.Installer is null ? Visibility.Collapsed : Visibility.Visible;
    public string DownloadButtonText => IsBusy ? "下载中…" : "下载";
    public double ProgressValue { get => _progressValue; set { _progressValue = value; Changed(); Changed(nameof(ProgressVisibility)); } }
    public Visibility ProgressVisibility => IsBusy || ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string StatusText { get => _statusText; set { _statusText = value; Changed(); } }
    public Brush StatusForeground { get => _statusForeground; set { _statusForeground = value; Changed(); } }
    public Visibility DeleteVisibility => Entry.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible;
    public SoftwareItem(SoftwareEntry entry) => Entry = entry;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class ServiceItem : INotifyPropertyChanged
{
    private ServiceInfo _info = ServiceInfo.Missing;
    private bool _hasSnapshot;
    public ServiceDefinition Definition { get; }
    public ServiceInfo Info { get => _info; set { _info = value; Changed(); ChangedStatus(); } }
    public bool HasSnapshot { get => _hasSnapshot; set { _hasSnapshot = value; Changed(); Changed(nameof(DetailText)); } }
    public string SearchText => $"{Definition.ServiceName} {Definition.DisplayName} {Definition.Group} {Definition.Description}";
    public string RiskText => Definition.Risk == RiskLevel.Low ? "低风险" : "中风险";
    public Brush RiskBackground => new SolidColorBrush(Definition.Risk == RiskLevel.Low ? Color.FromRgb(22, 60, 53) : Color.FromRgb(74, 54, 24));
    public Brush RiskForeground => new SolidColorBrush(Definition.Risk == RiskLevel.Low ? Color.FromRgb(101, 214, 173) : Color.FromRgb(251, 191, 36));
    public string StartModeText => !Info.Available ? "不可用" : Info.StartMode switch { System.ServiceProcess.ServiceStartMode.Automatic => Info.DelayedAutoStart ? "自动（延迟）" : "自动", System.ServiceProcess.ServiceStartMode.Manual => "手动", System.ServiceProcess.ServiceStartMode.Disabled => "已禁用", _ => Info.StartMode.ToString() };
    public string StatusText => !Info.Available ? "系统未安装" : Info.Status switch { System.ServiceProcess.ServiceControllerStatus.Running => "正在运行", System.ServiceProcess.ServiceControllerStatus.Stopped => "已停止", _ => Info.Status.ToString() };
    public string StateText => !Info.Available ? "不可用" : $"{StartModeText} · {StatusText}";
    public string DetailText => $"{Definition.Group} · 需要管理员" + (HasSnapshot ? " · 有恢复快照" : " · 尚无快照");
    public Brush StateBackground => new SolidColorBrush(Info.Available && Info.StartMode == System.ServiceProcess.ServiceStartMode.Disabled ? Color.FromRgb(74, 54, 24) : Color.FromRgb(38, 50, 73));
    public Brush StateForeground => new SolidColorBrush(Info.Available && Info.StartMode == System.ServiceProcess.ServiceStartMode.Disabled ? Color.FromRgb(251, 191, 36) : Color.FromRgb(148, 163, 184));
    public ServiceItem(ServiceDefinition definition) => Definition = definition;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private void ChangedStatus()
    {
        Changed(nameof(StartModeText)); Changed(nameof(StatusText)); Changed(nameof(StateText));
        Changed(nameof(DetailText)); Changed(nameof(StateBackground)); Changed(nameof(StateForeground));
    }
}

public sealed class TweakItem : INotifyPropertyChanged
{
    private TweakState _state = TweakState.Unknown;
    public TweakDefinition Definition { get; }
    public string SearchText => $"{Definition.Category} {Definition.Title} {Definition.Description}";
    public string RiskText => Definition.Risk switch { RiskLevel.Low => "低风险", RiskLevel.Medium => "中风险", _ => "高风险" };
    public Brush RiskBackground => new SolidColorBrush(Definition.Risk == RiskLevel.Low ? Color.FromRgb(22, 60, 53) : Color.FromRgb(74, 54, 24));
    public Brush RiskForeground => new SolidColorBrush(Definition.Risk == RiskLevel.Low ? Color.FromRgb(101, 214, 173) : Color.FromRgb(251, 191, 36));
    public string DetailText => $"{(Definition.RequiresAdmin ? "需要管理员 · " : "当前用户 · ")}{(Definition.RequiresExplorerRestart ? "需重启资源管理器" : "即时生效")}";
    public TweakState State { get => _state; set { _state = value; Changed(); Changed(nameof(StateText)); Changed(nameof(StateBackground)); Changed(nameof(StateForeground)); } }
    public string StateText => State switch { TweakState.Enabled => "已启用", TweakState.Disabled => "未启用", TweakState.Mixed => "部分生效", _ => "未知" };
    public Brush StateBackground => new SolidColorBrush(State == TweakState.Enabled ? Color.FromRgb(20, 66, 56) : Color.FromRgb(38, 50, 73));
    public Brush StateForeground => new SolidColorBrush(State == TweakState.Enabled ? Color.FromRgb(101, 214, 173) : Color.FromRgb(148, 163, 184));
    public TweakItem(TweakDefinition definition) => Definition = definition;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
