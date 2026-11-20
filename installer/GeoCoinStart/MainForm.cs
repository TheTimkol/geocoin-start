using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GeoCoinStart;

internal sealed class MainForm : Form
{
    private sealed record GpuDevice(string Backend, string Id, string Name)
    {
        public override string ToString() => $"{Name} — {Backend.ToUpperInvariant()} · {Id}";
    }

    private sealed record MinedBlockRecord(string BlockId, string TransactionId, long Timestamp, string Source);
    private sealed record MinedBlockLedger(List<MinedBlockRecord> Blocks, long? SessionStartedAtUnix,
        long? SessionEndedAtUnix, List<string> SessionBlockIds);

    private static readonly Color Canvas = Color.FromArgb(241, 241, 235);
    private static readonly Color Ink = Color.FromArgb(31, 39, 34);
    private static readonly Color Muted = Color.FromArgb(104, 115, 105);
    private static readonly Color Accent = Color.FromArgb(166, 129, 63);
    private static readonly Color Rail = Color.FromArgb(22, 31, 26);

    private readonly Panel _pagePanel = new();
    private readonly Panel _headerPanel = new();
    private readonly Label _pageTitle = new();
    private readonly Label _statusLabel = new();
    private readonly Label _balanceValue = new();
    private readonly Label _walletAddress = new();
    private readonly Label _nodeState = new();
    private readonly Label _syncState = new();
    private readonly Label _peerState = new();
    private readonly Label _miningState = new();
    private readonly Label _cpuHashrate = new();
    private readonly Label _gpuMiningState = new();
    private readonly Label _miningHashrate = new();
    private readonly Label _gpuRewardAddress = new();
    private readonly Label _transactionStatus = new();
    private readonly Label _transactionDetails = new();
    private readonly Label _walletKeyStatus = new();
    private readonly Label _nodeProcessStatus = new();
    private readonly Label _nodeRpcStatus = new();
    private readonly Label _nodeRuntimeStatus = new();
    private readonly Label _nodePeerStatus = new();
    private readonly Label _nodeProfileHint = new();
    private readonly Label _minedBlocksTodayLabel = new();
    private readonly Label _minedBlocksSessionLabel = new();
    private readonly TextBox _nodeLogBox = new();
    private readonly NumericUpDown _threadCount = new();
    private readonly ComboBox _gpuDeviceCombo = new();
    private readonly ComboBox _remoteGpuDeviceCombo = new();
    private readonly ComboBox _rpcBindCombo = new();
    private readonly ComboBox _walletAddressCombo = new();
    private readonly Button _refreshGpuButton = new();
    private readonly Button _refreshTransactionsButton = new();
    private readonly Button _copyTransactionIdButton = new();
    private readonly Button _walletFileExportButton = new();
    private readonly Button _walletKeyExportButton = new();
    private readonly Button _walletKeyImportButton = new();
    private readonly Button _refreshWalletAddressesButton = new();
    private readonly Button _nodeProfileCopyButton = new();
    private readonly Button _nodeLogOpenButton = new();
    private readonly Button _remoteGpuButton = new();
    private readonly Button _remoteGpuFindButton = new();
    private readonly TextBox _sendAddress = new();
    private readonly TextBox _sendAmount = new();
    private readonly TextBox _sendPassword = new();
    private readonly TextBox _privateKeyInput = new();
    private readonly TextBox _minerRpcUserInput = new() { Text = "geocoin-start-miner" };
    private readonly TextBox _minerRpcPasswordInput = new() { Text = RandomSecret(), UseSystemPasswordChar = true };
    private readonly TextBox _remoteNodeUrlInput = new() { Text = "http://192.168.1.10:3033/" };
    private readonly TextBox _remoteMinerUserInput = new() { Text = "geocoin-start-miner" };
    private readonly TextBox _remoteMinerPasswordInput = new() { UseSystemPasswordChar = true };
    private readonly TextBox _remoteRewardAddressInput = new();
    private readonly Button _toggleMinerPasswordVisibilityButton = new();
    private readonly Button _toggleRemotePasswordVisibilityButton = new();
    private readonly Label _sendMessage = new();
    private readonly Label _receiveAddress = new();
    private readonly Label _receiveMessage = new();
    private readonly Label _overviewHint = new();
    private readonly Button _nodeButton = new();
    private readonly Button _walletButton = new();
    private readonly Button _walletRestoreButton = new();
    private readonly Button _mineButton = new();
    private readonly Button _gpuMineButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _receiveButton = new();
    private readonly Button[] _navigation = new Button[8];
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };
    private readonly object _minedBlocksLock = new();
    private readonly List<MinedBlockRecord> _minedBlockRecords = new();
    private readonly HashSet<string> _miningSessionBlockIds = new(StringComparer.OrdinalIgnoreCase);

    private GeoCoinRpcClient? _rpc;
    private GeoCoinRpcClient? _minerRpc;
    private DataGridView? _transactionGrid;
    private Process? _nodeProcess;
    private Process? _gpuMinerProcess;
    private StreamWriter? _logWriter;
    private string? _adminPassword;
    private string? _minerPassword;
    private string _minerRpcUsername = "geocoin-start-miner";
    private string _rpcBindAddress = "127.0.0.1";
    private string? _sessionDirectory;
    private string? _minerPassFile;
    private string? _remoteMinerPassFile;
    private string? _lastAddress;
    private string _currentPage = "overview";
    private string _nodeStatus = "Нода остановлена";
    private decimal _balance;
    private long _height;
    private long _headers;
    private int _connections;
    private bool _cpuMiningActive;
    private bool _gpuMiningActive;
    private bool _remoteGpuMining;
    private bool _startingNode;
    private bool _startingRemoteGpu;
    private bool _rpcHealthy;
    private bool _miningSessionActive;
    private DateTimeOffset? _miningSessionStartedAt;
    private DateTimeOffset? _miningSessionEndedAt;
    private DateTimeOffset _lastWalletMiningStatsPollUtc = DateTimeOffset.MinValue;
    private bool _checkingGpuDevices;
    private bool _gpuDevicesScanned;
    private bool _loadingTransactions;
    private bool _loadingWalletAddresses;
    private double _cpuHashrateValue;
    private string _gpuHashrateText = "Скорость: —";
    private string _gpuStatusText = "GPU-майнинг остановлен";
    private readonly List<GpuDevice> _gpuDevices = new();
    private readonly List<string> _walletAddresses = new();
    private string? _selectedTransactionId;
    private bool _refreshing;
    private bool _allowClose;
    private bool _closePromptOpen;
    private int _lastPageViewportWidth;
    private int _selectedThreads = Math.Max(1, Environment.ProcessorCount / 2);

    public MainForm()
    {
        Text = "GeoCoin-Start — простой кошелёк";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        MinimumSize = new Size(940, 640);
        Size = new Size(1120, 760);
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        FormClosing += HandleClosing;

        LoadMinedBlockRecords();
        BuildShell();
        _nodeButton.Click += async (_, _) =>
        {
            if (IsNodeRunning()) await StopNodeAsync();
            else await StartNodeAsync();
            BuildPage();
        };
        _nodeProfileCopyButton.Click += (_, _) => CopyMinerConnectionProfile();
        _nodeLogOpenButton.Click += (_, _) => OpenNodeLog();
        _toggleMinerPasswordVisibilityButton.Click += (_, _) =>
        {
            _minerRpcPasswordInput.UseSystemPasswordChar = !_minerRpcPasswordInput.UseSystemPasswordChar;
            _toggleMinerPasswordVisibilityButton.Text = _minerRpcPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        };
        _toggleRemotePasswordVisibilityButton.Click += (_, _) =>
        {
            _remoteMinerPasswordInput.UseSystemPasswordChar = !_remoteMinerPasswordInput.UseSystemPasswordChar;
            _toggleRemotePasswordVisibilityButton.Text = _remoteMinerPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        };
        _walletButton.Click += async (_, _) =>
        {
            if (File.Exists(AppPaths.WalletPath)) await BackupWalletAsync();
            else await CreateWalletAsync();
            BuildPage();
        };
        _walletRestoreButton.Click += async (_, _) =>
        {
            await ImportWalletAsync();
            BuildPage();
        };
        _mineButton.Click += async (_, _) => await ToggleCpuMiningAsync();
        _gpuMineButton.Click += async (_, _) => await ToggleGpuMiningAsync();
        _refreshGpuButton.Click += async (_, _) => await RefreshGpuDevicesAsync();
        _remoteGpuFindButton.Click += async (_, _) => await RefreshGpuDevicesAsync();
        _remoteGpuButton.Click += async (_, _) => await ToggleRemoteGpuMiningAsync();
        _gpuDeviceCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_gpuMineButton.IsHandleCreated)
                _gpuMineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath) &&
                    (_gpuMiningActive || _gpuDeviceCombo.SelectedItem is GpuDevice);
        };
        _walletAddressCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_walletKeyExportButton.IsHandleCreated)
                _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        };
        _sendButton.Click += async (_, _) => await SendAsync();
        _receiveButton.Click += async (_, _) => await CreateReceiveAddressAsync();
        _refreshTransactionsButton.Click += async (_, _) => await RefreshTransactionsAsync();
        _copyTransactionIdButton.Click += (_, _) => CopySelectedTransactionId();
        _walletFileExportButton.Click += async (_, _) => await BackupWalletAsync();
        _walletKeyExportButton.Click += async (_, _) => await ExportPrivateKeyAsync();
        _walletKeyImportButton.Click += async (_, _) => await ImportPrivateKeyAsync();
        _refreshWalletAddressesButton.Click += async (_, _) => await RefreshWalletAddressesAsync();
        _threadCount.ValueChanged += (_, _) => _selectedThreads = (int)_threadCount.Value;
        _refreshTimer.Tick += async (_, _) =>
        {
            await RefreshStateAsync();
            if (_currentPage == "node") UpdateNodePage();
            UpdateMinedBlockCounters();
        };
        _refreshTimer.Start();
        Shown += (_, _) => FitWindowToScreen();
        DpiChanged += (_, _) =>
        {
            if (Controls.Count > 0 && Controls[0] is TableLayoutPanel shell)
            {
                shell.ColumnStyles[0].Width = ScaleUi(224);
                shell.RowStyles[0].Height = ScaleUi(100);
            }
            LayoutHeaderStatus();
            BuildPage();
        };
        SetPage("overview");
        UpdateStatus();
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Canvas
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleUi(224)));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleUi(100)));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var rail = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Rail };
        shell.Controls.Add(rail, 0, 0);
        shell.SetRowSpan(rail, 2);

        var brandIcon = new PictureBox
        {
            Location = UiPoint(20, 23),
            Size = UiSize(38, 38),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = (Icon ?? SystemIcons.Application).ToBitmap()
        };
        rail.Controls.Add(brandIcon);
        rail.Controls.Add(new Label
        {
            Text = "GeoCoin",
            Location = UiPoint(64, 22),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(233, 208, 153)
        });
        rail.Controls.Add(new Label
        {
            Text = "ПРОСТОЙ КОШЕЛЁК",
            Location = UiPoint(27, 70),
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Color.FromArgb(144, 161, 145)
        });

        string[] names = { "Обзор", "Майнинг", "Подмайнер", "Нода", "Отправить", "Получить", "История", "Кошелёк" };
        string[] pages = { "overview", "mining", "subminer", "node", "send", "receive", "transactions", "wallet" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Text = names[i],
                Tag = pages[i],
                TextAlign = ContentAlignment.MiddleLeft,
                Location = UiPoint(18, 132 + i * 56),
                Size = UiSize(188, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Rail,
                ForeColor = Color.FromArgb(206, 215, 205),
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(14, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 50, 41);
            button.Click += (_, _) => SetPage(pages[index]);
            rail.Controls.Add(button);
            _navigation[i] = button;
        }

        var railFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = ScaleUi(62),
            BackColor = Rail
        };
        rail.Controls.Add(railFooter);

        var originalLink = new LinkLabel
        {
            Text = "Оригинальный проект ↗",
            AutoSize = false,
            Location = UiPoint(25, 4),
            Size = UiSize(190, 22),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        originalLink.LinkClicked += (_, _) => OpenLink(AppPaths.OriginalRepositoryUrl);
        railFooter.Controls.Add(originalLink);

        var distributorLink = new LinkLabel
        {
            Text = "Редистрибьютор: TheTimkol ↗",
            AutoSize = false,
            Location = UiPoint(25, 29),
            Size = UiSize(190, 22),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        distributorLink.LinkClicked += (_, _) => OpenLink(AppPaths.DistributorUrl);
        railFooter.Controls.Add(distributorLink);

        _headerPanel.Dock = DockStyle.Fill;
        _headerPanel.Margin = Padding.Empty;
        _headerPanel.BackColor = Canvas;
        shell.Controls.Add(_headerPanel, 1, 0);
        _headerPanel.Controls.Add(_pageTitle);
        _pageTitle.Location = UiPoint(34, 20);
        _pageTitle.AutoSize = true;
        _pageTitle.Font = new Font("Segoe UI Semibold", 21, FontStyle.Bold);
        _pageTitle.ForeColor = Ink;
        _headerPanel.Controls.Add(new Label
        {
            Text = "Управление GeoCoin без лишних настроек",
            Location = UiPoint(37, 61),
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5f)
        });

        _statusLabel.Size = UiSize(292, 34);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _statusLabel.BackColor = Color.FromArgb(226, 228, 219);
        _statusLabel.ForeColor = Muted;
        _headerPanel.Controls.Add(_statusLabel);
        _headerPanel.SizeChanged += (_, _) => LayoutHeaderStatus();

        _pagePanel.Dock = DockStyle.Fill;
        _pagePanel.Margin = Padding.Empty;
        _pagePanel.AutoScroll = true;
        _pagePanel.BackColor = Canvas;
        _pagePanel.Padding = UiPadding(34, 8, 34, 28);
        shell.Controls.Add(_pagePanel, 1, 1);
        _pagePanel.ClientSizeChanged += (_, _) => RefreshPageLayoutForWidth();
        LayoutHeaderStatus();
    }

    private void LayoutHeaderStatus()
    {
        if (_statusLabel.IsDisposed || _headerPanel.IsDisposed) return;
        int margin = ScaleUi(34);
        int width = _statusLabel.Width > 0 ? _statusLabel.Width : ScaleUi(292);
        _statusLabel.Location = new Point(Math.Max(margin, _headerPanel.ClientSize.Width - margin - width), ScaleUi(36));
    }

    private void RefreshPageLayoutForWidth()
    {
        int width = _pagePanel.ClientSize.Width;
        if (!IsHandleCreated || IsDisposed || width <= 0 || width == _lastPageViewportWidth) return;
        _lastPageViewportWidth = width;
        BeginInvoke((Action)BuildPage);
    }

    private int PageCardWidth(int preferredWidth)
    {
        int availablePixels = _pagePanel.ClientSize.Width - _pagePanel.Padding.Horizontal - ScaleUi(44);
        if (availablePixels <= 0) return preferredWidth;
        int availableLogical = (int)Math.Floor(availablePixels * 96d / Math.Max(96, DeviceDpi));
        return Math.Max(520, Math.Min(preferredWidth, availableLogical));
    }

    private int ScaleUi(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    private Point UiPoint(int x, int y) => new(ScaleUi(x), ScaleUi(y));
    private Size UiSize(int width, int height) => new(ScaleUi(width), ScaleUi(height));
    private Padding UiPadding(int left, int top, int right, int bottom)
        => new(ScaleUi(left), ScaleUi(top), ScaleUi(right), ScaleUi(bottom));

    private void FitWindowToScreen()
    {
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        int width = Math.Max(720, Math.Min(Width, workArea.Width - 24));
        int height = Math.Max(540, Math.Min(Height, workArea.Height - 24));
        if (width >= Width && height >= Height) return;

        MinimumSize = new Size(Math.Min(MinimumSize.Width, width), Math.Min(MinimumSize.Height, height));
        Size = new Size(width, height);
        CenterToScreen();
    }

    private void SetPage(string page)
    {
        if (_currentPage == "wallet" && page != "wallet") _privateKeyInput.Clear();
        _currentPage = page;
        string[] titles = page switch
        {
            "mining" => new[] { "Майнинг", "Майнинг", "Майнинг", "Майнинг" },
            "subminer" => new[] { "Подмайнер", "Подмайнер", "Подмайнер", "Подмайнер" },
            "node" => new[] { "Нода и журнал", "Нода и журнал", "Нода и журнал", "Нода и журнал" },
            "send" => new[] { "Отправить монеты", "Отправить монеты", "Отправить монеты", "Отправить монеты" },
            "receive" => new[] { "Получить монеты", "Получить монеты", "Получить монеты", "Получить монеты" },
            "transactions" => new[] { "История транзакций", "История транзакций", "История транзакций", "История транзакций" },
            "wallet" => new[] { "Управление кошельком", "Управление кошельком", "Управление кошельком", "Управление кошельком" },
            _ => new[] { "Обзор", "Обзор", "Обзор", "Обзор" }
        };
        _pageTitle.Text = titles[0];
        string[] pages = { "overview", "mining", "subminer", "node", "send", "receive", "transactions", "wallet" };
        for (int i = 0; i < _navigation.Length; i++)
        {
            bool selected = pages[i] == page;
            _navigation[i].BackColor = selected ? Color.FromArgb(44, 59, 48) : Rail;
            _navigation[i].ForeColor = selected ? Color.FromArgb(241, 218, 173) : Color.FromArgb(206, 215, 205);
        }
        BuildPage();
        if (page == "transactions" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshTransactionsAsync();
        if (page == "wallet" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshWalletAddressesAsync();
        if ((page == "mining" || page == "subminer") && !_gpuDevicesScanned && !_checkingGpuDevices && File.Exists(AppPaths.GpuMinerPath))
            _ = RefreshGpuDevicesAsync();
        if (page == "node") UpdateNodePage();
    }

    private void BuildPage()
    {
        _pagePanel.SuspendLayout();
        _pagePanel.Controls.Clear();
        _transactionGrid = null;
        switch (_currentPage)
        {
            case "mining": BuildMiningPage(); break;
            case "subminer": BuildSubminerPage(); break;
            case "node": BuildNodePage(); break;
            case "send": BuildSendPage(); break;
            case "receive": BuildReceivePage(); break;
            case "transactions": BuildTransactionsPage(); break;
            case "wallet": BuildWalletPage(); break;
            default: BuildOverviewPage(); break;
        }
        _pagePanel.ResumeLayout();
        UpdateStatus();
    }

    private void BuildOverviewPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду.", 22, 12, cardWidth, 44);

        Panel balanceCard = AddCard(22, 72, cardWidth, 162);
        AddLabel(balanceCard, "БАЛАНС КОШЕЛЬКА", 24, 20, 250, 20, Muted, 9, bold: true);
        _balanceValue.Text = _nodeProcess is null ? "— GC" : $"{_balance.ToString("0.########", CultureInfo.InvariantCulture)} GC";
        _balanceValue.Location = UiPoint(22, 49);
        _balanceValue.AutoSize = true;
        _balanceValue.Font = new Font("Segoe UI Semibold", 30, FontStyle.Bold);
        _balanceValue.ForeColor = Ink;
        balanceCard.Controls.Add(_balanceValue);
        _nodeState.Text = _nodeProcess is null ? "Нода не запущена" : _nodeStatus;
        _nodeState.Location = UiPoint(26, 112);
        _nodeState.AutoSize = true;
        _nodeState.ForeColor = Muted;
        balanceCard.Controls.Add(_nodeState);

        Panel networkCard = AddCard(22, 252, cardWidth, 142);
        AddLabel(networkCard, "СОСТОЯНИЕ СЕТИ", 24, 20, 230, 20, Muted, 9, bold: true);
        _syncState.Text = _nodeProcess is null ? "Запустите ноду, чтобы начать подключение" : SyncDescription();
        _syncState.Location = UiPoint(24, 53);
        _syncState.AutoSize = false;
        _syncState.Size = UiSize(Math.Max(240, cardWidth - 58), 28);
        _syncState.Font = new Font("Segoe UI Semibold", 13, FontStyle.Bold);
        _syncState.ForeColor = Ink;
        networkCard.Controls.Add(_syncState);
        _peerState.Text = _nodeProcess is null ? "" : $"Блоков: {_height:N0}     Узлов сети: {_connections}";
        _peerState.Location = UiPoint(26, 95);
        _peerState.AutoSize = true;
        _peerState.ForeColor = Muted;
        networkCard.Controls.Add(_peerState);

        _nodeButton.Text = _startingNode ? "Запускаю ноду…" : !IsNodeProcessRunning() ? "Запустить ноду" : "Остановить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 416), new Size(192, 42));
        _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        _pagePanel.Controls.Add(_nodeButton);

        bool hasWallet = File.Exists(AppPaths.WalletPath);
        _walletButton.Text = hasWallet ? "Экспортировать кошелёк (.dat)" : "Создать кошелёк";
        StyleSecondaryButton(_walletButton, new Point(232, 416), new Size(230, 42));
        _walletButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletButton);

        _walletRestoreButton.Text = hasWallet ? "Импортировать другой" : "Импортировать кошелёк";
        StyleSecondaryButton(_walletRestoreButton, new Point(232, 468), new Size(230, 42));
        _walletRestoreButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletRestoreButton);

        _overviewHint.Text = File.Exists(AppPaths.WalletPath)
            ? "Сделайте резервную копию кошелька и храните её отдельно от компьютера."
            : "Создайте кошелёк или импортируйте существующий файл GeoCoin. Пароль хранится только у вас.";
        _overviewHint.Location = UiPoint(24, 524);
        _overviewHint.Size = UiSize(cardWidth - 24, 44);
        _overviewHint.ForeColor = Muted;
        _pagePanel.Controls.Add(_overviewHint);
    }

    private void BuildMiningPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("CPU и GPU настраиваются в одном окне. Их можно запускать по отдельности или одновременно.", 22, 12, cardWidth, 44);
        Panel blockStats = AddCard(22, 68, cardWidth, 74);
        AddLabel(blockStats, "ДОБЫТЫЕ БЛОКИ", 22, 11, 240, 18, Muted, 8.5f, bold: true);
        _minedBlocksTodayLabel.Location = UiPoint(22, 36);
        _minedBlocksTodayLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksTodayLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksTodayLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksTodayLabel);
        _minedBlocksSessionLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 36);
        _minedBlocksSessionLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksSessionLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksSessionLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksSessionLabel);
        Panel card = AddCard(22, 154, cardWidth, 500);

        AddLabel(card, "Майнинг на процессоре · CPU", 24, 18, cardWidth - 48, 24, Ink, 13, bold: true);
        AddLabel(card, "Потоки", 24, 52, 100, 22, Muted, 10, bold: true);
        _threadCount.Location = UiPoint(24, 78);
        _threadCount.Size = UiSize(112, 31);
        _threadCount.Minimum = 1;
        _threadCount.Maximum = Math.Max(1, Environment.ProcessorCount);
        _threadCount.Value = Math.Max(1, Math.Min((int)_threadCount.Maximum, _selectedThreads));
        _threadCount.Font = new Font("Segoe UI", 11);
        _threadCount.Enabled = !_cpuMiningActive;
        card.Controls.Add(_threadCount);

        _miningState.Text = _cpuMiningActive
            ? $"CPU-майнинг включён · потоков: {_selectedThreads}"
            : "CPU-майнинг остановлен";
        _miningState.Location = UiPoint(24, 120);
        _miningState.AutoSize = true;
        _miningState.ForeColor = _cpuMiningActive ? Color.FromArgb(53, 119, 74) : Muted;
        card.Controls.Add(_miningState);
        _cpuHashrate.Text = _cpuMiningActive
            ? $"Скорость CPU: {FormatHashrate(_cpuHashrateValue)}"
            : "Скорость CPU: —";
        _cpuHashrate.Location = UiPoint(24, 145);
        _cpuHashrate.Size = UiSize(cardWidth - 48, 24);
        _cpuHashrate.ForeColor = Muted;
        card.Controls.Add(_cpuHashrate);

        _mineButton.Text = _cpuMiningActive ? "Остановить CPU" : "Запустить CPU";
        StylePrimaryButton(_mineButton, new Point(24, 174), new Size(204, 42));
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_mineButton);

        var divider = new Panel
        {
            Location = UiPoint(24, 232),
            Size = UiSize(cardWidth - 48, 1),
            BackColor = Color.FromArgb(220, 220, 214)
        };
        card.Controls.Add(divider);

        AddLabel(card, "Майнинг на видеокарте · GPU", 24, 250, cardWidth - 48, 24, Ink, 13, bold: true);
        AddLabel(card, "Выберите видеокарту. Награда поступит на уже существующий адрес этого кошелька.", 24, 280, cardWidth - 48, 28, Muted, 9.5f);
        _gpuDeviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        GpuDevice? previousDevice = _gpuDeviceCombo.SelectedItem as GpuDevice;
        _gpuDeviceCombo.Items.Clear();
        foreach (GpuDevice device in _gpuDevices) _gpuDeviceCombo.Items.Add(device);
        int selectedDevice = previousDevice is null ? -1 : _gpuDevices.FindIndex(device =>
            device.Backend == previousDevice.Backend && device.Id == previousDevice.Id);
        _gpuDeviceCombo.SelectedIndex = selectedDevice >= 0 ? selectedDevice : (_gpuDeviceCombo.Items.Count > 0 ? 0 : -1);
        _gpuDeviceCombo.Location = UiPoint(24, 314);
        _gpuDeviceCombo.Size = UiSize(Math.Max(240, cardWidth - 150), 34);
        _gpuDeviceCombo.Font = new Font("Segoe UI", 10);
        _gpuDeviceCombo.Enabled = !_gpuMiningActive;
        card.Controls.Add(_gpuDeviceCombo);

        _refreshGpuButton.Text = "Найти";
        StyleSecondaryButton(_refreshGpuButton, new Point(cardWidth - 112, 312), new Size(88, 36));
        _refreshGpuButton.Enabled = !_gpuMiningActive && !_checkingGpuDevices;
        card.Controls.Add(_refreshGpuButton);

        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.Location = UiPoint(24, 358);
        _gpuMiningState.AutoSize = true;
        _gpuMiningState.ForeColor = _gpuMiningActive ? Color.FromArgb(53, 119, 74) : Muted;
        card.Controls.Add(_gpuMiningState);
        _miningHashrate.Text = _gpuHashrateText;
        _miningHashrate.Location = UiPoint(24, 383);
        _miningHashrate.Size = UiSize(cardWidth - 48, 24);
        _miningHashrate.ForeColor = Muted;
        card.Controls.Add(_miningHashrate);
        _gpuRewardAddress.Text = string.IsNullOrWhiteSpace(_lastAddress)
            ? "Адрес награды будет взят из текущего кошелька."
            : "Адрес награды: " + _lastAddress;
        _gpuRewardAddress.Location = UiPoint(24, 407);
        _gpuRewardAddress.Size = UiSize(cardWidth - 48, 24);
        _gpuRewardAddress.ForeColor = Muted;
        _gpuRewardAddress.AutoEllipsis = true;
        card.Controls.Add(_gpuRewardAddress);

        _gpuMineButton.Text = _remoteGpuMining ? "Перейти к подмайнеру" : _gpuMiningActive ? "Остановить GPU" : "Запустить GPU";
        StylePrimaryButton(_gpuMineButton, new Point(24, 444), new Size(_remoteGpuMining ? 240 : 204, 42));
        _gpuMineButton.Enabled = _remoteGpuMining || (IsNodeRunning() && File.Exists(AppPaths.WalletPath) &&
            (_gpuMiningActive || _gpuDeviceCombo.SelectedItem is GpuDevice));
        card.Controls.Add(_gpuMineButton);

        if (!IsNodeRunning()) AddParagraph("Сначала запустите ноду на странице «Обзор».", 24, 674, cardWidth - 48, 32);
        else if (!File.Exists(AppPaths.WalletPath)) AddParagraph("Сначала создайте или импортируйте кошелёк на странице «Обзор».", 24, 674, cardWidth - 48, 32);
        UpdateMinedBlockCounters();
    }

    private void BuildNodePage()
    {
        int cardWidth = PageCardWidth(800);
        AddParagraph("Один процесс GeoCoin выполняет работу ноды. RPC — интерфейс управления этой же нодой; отдельно запускать вторую RPC-ноду не нужно.",
            22, 12, cardWidth, 48);

        Panel statusCard = AddCard(22, 70, cardWidth, 132);
        AddLabel(statusCard, "СОСТОЯНИЕ УЗЛА", 22, 15, 280, 20, Muted, 9, bold: true);
        _nodeProcessStatus.Location = UiPoint(22, 40);
        _nodeProcessStatus.Size = UiSize(Math.Max(230, cardWidth - 44), 26);
        _nodeProcessStatus.Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold);
        statusCard.Controls.Add(_nodeProcessStatus);
        _nodeRpcStatus.Location = UiPoint(22, 72);
        _nodeRpcStatus.Size = UiSize(240, 22);
        _nodeRpcStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodeRpcStatus);
        _nodePeerStatus.Location = UiPoint(270, 72);
        _nodePeerStatus.Size = UiSize(Math.Max(190, cardWidth - 294), 22);
        _nodePeerStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodePeerStatus);
        _nodeButton.Text = _startingNode ? "Запускаю ноду…" : IsNodeRunning() ? "Остановить ноду" : "Запустить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 98), new Size(190, 34));
        _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        statusCard.Controls.Add(_nodeButton);
        _nodeRuntimeStatus.Location = UiPoint(228, 104);
        _nodeRuntimeStatus.Size = UiSize(Math.Max(200, cardWidth - 250), 22);
        _nodeRuntimeStatus.ForeColor = Muted;
        statusCard.Controls.Add(_nodeRuntimeStatus);

        Panel rpcCard = AddCard(22, 216, cardWidth, 378);
        AddLabel(rpcCard, "ДОСТУП ДЛЯ GPU-ПОДМАЙНЕРА", 22, 15, cardWidth - 44, 20, Muted, 9, bold: true);
        AddLabel(rpcCard, "Адрес прослушивания", 22, 44, 230, 20, Ink, 10, bold: true);
        _rpcBindCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _rpcBindCombo.SelectedIndexChanged -= RpcBindComboSelectedIndexChanged;
        _rpcBindCombo.Items.Clear();
        _rpcBindCombo.Items.Add("127.0.0.1 · только этот компьютер");
        foreach (string address in GetPrivateIpv4Addresses()) _rpcBindCombo.Items.Add(address);
        int bindIndex = 0;
        for (int i = 1; i < _rpcBindCombo.Items.Count; i++)
            if (string.Equals(_rpcBindCombo.Items[i]?.ToString(), _rpcBindAddress, StringComparison.Ordinal)) bindIndex = i;
        if (bindIndex == 0) _rpcBindAddress = "127.0.0.1";
        _rpcBindCombo.SelectedIndex = bindIndex;
        _rpcBindCombo.Location = UiPoint(22, 68);
        _rpcBindCombo.Size = UiSize(cardWidth - 44, 32);
        _rpcBindCombo.Enabled = !IsNodeRunning() && !_startingNode;
        _rpcBindCombo.SelectedIndexChanged += RpcBindComboSelectedIndexChanged;
        rpcCard.Controls.Add(_rpcBindCombo);
        AddLabel(rpcCard, "Для подключения с другого ПК выберите частный IP этого компьютера. Используйте только доверенную сеть или VPN; не настраивайте проброс порта из интернета.",
            22, 106, cardWidth - 44, 44, Muted, 8.5f);

        AddLabel(rpcCard, "Имя пользователя майнера", 22, 158, 250, 20, Ink, 10, bold: true);
        StyleTextBox(_minerRpcUserInput, new Point(22, 182), new Size(Math.Min(310, cardWidth - 44), 34));
        _minerRpcUserInput.Enabled = !IsNodeRunning() && !_startingNode;
        rpcCard.Controls.Add(_minerRpcUserInput);
        AddLabel(rpcCard, "Пароль майнера", 22, 226, 220, 20, Ink, 10, bold: true);
        int passwordWidth = Math.Min(370, cardWidth - 160);
        StyleTextBox(_minerRpcPasswordInput, new Point(22, 250), new Size(passwordWidth, 34));
        _minerRpcPasswordInput.Enabled = !IsNodeRunning() && !_startingNode;
        rpcCard.Controls.Add(_minerRpcPasswordInput);
        _toggleMinerPasswordVisibilityButton.Text = _minerRpcPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        StyleSecondaryButton(_toggleMinerPasswordVisibilityButton, new Point(passwordWidth + 34, 250), new Size(100, 34));
        rpcCard.Controls.Add(_toggleMinerPasswordVisibilityButton);
        _nodeProfileCopyButton.Text = "Скопировать данные для подмайнера";
        StyleSecondaryButton(_nodeProfileCopyButton, new Point(22, 296), new Size(Math.Min(310, cardWidth - 44), 34));
        rpcCard.Controls.Add(_nodeProfileCopyButton);
        _nodeProfileHint.Text = _rpcBindAddress == "127.0.0.1"
            ? "Пароль не сохраняется после закрытия приложения. Для другого ПК выберите частный IP и запустите ноду."
            : "Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети.";
        _nodeProfileHint.Location = UiPoint(22, 338);
        _nodeProfileHint.Size = UiSize(cardWidth - 44, 32);
        _nodeProfileHint.ForeColor = Muted;
        rpcCard.Controls.Add(_nodeProfileHint);

        Panel logCard = AddCard(22, 608, cardWidth, 430);
        AddLabel(logCard, "ЖУРНАЛ НОДЫ И МАЙНЕРА", 22, 15, 320, 20, Muted, 9, bold: true);
        _nodeLogOpenButton.Text = "Открыть файл журнала";
        StyleSecondaryButton(_nodeLogOpenButton, new Point(cardWidth - 228, 9), new Size(200, 32));
        logCard.Controls.Add(_nodeLogOpenButton);
        _nodeLogBox.Location = UiPoint(18, 50);
        _nodeLogBox.Size = UiSize(cardWidth - 38, 362);
        _nodeLogBox.Multiline = true;
        _nodeLogBox.ReadOnly = true;
        _nodeLogBox.ScrollBars = ScrollBars.Both;
        _nodeLogBox.WordWrap = false;
        _nodeLogBox.Font = new Font("Consolas", 8.5f);
        _nodeLogBox.BackColor = Color.FromArgb(25, 31, 28);
        _nodeLogBox.ForeColor = Color.FromArgb(218, 226, 216);
        _nodeLogBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        logCard.Controls.Add(_nodeLogBox);
        UpdateNodePage();
    }

    private void BuildSubminerPage()
    {
        int cardWidth = PageCardWidth(800);
        AddParagraph("Этот компьютер будет работать как GPU-подмайнер и подключится к уже запущенной ноде на другом ПК. Локальную ноду запускать не нужно.",
            22, 12, cardWidth, 48);
        Panel blockStats = AddCard(22, 70, cardWidth, 74);
        AddLabel(blockStats, "ДОБЫТЫЕ БЛОКИ", 22, 11, 240, 18, Muted, 8.5f, bold: true);
        _minedBlocksTodayLabel.Location = UiPoint(22, 36);
        _minedBlocksTodayLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksTodayLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksTodayLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksTodayLabel);
        _minedBlocksSessionLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 36);
        _minedBlocksSessionLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 30), 24);
        _minedBlocksSessionLabel.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        _minedBlocksSessionLabel.ForeColor = Ink;
        blockStats.Controls.Add(_minedBlocksSessionLabel);
        Panel card = AddCard(22, 156, cardWidth, 476);
        AddLabel(card, "ПОДКЛЮЧЕНИЕ К НОДЕ", 22, 16, cardWidth - 44, 20, Muted, 9, bold: true);
        AddLabel(card, "Адрес RPC-ноды", 22, 48, 230, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteNodeUrlInput, new Point(22, 72), new Size(Math.Min(470, cardWidth - 44), 34));
        _remoteNodeUrlInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteNodeUrlInput);
        AddLabel(card, "Имя пользователя майнера", 22, 122, 260, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteMinerUserInput, new Point(22, 146), new Size(Math.Min(300, cardWidth - 44), 34));
        _remoteMinerUserInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteMinerUserInput);
        AddLabel(card, "Пароль роли майнера", 22, 194, 240, 20, Ink, 10, bold: true);
        int remotePassWidth = Math.Min(370, cardWidth - 160);
        StyleTextBox(_remoteMinerPasswordInput, new Point(22, 218), new Size(remotePassWidth, 34));
        _remoteMinerPasswordInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteMinerPasswordInput);
        _toggleRemotePasswordVisibilityButton.Text = _remoteMinerPasswordInput.UseSystemPasswordChar ? "Показать" : "Скрыть";
        StyleSecondaryButton(_toggleRemotePasswordVisibilityButton, new Point(remotePassWidth + 34, 218), new Size(100, 34));
        card.Controls.Add(_toggleRemotePasswordVisibilityButton);
        AddLabel(card, "Адрес для награды", 22, 266, 260, 20, Ink, 10, bold: true);
        StyleTextBox(_remoteRewardAddressInput, new Point(22, 290), new Size(Math.Min(520, cardWidth - 44), 34));
        _remoteRewardAddressInput.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteRewardAddressInput);
        AddLabel(card, "Видеокарта", 22, 338, 200, 20, Ink, 10, bold: true);
        _remoteGpuDeviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        GpuDevice? previous = _remoteGpuDeviceCombo.SelectedItem as GpuDevice;
        _remoteGpuDeviceCombo.Items.Clear();
        foreach (GpuDevice device in _gpuDevices) _remoteGpuDeviceCombo.Items.Add(device);
        int selected = previous is null ? -1 : _gpuDevices.FindIndex(device => device.Backend == previous.Backend && device.Id == previous.Id);
        _remoteGpuDeviceCombo.SelectedIndex = selected >= 0 ? selected : (_remoteGpuDeviceCombo.Items.Count > 0 ? 0 : -1);
        _remoteGpuDeviceCombo.Location = UiPoint(22, 362);
        _remoteGpuDeviceCombo.Size = UiSize(Math.Max(240, cardWidth - 160), 34);
        _remoteGpuDeviceCombo.Enabled = !_remoteGpuMining && !_startingRemoteGpu;
        card.Controls.Add(_remoteGpuDeviceCombo);
        _remoteGpuFindButton.Text = "Найти";
        StyleSecondaryButton(_remoteGpuFindButton, new Point(cardWidth - 122, 360), new Size(96, 36));
        _remoteGpuFindButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_checkingGpuDevices;
        card.Controls.Add(_remoteGpuFindButton);
        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.Location = UiPoint(22, 402);
        _gpuMiningState.AutoSize = true;
        _gpuMiningState.ForeColor = _remoteGpuMining ? Color.FromArgb(53, 119, 74) : Muted;
        card.Controls.Add(_gpuMiningState);
        _miningHashrate.Text = _gpuHashrateText;
        _miningHashrate.Location = UiPoint(330, 402);
        _miningHashrate.Size = UiSize(Math.Max(180, cardWidth - 360), 24);
        _miningHashrate.ForeColor = Muted;
        card.Controls.Add(_miningHashrate);
        _remoteGpuButton.Text = _remoteGpuMining ? "Остановить подмайнер" : _startingRemoteGpu ? "Подключаюсь…" : "Подключить и начать GPU-майнинг";
        StylePrimaryButton(_remoteGpuButton, new Point(22, 432), new Size(300, 36));
        _remoteGpuButton.Enabled = _remoteGpuMining || (!_startingRemoteGpu && !_checkingGpuDevices && !IsNodeRunning() &&
            _gpuMinerProcess is null && _remoteGpuDeviceCombo.SelectedItem is GpuDevice);
        card.Controls.Add(_remoteGpuButton);
        AddParagraph("На узле нужен miner RPC-пользователь и пароль; admin RPC и пароль кошелька сюда не вводите. Подключайтесь только по доверенной LAN/VPN: RPC не шифрует пароль при передаче.",
            22, 650, cardWidth, 54);
        UpdateMinedBlockCounters();
    }

    private void RpcBindComboSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_rpcBindCombo.SelectedIndex == 0 || _rpcBindCombo.SelectedItem is null)
            _rpcBindAddress = "127.0.0.1";
        else _rpcBindAddress = _rpcBindCombo.SelectedItem.ToString() ?? "127.0.0.1";
        _nodeProfileHint.Text = _rpcBindAddress == "127.0.0.1"
            ? "Пароль не сохраняется после закрытия приложения. Для другого ПК выберите частный IP и запустите ноду."
            : "Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети.";
    }

    private void UpdateNodePage()
    {
        if (IsDisposed || _currentPage != "node") return;
        bool running = IsNodeRunning();
        _nodeProcessStatus.Text = running
            ? _nodeStatus
            : (_startingNode ? "Нода запускается…" : _startingRemoteGpu ? "Подключаю GPU к удалённой ноде…" :
                _remoteGpuMining ? "Локальная нода остановлена · работает режим подмайнера" : "Нода остановлена");
        _nodeProcessStatus.ForeColor = running ? Color.FromArgb(53, 119, 74) : Muted;
        _nodeRpcStatus.Text = running ? (_rpcHealthy ? "RPC отвечает" : "Процесс запущен · проверяю RPC…") : "RPC не запущен";
        _nodeRpcStatus.ForeColor = _rpcHealthy ? Color.FromArgb(53, 119, 74) : Muted;
        _nodePeerStatus.Text = running ? $"Блоков: {_height:N0} · подключений: {_connections}" : "Сеть: —";
        string runTime = "";
        if (_nodeProcess is { } process && IsNodeProcessRunning())
        {
            try { runTime = $"PID {process.Id} · запущена в {process.StartTime.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}"; }
            catch { runTime = $"PID {process.Id}"; }
        }
        _nodeRuntimeStatus.Text = runTime;
        if (_nodeButton.IsHandleCreated)
        {
            _nodeButton.Text = _startingNode ? "Запускаю ноду…" : running ? "Остановить ноду" : "Запустить ноду";
            _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        }
        if (_nodeProfileCopyButton.IsHandleCreated)
            _nodeProfileCopyButton.Enabled = running && _rpcBindAddress != "127.0.0.1";
        if (_nodeLogBox.IsHandleCreated)
        {
            string content = ReadNodeLogTail();
            if (!string.Equals(content, _nodeLogBox.Text, StringComparison.Ordinal))
            {
                _nodeLogBox.Text = content;
                _nodeLogBox.SelectionStart = _nodeLogBox.TextLength;
                _nodeLogBox.ScrollToCaret();
            }
        }
    }

    private static List<string> GetPrivateIpv4Addresses()
    {
        var addresses = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                {
                    IPAddress address = item.Address;
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) continue;
                    byte[] bytes = address.GetAddressBytes();
                    bool privateAddress = bytes[0] == 10 ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                        (bytes[0] == 192 && bytes[1] == 168);
                    if (privateAddress) addresses.Add(address.ToString());
                }
            }
        }
        catch { }
        return addresses.ToList();
    }

    private string ReadNodeLogTail()
    {
        if (!File.Exists(AppPaths.LogPath)) return "Журнал появится после запуска ноды.";
        try
        {
            using var stream = new FileStream(AppPaths.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            const int maxBytes = 96 * 1024;
            if (stream.Length > maxBytes) stream.Seek(-maxBytes, SeekOrigin.End);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string[] lines = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(Environment.NewLine, lines.TakeLast(300));
        }
        catch (IOException) { return "Не удалось прочитать журнал. Попробуйте открыть файл отдельно."; }
    }

    private void OpenNodeLog()
    {
        Directory.CreateDirectory(AppPaths.DataRoot);
        if (!File.Exists(AppPaths.LogPath)) File.WriteAllText(AppPaths.LogPath, "Журнал GeoCoin-Start" + Environment.NewLine);
        Process.Start(new ProcessStartInfo { FileName = AppPaths.LogPath, UseShellExecute = true });
    }

    private void CopyMinerConnectionProfile()
    {
        if (!IsNodeRunning() || _rpcBindAddress == "127.0.0.1" || string.IsNullOrWhiteSpace(_minerPassword))
        {
            MessageBox.Show(this, "Для подключения другого ПК сначала запустите ноду с частным IP на странице «Нода». Не публикуйте RPC в интернете.",
                "Нет удалённого доступа", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string profile = $"RPC URL: http://{_rpcBindAddress}:3033/{Environment.NewLine}" +
                         $"Пользователь майнера: {_minerRpcUsername}{Environment.NewLine}" +
                         $"Пароль майнера: {_minerPassword}{Environment.NewLine}" +
                         "Вставьте эти данные в GeoCoin-Start → Подмайнер на другом компьютере.";
        Clipboard.SetText(profile);
        MessageBox.Show(this, "Данные роли майнера скопированы. Передавайте их только доверенному компьютеру в своей LAN/VPN.",
            "Профиль скопирован", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BuildSendPage()
    {
        int cardWidth = PageCardWidth(700);
        int fieldWidth = cardWidth - 60;
        AddParagraph("Введите адрес получателя и сумму. Перед отправкой приложение покажет итоговое подтверждение.", 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, 424);
        AddLabel(card, "Адрес получателя", 26, 22, fieldWidth, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAddress, new Point(26, 51), new Size(fieldWidth, 36));
        _sendAddress.PlaceholderText = "Адрес GeoCoin получателя";
        card.Controls.Add(_sendAddress);

        AddLabel(card, "Сумма, GC", 26, 112, 200, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAmount, new Point(26, 141), new Size(Math.Min(220, fieldWidth), 36));
        _sendAmount.PlaceholderText = "Например, 1,25";
        card.Controls.Add(_sendAmount);

        AddLabel(card, "Пароль кошелька", 26, 198, 220, 22, Ink, 11, bold: true);
        StyleTextBox(_sendPassword, new Point(26, 227), new Size(Math.Min(320, fieldWidth), 36));
        _sendPassword.UseSystemPasswordChar = true;
        card.Controls.Add(_sendPassword);

        AddLabel(card, "Комиссия рассчитается автоматически. Отправленную транзакцию нельзя отменить.",
            26, 282, fieldWidth, 44, Muted, 9.5f);
        _sendButton.Text = "Проверить и отправить";
        StylePrimaryButton(_sendButton, new Point(26, 344), new Size(228, 42));
        _sendButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendButton);

        _sendMessage.Location = UiPoint(26, 390);
        _sendMessage.Size = UiSize(fieldWidth, 24);
        _sendMessage.ForeColor = Muted;
        card.Controls.Add(_sendMessage);
    }

    private void BuildReceivePage()
    {
        int cardWidth = PageCardWidth(700);
        int fieldWidth = cardWidth - 60;
        AddParagraph("Создайте новый адрес для получения монет и отправьте его человеку, который переводит вам GeoCoin.", 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, 286);
        AddLabel(card, "ВАШ АДРЕС ДЛЯ ПОЛУЧЕНИЯ", 26, 22, fieldWidth, 22, Muted, 9, bold: true);
        _receiveAddress.Text = string.IsNullOrWhiteSpace(_lastAddress) ? "Новый адрес появится здесь" : _lastAddress;
        _receiveAddress.Location = UiPoint(26, 59);
        _receiveAddress.Size = UiSize(fieldWidth, 70);
        _receiveAddress.AutoEllipsis = true;
        _receiveAddress.ForeColor = Ink;
        _receiveAddress.Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold);
        _receiveAddress.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_receiveAddress);

        _receiveButton.Text = "Создать адрес";
        StylePrimaryButton(_receiveButton, new Point(26, 152), new Size(180, 42));
        _receiveButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_receiveButton);

        var copy = new Button { Text = "Копировать адрес", Location = new Point(222, 152), Size = new Size(180, 42) };
        StyleSecondaryButton(copy, copy.Location, copy.Size);
        copy.Click += (_, _) => CopyReceiveAddress();
        card.Controls.Add(copy);

        _receiveMessage.Text = "Новый адрес можно использовать для одного или нескольких переводов.";
        _receiveMessage.Location = UiPoint(26, 219);
        _receiveMessage.Size = UiSize(fieldWidth, 32);
        _receiveMessage.ForeColor = Muted;
        card.Controls.Add(_receiveMessage);

        if (!IsNodeRunning()) AddParagraph("Сначала запустите ноду на странице «Обзор».", 24, 394, cardWidth - 48, 32);
        else if (!File.Exists(AppPaths.WalletPath)) AddParagraph("Сначала создайте кошелёк на странице «Обзор».", 24, 394, cardWidth - 48, 32);
    }

    private void BuildTransactionsPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("История входящих переводов, отправок и наград за майнинг.", 22, 12, cardWidth - 130, 38);
        _refreshTransactionsButton.Text = "Обновить";
        StyleSecondaryButton(_refreshTransactionsButton, new Point(cardWidth - 112, 8), new Size(88, 34));
        _refreshTransactionsButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath) && !_loadingTransactions;
        _pagePanel.Controls.Add(_refreshTransactionsButton);

        Panel card = AddCard(22, 58, cardWidth, 550);
        _transactionGrid = new DataGridView
        {
            Location = UiPoint(14, 14),
            Size = UiSize(cardWidth - 30, 410),
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(226, 226, 220),
            EnableHeadersVisualStyles = false,
            Font = new Font("Segoe UI", 9),
            RowTemplate = { Height = 30 }
        };
        _transactionGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 241, 235);
        _transactionGrid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        _transactionGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "date", HeaderText = "Дата", FillWeight = 125 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Операция", FillWeight = 92 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "amount", HeaderText = "Сумма", FillWeight = 105 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "fee", HeaderText = "Комиссия", FillWeight = 78 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Состояние", FillWeight = 112 });
        _transactionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "address", HeaderText = "Адрес", FillWeight = 178 });
        _transactionGrid.SelectionChanged += (_, _) =>
        {
            _selectedTransactionId = _transactionGrid?.CurrentRow?.Tag as string;
            _transactionDetails.Text = string.IsNullOrWhiteSpace(_selectedTransactionId)
                ? "Выберите запись, чтобы посмотреть номер транзакции."
                : "ID транзакции: " + _selectedTransactionId;
            _copyTransactionIdButton.Enabled = !string.IsNullOrWhiteSpace(_selectedTransactionId);
        };
        card.Controls.Add(_transactionGrid);

        _transactionDetails.Text = "Выберите запись, чтобы посмотреть номер транзакции.";
        _transactionDetails.Location = UiPoint(18, 438);
        _transactionDetails.Size = UiSize(cardWidth - 170, 42);
        _transactionDetails.ForeColor = Muted;
        _transactionDetails.AutoEllipsis = true;
        card.Controls.Add(_transactionDetails);

        _copyTransactionIdButton.Text = "Копировать ID";
        StyleSecondaryButton(_copyTransactionIdButton, new Point(cardWidth - 154, 438), new Size(132, 36));
        _copyTransactionIdButton.Enabled = false;
        card.Controls.Add(_copyTransactionIdButton);

        _transactionStatus.Text = !IsNodeRunning()
            ? "Сначала запустите ноду на странице «Обзор»."
            : !File.Exists(AppPaths.WalletPath)
                ? "Создайте или импортируйте кошелёк, чтобы увидеть историю."
                : "Запрашиваю историю кошелька…";
        _transactionStatus.Location = UiPoint(18, 496);
        _transactionStatus.Size = UiSize(cardWidth - 36, 28);
        _transactionStatus.ForeColor = Muted;
        _transactionStatus.AutoEllipsis = true;
        card.Controls.Add(_transactionStatus);
    }

    private void BuildWalletPage()
    {
        int cardWidth = PageCardWidth(700);
        int contentWidth = cardWidth - 48;
        AddParagraph("Резервная копия сохраняет весь кошелёк. Приватный ключ относится только к одному адресу.", 22, 12, cardWidth, 42);
        Panel card = AddCard(22, 66, cardWidth, 520);

        AddLabel(card, "Полная копия кошелька", 24, 18, contentWidth, 24, Ink, 13, bold: true);
        AddLabel(card, "Файл .dat включает весь текущий кошелёк и подходит для его восстановления.",
            24, 48, contentWidth, 38, Muted, 9.5f);
        _walletFileExportButton.Text = "Экспортировать весь кошелёк (.dat)";
        StylePrimaryButton(_walletFileExportButton, new Point(24, 88), new Size(268, 40));
        _walletFileExportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_walletFileExportButton);

        var divider = new Panel
        {
            Location = UiPoint(24, 145),
            Size = UiSize(contentWidth, 1),
            BackColor = Color.FromArgb(220, 220, 214)
        };
        card.Controls.Add(divider);

        AddLabel(card, "Приватный ключ одного адреса", 24, 162, contentWidth, 24, Ink, 13, bold: true);
        AddLabel(card,
            "В GeoCoin нет мнемонической фразы. Приватный ключ из 64 hex-символов открывает один адрес.",
            24, 191, contentWidth, 44, Muted, 9.5f);

        AddLabel(card, "Адрес для экспорта ключа", 24, 240, contentWidth, 20, Ink, 10, bold: true);
        _walletAddressCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _walletAddressCombo.Location = UiPoint(24, 264);
        _walletAddressCombo.Size = UiSize(Math.Max(260, cardWidth - 160), 34);
        _walletAddressCombo.Font = new Font("Segoe UI", 9.5f);
        _walletAddressCombo.Enabled = !_loadingWalletAddresses;
        card.Controls.Add(_walletAddressCombo);

        _refreshWalletAddressesButton.Text = "Обновить";
        StyleSecondaryButton(_refreshWalletAddressesButton, new Point(cardWidth - 112, 262), new Size(88, 36));
        _refreshWalletAddressesButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath) && !_loadingWalletAddresses;
        card.Controls.Add(_refreshWalletAddressesButton);

        _walletKeyExportButton.Text = "Сохранить ключ выбранного адреса";
        StyleSecondaryButton(_walletKeyExportButton, new Point(24, 306), new Size(268, 38));
        _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        card.Controls.Add(_walletKeyExportButton);

        AddLabel(card, "Импорт ключа в текущий кошелёк", 24, 358, contentWidth, 20, Ink, 10, bold: true);
        StyleTextBox(_privateKeyInput, new Point(24, 382), new Size(Math.Max(230, cardWidth - 300), 36));
        _privateKeyInput.PlaceholderText = "64 шестнадцатеричных символа";
        _privateKeyInput.UseSystemPasswordChar = true;
        card.Controls.Add(_privateKeyInput);

        _walletKeyImportButton.Text = "Импортировать ключ";
        StylePrimaryButton(_walletKeyImportButton, new Point(cardWidth - 240, 380), new Size(216, 40));
        _walletKeyImportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_walletKeyImportButton);

        _walletKeyStatus.Text = "Ключи — секретные данные. Не отправляйте их другим людям и не храните в общедоступных файлах.";
        _walletKeyStatus.Location = UiPoint(24, 440);
        _walletKeyStatus.Size = UiSize(contentWidth, 48);
        _walletKeyStatus.ForeColor = Muted;
        card.Controls.Add(_walletKeyStatus);
    }

    private async Task StartNodeAsync(bool offerWalletCreation = true, string? suppliedWalletPassword = null)
    {
        if (_startingNode) return;
        if (_remoteGpuMining || _startingRemoteGpu)
        {
            MessageBox.Show(this, "Сначала остановите GPU-подмайнер. В этом режиме приложение подключено к ноде на другом компьютере.",
                "Сначала остановите подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.NodePath))
        {
            MessageBox.Show(this, "Файл ноды GeoCoin не найден. Запустите установщик ещё раз.",
                "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (_nodeProcess is not null && !IsNodeProcessRunning()) CleanupNodeSession();
        if (IsNodeProcessRunning()) return;

        _minerRpcUsername = _minerRpcUserInput.Text.Trim();
        string minerPassword = _minerRpcPasswordInput.Text;
        _minerPassword = minerPassword;
        if (string.IsNullOrWhiteSpace(_minerRpcUsername) || _minerRpcUsername.Contains(':') || _minerRpcUsername.Any(char.IsWhiteSpace))
        {
            MessageBox.Show(this, "Имя miner RPC должно быть непустым и не содержать пробелов или двоеточия.",
                "Проверьте имя пользователя", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.Equals(_minerRpcUsername, "geocoin-start", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Для подмайнера задайте отдельное имя пользователя, отличное от admin RPC.",
                "Имя занято", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (minerPassword.Length < 16 || minerPassword.Contains('\r') || minerPassword.Contains('\n'))
        {
            MessageBox.Show(this, "Задайте для miner RPC пароль не короче 16 символов без переводов строки.",
                "Проверьте пароль", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_rpcBindAddress != "127.0.0.1" && !GetPrivateIpv4Addresses().Contains(_rpcBindAddress, StringComparer.Ordinal))
        {
            MessageBox.Show(this, "Выбранный сетевой адрес больше не активен. Откройте страницу «Нода», выберите актуальный частный IP и запустите снова.",
                "Обновите адрес RPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _rpcBindAddress = "127.0.0.1";
            return;
        }
        if (_rpcBindAddress != "127.0.0.1" && MessageBox.Show(this,
                $"Открыть miner RPC на частном адресе {_rpcBindAddress}:3033?\n\n" +
                "Продолжайте только в доверенной локальной сети или VPN. Ограничьте порт 3033 в брандмауэре и не настраивайте проброс порта из интернета.",
                "Разрешить подключение подмайнера", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string? walletPassword = null;
        bool existingWallet = File.Exists(AppPaths.WalletPath);
        if (existingWallet)
        {
            walletPassword = suppliedWalletPassword ?? PasswordDialog.AskToUnlock(this);
            if (walletPassword is null) return;
        }

        string sessionDirectory = Path.Combine(Path.GetTempPath(), "geocoin-start-" + Guid.NewGuid().ToString("N"));
        _startingNode = true;
        _nodeStatus = "Запускаю ноду…";
        _rpcHealthy = false;
        UpdateStatus();
        BuildPage();
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            Directory.CreateDirectory(sessionDirectory);
            _adminPassword = RandomSecret();
            _sessionDirectory = sessionDirectory;
            string adminFile = Path.Combine(sessionDirectory, "rpc-admin.pass");
            _minerPassFile = Path.Combine(sessionDirectory, "rpc-miner.pass");
            string? walletFile = existingWallet ? Path.Combine(sessionDirectory, "wallet.pass") : null;
            var secretEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(adminFile, _adminPassword + Environment.NewLine, secretEncoding);
            await File.WriteAllTextAsync(_minerPassFile, minerPassword + Environment.NewLine, secretEncoding);
            if (walletFile is not null) await File.WriteAllTextAsync(walletFile, walletPassword + Environment.NewLine, secretEncoding);

            var start = new ProcessStartInfo
            {
                FileName = AppPaths.NodePath,
                WorkingDirectory = AppPaths.OfficialDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            AddArgument(start, "--data-dir", AppPaths.DataRoot);
            AddArgument(start, "--network", "degree");
            start.ArgumentList.Add("--rpc");
            AddArgument(start, "--rpc-bind", _rpcBindAddress);
            if (_rpcBindAddress != "127.0.0.1")
                start.Environment["GEOCOIN_ALLOW_PUBLIC_RPC_BIND"] = "1";
            AddArgument(start, "--rpc-port", "3033");
            AddArgument(start, "--rpc-user", "geocoin-start");
            AddArgument(start, "--rpc-pass-file", adminFile);
            AddArgument(start, "--rpc-miner-user", _minerRpcUsername);
            AddArgument(start, "--rpc-miner-pass-file", _minerPassFile);
            AddArgument(start, "--activation-manifest", AppPaths.ProductionManifestPath);
            AddArgument(start, "--status-interval", "30");
            if (walletFile is not null) AddArgument(start, "--wallet-password-file", walletFile);

            _logWriter?.Dispose();
            _logWriter = new StreamWriter(new FileStream(AppPaths.LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            { AutoFlush = true };
            _nodeProcess = new Process { StartInfo = start, EnableRaisingEvents = true };
            _nodeProcess.OutputDataReceived += (_, e) => WriteNodeLog(e.Data);
            _nodeProcess.ErrorDataReceived += (_, e) => WriteNodeLog(e.Data);
            if (!_nodeProcess.Start()) throw new InvalidOperationException("Windows не запустил ноду GeoCoin.");
            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            _rpc?.Dispose();
            string localRpcEndpoint = $"http://{_rpcBindAddress}:3033/";
            _rpc = new GeoCoinRpcClient("geocoin-start", _adminPassword, localRpcEndpoint);
            _minerRpc?.Dispose();
            _minerRpc = new GeoCoinRpcClient(_minerRpcUsername, _minerPassword!, localRpcEndpoint);

            bool ready = false;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (!IsNodeProcessRunning())
                    throw new InvalidOperationException("Нода завершила запуск с ошибкой. Подробности записаны в файл журнала.");
                try
                {
                    await _rpc.CallAsync("help");
                    ready = true;
                    _rpcHealthy = true;
                    break;
                }
                catch { await Task.Delay(1000); }
            }
            if (!ready) throw new TimeoutException("Нода запускается дольше обычного. Проверьте файл журнала и попробуйте ещё раз.");

            _nodeStatus = "Нода подключена";
            _refreshTimer.Start();
            await RefreshStateAsync();
            if (!existingWallet && offerWalletCreation)
            {
                var result = MessageBox.Show(this,
                    "Нода подключена. Создать новый защищённый кошелёк?\n\n" +
                    "Если у вас уже есть кошелёк GeoCoin, сначала остановите ноду и восстановите резервную копию. Не создавайте новый поверх старого файла.",
                    "Первый запуск", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes) await CreateWalletAsync();
            }
        }
        catch (Exception ex)
        {
            if (IsNodeProcessRunning() && _nodeProcess is Process failedNode)
            {
                try { failedNode.Kill(entireProcessTree: true); await failedNode.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); } catch { }
            }
            bool stopped = !IsNodeProcessRunning();
            if (stopped)
            {
                if (_nodeProcess is not null) CleanupNodeSession();
                else
                {
                    try { Directory.Delete(sessionDirectory, recursive: true); } catch { }
                    _sessionDirectory = null;
                    _minerPassFile = null;
                    _minerPassword = null;
                    _adminPassword = null;
                }
                _nodeStatus = "Нода не подключена";
                _rpcHealthy = false;
            }
            else _nodeStatus = "Нода продолжает работать";
            MessageBox.Show(this, ex.Message + "\n\nЖурнал: " + AppPaths.LogPath,
                "Не удалось запустить GeoCoin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _startingNode = false;
        }
        UpdateStatus();
        BuildPage();
    }

    private async Task StopNodeAsync()
    {
        if (!IsNodeProcessRunning()) return;
        _nodeStatus = "Останавливаю ноду…";
        UpdateStatus();
        try
        {
            if (_gpuMiningActive || _gpuMinerProcess is not null) await StopGpuMinerAsync();
            if (_cpuMiningActive && _rpc is not null)
            {
                try { await _rpc.CallAsync("setgenerate", new object[] { false }); } catch { }
                _cpuMiningActive = false;
            }
            if (_rpc is not null) await _rpc.CallAsync("stop");
            for (int i = 0; i < 200 && IsNodeProcessRunning(); i++) await Task.Delay(100);
            if (IsNodeProcessRunning())
                throw new TimeoutException("Нода ещё завершает работу. Подождите немного и повторите.");
            await StopGpuMinerAsync();
            CleanupNodeSession();
            _nodeStatus = "Нода остановлена";
            _rpcHealthy = false;
            _balance = 0;
            _height = 0;
            _headers = 0;
            _connections = 0;
        }
        catch (Exception ex)
        {
            _nodeStatus = "Нода продолжает работать";
            MessageBox.Show(this, ex.Message + "\n\nОставьте GeoCoin-Start открытым и попробуйте остановить ноду ещё раз.",
                "GeoCoin ещё работает", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        UpdateStatus();
    }

    private async Task CreateWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (File.Exists(AppPaths.WalletPath))
        {
            MessageBox.Show(this, "В папке GeoCoin уже есть кошелёк. Он не будет перезаписан.",
                "Кошелёк уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? password = PasswordDialog.AskToCreate(this);
        if (password is null) return;
        try
        {
            JsonElement result = await _rpc.CallAsync("createwallet", new[] { password });
            _lastAddress = result.TryGetProperty("address", out JsonElement address) ? address.GetString() : null;
            _receiveMessage.Text = "Кошелёк создан. Сейчас сделайте резервную копию на странице «Обзор».";
            MessageBox.Show(this,
                "Кошелёк создан и защищён паролем.\n\n" +
                "Сначала сохраните пароль, затем создайте зашифрованную резервную копию. Храните копию отдельно от компьютера.",
                "Кошелёк готов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось создать кошелёк", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task BackupWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null || !File.Exists(AppPaths.WalletPath)) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить резервную копию",
            FileName = "GeoCoin-wallet-backup.dat",
            Filter = "Файл резервной копии (*.dat)|*.dat",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (File.Exists(dialog.FileName))
        {
            MessageBox.Show(this, "Выберите новый файл. Существующую резервную копию приложение не перезаписывает.",
                "Файл уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            await _rpc.CallAsync("backupwallet", new[] { dialog.FileName });
            MessageBox.Show(this, "Копия кошелька сохранена. Храните её отдельно от компьютера; для защищённого файла также сохраните пароль.",
                "Копия готова", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось создать копию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ImportWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл кошелька GeoCoin",
            Filter = "Файлы GeoCoin (*.dat;*.wallet;*.bin)|*.dat;*.wallet;*.bin|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this,
                "Перед импортом закройте оригинальный клиент GeoCoin, если выбранный кошелёк открыт в нём. Исходный файл останется без изменений.",
                "Импорт кошелька", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
            return;

        string sourcePath = Path.GetFullPath(dialog.FileName);
        if (string.Equals(sourcePath, Path.GetFullPath(AppPaths.WalletPath), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Этот файл уже выбран как текущий кошелёк.", "Кошелёк уже открыт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? sourcePassword = PasswordDialog.AskToUnlock(this);
        if (sourcePassword is null) return;

        bool replacingWallet = File.Exists(AppPaths.WalletPath);
        string? preservedWallet = null;
        string? recoveryCopy = null;
        bool stoppedForImport = false;
        bool imported = false;
        if (replacingWallet && MessageBox.Show(this,
                "Текущий кошелёк будет заменён. Перед этим GeoCoin-Start создаст его резервную копию и сохранит исходный файл отдельно. Продолжить?",
                "Импортировать другой кошелёк", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            Directory.CreateDirectory(AppPaths.WalletBackupDirectory);
            if (replacingWallet)
            {
                recoveryCopy = UniqueWalletBackupPath("до-импорта");
                await _rpc.CallAsync("backupwallet", new[] { recoveryCopy });
            }

            await StopNodeAsync();
            if (IsNodeRunning()) throw new InvalidOperationException("Не удалось безопасно остановить ноду GeoCoin.");
            stoppedForImport = true;
            if (replacingWallet)
            {
                preservedWallet = UniqueWalletBackupPath("старый-кошелёк");
                File.Move(AppPaths.WalletPath, preservedWallet);
            }

            // Open the selected wallet file with the same passphrase-based loader used by the official client.
            File.Copy(sourcePath, AppPaths.WalletPath, overwrite: false);
            await StartNodeAsync(offerWalletCreation: false, suppliedWalletPassword: sourcePassword);
            if (!IsNodeRunning()) throw new InvalidOperationException("GeoCoin не смог открыть этот файл. Проверьте пароль и выберите файл кошелька из оригинального клиента.");
            imported = true;
            await RefreshStateAsync();
            MessageBox.Show(this,
                "Кошелёк импортирован и открыт. Исходный файл не изменён. История операций будет синхронизирована с сетью.",
                "Кошелёк импортирован", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            string? failedImport = null;
            if (stoppedForImport && !imported)
            {
                try
                {
                    if (IsNodeRunning()) await StopNodeAsync();
                    if (File.Exists(AppPaths.WalletPath))
                    {
                        failedImport = UniqueWalletBackupPath("неудачный-импорт");
                        File.Move(AppPaths.WalletPath, failedImport);
                    }
                    if (!string.IsNullOrEmpty(preservedWallet) && File.Exists(preservedWallet))
                        File.Move(preservedWallet, AppPaths.WalletPath);
                    await StartNodeAsync(offerWalletCreation: false);
                }
                catch (Exception rollbackError)
                {
                    MessageBox.Show(this,
                        "Импорт не завершился. Исходный кошелёк сохранён здесь:\n" + (preservedWallet ?? recoveryCopy ?? "") +
                        "\n\n" + rollbackError.Message,
                        "Проверьте файл кошелька", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            MessageBox.Show(this,
                "Не удалось импортировать файл кошелька. Убедитесь, что выбран файл GeoCoin и введён его пароль.\n\n" + ex.Message +
                (recoveryCopy is null ? "" : "\n\nРезервная копия прежнего кошелька сохранена: " + recoveryCopy) +
                (failedImport is null ? "" : "\n\nНеудачная копия сохранена отдельно: " + failedImport),
                "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string UniqueWalletBackupPath(string reason)
        => Path.Combine(AppPaths.WalletBackupDirectory,
            $"wallet-{reason}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dat");

    private async Task RefreshGpuDevicesAsync()
    {
        if (_checkingGpuDevices || _gpuMiningActive) return;
        _checkingGpuDevices = true;
        if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            _gpuStatusText = "GPU-майнер не найден. Переустановите GeoCoin-Start.";
            _gpuDevicesScanned = true;
            _checkingGpuDevices = false;
            if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
            return;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = AppPaths.GpuMinerPath,
                WorkingDirectory = AppPaths.OfficialDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("--list-devices");
            start.ArgumentList.Add("--stats-json");
            using var process = new Process { StartInfo = start };
            if (!process.Start()) throw new InvalidOperationException("Windows не запустил проверку видеокарт.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch
            {
                try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } catch { }
                throw;
            }
            string output = await stdout;
            string error = await stderr;
            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Не удалось проверить видеокарты." : error.Trim());

            _gpuDevices.Clear();
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line);
                    JsonElement root = document.RootElement;
                    if (!root.TryGetProperty("devices", out JsonElement devices) || devices.ValueKind != JsonValueKind.Array) continue;
                    foreach (JsonElement device in devices.EnumerateArray())
                    {
                        string backend = device.TryGetProperty("backend", out JsonElement backendValue) ? backendValue.GetString() ?? "" : "";
                        string id = device.TryGetProperty("id", out JsonElement idValue)
                            ? idValue.ValueKind == JsonValueKind.String ? idValue.GetString() ?? "0" : idValue.ToString()
                            : "0";
                        string name = device.TryGetProperty("name", out JsonElement nameValue) ? nameValue.GetString() ?? "Видеокарта" : "Видеокарта";
                        if (!string.IsNullOrWhiteSpace(backend)) _gpuDevices.Add(new GpuDevice(backend, id, name));
                    }
                }
                catch (JsonException) { }
            }
            _gpuDevices.Sort((left, right) => string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase));
            _gpuDevicesScanned = true;
            _gpuStatusText = _gpuDevices.Count == 0
                ? "Совместимая видеокарта не найдена. Проверьте драйверы."
                : $"Найдено видеокарт: {_gpuDevices.Count}.";
        }
        catch (Exception ex)
        {
            _gpuDevicesScanned = true;
            _gpuStatusText = "Не удалось проверить видеокарту.";
            if (_currentPage == "mining" || _currentPage == "subminer")
                MessageBox.Show(this, ex.Message, "Проверка GPU", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _checkingGpuDevices = false;
            if (_currentPage == "mining" || _currentPage == "subminer") BuildPage();
        }
    }

    private async Task ToggleCpuMiningAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (_cpuMiningActive)
        {
            try
            {
                await _rpc.CallAsync("setgenerate", new object[] { false });
                _cpuMiningActive = false;
                _cpuHashrateValue = 0;
                UpdateMiningSessionState();
                BuildPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Не удалось остановить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        string warning = $"Запустить майнинг на {(int)_threadCount.Value} потоках процессора?\n\n" +
                         "Это увеличит нагрузку, нагрев и расход электричества. Если GPU-майнинг уже запущен, обе нагрузки будут работать одновременно.";
        if (MessageBox.Show(this, warning, "Начать майнинг", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            await _rpc.CallAsync("setgenerate", new object[] { true, (int)_threadCount.Value });
            _cpuMiningActive = true;
            UpdateMiningSessionState();
            BuildPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось запустить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleGpuMiningAsync()
    {
        if (_remoteGpuMining)
        {
            SetPage("subminer");
            return;
        }
        if (_gpuMiningActive || _gpuMinerProcess is not null)
        {
            await StopGpuMinerAsync();
            BuildPage();
            return;
        }
        await StartGpuMiningAsync();
    }

    private async Task StartGpuMiningAsync()
    {
        if (_remoteGpuMining)
        {
            MessageBox.Show(this, "Сейчас GPU-процесс подключён к удалённой ноде. Управляйте им на странице «Подмайнер».",
                "Работает подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!IsNodeRunning() || _rpc is null || _minerPassFile is null || !File.Exists(_minerPassFile)) return;
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            MessageBox.Show(this, "GPU-майнер не найден. Переустановите GeoCoin-Start, чтобы получить официальный пакет целиком.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_gpuDeviceCombo.SelectedItem is not GpuDevice device)
        {
            MessageBox.Show(this, "Сначала нажмите «Найти» и выберите видеокарту.",
                "Видеокарта не выбрана", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string warning = $"Начать майнинг на видеокарте «{device.Name}»?\n\n" +
                         "Видеокарта будет работать с высокой нагрузкой и может нагреваться. Награда поступит на существующий адрес текущего кошелька.";
        if (MessageBox.Show(this, warning, "Начать GPU-майнинг", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            string address = await GetExistingMiningAddressAsync();

            var start = new ProcessStartInfo
            {
                FileName = AppPaths.GpuMinerPath,
                WorkingDirectory = AppPaths.OfficialDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            AddArgument(start, "--url", $"http://{_rpcBindAddress}:3033/");
            AddArgument(start, "--rpc-user", _minerRpcUsername);
            AddArgument(start, "--rpc-pass-file", _minerPassFile);
            AddArgument(start, "--address", address);
            AddArgument(start, "--backend", device.Backend);
            AddArgument(start, "--devices", device.Id);
            start.ArgumentList.Add("--continuous");
            start.ArgumentList.Add("--control-stdin");
            start.ArgumentList.Add("--stats-json");

            var miner = new Process { StartInfo = start, EnableRaisingEvents = true };
            miner.OutputDataReceived += (_, e) => HandleGpuMinerLine(e.Data, remote: false);
            miner.ErrorDataReceived += (_, e) => WriteNodeLog("[GPU] " + e.Data);
            miner.Exited += (_, _) =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)(() =>
                    {
                        if (!ReferenceEquals(_gpuMinerProcess, miner)) return;
                        _gpuMinerProcess = null;
                        _gpuMiningActive = false;
                        _remoteGpuMining = false;
                        UpdateMiningSessionState();
                        DeleteRemoteMinerPassFile();
                        _gpuStatusText = "GPU-майнинг завершён.";
                        _gpuHashrateText = "Скорость GPU: —";
                        if (!IsDisposed && (_currentPage == "mining" || _currentPage == "subminer")) BuildPage();
                        if (!IsDisposed && _currentPage == "node") UpdateNodePage();
                    }));
                }
                catch (InvalidOperationException) { }
            };
            if (!miner.Start()) throw new InvalidOperationException("Windows не запустил GPU-майнер.");
            _gpuMinerProcess = miner;
            if (miner.HasExited)
            {
                _gpuMinerProcess = null;
                throw new InvalidOperationException("GPU-майнер завершился сразу после запуска. Проверьте драйвер видеокарты.");
            }
            _gpuMiningActive = true;
            UpdateMiningSessionState();
            miner.BeginOutputReadLine();
            miner.BeginErrorReadLine();
            _lastAddress = address;
            _gpuStatusText = "GPU-майнинг запущен. Ожидаю данные о скорости…";
            _gpuHashrateText = "Скорость GPU: ожидание данных…";
            BuildPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось запустить GPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleRemoteGpuMiningAsync()
    {
        if (_startingRemoteGpu) return;
        if (_remoteGpuMining && _gpuMinerProcess is not null)
        {
            await StopGpuMinerAsync();
            BuildPage();
            return;
        }
        if (_gpuMinerProcess is { HasExited: false })
        {
            MessageBox.Show(this, "GPU-майнер уже запущен. Остановите текущий процесс перед сменой режима.",
                "GPU уже занят", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (IsNodeProcessRunning())
        {
            MessageBox.Show(this, "Сначала остановите локальную ноду. Подмайнер работает отдельно и подключается к ноде на другом ПК.",
                "Остановите локальную ноду", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            MessageBox.Show(this, "Официальный GPU-майнер не найден. Переустановите GeoCoin-Start.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Uri.TryCreate(_remoteNodeUrlInput.Text.Trim(), UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            MessageBox.Show(this, "Введите полный адрес RPC-ноды, например http://192.168.1.10:3033/. Не добавляйте имя пользователя или пароль в адрес.",
                "Проверьте адрес ноды", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string username = _remoteMinerUserInput.Text.Trim();
        string password = _remoteMinerPasswordInput.Text;
        string address = _remoteRewardAddressInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Contains(':') || username.Any(char.IsWhiteSpace) || password.Length < 16 || string.IsNullOrWhiteSpace(address))
        {
            MessageBox.Show(this, "Укажите имя miner RPC без пробелов, пароль не короче 16 символов и публичный адрес для награды.",
                "Заполните данные подмайнера", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_remoteGpuDeviceCombo.SelectedItem is not GpuDevice device)
        {
            MessageBox.Show(this, "Нажмите «Найти» и выберите видеокарту.", "Видеокарта не выбрана",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this,
                "Подключить видеокарту к удалённой RPC-ноде?\n\n" +
                "Передача miner RPC-пароля по обычному HTTP не шифруется. Продолжайте только в своей доверенной локальной сети или VPN. Никогда не вводите здесь admin RPC пароль или пароль кошелька.",
                "Запустить GPU-подмайнер", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string? passFile = null;
        _startingRemoteGpu = true;
        _gpuStatusText = "Подготавливаю подключение к удалённой ноде…";
        BuildPage();
        try
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "geocoin-start-worker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            passFile = Path.Combine(tempDirectory, "rpc-miner.pass");
            await File.WriteAllTextAsync(passFile, password + Environment.NewLine, new UTF8Encoding(false));
            var start = new ProcessStartInfo
            {
                FileName = AppPaths.GpuMinerPath,
                WorkingDirectory = AppPaths.OfficialDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };
            AddArgument(start, "--url", endpoint.ToString());
            AddArgument(start, "--rpc-user", username);
            AddArgument(start, "--rpc-pass-file", passFile);
            AddArgument(start, "--address", address);
            AddArgument(start, "--backend", device.Backend);
            AddArgument(start, "--devices", device.Id);
            start.ArgumentList.Add("--continuous");
            start.ArgumentList.Add("--control-stdin");
            start.ArgumentList.Add("--stats-json");

            var miner = new Process { StartInfo = start, EnableRaisingEvents = true };
            miner.OutputDataReceived += (_, e) => HandleGpuMinerLine(e.Data, remote: true);
            miner.ErrorDataReceived += (_, e) => WriteNodeLog("[GPU-подмайнер] " + e.Data);
            miner.Exited += (_, _) =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)(() =>
                    {
                        if (!ReferenceEquals(_gpuMinerProcess, miner)) return;
                        _gpuMinerProcess = null;
                        _gpuMiningActive = false;
                        _remoteGpuMining = false;
                        UpdateMiningSessionState();
                        DeleteRemoteMinerPassFile();
                        _gpuStatusText = "GPU-подмайнер отключён.";
                        _gpuHashrateText = "Скорость GPU: —";
                        if (!IsDisposed && (_currentPage == "subminer" || _currentPage == "mining")) BuildPage();
                        if (!IsDisposed && _currentPage == "node") UpdateNodePage();
                    }));
                }
                catch (InvalidOperationException) { }
            };
            if (!miner.Start()) throw new InvalidOperationException("Windows не запустил GPU-подмайнер.");
            _gpuMinerProcess = miner;
            _remoteMinerPassFile = passFile;
            passFile = null;
            if (miner.HasExited)
            {
                _gpuMinerProcess = null;
                DeleteRemoteMinerPassFile();
                _remoteGpuMining = false;
                throw new InvalidOperationException("GPU-подмайнер сразу завершился. Проверьте адрес, miner RPC-пароль и доступность видеокарты.");
            }
            _remoteGpuMining = true;
            _gpuMiningActive = true;
            UpdateMiningSessionState();
            miner.BeginOutputReadLine();
            miner.BeginErrorReadLine();
            WriteNodeLog($"[GPU-подмайнер] Подключение к {endpoint.Host}:{endpoint.Port}, устройство {device.Name}, адрес награды {address}.");
            _gpuStatusText = $"Подключаюсь к {endpoint.Host}:{endpoint.Port}…";
            _gpuHashrateText = "Скорость GPU: ожидание данных…";
            BuildPage();
        }
        catch (Exception ex)
        {
            if (passFile is not null)
            {
                try { Directory.Delete(Path.GetDirectoryName(passFile)!, recursive: true); } catch { }
            }
            MessageBox.Show(this, ex.Message, "Не удалось запустить подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _startingRemoteGpu = false;
            if (!IsDisposed && (_currentPage == "subminer" || _currentPage == "node")) BuildPage();
        }
    }

    private void DeleteRemoteMinerPassFile()
    {
        string? passFile = _remoteMinerPassFile;
        _remoteMinerPassFile = null;
        if (string.IsNullOrWhiteSpace(passFile)) return;
        try { Directory.Delete(Path.GetDirectoryName(passFile)!, recursive: true); } catch { }
    }

    private async Task<string> GetExistingMiningAddressAsync()
    {
        if (_rpc is null) throw new InvalidOperationException("Нода GeoCoin не подключена.");
        JsonElement result = await _rpc.CallAsync("getaddressesbyaccount", new object[] { "*" });
        if (result.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Нода не вернула список адресов текущего кошелька.");
        string[] addresses = result.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (addresses.Length == 0)
            throw new InvalidOperationException("В кошельке пока нет адреса для награды. Сначала создайте адрес на странице «Получить».");
        if (string.IsNullOrWhiteSpace(_lastAddress) || !addresses.Contains(_lastAddress, StringComparer.Ordinal))
            _lastAddress = addresses[0];
        return _lastAddress;
    }

    private void HandleGpuMinerLine(string? line, bool remote)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string eventName = root.TryGetProperty("event", out JsonElement eventValue) ? eventValue.GetString() ?? "" : "";
            if (eventName == "block_accepted")
            {
                string blockId = ReadString(root, "hash");
                if (string.IsNullOrWhiteSpace(blockId)) blockId = ReadString(root, "blockhash");
                if (!string.IsNullOrWhiteSpace(blockId))
                {
                    long height = ReadInt64(root, "height");
                    string source = remote ? "remote-gpu" : "local-gpu";
                    string label = remote ? "GPU-подмайнер" : "GPU";
                    RegisterMinedBlock(new MinedBlockRecord(blockId, "", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), source));
                    WriteNodeLog($"[{label}] Нода приняла блок {height:N0}: {blockId}");
                }
            }
            else if (eventName == "error")
            {
                string message = root.TryGetProperty("message", out JsonElement msg) ? msg.GetString() ?? "Ошибка GPU-майнера" : "Ошибка GPU-майнера";
                _gpuStatusText = message;
                WriteNodeLog(($"[{(remote ? "GPU-подмайнер" : "GPU")}] ") + message);
                if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() =>
                {
                        if (_currentPage == "mining" || _currentPage == "subminer") _gpuMiningState.Text = message;
                }));
            }
            else if (eventName == "status")
            {
                string rate = "";
                if (root.TryGetProperty("hashrate", out JsonElement rateValue))
                {
                    string rawRate = rateValue.ValueKind == JsonValueKind.String ? rateValue.GetString() ?? "" : rateValue.ToString();
                    if (rateValue.ValueKind == JsonValueKind.Number && rateValue.TryGetDouble(out double numericRate))
                        rate = FormatHashrate(numericRate);
                    else if (double.TryParse(rawRate, NumberStyles.Float, CultureInfo.InvariantCulture, out numericRate))
                        rate = FormatHashrate(numericRate);
                    else rate = rawRate;
                }
                string state = root.TryGetProperty("message", out JsonElement stateValue) ? stateValue.GetString() ?? "" : "";
                _gpuStatusText = string.IsNullOrWhiteSpace(state) ? "GPU-майнинг работает" : state;
                _gpuHashrateText = string.IsNullOrWhiteSpace(rate) ? "Скорость GPU: —" : $"Скорость GPU: {rate}";
                if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed && (_currentPage == "mining" || _currentPage == "subminer"))
                    {
                        _gpuMiningState.Text = _gpuStatusText;
                        _miningHashrate.Text = _gpuHashrateText;
                    }
                }));
            }
        }
        catch (JsonException) { WriteNodeLog(remote ? "[GPU-подмайнер] " + line : "[GPU] " + line); }
        catch (InvalidOperationException) { }
    }

    private void LoadMinedBlockRecords()
    {
        try
        {
            if (!File.Exists(AppPaths.MinedBlocksPath)) return;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(AppPaths.MinedBlocksPath, Encoding.UTF8));
            List<MinedBlockRecord>? records;
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                records = JsonSerializer.Deserialize<List<MinedBlockRecord>>(document.RootElement.GetRawText());
            }
            else
            {
                MinedBlockLedger? ledger = JsonSerializer.Deserialize<MinedBlockLedger>(document.RootElement.GetRawText());
                records = ledger?.Blocks;
                if (ledger?.SessionStartedAtUnix is long sessionStartedAt && sessionStartedAt > 0)
                    _miningSessionStartedAt = DateTimeOffset.FromUnixTimeSeconds(sessionStartedAt);
                if (ledger?.SessionEndedAtUnix is long sessionEndedAt && sessionEndedAt > 0)
                    _miningSessionEndedAt = DateTimeOffset.FromUnixTimeSeconds(sessionEndedAt);
                else if (_miningSessionStartedAt.HasValue)
                    _miningSessionEndedAt = DateTimeOffset.Now;
                if (ledger?.SessionBlockIds is not null)
                    foreach (string blockId in ledger.SessionBlockIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                        _miningSessionBlockIds.Add(blockId);
            }
            if (records is null) return;
            long keepAfter = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            foreach (MinedBlockRecord record in records)
            {
                if (string.IsNullOrWhiteSpace(record.BlockId) || record.Timestamp < keepAfter || record.Timestamp <= 0 ||
                    record.Timestamp > DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds())
                    continue;
                if (_minedBlockRecords.All(item => !string.Equals(item.BlockId, record.BlockId, StringComparison.OrdinalIgnoreCase)))
                    _minedBlockRecords.Add(record);
            }
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось загрузить счётчик добытых блоков: " + ex.Message);
        }
    }

    private async Task RefreshWalletMinedBlocksAsync()
    {
        GeoCoinRpcClient? rpc = _rpc;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (rpc is null || !File.Exists(AppPaths.WalletPath) || now - _lastWalletMiningStatsPollUtc < TimeSpan.FromSeconds(20))
            return;

        _lastWalletMiningStatsPollUtc = now;
        try
        {
            JsonElement result = await rpc.CallAsync("listtransactions", new object[] { "*", 1000, 0, true });
            if (!ReferenceEquals(rpc, _rpc) || result.ValueKind != JsonValueKind.Array) return;

            lock (_minedBlocksLock)
            {
                foreach (JsonElement entry in result.EnumerateArray())
                {
                    string category = ReadString(entry, "category").ToLowerInvariant();
                    string txid = ReadString(entry, "txid");
                    string blockId = ReadString(entry, "blockhash");

                    if (category == "orphan")
                    {
                        RemoveMinedBlockLocked(blockId, txid);
                        continue;
                    }
                    if (category is not ("immature" or "generate") || string.IsNullOrWhiteSpace(txid))
                        continue;

                    long timestamp = ReadInt64(entry, "blocktime");
                    if (timestamp <= 0) timestamp = ReadInt64(entry, "time");
                    if (timestamp <= 0 || timestamp > now.AddDays(1).ToUnixTimeSeconds()) continue;
                    if (string.IsNullOrWhiteSpace(blockId)) blockId = txid;

                    UpsertMinedBlockLocked(new MinedBlockRecord(blockId, txid, timestamp, "local-wallet"));
                }

                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
            RefreshMinedBlockCountersOnUi();
        }
        catch
        {
            // The node may be starting, the wallet may be unavailable, or RPC may be reconnecting.
        }
    }

    private void RegisterMinedBlock(MinedBlockRecord record)
    {
        lock (_minedBlocksLock)
        {
            UpsertMinedBlockLocked(record);
            PruneMinedBlockRecordsLocked();
            SaveMinedBlockRecordsLocked();
        }
        RefreshMinedBlockCountersOnUi();
    }

    private void UpsertMinedBlockLocked(MinedBlockRecord record)
    {
        int existingIndex = _minedBlockRecords.FindIndex(item =>
            string.Equals(item.BlockId, record.BlockId, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            MinedBlockRecord existing = _minedBlockRecords[existingIndex];
            bool hasGpuAcceptanceTime = existing.Source is "local-gpu" or "remote-gpu";
            record = record with
            {
                TransactionId = string.IsNullOrWhiteSpace(record.TransactionId) ? existing.TransactionId : record.TransactionId,
                Timestamp = hasGpuAcceptanceTime ? existing.Timestamp : record.Timestamp,
                Source = hasGpuAcceptanceTime ? existing.Source : record.Source
            };
            _minedBlockRecords[existingIndex] = record;
        }
        else
        {
            _minedBlockRecords.Add(record);
        }

        long timestamp = record.Timestamp;
        bool belongsToSession = _miningSessionStartedAt is DateTimeOffset sessionStart &&
            timestamp >= sessionStart.ToUnixTimeSeconds() &&
            (_miningSessionActive || (_miningSessionEndedAt is DateTimeOffset sessionEnd && timestamp <= sessionEnd.ToUnixTimeSeconds()));
        if (belongsToSession)
            _miningSessionBlockIds.Add(record.BlockId);
        else if (existingIndex >= 0)
            _miningSessionBlockIds.Remove(record.BlockId);
    }

    private void RemoveMinedBlockLocked(string blockId, string transactionId)
    {
        foreach (MinedBlockRecord record in _minedBlockRecords.Where(record =>
                     (!string.IsNullOrWhiteSpace(blockId) && string.Equals(record.BlockId, blockId, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(transactionId) && string.Equals(record.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase))).ToArray())
        {
            _minedBlockRecords.Remove(record);
            _miningSessionBlockIds.Remove(record.BlockId);
        }
    }

    private void UpdateMiningSessionState()
    {
        bool changed = false;
        lock (_minedBlocksLock)
        {
            bool miningActive = _cpuMiningActive || _gpuMiningActive;
            if (miningActive && !_miningSessionActive)
            {
                _miningSessionStartedAt = DateTimeOffset.Now;
                _miningSessionEndedAt = null;
                _miningSessionBlockIds.Clear();
                _miningSessionActive = true;
                changed = true;
            }
            else if (!miningActive && _miningSessionActive)
            {
                _miningSessionActive = false;
                _miningSessionEndedAt = DateTimeOffset.Now;
                changed = true;
            }
            if (changed)
            {
                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
        }
        RefreshMinedBlockCountersOnUi();
    }

    private void PruneMinedBlockRecordsLocked()
    {
        long keepAfter = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
        _minedBlockRecords.RemoveAll(record => record.Timestamp < keepAfter &&
            (!_miningSessionActive || !_miningSessionBlockIds.Contains(record.BlockId)));
    }

    private void SaveMinedBlockRecordsLocked()
    {
        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            tempPath = AppPaths.MinedBlocksPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var ledger = new MinedBlockLedger(
                _minedBlockRecords.ToList(),
                _miningSessionStartedAt?.ToUnixTimeSeconds(),
                _miningSessionEndedAt?.ToUnixTimeSeconds(),
                _miningSessionBlockIds.ToList());
            File.WriteAllText(tempPath, JsonSerializer.Serialize(ledger), new UTF8Encoding(false));
            File.Move(tempPath, AppPaths.MinedBlocksPath, overwrite: true);
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось сохранить счётчик добытых блоков: " + ex.Message);
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private void RefreshMinedBlockCountersOnUi()
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)UpdateMinedBlockCounters); } catch (InvalidOperationException) { }
            return;
        }
        UpdateMinedBlockCounters();
    }

    private void UpdateMinedBlockCounters()
    {
        if (_minedBlocksTodayLabel.IsDisposed || _minedBlocksSessionLabel.IsDisposed) return;
        int todayCount;
        int sessionCount;
        bool sessionActive;
        bool sessionStarted;
        DateTime today = DateTime.Now.Date;
        lock (_minedBlocksLock)
        {
            todayCount = _minedBlockRecords
                .Where(record => record.Timestamp > 0 && DateTimeOffset.FromUnixTimeSeconds(record.Timestamp).ToLocalTime().Date == today)
                .Select(record => record.BlockId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            sessionCount = _miningSessionBlockIds.Count;
            sessionActive = _miningSessionActive;
            sessionStarted = _miningSessionStartedAt.HasValue;
        }

        _minedBlocksTodayLabel.Text = $"Сегодня: {FormatBlockCount(todayCount)}";
        string sessionTitle = sessionActive ? "Текущая сессия" : sessionStarted ? "Последняя сессия" : "Сессия";
        _minedBlocksSessionLabel.Text = $"{sessionTitle}: {FormatBlockCount(sessionCount)}";
    }

    private static string FormatBlockCount(int count)
    {
        long value = Math.Abs((long)count);
        long lastTwoDigits = value % 100;
        long lastDigit = value % 10;
        string noun = lastTwoDigits is >= 11 and <= 14 ? "блоков" : lastDigit switch
        {
            1 => "блок",
            >= 2 and <= 4 => "блока",
            _ => "блоков"
        };
        return $"{count:N0} {noun}";
    }

    private async Task StopGpuMinerAsync()
    {
        Process? miner = _gpuMinerProcess;
        if (miner is null)
        {
            _gpuMiningActive = false;
            _remoteGpuMining = false;
            UpdateMiningSessionState();
            DeleteRemoteMinerPassFile();
            return;
        }
        try
        {
            if (!miner.HasExited)
            {
                try
                {
                    await miner.StandardInput.WriteLineAsync("stop");
                    await miner.StandardInput.FlushAsync();
                    await miner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                }
                catch (TimeoutException) { miner.Kill(entireProcessTree: true); await miner.WaitForExitAsync(); }
                catch (InvalidOperationException) { }
            }
        }
        finally
        {
            if (ReferenceEquals(_gpuMinerProcess, miner)) _gpuMinerProcess = null;
            try { miner.Dispose(); } catch { }
            _gpuMiningActive = false;
            _remoteGpuMining = false;
            UpdateMiningSessionState();
            DeleteRemoteMinerPassFile();
            _gpuStatusText = "GPU-майнинг остановлен";
            _gpuHashrateText = "Скорость GPU: —";
        }
    }

    private async Task CreateReceiveAddressAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        string? password = PasswordDialog.AskForAction(this, "создать адрес для получения");
        if (password is null) return;
        try
        {
            var parameters = new object[] { "Получение", password };
            JsonElement result = await _rpc.CallAsync("getnewaddress", parameters);
            _lastAddress = result.ValueKind == JsonValueKind.String ? result.GetString() : result.ToString();
            _receiveMessage.Text = "Адрес создан. Нажмите «Копировать адрес» и отправьте его отправителю.";
            BuildPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось создать адрес", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyReceiveAddress()
    {
        if (string.IsNullOrWhiteSpace(_lastAddress))
        {
            MessageBox.Show(this, "Сначала создайте новый адрес.", "Адрес ещё не создан",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Clipboard.SetText(_lastAddress);
        _receiveMessage.Text = "Адрес скопирован. Теперь его можно отправить отправителю.";
    }

    private async Task RefreshTransactionsAsync()
    {
        if (_loadingTransactions || _rpc is null || _currentPage != "transactions" || _transactionGrid is null) return;
        DataGridView grid = _transactionGrid;
        _loadingTransactions = true;
        _refreshTransactionsButton.Enabled = false;
        _transactionStatus.Text = "Обновляю историю кошелька…";
        try
        {
            JsonElement result = await _rpc.CallAsync("listtransactions", new object[] { "*", 100, 0, true });
            if (!ReferenceEquals(grid, _transactionGrid) || _currentPage != "transactions") return;
            if (result.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Нода вернула историю в неизвестном формате.");

            grid.Rows.Clear();
            foreach (JsonElement entry in result.EnumerateArray())
            {
                string txid = ReadString(entry, "txid");
                string category = ReadString(entry, "category").ToLowerInvariant();
                string address = ReadString(entry, "address");
                string operation = category switch
                {
                    "receive" => "Получение",
                    "send" => "Отправка",
                    "generate" => "Майнинг",
                    "immature" => "Награда",
                    "orphan" => "Отменена",
                    _ => string.IsNullOrWhiteSpace(category) ? "Операция" : category
                };
                decimal amount = ReadDecimal(entry, "amount");
                decimal fee = ReadDecimal(entry, "fee");
                long confirmations = ReadInt64(entry, "confirmations");
                string status = category switch
                {
                    "immature" => "Ожидает зрелости",
                    "orphan" => "Отменена",
                    _ when confirmations < 0 => "Конфликт",
                    _ when confirmations == 0 => "Ожидает",
                    _ => $"{confirmations:N0} подтв."
                };
                string date = FormatTransactionTime(entry);
                int rowIndex = grid.Rows.Add(date, operation, FormatGcAmount(amount, includePositiveSign: true),
                    fee == 0 ? "—" : Math.Abs(fee).ToString("0.########", CultureInfo.CurrentCulture) + " GC", status, address);
                DataGridViewRow row = grid.Rows[rowIndex];
                row.Tag = txid;
                row.Cells[5].ToolTipText = address;
                row.Cells[2].Style.ForeColor = amount < 0 ? Color.FromArgb(161, 70, 53) : Color.FromArgb(53, 119, 74);
            }
            if (grid.Rows.Count > 0)
            {
                grid.ClearSelection();
                grid.CurrentCell = grid.Rows[0].Cells[0];
                grid.Rows[0].Selected = true;
                _selectedTransactionId = grid.Rows[0].Tag as string;
                _transactionDetails.Text = string.IsNullOrWhiteSpace(_selectedTransactionId)
                    ? "Выберите запись, чтобы посмотреть номер транзакции."
                    : "ID транзакции: " + _selectedTransactionId;
                _copyTransactionIdButton.Enabled = !string.IsNullOrWhiteSpace(_selectedTransactionId);
            }
            else
            {
                _selectedTransactionId = null;
                _transactionDetails.Text = "Выберите запись, чтобы посмотреть номер транзакции.";
                _copyTransactionIdButton.Enabled = false;
            }
            _transactionStatus.Text = grid.Rows.Count == 0
                ? "В истории пока нет операций. После синхронизации обновите список."
                : $"Показаны последние {grid.Rows.Count} записей. Разные выходы одной транзакции могут отображаться отдельно.";
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(grid, _transactionGrid))
                _transactionStatus.Text = "Не удалось загрузить историю: " + ex.Message;
        }
        finally
        {
            _loadingTransactions = false;
            if (_currentPage == "transactions" && !IsDisposed)
                _refreshTransactionsButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private void CopySelectedTransactionId()
    {
        if (string.IsNullOrWhiteSpace(_selectedTransactionId)) return;
        try
        {
            Clipboard.SetText(_selectedTransactionId);
            _transactionStatus.Text = "ID транзакции скопирован.";
        }
        catch (Exception ex)
        {
            _transactionStatus.Text = "Не удалось скопировать ID: " + ex.Message;
        }
    }

    private async Task RefreshWalletAddressesAsync()
    {
        if (_loadingWalletAddresses || _rpc is null || _currentPage != "wallet" || !File.Exists(AppPaths.WalletPath)) return;
        _loadingWalletAddresses = true;
        _refreshWalletAddressesButton.Enabled = false;
        _walletAddressCombo.Enabled = false;
        _walletKeyStatus.Text = "Загружаю адреса текущего кошелька…";
        try
        {
            string? previous = _walletAddressCombo.SelectedItem as string;
            List<string> addresses = await GetWalletAddressesAsync();
            _walletAddresses.Clear();
            _walletAddresses.AddRange(addresses);
            _walletAddressCombo.BeginUpdate();
            _walletAddressCombo.Items.Clear();
            foreach (string address in addresses) _walletAddressCombo.Items.Add(address);
            int selected = previous is null ? -1 : addresses.FindIndex(address => address == previous);
            _walletAddressCombo.SelectedIndex = selected >= 0 ? selected : (addresses.Count > 0 ? 0 : -1);
            _walletAddressCombo.EndUpdate();
            _walletKeyStatus.Text = addresses.Count == 0
                ? "В кошельке нет доступных адресов."
                : $"Найдено адресов: {addresses.Count}. Экспорт ключа создаёт незашифрованный текстовый файл.";
        }
        catch (Exception ex)
        {
            _walletAddresses.Clear();
            _walletAddressCombo.Items.Clear();
            _walletKeyExportButton.Enabled = false;
            _walletKeyStatus.Text = "Не удалось загрузить адреса: " + ex.Message;
        }
        finally
        {
            _loadingWalletAddresses = false;
            if (_currentPage == "wallet" && !IsDisposed)
            {
                _walletAddressCombo.Enabled = true;
                _refreshWalletAddressesButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
                _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
            }
        }
    }

    private async Task<List<string>> GetWalletAddressesAsync()
    {
        if (_rpc is null) throw new InvalidOperationException("Нода GeoCoin не подключена.");
        const int pageSize = 5000;
        var addresses = new List<string>();
        for (int offset = 0; offset < 100_000; offset += pageSize)
        {
            JsonElement result = await _rpc.CallAsync("getaddressesbyaccount", new object[] { "*", offset, pageSize });
            if (result.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Нода вернула список адресов в неизвестном формате.");
            int count = 0;
            foreach (JsonElement item in result.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string address = item.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(address)) addresses.Add(address);
                count++;
            }
            if (count < pageSize) break;
        }
        return addresses.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task ImportPrivateKeyAsync()
    {
        if (!IsNodeRunning() || _rpc is null || !File.Exists(AppPaths.WalletPath)) return;
        string privateKey = _privateKeyInput.Text.Trim();
        if (privateKey.Length != 64 || privateKey.Any(character => !Uri.IsHexDigit(character)))
        {
            _walletKeyStatus.Text = "Проверьте ключ: нужно ровно 64 шестнадцатеричных символа 0–9, A–F.";
            return;
        }
        if (MessageBox.Show(this,
                "Этот ключ будет добавлен в текущий кошелёк. Импорт восстановит историю адреса и может занять время. Продолжить?",
                "Импорт приватного ключа", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        string? password = PasswordDialog.AskForAction(this, "импортировать приватный ключ");
        if (password is null) return;

        _walletKeyImportButton.Enabled = false;
        _walletKeyStatus.Text = "Импортирую ключ и восстанавливаю историю…";
        try
        {
            JsonElement result = await _rpc.CallAsync("importprivkey", new object[] { privateKey, "GeoCoin-Start", password, true, 0 });
            string address = ReadString(result, "address");
            bool rescanComplete = result.TryGetProperty("rescan_complete", out JsonElement complete) && complete.ValueKind == JsonValueKind.True;
            string scanStatus = ReadString(result, "rescan_status");
            _lastAddress = address;
            _privateKeyInput.Clear();
            string importStatus = rescanComplete
                ? "Ключ добавлен. История адреса восстановлена."
                : $"Ключ добавлен, но история восстановлена не полностью ({scanStatus}).";
            await RefreshWalletAddressesAsync();
            await RefreshStateAsync();
            _walletKeyStatus.Text = importStatus;
        }
        catch (Exception ex)
        {
            _walletKeyStatus.Text = "Не удалось импортировать ключ: " + ex.Message;
        }
        finally
        {
            _walletKeyImportButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private async Task ExportPrivateKeyAsync()
    {
        if (!IsNodeRunning() || _rpc is null || _walletAddressCombo.SelectedItem is not string address) return;
        if (MessageBox.Show(this,
                "Ключ даёт полный контроль над монетами этого адреса. Он будет записан в незашифрованный текстовый файл. Любой, кто получит файл, сможет потратить эти монеты. Продолжить?",
                "Экспорт секретного ключа", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        using var dialog = new SaveFileDialog
        {
            Title = "Куда сохранить приватный ключ",
            FileName = "GeoCoin-private-key.txt",
            Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (File.Exists(dialog.FileName))
        {
            MessageBox.Show(this, "Выберите новый файл. Экспорт приватного ключа не перезаписывает существующие файлы.",
                "Файл уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? password = PasswordDialog.AskForAction(this, "экспортировать приватный ключ выбранного адреса");
        if (password is null) return;

        _walletKeyExportButton.Enabled = false;
        _walletKeyStatus.Text = "Экспортирую ключ выбранного адреса…";
        try
        {
            JsonElement result = await _rpc.CallAsync("dumpprivkey", new object[] { address, password });
            string privateKey = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : "";
            if (privateKey.Length != 64 || privateKey.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidOperationException("Оригинальная нода вернула ключ в неожиданном формате.");
            await File.WriteAllTextAsync(dialog.FileName, privateKey + Environment.NewLine, new UTF8Encoding(false));
            _walletKeyStatus.Text = "Ключ сохранён в выбранный файл. Храните его отдельно и никому не передавайте.";
            MessageBox.Show(this, "Приватный ключ сохранён. Он открывает только выбранный адрес; файл не зашифрован.",
                "Экспорт завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _walletKeyStatus.Text = "Не удалось экспортировать ключ: " + ex.Message;
        }
        finally
        {
            _walletKeyExportButton.Enabled = IsNodeRunning() && _walletAddressCombo.SelectedItem is string;
        }
    }

    private async Task SendAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        string address = _sendAddress.Text.Trim();
        string rawAmount = _sendAmount.Text.Trim().Replace(',', '.');
        if (address.Length < 20)
        {
            _sendMessage.Text = "Проверьте адрес получателя.";
            _sendMessage.ForeColor = Color.FromArgb(161, 70, 53);
            return;
        }
        if (!decimal.TryParse(rawAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
        {
            _sendMessage.Text = "Введите сумму больше нуля, используя точку или запятую.";
            _sendMessage.ForeColor = Color.FromArgb(161, 70, 53);
            return;
        }
        if (decimal.Round(amount, 8) != amount)
        {
            _sendMessage.Text = "Сумма может содержать не больше 8 знаков после запятой.";
            _sendMessage.ForeColor = Color.FromArgb(161, 70, 53);
            return;
        }
        string password = _sendPassword.Text;
        if (password.Length == 0)
        {
            _sendMessage.Text = "Введите пароль кошелька.";
            _sendMessage.ForeColor = Color.FromArgb(161, 70, 53);
            return;
        }

        string review = $"Отправить {amount.ToString("0.########", CultureInfo.InvariantCulture)} GC?\n\n" +
                        $"Получатель:\n{address}\n\nКомиссия рассчитается автоматически. Отменить отправку после подтверждения нельзя.";
        if (MessageBox.Show(this, review, "Проверьте перевод", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _sendButton.Enabled = false;
        _sendMessage.Text = "Отправляю перевод…";
        _sendMessage.ForeColor = Muted;
        try
        {
            JsonElement feeInfo = await _rpc.CallAsync("getfeerate");
            if (!feeInfo.TryGetProperty("min_tx_fee_gc", out JsonElement feeElement) ||
                !feeElement.TryGetDecimal(out decimal minimumFee) || minimumFee <= 0)
                throw new InvalidOperationException("Нода не сообщила минимальную комиссию. Перевод не отправлен.");

            // v0.1.3 takes positional arguments: address, amount, optional fee, password.
            // Passing the minimum fee lets the node calculate its current required fee.
            var parameters = new object[] { address, amount, minimumFee, password };
            JsonElement result = await _rpc.CallAsync("sendtoaddress", parameters);
            string txid = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : result.ToString();
            _sendMessage.Text = "Перевод принят нодой. Номер: " + txid;
            _sendMessage.ForeColor = Color.FromArgb(53, 119, 74);
            _sendPassword.Clear();
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            _sendMessage.Text = ex.Message;
            _sendMessage.ForeColor = Color.FromArgb(161, 70, 53);
        }
        finally
        {
            _sendButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private async Task RefreshStateAsync()
    {
        if (_refreshing) return;
        if (!IsNodeRunning() || _rpc is null)
        {
            _rpcHealthy = false;
            if (_nodeProcess is not null && !IsNodeProcessRunning())
            {
                _nodeStatus = "Нода завершилась";
                CleanupNodeSession();
                UpdateStatus();
                if (_currentPage == "overview") BuildPage();
            }
            if (_currentPage == "node") UpdateNodePage();
            return;
        }
        _refreshing = true;
        try
        {
            JsonElement chain = await _rpc.CallAsync("getblockchaininfo");
            _rpcHealthy = true;
            _height = ReadInt64(chain, "blocks");
            _headers = ReadInt64(chain, "headers");
            try
            {
                JsonElement connections = await _rpc.CallAsync("getconnectioncount");
                _connections = connections.ValueKind == JsonValueKind.Number && connections.TryGetInt32(out int count)
                    ? count
                    : 0;
            }
            catch { }

            if (File.Exists(AppPaths.WalletPath))
            {
                try
                {
                    JsonElement balance = await _rpc.CallAsync("getbalance");
                    if (balance.ValueKind == JsonValueKind.Number && balance.TryGetDecimal(out decimal value))
                        _balance = value;
                }
                catch { }
            }

            if (_minerRpc is not null)
            {
                try
                {
                    JsonElement mining = await _minerRpc.CallAsync("getmininginfo");
                    _cpuMiningActive = mining.TryGetProperty("generate", out JsonElement generate) &&
                        generate.ValueKind == JsonValueKind.True;
                    _cpuHashrateValue = _cpuMiningActive ? ReadDouble(mining, "hashrate") : 0;
                    UpdateMiningSessionState();
                }
                catch { }
            }

            await RefreshWalletMinedBlocksAsync();
            _nodeStatus = _headers > 0 && _height >= _headers ? "Синхронизация завершена" : "Синхронизация";
        }
        catch
        {
            _rpcHealthy = false;
            if (_nodeProcess is not null && !IsNodeProcessRunning())
            {
                _nodeStatus = "Нода остановлена";
                CleanupNodeSession();
            }
        }
        finally
        {
            _refreshing = false;
            UpdateStatus();
            if (_currentPage == "overview")
            {
                _balanceValue.Text = File.Exists(AppPaths.WalletPath)
                    ? $"{_balance.ToString("0.########", CultureInfo.InvariantCulture)} GC"
                    : "Кошелёк ещё не создан";
                _nodeState.Text = _nodeStatus;
                _syncState.Text = SyncDescription();
                _peerState.Text = $"Блоков: {_height:N0}     Узлов сети: {_connections}";
            }
            RefreshMiningControls();
            if (_currentPage == "node") UpdateNodePage();
        }
    }

    private void RefreshMiningControls()
    {
        if (_currentPage == "subminer" && !IsDisposed)
        {
            _gpuMiningState.Text = _gpuStatusText;
            _miningHashrate.Text = _gpuHashrateText;
            _remoteGpuButton.Text = _remoteGpuMining ? "Остановить подмайнер" : _startingRemoteGpu ? "Подключаюсь…" : "Подключить и начать GPU-майнинг";
            _remoteGpuButton.Enabled = _remoteGpuMining || (!_startingRemoteGpu && !_checkingGpuDevices && !IsNodeRunning() &&
                _gpuMinerProcess is null && _remoteGpuDeviceCombo.SelectedItem is GpuDevice);
            _remoteGpuDeviceCombo.Enabled = !_remoteGpuMining;
            _remoteGpuFindButton.Enabled = !_remoteGpuMining && !_checkingGpuDevices;
        }
        if (_currentPage != "mining" || IsDisposed) return;
        _miningState.Text = _cpuMiningActive
            ? $"CPU-майнинг включён · потоков: {_selectedThreads}"
            : "CPU-майнинг остановлен";
        _miningState.ForeColor = _cpuMiningActive ? Color.FromArgb(53, 119, 74) : Muted;
        _cpuHashrate.Text = _cpuMiningActive
            ? $"Скорость CPU: {FormatHashrate(_cpuHashrateValue)}"
            : "Скорость CPU: —";
        _threadCount.Enabled = !_cpuMiningActive;
        _mineButton.Text = _cpuMiningActive ? "Остановить CPU" : "Запустить CPU";
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);

        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.ForeColor = _gpuMiningActive ? Color.FromArgb(53, 119, 74) : Muted;
        _miningHashrate.Text = _gpuHashrateText;
        _gpuRewardAddress.Text = string.IsNullOrWhiteSpace(_lastAddress)
            ? "Адрес награды будет взят из текущего кошелька."
            : "Адрес награды: " + _lastAddress;
        _gpuMineButton.Text = _remoteGpuMining ? "Перейти к подмайнеру" : _gpuMiningActive ? "Остановить GPU" : "Запустить GPU";
        _gpuMineButton.Enabled = _remoteGpuMining || (IsNodeRunning() && File.Exists(AppPaths.WalletPath) &&
            (_gpuMiningActive || _gpuDeviceCombo.SelectedItem is GpuDevice));
        _gpuDeviceCombo.Enabled = !_gpuMiningActive;
        _refreshGpuButton.Enabled = !_gpuMiningActive && !_checkingGpuDevices;
    }

    private string SyncDescription()
    {
        if (!IsNodeRunning()) return "Запустите ноду, чтобы начать подключение";
        if (_headers <= 0) return "Подключаюсь к сети GeoCoin…";
        if (_height >= _headers) return "Синхронизация завершена";
        decimal percent = Math.Clamp((decimal)_height * 100m / _headers, 0m, 100m);
        return $"Синхронизировано {percent:0.0}%";
    }

    private void UpdateStatus()
    {
        bool processRunning = IsNodeProcessRunning();
        _statusLabel.Text = processRunning ? _nodeStatus : "Нода остановлена";
        _statusLabel.ForeColor = processRunning
            ? (_nodeStatus.Contains("завершена", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(44, 117, 69) : Accent)
            : Muted;
        _statusLabel.BackColor = processRunning
            ? Color.FromArgb(229, 235, 221) : Color.FromArgb(226, 228, 219);
    }

    private bool IsNodeProcessRunning()
    {
        if (_nodeProcess is null) return false;
        try { return !_nodeProcess.HasExited; }
        catch (InvalidOperationException) { return false; }
    }

    private bool IsNodeRunning() => IsNodeProcessRunning() && _rpc is not null;

    private void CleanupNodeSession()
    {
        _rpcHealthy = false;
        _rpc?.Dispose();
        _rpc = null;
        _minerRpc?.Dispose();
        _minerRpc = null;
        _adminPassword = null;
        _minerPassword = null;
        _minerPassFile = null;
        _cpuMiningActive = false;
        _cpuHashrateValue = 0;
        _gpuMiningActive = false;
        UpdateMiningSessionState();
        _gpuStatusText = "GPU-майнинг остановлен";
        _gpuHashrateText = "Скорость GPU: —";
        _lastAddress = null;
        _walletAddresses.Clear();
        _walletAddressCombo.Items.Clear();
        if (_gpuMinerProcess is { } miner)
        {
            try { if (!miner.HasExited) miner.Kill(entireProcessTree: true); } catch { }
            try { miner.Dispose(); } catch { }
            _gpuMinerProcess = null;
        }
        _remoteGpuMining = false;
        DeleteRemoteMinerPassFile();
        _logWriter?.Dispose();
        _logWriter = null;
        if (_nodeProcess is not null && !IsNodeProcessRunning())
        {
            _nodeProcess.Dispose();
            _nodeProcess = null;
        }
        if (_nodeProcess is null && !string.IsNullOrWhiteSpace(_sessionDirectory))
        {
            try { Directory.Delete(_sessionDirectory, recursive: true); } catch { }
            _sessionDirectory = null;
        }
    }

    private async void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (_startingNode || _startingRemoteGpu)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Дождитесь завершения запуска узла или подмайнера, затем повторите выход.",
                "Выполняется запуск", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool gpuRunning = _gpuMinerProcess is { HasExited: false };
        if (_allowClose || (!IsNodeRunning() && !gpuRunning)) return;
        e.Cancel = true;
        if (_closePromptOpen) return;
        _closePromptOpen = true;
        var result = MessageBox.Show(this, gpuRunning && !IsNodeRunning()
                ? "Перед выходом остановить GPU-подмайнер?"
                : "Перед выходом остановить ноду GeoCoin и запущенный GPU-майнер?",
            "Закрытие GeoCoin-Start",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            if (_gpuMinerProcess is not null) await StopGpuMinerAsync();
            if (IsNodeRunning()) await StopNodeAsync();
            if (!IsNodeRunning() && _gpuMinerProcess is null)
            {
                _allowClose = true;
                Close();
            }
        }
        _closePromptOpen = false;
    }

    private static string RandomSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static void AddArgument(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private void WriteNodeLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            lock (this)
            {
                if (_logWriter is not null) _logWriter.WriteLine(line);
                else
                {
                    Directory.CreateDirectory(AppPaths.DataRoot);
                    File.AppendAllText(AppPaths.LogPath, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
        }
        catch { }
    }

    private static long ReadInt64(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.TryGetInt64(out long number) ? number : 0;

    private static string ReadString(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? ""
            : "";

    private static decimal ReadDecimal(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element)) return 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out decimal number)) return number;
        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number)) return number;
        return 0;
    }

    private static string FormatGcAmount(decimal amount, bool includePositiveSign = false)
    {
        string prefix = includePositiveSign && amount > 0 ? "+" : "";
        return prefix + amount.ToString("0.########", CultureInfo.CurrentCulture) + " GC";
    }

    private static string FormatTransactionTime(JsonElement entry)
    {
        long timestamp = ReadInt64(entry, "time");
        if (timestamp <= 0) timestamp = ReadInt64(entry, "timereceived");
        if (timestamp <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("dd.MM.yyyy HH:mm"); }
        catch (ArgumentOutOfRangeException) { return "—"; }
    }

    private static double ReadDouble(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement element)) return 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number)) return Math.Max(0, number);
        if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number)) return Math.Max(0, number);
        return 0;
    }

    private static string FormatHashrate(double value)
    {
        string[] units = { "хеш/с", "кХеш/с", "МХеш/с", "ГХеш/с", "ТХеш/с", "ПХеш/с" };
        int unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00", CultureInfo.CurrentCulture) + " " + units[unit];
    }

    private Panel AddCard(int x, int y, int width, int height)
    {
        var card = new Panel
        {
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _pagePanel.Controls.Add(card);
        return card;
    }

    private void AddParagraph(string text, int x, int y, int width, int height)
    {
        _pagePanel.Controls.Add(new Label
        {
            Text = text,
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10),
            AutoEllipsis = true
        });
    }

    private void AddLabel(Control parent, string text, int x, int y, int width, int height, Color color, float size, bool bold = false)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            ForeColor = color,
            Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoEllipsis = true
        });
    }

    private void StylePrimaryButton(Button button, Point location, Size size)
    {
        button.Location = UiPoint(location.X, location.Y);
        button.Size = UiSize(size.Width, size.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void StyleSecondaryButton(Button button, Point location, Size size)
    {
        button.Location = UiPoint(location.X, location.Y);
        button.Size = UiSize(size.Width, size.Height);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(194, 198, 187);
        button.BackColor = Color.White;
        button.ForeColor = Ink;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void StyleTextBox(TextBox box, Point location, Size size)
    {
        box.Location = UiPoint(location.X, location.Y);
        box.Size = UiSize(size.Width, size.Height);
        box.Font = new Font("Segoe UI", 11);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Color.FromArgb(250, 250, 247);
        box.ForeColor = Ink;
    }

    private static void OpenLink(string address)
        => Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
}
