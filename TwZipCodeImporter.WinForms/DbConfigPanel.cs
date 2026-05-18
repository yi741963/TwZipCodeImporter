using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter.WinForms;

// 共用的 DB 設定列:Provider 下拉 + 連線字串/SQLite 檔案 + 瀏覽
public class DbConfigPanel : TableLayoutPanel
{
    public ComboBox CboProvider { get; }
    public TextBox  TxtConnStr  { get; }
    public Button   BtnBrowse   { get; }
    public Label    LblConnStr  { get; }

    public event EventHandler? ProviderChanged;

    public DbConfigPanel()
    {
        Dock        = DockStyle.Top;
        ColumnCount = 4;
        RowCount    = 1;
        AutoSize    = true;
        Padding     = new Padding(8, 8, 8, 4);

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        Controls.Add(new Label { Text = "資料庫:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);

        CboProvider = new ComboBox
        {
            Dock          = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin        = new Padding(0, 2, 4, 2),
        };
        CboProvider.Items.AddRange(new object[] { "SQLite (本機檔案)", "MS-SQL Server" });
        CboProvider.SelectedIndex = 0;
        CboProvider.SelectedIndexChanged += (s, e) =>
        {
            UpdateLabelAndDefault();
            ProviderChanged?.Invoke(this, EventArgs.Empty);
        };
        Controls.Add(CboProvider, 1, 0);

        TxtConnStr = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 2) };
        Controls.Add(TxtConnStr, 2, 0);

        BtnBrowse = new Button { Text = "選擇...", Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        BtnBrowse.Click += BrowseClick;
        Controls.Add(BtnBrowse, 3, 0);

        LblConnStr = new Label();
        UpdateLabelAndDefault();
    }

    public DatabaseProvider SelectedProvider =>
        CboProvider.SelectedIndex == 0 ? DatabaseProvider.Sqlite : DatabaseProvider.SqlServer;

    public void SetProvider(DatabaseProvider p)
    {
        CboProvider.SelectedIndex = p == DatabaseProvider.Sqlite ? 0 : 1;
    }

    public void SetConnectionString(string s) => TxtConnStr.Text = s ?? "";

    private void UpdateLabelAndDefault()
    {
        if (SelectedProvider == DatabaseProvider.Sqlite)
        {
            BtnBrowse.Text = "選檔...";
            BtnBrowse.Visible = true;
            if (string.IsNullOrWhiteSpace(TxtConnStr.Text))
                TxtConnStr.Text = Path.Combine(AppContext.BaseDirectory, "zipcode.db");
        }
        else
        {
            BtnBrowse.Text = "";
            BtnBrowse.Visible = false;
        }
    }

    private void BrowseClick(object? s, EventArgs e)
    {
        if (SelectedProvider != DatabaseProvider.Sqlite) return;

        using var dlg = new SaveFileDialog
        {
            Title            = "選擇 / 建立 SQLite 資料庫檔",
            Filter           = "SQLite (*.db;*.sqlite)|*.db;*.sqlite|所有檔案 (*.*)|*.*",
            FileName         = "zipcode.db",
            OverwritePrompt  = false,
            CheckFileExists  = false,
        };
        var existing = TxtConnStr.Text.Trim();
        if (!string.IsNullOrEmpty(existing) && File.Exists(existing))
            dlg.InitialDirectory = Path.GetDirectoryName(existing) ?? "";

        if (dlg.ShowDialog() == DialogResult.OK)
            TxtConnStr.Text = dlg.FileName;
    }
}
