using Microsoft.Extensions.Configuration;
using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter.WinForms;

public class MainForm : Form
{
    private readonly DbConfigPanel _dbPanel;
    private readonly TabControl    _tabs;
    private readonly ImportTab     _importTab;
    private readonly LookupTab     _lookupTab;

    public MainForm()
    {
        Text          = "3+3 郵遞區號工具";
        Size          = new Size(900, 680);
        MinimumSize   = new Size(720, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Microsoft JhengHei UI", 9.5f);

        // ── 讀取 appsettings ──
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var providerStr = config["DatabaseProvider"];
        var defaultProvider = DatabaseProvider.Sqlite;
        if (RepositoryFactory.TryParse(providerStr, out var p)) defaultProvider = p;

        var sqliteConn = config.GetConnectionString("Sqlite") ?? Path.Combine(AppContext.BaseDirectory, "zipcode.db");
        var mssqlConn  = config.GetConnectionString("DefaultConnection") ?? "";
        var defaultDir = config["ExcelDirectory"] ?? "";

        // ── DB 設定列 ──
        _dbPanel = new DbConfigPanel();
        _dbPanel.SetProvider(defaultProvider);
        _dbPanel.SetConnectionString(defaultProvider == DatabaseProvider.Sqlite ? sqliteConn : mssqlConn);

        // Provider 切換時自動填預設連線字串
        _dbPanel.ProviderChanged += (s, e) =>
        {
            _dbPanel.SetConnectionString(_dbPanel.SelectedProvider == DatabaseProvider.Sqlite ? sqliteConn : mssqlConn);
        };

        // ── 分頁 ──
        _tabs = new TabControl { Dock = DockStyle.Fill };

        _importTab = new ImportTab(() => _dbPanel.SelectedProvider, () => _dbPanel.TxtConnStr.Text);
        _importTab.AddInitialDirectory(defaultDir);

        _lookupTab = new LookupTab(() => _dbPanel.SelectedProvider, () => _dbPanel.TxtConnStr.Text);

        _tabs.TabPages.Add(_importTab);
        _tabs.TabPages.Add(_lookupTab);

        Controls.Add(_tabs);
        Controls.Add(_dbPanel);
    }
}
