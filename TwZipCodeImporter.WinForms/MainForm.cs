using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using TwZipCodeImporter;

namespace TwZipCodeImporter.WinForms;

public class MainForm : Form
{
    // ── 控制項 ────────────────────────────────────────────
    private readonly TextBox _txtConnStr;
    private readonly TextBox _txtExcelDir;
    private readonly Button  _btnBrowse;
    private readonly Button  _btnImport;
    private readonly Button  _btnSaveWarnings;
    private readonly ProgressBar _progress;
    private readonly Label   _lblStatus;
    private readonly RichTextBox _log;

    // ── 狀態 ─────────────────────────────────────────────
    private List<string>? _lastWarnings;
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text            = "3+3 郵遞區號匯入工具";
        Size            = new Size(760, 620);
        MinimumSize     = new Size(640, 500);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Microsoft JhengHei UI", 9.5f);

        // ── 讀取 appsettings ───────────────────────────
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var defaultConn = config.GetConnectionString("DefaultConnection") ?? "";
        var defaultDir  = config["ExcelDirectory"] ?? "";

        // ── 版面配置 ───────────────────────────────────
        var panel = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            ColumnCount = 3,
            RowCount    = 2,
            AutoSize    = true,
            Padding     = new Padding(8, 8, 8, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        panel.Controls.Add(new Label { Text = "連線字串:", Anchor = AnchorStyles.Left | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _txtConnStr = new TextBox { Text = defaultConn, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 2) };
        panel.Controls.Add(_txtConnStr, 1, 0);
        panel.SetColumnSpan(_txtConnStr, 2);

        panel.Controls.Add(new Label { Text = "Excel 目錄:", Anchor = AnchorStyles.Left | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        _txtExcelDir = new TextBox { Text = defaultDir, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 2) };
        panel.Controls.Add(_txtExcelDir, 1, 1);
        _btnBrowse = new Button { Text = "瀏覽...", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        _btnBrowse.Click += BrowseClick;
        panel.Controls.Add(_btnBrowse, 2, 1);

        // ── 按鈕列 ────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock        = DockStyle.Top,
            AutoSize    = true,
            Padding     = new Padding(8, 0, 8, 6),
            FlowDirection = FlowDirection.LeftToRight
        };

        _btnImport = new Button
        {
            Text      = "▶ 開始匯入",
            Width     = 120,
            Height    = 32,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin    = new Padding(0, 0, 8, 0)
        };
        _btnImport.FlatAppearance.BorderSize = 0;
        _btnImport.Click += ImportClick;
        btnPanel.Controls.Add(_btnImport);

        _btnSaveWarnings = new Button
        {
            Text      = "💾 儲存警告 Log",
            Width     = 130,
            Height    = 32,
            Enabled   = false,
            Margin    = new Padding(0, 0, 8, 0)
        };
        _btnSaveWarnings.Click += SaveWarningsClick;
        btnPanel.Controls.Add(_btnSaveWarnings);

        // ── 進度條 ────────────────────────────────────
        _progress = new ProgressBar
        {
            Dock    = DockStyle.Top,
            Height  = 6,
            Style   = ProgressBarStyle.Marquee,
            Visible = false
        };

        // ── 狀態列 ────────────────────────────────────
        _lblStatus = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
            Text      = "就緒"
        };

        // ── Log 區 ────────────────────────────────────
        _log = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font      = new Font("Consolas", 9.5f),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        Controls.Add(_log);
        Controls.Add(_lblStatus);
        Controls.Add(_progress);
        Controls.Add(btnPanel);
        Controls.Add(panel);
    }

    // ── 瀏覽目錄 ─────────────────────────────────────────
    private void BrowseClick(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description  = "選擇 Excel 目錄",
            SelectedPath = _txtExcelDir.Text
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtExcelDir.Text = dlg.SelectedPath;
    }

    // ── 開始匯入 ─────────────────────────────────────────
    private async void ImportClick(object? s, EventArgs e)
    {
        if (_btnImport.Text.StartsWith("⏹"))
        {
            _cts?.Cancel();
            return;
        }

        _log.Clear();
        _lastWarnings = null;
        _btnSaveWarnings.Enabled = false;
        SetBusy(true);

        var connStr   = _txtConnStr.Text.Trim();
        var excelDir  = _txtExcelDir.Text.Trim();
        var warnings  = new List<string>();
        var sw        = Stopwatch.StartNew();

        _cts = new CancellationTokenSource();

        try
        {
            AppendLog($"Excel 目錄: {excelDir}", Color.Cyan);
            AppendLog("讀取 Excel...");

            // 在背景執行緒讀取 + 匯入，避免凍結 UI
            var rows = await Task.Run(() =>
                ExcelReader.ReadAll(excelDir, warnings)
                           .Select(x => x.Row)
                           .ToList(), _cts.Token);

            AppendLog($"讀取完成: {rows.Count:N0} 筆");

            if (warnings.Count > 0)
                AppendLog($"解析警告: {warnings.Count:N0} 筆（可儲存 Log 查看）", Color.Gold);

            AppendLog("寫入資料庫...");
            SetStatus($"匯入中... {rows.Count:N0} 筆");

            var importer = new ZoneImporter(connStr);
            var (inserted, updated) = await importer.ImportAsync(rows);

            sw.Stop();

            AppendLog("", Color.White);
            AppendLog($"✔ MERGE 完成 — Insert: {inserted:N0}  Update: {updated:N0}", Color.LimeGreen);
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

    // ── 儲存警告 Log ──────────────────────────────────────
    private async void SaveWarningsClick(object? s, EventArgs e)
    {
        if (_lastWarnings == null) return;
        using var dlg = new SaveFileDialog
        {
            Title      = "儲存警告 Log",
            FileName   = "import_warnings.log",
            Filter     = "Log 檔 (*.log)|*.log|文字檔 (*.txt)|*.txt"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            await File.WriteAllLinesAsync(dlg.FileName, _lastWarnings, Encoding.UTF8);
            AppendLog($"警告 Log 已儲存: {dlg.FileName}", Color.Cyan);
        }
    }

    // ── 輔助 ─────────────────────────────────────────────
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
        _btnImport.Text      = busy ? "⏹ 取消" : "▶ 開始匯入";
        _btnBrowse.Enabled   = !busy;
        _txtExcelDir.Enabled = !busy;
        _txtConnStr.Enabled  = !busy;
        _progress.Visible    = busy;
    }
}
