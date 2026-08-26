using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static class Program
{
    private const string MutexName = "Local\\MarkdownLauncher.SingleInstance";
    private const string PipeName = "MarkdownLauncher.OpenFile";

    [STAThread]
    private static async Task Main(string[] args)
    {
        if (args.Length == 0 || !TryGetMarkdownPath(args[0], out var markdownPath))
        {
            Message("请将 .md 文件关联到本程序后再打开。\n\n也可以把 Markdown 文件拖到 MdLauncher.exe 上测试。", "Markdown 启动器");
            return;
        }

        using var mutex = new Mutex(true, MutexName, out var isOwner);
        if (!isOwner)
        {
            await SendToExistingInstance(markdownPath);
            return;
        }

        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        try { listener.Start(); }
        catch (Exception ex) { Message($"无法启动本地预览服务：{ex.Message}", "Markdown 启动器"); return; }

        var documents = new ConcurrentDictionary<string, string>();
        _ = Task.Run(() => PipeLoop(documents, port));
        OpenDocument(markdownPath, documents, port);

        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < TimeSpan.FromHours(2))
        {
            var context = await listener.GetContextAsync();
            await Handle(context, documents);
        }
    }

    private static bool TryGetMarkdownPath(string value, out string path)
    {
        path = string.Empty;
        if (!File.Exists(value)) return false;
        var extension = Path.GetExtension(value);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)) return false;
        path = Path.GetFullPath(value);
        return true;
    }

    private static async Task SendToExistingInstance(string path)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(1500);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync(path);
        }
        catch { Message("已有 Markdown 启动器正在运行，但无法发送文件路径。请稍后重试。", "Markdown 启动器"); }
    }

    private static async Task PipeLoop(ConcurrentDictionary<string, string> documents, int port)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var path = await reader.ReadLineAsync();
                if (path is not null && TryGetMarkdownPath(path, out var markdownPath)) OpenDocument(markdownPath, documents, port);
            }
            catch { await Task.Delay(200); }
        }
    }

    private static void OpenDocument(string markdownPath, ConcurrentDictionary<string, string> documents, int port)
    {
        // 每次打开文件都重新读取配置，避免单实例启动器继续使用修改前的浏览器设置。
        var browserChoice = LoadBrowserChoice();
        var extensionId = LoadExtensionId(browserChoice);
        var token = Guid.NewGuid().ToString("N");
        documents[token] = markdownPath;
        var sourceUrl = $"http://127.0.0.1:{port}/content?doc={Uri.EscapeDataString(token)}";
        var assetBase = $"http://127.0.0.1:{port}/asset";
        var name = Uri.EscapeDataString(Path.GetFileName(markdownPath));
        var url = extensionId is null
            ? $"http://127.0.0.1:{port}/?doc={Uri.EscapeDataString(token)}&name={name}"
            : $"chrome-extension://{extensionId}/viewer.html?source={Uri.EscapeDataString(sourceUrl)}&assetBase={Uri.EscapeDataString(assetBase)}&name={name}";
        OpenBrowser(url, browserChoice);
    }

    private static async Task Handle(HttpListenerContext context, ConcurrentDictionary<string, string> documents)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? "/";
        var token = request.QueryString["doc"];
        var markdownPath = token is not null && documents.TryGetValue(token, out var selected) ? selected : null;
        try
        {
            if (path == "/" || path == "/index.html")
            {
                if (markdownPath is null) { context.Response.StatusCode = 404; return; }
                var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "viewer.html"));
                html = html.Replace("__TITLE__", WebUtility.HtmlEncode(Path.GetFileName(markdownPath)));
                await Write(context.Response, html, "text/html; charset=utf-8");
            }
            else if (path == "/content" && markdownPath is not null)
            {
                await Write(context.Response, await File.ReadAllTextAsync(markdownPath), "text/plain; charset=utf-8");
            }
            else if (path == "/asset" && markdownPath is not null)
            {
                var relative = request.QueryString["path"] ?? string.Empty;
                var baseDir = Path.GetDirectoryName(markdownPath)!;
                var full = Path.GetFullPath(Path.Combine(baseDir, relative));
                if (!IsInside(full, baseDir) || !File.Exists(full)) { context.Response.StatusCode = 404; return; }
                await Write(context.Response, await File.ReadAllBytesAsync(full), Mime(Path.GetExtension(full)));
            }
            else context.Response.StatusCode = 404;
        }
        catch { context.Response.StatusCode = 500; }
        finally { context.Response.Close(); }
    }

    private static async Task Write(HttpListenerResponse response, string value, string contentType) => await Write(response, Encoding.UTF8.GetBytes(value), contentType);

    private static async Task Write(HttpListenerResponse response, byte[] bytes, string contentType)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.ContentType = contentType;
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static bool IsInside(string path, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string Mime(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml", ".css" => "text/css", _ => "application/octet-stream"
    };

    private static int GetFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private static void OpenBrowser(string url, string? browserChoice)
    {
        var brave = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "Application", "brave.exe") };
        var chrome = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe") };
        var candidates = browserChoice switch { "chrome" => chrome, "brave" => brave, _ => brave.Concat(chrome).ToArray() };
        var browserPath = candidates.FirstOrDefault(File.Exists);
        Process.Start(new ProcessStartInfo { FileName = browserPath ?? url, Arguments = browserPath is null ? "" : url, UseShellExecute = true });
    }

    private static string? LoadBrowserChoice()
    {
        var config = Path.Combine(AppContext.BaseDirectory, "browser.txt");
        if (!File.Exists(config)) return null;
        var value = File.ReadAllText(config).Trim().ToLowerInvariant();
        return value is "chrome" or "brave" ? value : null;
    }

    private static string? LoadExtensionId(string? browserChoice)
    {
        var config = Path.Combine(AppContext.BaseDirectory, "extension-id.txt");
        if (!File.Exists(config)) return null;
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? fallback = null;
        foreach (var rawLine in File.ReadAllLines(config))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                var key = line[..separator].Trim().ToLowerInvariant();
                var value = line[(separator + 1)..].Trim();
                if (IsExtensionId(value)) entries[key] = value;
            }
            else if (fallback is null && IsExtensionId(line))
            {
                fallback = line;
            }
        }

        if (browserChoice is not null && entries.TryGetValue(browserChoice, out var selected)) return selected;
        if (entries.TryGetValue("auto", out var automatic)) return automatic;
        return fallback;
    }

    private static bool IsExtensionId(string value) => value.Length == 32 && value.All(c => c is >= 'a' and <= 'p');

    private static void Message(string text, string title) => System.Windows.Forms.MessageBox.Show(text, title, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
}
