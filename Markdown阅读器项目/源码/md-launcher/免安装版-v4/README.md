# Markdown 默认打开启动器

## 使用扩展打开

1. 在 Brave 打开 `brave://extensions/`，找到“本地 Markdown 阅读器”。
2. 复制扩展 ID（32 位小写字母），写入 `extension-id.txt`，覆盖占位文字。
3. 确认扩展详情中的“允许访问文件网址”已开启。
4. 将任意 `.md` 文件拖到 `免安装版\MdLauncher.exe` 上，或把 `.md` 默认关联到它。

启动器会打开扩展自己的 `viewer.html`，Markdown 内容由扩展渲染。若没有配置有效 ID，则自动使用独立后备阅读页。

## 运行环境

`免安装版\MdLauncher.exe` 是自包含版本，不要求安装 .NET；程序只监听 `127.0.0.1`，不会上传文件。
