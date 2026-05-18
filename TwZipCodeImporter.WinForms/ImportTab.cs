using System.Diagnostics;
using System.Text;
using TwZipCodeImporter;
using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter.WinForms;

public class ImportTab : TabPage
{
    private readonly Func<DatabaseProvider> _getProvider;
    private readonly Func<string> _getConnStr;

    private readonly ListBox _lstFiles;
    private readonly Button  _btnAddFiles;
    private readonly Button  _btnAddDir;
    private readonly Button  _btnRemove;
    private readonly Button  _btnClear;
    private readonly Button  _btnImport;
    private readonly Button  _btnSaveWarnings;
    private readonly ProgressBar _progress;
    private readonly Label   _lblStatus;
    private readonly RichTextBox _log;

    private List<string>? _lastWarnings;
    private CancellationTokenSource? _cts;

    public ImportTab(Func<DatabaseProvider> getProvider, Func<string> getConnStr)
    {
        _getProvider = getProvider;
        _getConnStr  = getConnStr;

        Text = "匯入 Excel";
        Padding = new Padding(6);

        // 檔案清單 + 操作按鈕
        _lstFiles = new ListBox
        {
            Dock           = DockStyle.Fill,
            SelectionMode  = SelectionMode.MultiExtended,
            IntegralHeight = false,
        };

        var fileBtnPanel = new FlowLayoutPanel
        {
            Dock     = DockStyle.Top,
            AutoSize = true,
            Padding  = new Padding(0, 0, 0, 4),
        };

        _btnAddFiles = new Button { Text = "📄 選擇檔案...", Width = 130, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        _btnAddFiles.Click += AddFilesClick;
        fileBtnPanel.Controls.Add(_btnAddFiles);

        _btnAddDir = new Button { Text = "📁 加入目錄...", Width = 130, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        _btnAddDir.Click += AddDirClick;
        fileBtnPanel.Controls.Add(_btnAddDir);

        _btnRemove = new Button { Text = "移除選取", Width = 100, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        _btnRemove.Click += (s, e) =>
        {
            var sel = _lstFiles.SelectedItems.Cast<string>().ToList();
            foreach (var item in sel) _lstFiles.Items.Remove(item);
        };
        fileBtnPanel.Controls.Add(_btnRemove);

        _btnClear = new Button { Text = "清空", Width = 80, Height = 28, Margin = new Padding(0, 0, 6, 0) };
        _btnClear.Click += (s, e) => _lstFiles.Items.Clear();
        fileBtnPanel.Controls.Add(_btnClear);

        var filesGroup = new GroupBox
        {
            Text = "Excel 檔案 (可多選,或加入整個目錄)",
            Dock = DockStyle.Top,
            Height = 220,
            Padding = new Padding(6),
        };
        filesGroup.Controls.Add(_lstFiles);
        filesGroup.Controls.Add(fileBtnPanel);

        // 匯入按鈕列
        var actionPanel = new FlowLayoutPanel
        {
            Dock     = DockStyle.Top,
            AutoSize = true,
            Padding  = new Padding(0, 4, 0, 4),
        };

        _btnImport = new Button
        {
            Text      = "▶ 開始匯入",
            Width     = 140,
            Height    = 32,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin    = new Padding(0, 0, 8, 0),
        };
        _btnImport.FlatAppearance.BorderSize = 0;
        _btnImport.Click += ImportClick;
        actionPanel.Controls.Add(_btnImport);

        _btnSaveWarnings = new Button
        {
            Text    = "💾 儲存警告 Log",
            Width   = 140,
            Height  = 32,
            Enabled = false,
            Margin  = new Padding(0, 0, 8, 0),
        };
        _btnSaveWarnings.Click += SaveWarningsClick;
        actionPanel.Controls.Add(_btnSaveWarnings);

        _progress = new ProgressBar
        {
            Dock    = DockStyle.Top,
            Height  = 6,
            Style   = ProgressBarStyle.Marquee,
            Visible = false,
        };

        _lblStatus = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Text      = "就緒",
        };

        _log = new RichTextBox
        {
            Dock       = DockStyle.Fill,
            ReadOnly   = true,
            BackColor  = Color.FromArgb(30, 30, 30),
            ForeColor  = Color.FromArgb(220, 220, 220),
            Font       = new Font("Consolas", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };

        Controls.Add(_log);
        Controls.Add(_lblStatus);
        Controls.Add(_progress);
        Controls.Add(actionPanel);
        Controls.Add(filesGroup);
    }

    public void AddInitialDirectory(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        AddDirectoryFiles(dir);
    }

    private void AddFilesClick(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title       = "選擇 Excel 檔",
            Filter      = "Excel 檔 (*.xls;*.xlsx)|*.xls;*.xlsx|所有檔案 (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        foreach (var f in dlg.FileNames) AddFile(f);
    }

    private void AddDirClick(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "選擇含 Excel 的目錄" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        AddDirectoryFiles(dlg.SelectedPath);
    }

    private void AddDirectoryFiles(string dir)
    {
        var files = Directory.GetFiles(dir, "*.xls")
            .Concat(Directory.GetFiles(dir, "*.xlsx"))
            .OrderBy(f => f);
        foreach (var f in files) AddFile(f);
    }

    private void AddFile(string path)
    {
        if (!_lstFiles.Items.Contains(path))
            _lstFiles.Items.Add(path);
    }

    private async void ImportClick(object? s, EventArgs e)
    {
        if (_btnImport.Text.StartsWith("⏹"))
        {
            _cts?.Cancel();
            return;
        }

        if (_lstFiles.Items.Count == 0)
        {
            MessageBox.Show("請先加入要匯入的 Excel 檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var connStr = _getConnStr().Trim();
        var provider = _getProvider();

        if (string.IsNullOrWhiteSpace(connStr))
        {
            MessageBox.Show("請設定資料庫連線。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _log.Clear();
        _lastWarnings = null;
        _btnSaveWarnings.Enabled = false;
        SetBusy(true);

        var files = _lstFiles.Items.Cast<string>().ToList();
        var warnings = new List<string>();
        var sw = Stopwatch.StartNew();

        _cts = new CancellationTokenSource();

        try
        {
            AppendLog($"DB 提供者: {provider}", Color.Cyan);
            AppendLog($"連線:     {Mask(connStr)}", Color.Cyan);
            AppendLog($"檔案數:   {files.Count}");
            foreach (var f in files) AppendLog($"  - {Path.GetFileName(f)}");
            AppendLog("");

            var repo = RepositoryFactory.Create(provider, connStr);

            AppendLog("確認資料庫結構...");
            await repo.EnsureSchemaAsync();

            AppendLog("讀取 Excel...");
            var rows = await Task.Run(() =>
                ExcelReader.ReadFiles(files, warnings).Select(x => x.Row).ToList(),
                _cts.Token);

            AppendLog($"讀取完成: {rows.Count:N0} 筆");
            if (warnings.Count > 0)
                AppendLog($"解析警告: {warnings.Count:N0} 筆 (可儲存 Log 查看)", Color.Gold);

            AppendLog("");
            AppendLog("寫入資料庫...");
            SetStatus($"匯入中... {rows.Count:N0} 筆");

            var progress = new Progress<string>(msg => AppendLog(msg));
            var (inserted, updated) = await repo.UpsertAsync(rows, progress, _cts.Token);

            sw.Stop();

            AppendLog("", Color.White);
            AppendLog($"✔ 完成 — Insert: {inserted:N0}  Update: {updated:N0}", Color.LimeGreen);
            AppendLog($"  警告筆數: {warnings.Count:N0}　總耗時: {sw.Elapsed.TotalSeconds:F1} 秒", Color.LimeGreen);

            SetStatus($"完成 ✔  Insert {inserted:N0} / Update {updated:N0} / 警告 {warnings.Count:N0} / {sw.Elapsed.TotalSeconds:F1} 秒");

            if (warnings.Count > 0)
            {
                _lastWarnings = warnings;
                _btnSaveWarnings.Enabled = true;
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("已取消。", Color.Orange);
            SetStatus("已取消");
        }
        catch (Exception ex)
        {
            AppendLog($"✘ 錯誤: {ex.Message}", Color.Tomato);
            SetStatus("執行失敗");
            MessageBox.Show(ex.Message, "匯入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveWarningsClick(object? s, EventArgs e)
    {
        if (_lastWarnings == null) return;
        using var dlg = new SaveFileDialog
        {
            Title    = "儲存警告 Log",
            FileName = "import_warnings.log",
            Filter   = "Log 檔 (*.log)|*.log|文字檔 (*.txt)|*.txt",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await File.WriteAllLinesAsync(dlg.FileName, _lastWarnings, Encoding.UTF8);
            AppendLog($"警告 Log 已儲存: {dlg.FileName}", Color.Cyan);
        }
    }

    private void AppendLog(string text, Color? color = null)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(text, color)); return; }
        _log.SelectionStart  = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor  = color ?? Color.FromArgb(220, 220, 220);
        _log.AppendText(text + "\n");
        _log.ScrollToCaret();
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _lblStatus.Text = text;
    }

    private void SetBusy(bool busy)
    {
        if (InvokeRequired) { Invoke(() => SetBusy(busy)); return; }
        _btnImport.Text     = busy ? "⏹ 取消" : "▶ 開始匯入";
        _btnAddFiles.Enabled = !busy;
        _btnAddDir.Enabled   = !busy;
        _btnRemove.Enabled   = !busy;
        _btnClear.Enabled    = !busy;
        _lstFiles.Enabled    = !busy;
        _progress.Visible    = busy;
    }

    private static string Mask(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"(Password|Pwd)\s*=\s*[^;]+", "$1=***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
