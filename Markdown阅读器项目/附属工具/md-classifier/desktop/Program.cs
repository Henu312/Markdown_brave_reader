using System.Drawing;
using System.Windows.Forms;

namespace MdClassifier;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly ListBox _sources = new() { Dock = DockStyle.Fill };
    private readonly TextBox _target = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _timeKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly DateTimePicker _start = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 135 };
    private readonly DateTimePicker _end = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 135 };
    private readonly ComboBox _groupKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 185 };
    private readonly CheckBox _recursive = new() { Text = "包含子文件夹", Checked = true, AutoSize = true };
    private readonly Button _run = new() { Text = "开始复制并分类", AutoSize = true };
    private readonly Label _status = new() { Text = "就绪。默认仅复制文件，不会修改或删除来源文件。", AutoSize = true };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };

    public MainForm()
    {
        Text = "Markdown 文件分类器";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 650);
        Size = new Size(900, 720);
        Font = new Font("Microsoft YaHei UI", 9);

        _timeKind.Items.AddRange(new object[] { "修改时间", "创建时间" });
        _timeKind.SelectedIndex = 0;
        _groupKind.Items.AddRange(new object[] { "按年月（YYYY-MM）", "按日期（YYYY-MM-DD）", "不创建分类目录" });
        _groupKind.SelectedIndex = 0;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "来源文件夹（仅扫描此列表中的目录）", AutoSize = true }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 3);
        layout.Controls.Add(_sources, 0, 1);
        layout.SetColumnSpan(_sources, 2);
        var add = new Button { Text = "添加文件夹", Width = 110, Height = 30 };
        var remove = new Button { Text = "移除选中项", Width = 110, Height = 30 };
        var sourceButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        sourceButtons.Controls.Add(add); sourceButtons.Controls.Add(remove);
        layout.Controls.Add(sourceButtons, 2, 1);

        layout.Controls.Add(new Label { Text = "目标文件夹", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.SetColumnSpan(_target, 2);
        layout.Controls.Add(_target, 1, 2);
        var targetButton = new Button { Text = "选择目标", Width = 110 };
        layout.Controls.Add(targetButton, 2, 2);

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        options.Controls.Add(new Label { Text = "筛选时间", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
        options.Controls.Add(_timeKind);
        options.Controls.Add(_start);
        options.Controls.Add(new Label { Text = "至", AutoSize = true, Margin = new Padding(4, 7, 4, 0) });
        options.Controls.Add(_end);
        options.Controls.Add(_recursive);
        options.Controls.Add(new Label { Text = "归类方式", AutoSize = true, Margin = new Padding(8, 7, 4, 0) });
        options.Controls.Add(_groupKind);
        layout.Controls.Add(options, 0, 3);
        layout.SetColumnSpan(options, 3);

        var runPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _run.BackColor = Color.FromArgb(30, 111, 235); _run.ForeColor = Color.White; _run.FlatStyle = FlatStyle.Flat;
        runPanel.Controls.Add(_run); runPanel.Controls.Add(_status);
        layout.Controls.Add(runPanel, 0, 4);
        layout.SetColumnSpan(runPanel, 3);
        layout.Controls.Add(_log, 0, 5);
        layout.SetColumnSpan(_log, 3);

        add.Click += (_, _) => AddSource();
        remove.Click += (_, _) => { if (_sources.SelectedIndex >= 0) _sources.Items.RemoveAt(_sources.SelectedIndex); };
        targetButton.Click += (_, _) => ChooseTarget();
        _run.Click += async (_, _) => await ClassifyAsync();
    }

    private void AddSource()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择要扫描 Markdown 文件的来源目录" };
        if (dialog.ShowDialog(this) == DialogResult.OK && !_sources.Items.Contains(dialog.SelectedPath)) _sources.Items.Add(dialog.SelectedPath);
    }

    private void ChooseTarget()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择分类后的 Markdown 文件保存位置" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _target.Text = dialog.SelectedPath;
    }

    private async Task ClassifyAsync()
    {
        var sources = _sources.Items.Cast<string>().ToArray();
        if (sources.Length == 0) { MessageBox.Show(this, "请至少添加一个来源文件夹。", "缺少来源目录"); return; }
        if (string.IsNullOrWhiteSpace(_target.Text)) { MessageBox.Show(this, "请选择目标文件夹。", "缺少目标目录"); return; }

        var target = Path.GetFullPath(_target.Text.Trim());
        if (sources.Any(source => IsChildPath(target, Path.GetFullPath(source))))
        {
            MessageBox.Show(this, "目标文件夹不能位于来源文件夹内，否则会重复扫描刚复制出的文件。", "目录范围冲突");
            return;
        }
        var start = _start.Checked ? _start.Value.Date : (DateTime?)null;
        var end = _end.Checked ? _end.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;
        if (start > end) { MessageBox.Show(this, "开始日期不能晚于结束日期。", "日期范围无效"); return; }

        var timeKind = _timeKind.SelectedIndex;
        var groupKind = _groupKind.SelectedIndex;
        var recursive = _recursive.Checked;
        _run.Enabled = false; _log.Clear(); _status.Text = "正在扫描和复制，请稍候...";
        var result = await Task.Run(() => CopyFiles(sources, target, start, end, timeKind, groupKind, recursive));
        foreach (var error in result.Errors) _log.AppendText($"失败: {error}\r\n");
        _log.AppendText($"完成。复制: {result.Copied}；因时间范围跳过: {result.Skipped}；失败: {result.Errors.Count}。\r\n");
        _status.Text = $"完成：复制 {result.Copied} 个文件。";
        _run.Enabled = true;
    }

    private static CopyResult CopyFiles(IEnumerable<string> sources, string target, DateTime? start, DateTime? end, int timeKind, int groupKind, bool recursive)
    {
        Directory.CreateDirectory(target);
        var result = new CopyResult();
        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true };
        foreach (var source in sources)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(source, "*.md", options))
                {
                    try
                    {
                        var info = new FileInfo(path);
                        var time = timeKind == 0 ? info.LastWriteTime : info.CreationTime;
                        if ((start.HasValue && time < start) || (end.HasValue && time > end)) { result.Skipped++; continue; }
                        var folder = groupKind switch { 0 => Path.Combine(target, time.ToString("yyyy-MM")), 1 => Path.Combine(target, time.ToString("yyyy-MM-dd")), _ => target };
                        Directory.CreateDirectory(folder);
                        File.Copy(path, UniquePath(folder, info.Name));
                        result.Copied++;
                    }
                    catch (Exception ex) { result.Errors.Add($"{path} - {ex.Message}"); }
                }
            }
            catch (Exception ex) { result.Errors.Add($"{source} - {ex.Message}"); }
        }
        return result;
    }

    private static bool IsChildPath(string child, string parent)
    {
        var prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || string.Equals(child, parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniquePath(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        if (!File.Exists(path)) return path;
        var baseName = Path.GetFileNameWithoutExtension(name); var extension = Path.GetExtension(name);
        for (var index = 2; ; index++) { path = Path.Combine(folder, $"{baseName} ({index}){extension}"); if (!File.Exists(path)) return path; }
    }

    private sealed class CopyResult { public int Copied; public int Skipped; public List<string> Errors { get; } = new(); }
}
