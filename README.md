# WinPilot

WinPilot 是一个面向 Windows 11 的安全、可回滚系统设置工具。项目采用 **.NET 8 + WPF**，无需 Python 环境。

## 当前功能

- 16 项资源管理器、任务栏、隐私、性能、Edge 和更新设置；
- 11 项可选 Windows 服务状态检测、禁用与精确恢复；
- 便携式软件目录，支持 HTTPS、局域网 HTTP 和 UNC 共享文件；
- 可配置 EXE/MSI 安装方式、参数、管理员权限与 SHA-256；
- 可迁移的开荒方案：系统设置、可选服务与软件清单集中在 JSON；
- 可捕获当前电脑的设置状态，复制发布目录即可带到另一台电脑；
- 分类导航与实时搜索；
- 自动检测当前状态；
- 低/中风险分级和操作前影响预览；
- 每次写入前保存 JSON 快照；
- 多项写入失败时自动恢复本次快照；
- 最近变更一键回滚；
- 系统范围设置按需申请管理员权限；
- 需要时手动重启资源管理器。

WinPilot 首版不会禁用 Defender、SmartScreen、UAC，不删除受保护 AppX，也不修改 BCD、驱动或系统文件 ACL。

服务管理只收录诊断、离线地图、传真、零售演示、Xbox、搜索、SysMain 和打印等可选服务。RPC、事件日志、网络核心组件、WMI 等关键服务不进入目录。服务禁用前会记录启动类型、延迟启动标志和运行状态；“按快照恢复”不会猜测系统默认值。

软件下载文件保存在 `%USERPROFILE%\Downloads\WinPilot`。下载器接受互联网 HTTPS、私有 IP/局域网主机的 HTTP，以及 `\\服务器\共享\文件`，限制单文件最大 2 GB，使用 `.part` 临时文件并在完成后计算 SHA-256。配置了预期哈希时，不匹配的文件会被删除。下载与安装分开确认，程序不会在无提示的情况下运行安装包。

便携配置位于发布目录的 `config\`：

- `software.json`：软件名称、来源、文件名、哈希与安装参数；
- `profile.json`：要启用的系统设置、要禁用的服务、要下载的软件 ID。

详细格式和局域网示例见 [配置说明](docs/CONFIGURATION.md)。

## 运行

需要 Windows 10/11 和 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
dotnet run --project .\WinPilot.csproj
```

需要修改 `HKEY_LOCAL_MACHINE` 的项目时，在界面中点击“管理员运行”。普通用户设置不要求提升权限。

## 构建

```powershell
.\build.ps1
```

生成物位于 `publish\`。如果希望生成包含 .NET 运行时的单文件版本：

```powershell
.\build.ps1 -SelfContained
```

## 测试

```powershell
dotnet test .\WinPilot.sln
```

## 快照

快照默认保存在：

```text
%LOCALAPPDATA%\WinPilot\Snapshots
```

快照保留注册表值是否存在、类型与原始内容。点击“恢复”表示写入该功能的保守默认值；点击“回滚最近更改”则精确恢复操作前的原始值。

## 项目结构

```text
WinPilot/
├─ config/                 可迁移的软件目录与开荒方案
├─ Models/                 设置定义与快照模型
├─ Services/               设置目录、注册表事务和快照存储
├─ tests/WinPilot.Tests/   自动化测试
├─ MainWindow.xaml         WPF 主界面
├─ MainWindow.xaml.cs      交互与状态刷新
├─ build.ps1               测试与发布脚本
└─ docs/REFERENCE_ANALYSIS.md
```

## 安全说明

系统调优没有适用于所有电脑的统一答案。企业策略、OEM 镜像和 Windows 版本可能覆盖注册表设置。建议逐项修改并观察一段时间；遇到问题先使用快照回滚。
