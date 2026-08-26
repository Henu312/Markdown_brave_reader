# Markdown Brave Reader

一个面向 Windows 的本地 Markdown 阅读工具，使用 Chromium 浏览器扩展渲染 Markdown，并通过本地启动器解决 `.md` 文件默认打开、相对路径图片和单实例复用问题。

## 功能

- 支持 Brave 和 Google Chrome（Manifest V3）
- 双击或右键打开本地 `.md` / `.markdown` 文件
- Markdown 标题、列表、任务列表、引用、表格、代码块、链接和图片渲染
- 自动生成目录，支持目录跳转
- 支持 Base64 内嵌图片和 Markdown 相对路径图片
- 支持深色模式
- 编辑模式：左侧编辑、右侧实时预览
- 可隐藏预览，让编辑区扩展为全宽
- `Ctrl+S` 保存 Markdown 副本
- 启动器单实例，连续打开多个 Markdown 不会重复创建启动器进程

## 当前版本

产品版本：`V2.7.3`

- 浏览器扩展：位于 `Markdown阅读器项目/发布/当前版本/brave-markdown-reader`
- Windows 启动器：位于 `Markdown阅读器项目/发布/当前版本/md-launcher/MdLauncher.exe`
- 完整版本记录：`Markdown阅读器项目/版本变更记录.md`

## 环境要求

### 普通使用

- Windows 10 或更高版本（64 位）
- Brave 或 Google Chrome 等 Chromium 浏览器
- 浏览器允许加载未打包扩展，并开启扩展的“允许访问文件网址”权限
- 使用当前发布目录中的 `MdLauncher.exe` 时，不需要额外安装 .NET、Node.js 或 Python；启动器为 Windows 自包含版本

### 开发和构建

- Windows 10/11 64 位
- .NET 6 SDK（仅构建 Windows 启动器和分类器时需要）
- Git（用于获取源码和提交修改）
- Node.js（可选，仅用于执行 JavaScript 语法检查；扩展本身无需 Node.js）

启动器只在本机 `127.0.0.1` 提供临时预览服务，不需要网络服务器或数据库。Markdown 文件内容和相对路径图片不会上传到外部服务。

## 安装使用

### 1. 加载扩展

1. 打开 `brave://extensions/` 或 `chrome://extensions/`。
2. 开启“开发者模式”。
3. 点击“加载已解压的扩展程序”。
4. 选择 `Markdown阅读器项目/发布/当前版本/brave-markdown-reader`。
5. 在扩展详情中开启“允许访问文件网址”。

### 2. 配置启动器

编辑 `Markdown阅读器项目/发布/当前版本/md-launcher/browser.txt`，填写：

```text
brave
```

或：

```text
chrome
```

然后把对应浏览器中显示的扩展 ID 写入同目录的 `extension-id.txt`。两个浏览器的扩展 ID 可能不同，可以分别填写：

```text
brave=你的Brave扩展ID
chrome=你的Chrome扩展ID
```

最后将 `.md` 文件的默认打开方式设置为 `MdLauncher.exe`。

配置会在每次打开 Markdown 时重新读取，不需要重启单实例启动器。

## 项目结构

```text
Markdown阅读器项目/
├─ 源码/
│  ├─ brave-markdown-reader/  # 浏览器扩展源码
│  ├─ md-launcher/            # Windows 启动器源码
│  └─ icon-generator/         # 图标生成工具源码
├─ 发布/当前版本/             # 当前推荐版本
├─ 历史发布/                  # 历史版本资料
├─ 附属工具/md-classifier/    # Markdown 文件分类器
├─ 示例资料/                  # 测试 Markdown 文件
├─ 安装说明.md
├─ 项目说明.md
└─ 版本变更记录.md
```

## 构建启动器

需要安装 .NET 6 SDK。进入 `Markdown阅读器项目/源码/md-launcher` 后执行：

```powershell
dotnet build MdLauncher.csproj -c Release
dotnet publish MdLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

扩展本身无需构建，重新加载扩展目录即可生效。构建产物和发布二进制文件已通过 `.gitignore` 排除。

## 说明

`chrome-extension://` 是 Chrome、Brave 等 Chromium 浏览器共用的扩展地址协议，不代表固定使用 Chrome。实际启动浏览器由 `browser.txt` 决定。

## 许可证

当前仓库未单独声明开源许可证，代码仅供个人使用和维护。
