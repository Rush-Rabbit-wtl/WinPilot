# WinPilot 便携配置

把 `publish` 文件夹整体复制到另一台电脑，即可同时带走程序、`config/software.json` 和 `config/profile.json`。下载的安装包仍保存在目标电脑的 `%USERPROFILE%\Downloads\WinPilot`。

## 软件目录

`software.json` 是一个 JSON 数组。局域网 HTTP 示例：

```json
{
  "id": "7zip",
  "name": "7-Zip",
  "description": "内网软件仓库中的 7-Zip 安装包",
  "source": "http://192.168.1.20/software/7z.exe",
  "fileName": "7z.exe",
  "sha256": "填写 64 位 SHA-256，可留空",
  "selectedByDefault": true,
  "isBuiltIn": false,
  "installer": {
    "kind": "exe",
    "arguments": ["/S"],
    "requiresAdmin": true
  }
}
```

UNC 共享只需替换来源，JSON 中的反斜杠要写两次：

```json
"source": "\\\\NAS01\\Software\\7zip.exe"
```

MSI 示例：

```json
"installer": {
  "kind": "msi",
  "arguments": ["/qn", "/norestart"],
  "requiresAdmin": true
}
```

注意：静默参数由各软件厂商定义，不能通用。建议从厂商文档确认参数，并为内网固定安装包配置 SHA-256。互联网来源必须使用 HTTPS；HTTP 只接受私有 IP、单标签主机名或 `.local`、`.lan`、`.home`、`.internal` 主机。

## 开荒方案

`profile.json` 引用程序目录中的 ID：

```json
{
  "schemaVersion": 1,
  "name": "办公室电脑开荒",
  "enableTweaks": ["show-extensions", "show-hidden", "disable-ad-id"],
  "disableServices": ["Fax", "RetailDemo"],
  "software": ["7zip", "vscode", "chrome"]
}
```

“捕获本机状态”会保存当前已启用的 WinPilot 设置与已禁用的受管服务，并保留方案原有的软件清单。它不会猜测系统中安装过哪些软件。

推荐流程：先在“开荒方案”页预览，应用系统配置，再下载方案软件；最后到“软件下载”页逐个确认安装。每项系统变更仍会创建回滚快照。
