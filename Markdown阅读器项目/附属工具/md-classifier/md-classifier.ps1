Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

function New-Label([string]$Text, [int]$X, [int]$Y, [int]$Width = 100) {
    $control = [System.Windows.Forms.Label]::new()
    $control.Text = $Text
    $control.Location = [System.Drawing.Point]::new($X, $Y)
    $control.Size = [System.Drawing.Size]::new($Width, 24)
    $control.TextAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    return $control
}

function Get-UniqueDestination([string]$Folder, [string]$Name) {
    $candidate = Join-Path $Folder $Name
    if (-not (Test-Path -LiteralPath $candidate)) { return $candidate }

    $base = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $extension = [System.IO.Path]::GetExtension($Name)
    $index = 2
    do {
        $candidate = Join-Path $Folder ("{0} ({1}){2}" -f $base, $index, $extension)
        $index += 1
    } while (Test-Path -LiteralPath $candidate)
    return $candidate
}

$form = [System.Windows.Forms.Form]::new()
$form.Text = 'Markdown 文件分类器'
$form.StartPosition = 'CenterScreen'
$form.MinimumSize = [System.Drawing.Size]::new(760, 650)
$form.Size = [System.Drawing.Size]::new(900, 720)
$form.Font = [System.Drawing.Font]::new('Microsoft YaHei UI', 9)

$form.Controls.Add((New-Label '来源文件夹（仅扫描此列表中的目录）' 18 16 310))
$sourceList = [System.Windows.Forms.ListBox]::new()
$sourceList.Location = [System.Drawing.Point]::new(18, 42)
$sourceList.Size = [System.Drawing.Size]::new(720, 125)
$sourceList.Anchor = 'Top,Left,Right'
$form.Controls.Add($sourceList)

$addSource = [System.Windows.Forms.Button]::new()
$addSource.Text = '添加文件夹'
$addSource.Location = [System.Drawing.Point]::new(750, 42)
$addSource.Size = [System.Drawing.Size]::new(120, 32)
$addSource.Anchor = 'Top,Right'
$form.Controls.Add($addSource)

$removeSource = [System.Windows.Forms.Button]::new()
$removeSource.Text = '移除选中项'
$removeSource.Location = [System.Drawing.Point]::new(750, 82)
$removeSource.Size = [System.Drawing.Size]::new(120, 32)
$removeSource.Anchor = 'Top,Right'
$form.Controls.Add($removeSource)

$form.Controls.Add((New-Label '目标文件夹' 18 185 100))
$targetBox = [System.Windows.Forms.TextBox]::new()
$targetBox.Location = [System.Drawing.Point]::new(118, 185)
$targetBox.Size = [System.Drawing.Size]::new(620, 27)
$targetBox.Anchor = 'Top,Left,Right'
$form.Controls.Add($targetBox)

$targetButton = [System.Windows.Forms.Button]::new()
$targetButton.Text = '选择目标'
$targetButton.Location = [System.Drawing.Point]::new(750, 181)
$targetButton.Size = [System.Drawing.Size]::new(120, 32)
$targetButton.Anchor = 'Top,Right'
$form.Controls.Add($targetButton)

$form.Controls.Add((New-Label '筛选时间' 18 230 90))
$timeKind = [System.Windows.Forms.ComboBox]::new()
$timeKind.DropDownStyle = 'DropDownList'
$timeKind.Items.AddRange(@('修改时间', '创建时间'))
$timeKind.SelectedIndex = 0
$timeKind.Location = [System.Drawing.Point]::new(118, 230)
$timeKind.Size = [System.Drawing.Size]::new(110, 27)
$form.Controls.Add($timeKind)

$startDate = [System.Windows.Forms.DateTimePicker]::new()
$startDate.Format = 'Short'
$startDate.ShowCheckBox = $true
$startDate.Checked = $false
$startDate.Location = [System.Drawing.Point]::new(248, 230)
$startDate.Size = [System.Drawing.Size]::new(145, 27)
$form.Controls.Add($startDate)
$form.Controls.Add((New-Label '至' 402 230 26))
$endDate = [System.Windows.Forms.DateTimePicker]::new()
$endDate.Format = 'Short'
$endDate.ShowCheckBox = $true
$endDate.Checked = $false
$endDate.Location = [System.Drawing.Point]::new(428, 230)
$endDate.Size = [System.Drawing.Size]::new(145, 27)
$form.Controls.Add($endDate)

$recursive = [System.Windows.Forms.CheckBox]::new()
$recursive.Text = '包含子文件夹'
$recursive.Checked = $true
$recursive.Location = [System.Drawing.Point]::new(18, 277)
$recursive.Size = [System.Drawing.Size]::new(125, 27)
$form.Controls.Add($recursive)

$form.Controls.Add((New-Label '归类方式' 165 277 70))
$groupKind = [System.Windows.Forms.ComboBox]::new()
$groupKind.DropDownStyle = 'DropDownList'
$groupKind.Items.AddRange(@('按年月（YYYY-MM）', '按日期（YYYY-MM-DD）', '不创建分类目录'))
$groupKind.SelectedIndex = 0
$groupKind.Location = [System.Drawing.Point]::new(235, 277)
$groupKind.Size = [System.Drawing.Size]::new(180, 27)
$form.Controls.Add($groupKind)

$runButton = [System.Windows.Forms.Button]::new()
$runButton.Text = '开始复制并分类'
$runButton.Location = [System.Drawing.Point]::new(18, 324)
$runButton.Size = [System.Drawing.Size]::new(150, 36)
$runButton.BackColor = [System.Drawing.Color]::FromArgb(30, 111, 235)
$runButton.ForeColor = [System.Drawing.Color]::White
$runButton.FlatStyle = 'Flat'
$form.Controls.Add($runButton)

$status = New-Label '就绪。默认仅复制文件，不会修改或删除来源文件。' 184 330 670
$status.Anchor = 'Top,Left,Right'
$form.Controls.Add($status)

$log = [System.Windows.Forms.TextBox]::new()
$log.Multiline = $true
$log.ReadOnly = $true
$log.ScrollBars = 'Vertical'
$log.Location = [System.Drawing.Point]::new(18, 378)
$log.Size = [System.Drawing.Size]::new(852, 285)
$log.Anchor = 'Top,Bottom,Left,Right'
$log.Font = [System.Drawing.Font]::new('Consolas', 9)
$form.Controls.Add($log)

$addSource.Add_Click({
    $dialog = [System.Windows.Forms.FolderBrowserDialog]::new()
    $dialog.Description = '选择要扫描 Markdown 文件的来源目录'
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK -and -not $sourceList.Items.Contains($dialog.SelectedPath)) {
        [void]$sourceList.Items.Add($dialog.SelectedPath)
    }
})

$removeSource.Add_Click({
    if ($sourceList.SelectedIndex -ge 0) { $sourceList.Items.RemoveAt($sourceList.SelectedIndex) }
})

$targetButton.Add_Click({
    $dialog = [System.Windows.Forms.FolderBrowserDialog]::new()
    $dialog.Description = '选择分类后的 Markdown 文件保存位置'
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $targetBox.Text = $dialog.SelectedPath }
})

$runButton.Add_Click({
    if ($sourceList.Items.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show('请至少添加一个来源文件夹。', '缺少来源目录') | Out-Null
        return
    }
    if ([string]::IsNullOrWhiteSpace($targetBox.Text)) {
        [System.Windows.Forms.MessageBox]::Show('请选择目标文件夹。', '缺少目标目录') | Out-Null
        return
    }

    $target = $targetBox.Text.Trim()
    $targetFull = [System.IO.Path]::GetFullPath($target).TrimEnd('\') + '\'
    foreach ($source in $sourceList.Items) {
        $sourceFull = [System.IO.Path]::GetFullPath([string]$source).TrimEnd('\') + '\'
        if ($targetFull.StartsWith($sourceFull, [System.StringComparison]::OrdinalIgnoreCase)) {
            [System.Windows.Forms.MessageBox]::Show('目标文件夹不能位于来源文件夹内，否则会重复扫描刚复制出的文件。', '目录范围冲突') | Out-Null
            return
        }
    }
    if (-not (Test-Path -LiteralPath $target)) { [void](New-Item -ItemType Directory -Path $target -Force) }
    $start = if ($startDate.Checked) { $startDate.Value.Date } else { $null }
    $end = if ($endDate.Checked) { $endDate.Value.Date.AddDays(1).AddTicks(-1) } else { $null }
    if ($start -and $end -and $start -gt $end) {
        [System.Windows.Forms.MessageBox]::Show('开始日期不能晚于结束日期。', '日期范围无效') | Out-Null
        return
    }

    $log.Clear()
    $runButton.Enabled = $false
    $copied = 0; $skipped = 0; $failed = 0
    $status.Text = '正在扫描和复制，请稍候...'
    $form.Refresh()

    foreach ($source in $sourceList.Items) {
        $params = @{ LiteralPath = [string]$source; Filter = '*.md'; File = $true; ErrorAction = 'SilentlyContinue' }
        if ($recursive.Checked) { $params.Recurse = $true }
        Get-ChildItem @params | ForEach-Object {
            try {
                $time = if ($timeKind.SelectedIndex -eq 0) { $_.LastWriteTime } else { $_.CreationTime }
                if (($start -and $time -lt $start) -or ($end -and $time -gt $end)) { $skipped += 1; return }
                $folder = switch ($groupKind.SelectedIndex) {
                    0 { Join-Path $target $time.ToString('yyyy-MM') }
                    1 { Join-Path $target $time.ToString('yyyy-MM-dd') }
                    default { $target }
                }
                if (-not (Test-Path -LiteralPath $folder)) { [void](New-Item -ItemType Directory -Path $folder -Force) }
                $destination = Get-UniqueDestination $folder $_.Name
                Copy-Item -LiteralPath $_.FullName -Destination $destination -ErrorAction Stop
                $copied += 1
            } catch {
                $failed += 1
                $log.AppendText("失败: $($_.FullName) - $($_.Exception.Message)`r`n")
            }
        }
    }
    $log.AppendText("完成。复制: $copied；因时间范围跳过: $skipped；失败: $failed。`r`n")
    $status.Text = "完成：复制 $copied 个文件。"
    $runButton.Enabled = $true
})

[void]$form.ShowDialog()
