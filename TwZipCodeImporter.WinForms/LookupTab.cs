using TwZipCodeImporter;
using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter.WinForms;

public class LookupTab : TabPage
{
    private readonly Func<DatabaseProvider> _getProvider;
    private readonly Func<string> _getConnStr;

    private readonly TextBox _txtAddress;
    private readonly Button  _btnLookup;
    private readonly Label   _lblParsed;
    private readonly Label   _lblBestCode;
    private readonly DataGridView _grid;
    private readonly Label _lblNote;

    public LookupTab(Func<DatabaseProvider> getProvider, Func<string> getConnStr)
    {
        _getProvider = getProvider;
        _getConnStr  = getConnStr;
        Text = "地址查 3+3";
        Padding = new Padding(6);

        // 輸入區
        var inputPanel = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            ColumnCount = 3,
            RowCount    = 1,
            AutoSize    = true,
            Padding     = new Padding(0, 0, 0, 6),
        };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        inputPanel.Controls.Add(new Label { Text = "地址:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        _txtAddress = new TextBox
        {
            Dock         = DockStyle.Fill,
            Margin       = new Padding(0, 2, 4, 2),
            PlaceholderText = "範例:台北市中山區中山北路二段100號",
        };
        _txtAddress.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = DoLookupAsync(); } };
        inputPanel.Controls.Add(_txtAddress, 1, 0);

        _btnLookup = new Button
        {
            Text      = "🔍 查詢",
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin    = new Padding(0, 2, 0, 2),
        };
        _btnLookup.FlatAppearance.BorderSize = 0;
        _btnLookup.Click += async (s, e) => await DoLookupAsync();
        inputPanel.Controls.Add(_btnLookup, 2, 0);

        _lblParsed = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0),
            ForeColor = Color.DarkSlateGray,
        };

        _lblBestCode = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0),
            Font      = new Font("Microsoft JhengHei UI", 14f, FontStyle.Bold),
            ForeColor = Color.DarkBlue,
        };

        _lblNote = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0),
            ForeColor = Color.Gray,
        };

        _grid = new DataGridView
        {
            Dock                          = DockStyle.Fill,
            AllowUserToAddRows            = false,
            AllowUserToDeleteRows         = false,
            ReadOnly                      = true,
            AutoSizeColumnsMode           = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode                 = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible             = false,
            BackgroundColor               = Color.White,
        };
        SetupGridColumns();

        Controls.Add(_grid);
        Controls.Add(_lblNote);
        Controls.Add(_lblBestCode);
        Controls.Add(_lblParsed);
        Controls.Add(inputPanel);
    }

    private void SetupGridColumns()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "3+3",   DataPropertyName = "Code6",    FillWeight = 60, DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Consolas", 9.5f, FontStyle.Bold) } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "縣市",  DataPropertyName = "CityName", FillWeight = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "區域",  DataPropertyName = "AreaName", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "街路",  DataPropertyName = "RoadName", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "投遞範圍", DataPropertyName = "DeliveryRangeRaw", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "投遞局",  DataPropertyName = "PostOffice", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "備註",   DataPropertyName = "BulkNote",   FillWeight = 90 });
    }

    private async Task DoLookupAsync()
    {
        var address = _txtAddress.Text.Trim();
        if (string.IsNullOrEmpty(address))
        {
            MessageBox.Show("請輸入地址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var connStr = _getConnStr().Trim();
        if (string.IsNullOrEmpty(connStr))
        {
            MessageBox.Show("請先設定資料庫連線。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnLookup.Enabled = false;
        _lblNote.Text = "查詢中...";

        try
        {
            var repo = RepositoryFactory.Create(_getProvider(), connStr);
            var svc  = new AddressLookupService(repo);
            var result = await svc.LookupAsync(address);

            var p = result.Parsed;
            _lblParsed.Text = $"解析:縣市[{p.CityName}]　區域[{p.AreaName}]　街路[{p.RoadName}]　號[{p.HouseNo}]";

            if (result.Matches.Count == 1)
            {
                _lblBestCode.Text = $"郵遞區號:{result.Matches[0].Code6}";
                _lblBestCode.ForeColor = Color.DarkGreen;
                _lblNote.Text = $"找到 1 筆完全符合 (路段共 {result.Candidates.Count} 筆規則)";
            }
            else if (result.Matches.Count > 1)
            {
                var distinctCodes = result.Matches.Select(m => m.Code6).Distinct().ToList();
                if (distinctCodes.Count == 1)
                {
                    _lblBestCode.Text = $"郵遞區號:{distinctCodes[0]}";
                    _lblBestCode.ForeColor = Color.DarkGreen;
                }
                else
                {
                    _lblBestCode.Text = $"多個可能:{string.Join(", ", distinctCodes)}";
                    _lblBestCode.ForeColor = Color.DarkOrange;
                }
                _lblNote.Text = $"符合 {result.Matches.Count} 筆 (路段共 {result.Candidates.Count} 筆規則)";
            }
            else
            {
                _lblBestCode.Text = "查無符合";
                _lblBestCode.ForeColor = Color.Firebrick;
                if (result.Candidates.Count > 0)
                    _lblNote.Text = $"找到 {result.Candidates.Count} 筆同路段資料但門牌號不在範圍內 (下表顯示同路段全部規則)";
                else
                    _lblNote.Text = "資料庫中無此路段資料,請確認縣市/區/路名是否正確,或先匯入 Excel 資料。";
            }

            var display = result.Matches.Count > 0 ? result.Matches : result.Candidates;
            _grid.DataSource = display.ToList();
        }
        catch (Exception ex)
        {
            _lblBestCode.Text = "查詢失敗";
            _lblBestCode.ForeColor = Color.Firebrick;
            _lblNote.Text = ex.Message;
        }
        finally
        {
            _btnLookup.Enabled = true;
        }
    }
}
