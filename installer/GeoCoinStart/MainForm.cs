using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GeoCoinStart;

internal sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(241, 241, 235);
    private static readonly Color Ink = Color.FromArgb(31, 39, 34);
    private static readonly Color Muted = Color.FromArgb(104, 115, 105);
    private static readonly Color Accent = Color.FromArgb(166, 129, 63);
    private static readonly Color Rail = Color.FromArgb(22, 31, 26);

    private readonly Panel _pagePanel = new();
    private readonly Label _pageTitle = new();
    private readonly Label _statusLabel = new();
    private readonly Label _balanceValue = new();
    private readonly Label _walletAddress = new();
    private readonly Label _nodeState = new();
    private readonly Label _syncState = new();
    private readonly Label _peerState = new();
    private readonly Label _miningState = new();
    private readonly NumericUpDown _threadCount = new();
    private readonly TextBox _sendAddress = new();
    private readonly TextBox _sendAmount = new();
    private readonly TextBox _sendPassword = new();
    private readonly Label _sendMessage = new();
    private readonly Label _receiveAddress = new();
    private readonly Label _receiveMessage = new();
    private readonly Label _overviewHint = new();
    private readonly Button _nodeButton = new();
    private readonly Button _walletButton = new();
    private readonly Button _mineButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _receiveButton = new();
    private readonly Button[] _navigation = new Button[4];
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };

    private GeoCoinRpcClient? _rpc;
    private Process? _nodeProcess;
    private StreamWriter? _logWriter;
    private string? _adminPassword;
    private string? _lastAddress;
    private string _currentPage = "overview";
    private string _nodeStatus = "Нода остановлена";
    private decimal _balance;
    private long _height;
    private long _headers;
    private int _connections;
    private bool _miningActive;
    private bool _refreshing;
    private bool _allowClose;
    private bool _closePromptOpen;
    private int _selectedThreads = Math.Max(1, Environment.ProcessorCount / 2);

    public MainForm()
    {
        Text = "GeoCoin-Start — простой кошелёк";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 640);
        Size = new Size(1120, 760);
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10);
        FormClosing += HandleClosing;

        BuildShell();
        _nodeButton.Click += async (_, _) =>
        {
            if (IsNodeRunning()) await StopNodeAsync();
            else await StartNodeAsync();
            BuildPage();
        };
        _walletButton.Click += async (_, _) =>
        {
            if (File.Exists(AppPaths.WalletPath)) await BackupWalletAsync();
            else await CreateWalletAsync();
            BuildPage();
        };
        _mineButton.Click += async (_, _) => await ToggleMiningAsync();
        _sendButton.Click += async (_, _) => await SendAsync();
        _receiveButton.Click += async (_, _) => await CreateReceiveAddressAsync();
        _threadCount.ValueChanged += (_, _) => _selectedThreads = (int)_threadCount.Value;
        _refreshTimer.Tick += async (_, _) => await RefreshStateAsync();
        _refreshTimer.Start();
        SetPage("overview");
        UpdateStatus();
    }

    private void BuildShell()
    {
        var rail = new Panel { Dock = DockStyle.Left, Width = 224, BackColor = Rail };
        Controls.Add(rail);

        rail.Controls.Add(new Label
        {
            Text = "GeoCoin",
            Location = new Point(24, 28),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(233, 208, 153)
        });
        rail.Controls.Add(new Label
        {
            Text = "ПРОСТОЙ КОШЕЛЁК",
            Location = new Point(27, 66),
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Color.FromArgb(144, 161, 145)
        });

        string[] names = { "Обзор", "Майнинг", "Отправить", "Получить" };
        string[] pages = { "overview", "mining", "send", "receive" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Text = names[i],
                Tag = pages[i],
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(18, 128 + i * 56),
                Size = new Size(188, 44),
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

        var originalLink = new LinkLabel
        {
            Text = "Оригинальный проект ↗",
            AutoSize = true,
            Location = new Point(25, 626),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        originalLink.LinkClicked += (_, _) => OpenLink(AppPaths.OriginalRepositoryUrl);
        rail.Controls.Add(originalLink);

        var distributorLink = new LinkLabel
        {
            Text = "Редистрибьютор: TheTimkol ↗",
            AutoSize = true,
            Location = new Point(25, 650),
            LinkColor = Color.FromArgb(202, 180, 132),
            ActiveLinkColor = Color.White,
            Font = new Font("Segoe UI", 8.5f)
        };
        distributorLink.LinkClicked += (_, _) => OpenLink(AppPaths.DistributorUrl);
        rail.Controls.Add(distributorLink);

        var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = Canvas };
        Controls.Add(header);
        header.Controls.Add(_pageTitle);
        _pageTitle.Location = new Point(34, 20);
        _pageTitle.AutoSize = true;
        _pageTitle.Font = new Font("Segoe UI Semibold", 21, FontStyle.Bold);
        _pageTitle.ForeColor = Ink;
        header.Controls.Add(new Label
        {
            Text = "Управление GeoCoin без лишних настроек",
            Location = new Point(37, 61),
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5f)
        });

        _statusLabel.Location = new Point(768, 36);
        _statusLabel.Size = new Size(292, 34);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _statusLabel.BackColor = Color.FromArgb(226, 228, 219);
        _statusLabel.ForeColor = Muted;
        header.Controls.Add(_statusLabel);

        _pagePanel.Dock = DockStyle.Fill;
        _pagePanel.AutoScroll = true;
        _pagePanel.BackColor = Canvas;
        _pagePanel.Padding = new Padding(34, 8, 34, 28);
        Controls.Add(_pagePanel);
    }

    private void SetPage(string page)
    {
        _currentPage = page;
        string[] titles = page switch
        {
            "mining" => new[] { "Майнинг", "Майнинг", "Майнинг", "Майнинг" },
            "send" => new[] { "Отправить монеты", "Отправить монеты", "Отправить монеты", "Отправить монеты" },
            "receive" => new[] { "Получить монеты", "Получить монеты", "Получить монеты", "Получить монеты" },
            _ => new[] { "Обзор", "Обзор", "Обзор", "Обзор" }
        };
        _pageTitle.Text = titles[0];
        string[] pages = { "overview", "mining", "send", "receive" };
        for (int i = 0; i < _navigation.Length; i++)
        {
            bool selected = pages[i] == page;
            _navigation[i].BackColor = selected ? Color.FromArgb(44, 59, 48) : Rail;
            _navigation[i].ForeColor = selected ? Color.FromArgb(241, 218, 173) : Color.FromArgb(206, 215, 205);
        }
        BuildPage();
    }

    private void BuildPage()
    {
        _pagePanel.SuspendLayout();
        _pagePanel.Controls.Clear();
        switch (_currentPage)
        {
            case "mining": BuildMiningPage(); break;
            case "send": BuildSendPage(); break;
            case "receive": BuildReceivePage(); break;
            default: BuildOverviewPage(); break;
        }
        _pagePanel.ResumeLayout();
        UpdateStatus();
    }

    private void BuildOverviewPage()
    {
        AddParagraph("Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду.", 22, 12, 700, 44);

        Panel balanceCard = AddCard(22, 72, 700, 162);
        AddLabel(balanceCard, "БАЛАНС КОШЕЛЬКА", 24, 20, 250, 20, Muted, 9, bold: true);
        _balanceValue.Text = _nodeProcess is null ? "— GC" : $"{_balance.ToString("0.########", CultureInfo.InvariantCulture)} GC";
        _balanceValue.Location = new Point(22, 49);
        _balanceValue.AutoSize = true;
        _balanceValue.Font = new Font("Segoe UI Semibold", 30, FontStyle.Bold);
        _balanceValue.ForeColor = Ink;
        balanceCard.Controls.Add(_balanceValue);
        _nodeState.Text = _nodeProcess is null ? "Нода не запущена" : _nodeStatus;
        _nodeState.Location = new Point(26, 112);
        _nodeState.AutoSize = true;
        _nodeState.ForeColor = Muted;
        balanceCard.Controls.Add(_nodeState);

        Panel networkCard = AddCard(22, 252, 700, 142);
        AddLabel(networkCard, "СОСТОЯНИЕ СЕТИ", 24, 20, 230, 20, Muted, 9, bold: true);
        _syncState.Text = _nodeProcess is null ? "Запустите ноду, чтобы начать подключение" : SyncDescription();
        _syncState.Location = new Point(24, 53);
        _syncState.AutoSize = false;
        _syncState.Size = new Size(642, 28);
        _syncState.Font = new Font("Segoe UI Semibold", 13, FontStyle.Bold);
        _syncState.ForeColor = Ink;
        networkCard.Controls.Add(_syncState);
        _peerState.Text = _nodeProcess is null ? "" : $"Блоков: {_height:N0}     Узлов сети: {_connections}";
        _peerState.Location = new Point(26, 95);
        _peerState.AutoSize = true;
        _peerState.ForeColor = Muted;
        networkCard.Controls.Add(_peerState);

        _nodeButton.Text = _nodeProcess is null ? "Запустить ноду" : "Остановить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 416), new Size(192, 42));
        _pagePanel.Controls.Add(_nodeButton);

        _walletButton.Text = File.Exists(AppPaths.WalletPath) ? "Создать резервную копию" : "Создать кошелёк";
        StyleSecondaryButton(_walletButton, new Point(232, 416), new Size(230, 42));
        _walletButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletButton);

        _overviewHint.Text = File.Exists(AppPaths.WalletPath)
            ? "Сделайте резервную копию кошелька и храните её отдельно от компьютера."
            : "После подключения создайте защищённый паролем кошелёк. Пароль нельзя восстановить через это приложение.";
        _overviewHint.Location = new Point(24, 474);
        _overviewHint.Size = new Size(700, 44);
        _overviewHint.ForeColor = Muted;
        _pagePanel.Controls.Add(_overviewHint);
    }

    private void BuildMiningPage()
    {
        AddParagraph("Майнинг использует процессор этого компьютера. Начните с небольшого числа потоков: компьютер будет работать тише и холоднее.", 22, 12, 700, 58);
        Panel card = AddCard(22, 86, 700, 258);
        AddLabel(card, "Потоки процессора", 24, 22, 280, 24, Ink, 13, bold: true);
        AddLabel(card, "Чем больше потоков, тем выше нагрузка на компьютер.", 24, 54, 500, 25, Muted, 9.5f);
        _threadCount.Location = new Point(27, 101);
        _threadCount.Size = new Size(112, 31);
        _threadCount.Minimum = 1;
        _threadCount.Maximum = Math.Max(1, Environment.ProcessorCount);
        _threadCount.Value = Math.Max(1, Math.Min((int)_threadCount.Maximum, _selectedThreads));
        _threadCount.Font = new Font("Segoe UI", 11);
        card.Controls.Add(_threadCount);

        _miningState.Text = _miningActive ? $"Майнинг включён · потоков: {(int)_threadCount.Value}" : "Майнинг остановлен";
        _miningState.Location = new Point(24, 157);
        _miningState.AutoSize = true;
        _miningState.ForeColor = _miningActive ? Color.FromArgb(53, 119, 74) : Muted;
        card.Controls.Add(_miningState);

        _mineButton.Text = _miningActive ? "Остановить майнинг" : "Начать майнинг";
        StylePrimaryButton(_mineButton, new Point(24, 196), new Size(204, 42));
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_mineButton);

        if (!IsNodeRunning()) AddParagraph("Сначала запустите ноду на странице «Обзор».", 24, 372, 650, 32);
        else if (!File.Exists(AppPaths.WalletPath)) AddParagraph("Сначала создайте кошелёк на странице «Обзор».", 24, 372, 650, 32);
    }

    private void BuildSendPage()
    {
        AddParagraph("Введите адрес получателя и сумму. Перед отправкой приложение покажет итоговое подтверждение.", 22, 12, 700, 48);
        Panel card = AddCard(22, 78, 700, 424);
        AddLabel(card, "Адрес получателя", 26, 22, 300, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAddress, new Point(26, 51), new Size(640, 36));
        _sendAddress.PlaceholderText = "Адрес GeoCoin получателя";
        card.Controls.Add(_sendAddress);

        AddLabel(card, "Сумма, GC", 26, 112, 200, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAmount, new Point(26, 141), new Size(220, 36));
        _sendAmount.PlaceholderText = "Например, 1,25";
        card.Controls.Add(_sendAmount);

        AddLabel(card, "Пароль кошелька", 26, 198, 220, 22, Ink, 11, bold: true);
        StyleTextBox(_sendPassword, new Point(26, 227), new Size(320, 36));
        _sendPassword.UseSystemPasswordChar = true;
        card.Controls.Add(_sendPassword);

        AddLabel(card, "Комиссия рассчитается автоматически. Отправленную транзакцию нельзя отменить.",
            26, 282, 635, 44, Muted, 9.5f);
        _sendButton.Text = "Проверить и отправить";
        StylePrimaryButton(_sendButton, new Point(26, 344), new Size(228, 42));
        _sendButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendButton);

        _sendMessage.Location = new Point(26, 390);
        _sendMessage.Size = new Size(640, 24);
        _sendMessage.ForeColor = Muted;
        card.Controls.Add(_sendMessage);
    }

    private void BuildReceivePage()
    {
        AddParagraph("Создайте новый адрес для получения монет и отправьте его человеку, который переводит вам GeoCoin.", 22, 12, 700, 48);
        Panel card = AddCard(22, 78, 700, 286);
        AddLabel(card, "ВАШ АДРЕС ДЛЯ ПОЛУЧЕНИЯ", 26, 22, 450, 22, Muted, 9, bold: true);
        _receiveAddress.Text = string.IsNullOrWhiteSpace(_lastAddress) ? "Новый адрес появится здесь" : _lastAddress;
        _receiveAddress.Location = new Point(26, 59);
        _receiveAddress.Size = new Size(638, 70);
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
        _receiveMessage.Location = new Point(26, 219);
        _receiveMessage.Size = new Size(640, 32);
        _receiveMessage.ForeColor = Muted;
        card.Controls.Add(_receiveMessage);

        if (!IsNodeRunning()) AddParagraph("Сначала запустите ноду на странице «Обзор».", 24, 394, 650, 32);
        else if (!File.Exists(AppPaths.WalletPath)) AddParagraph("Сначала создайте кошелёк на странице «Обзор».", 24, 394, 650, 32);
    }

    private async Task StartNodeAsync()
    {
        if (!File.Exists(AppPaths.NodePath))
        {
            MessageBox.Show(this, "Файл ноды GeoCoin не найден. Запустите установщик ещё раз.",
                "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (_nodeProcess is { HasExited: false }) return;

        string? walletPassword = null;
        bool existingWallet = File.Exists(AppPaths.WalletPath);
        if (existingWallet)
        {
            walletPassword = PasswordDialog.AskToUnlock(this);
            if (walletPassword is null) return;
        }

        Directory.CreateDirectory(AppPaths.DataRoot);
        string sessionDirectory = Path.Combine(Path.GetTempPath(), "geocoin-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionDirectory);
        _adminPassword = RandomSecret();
        string adminFile = Path.Combine(sessionDirectory, "rpc-admin.pass");
        string? walletFile = existingWallet ? Path.Combine(sessionDirectory, "wallet.pass") : null;
        var secretEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(adminFile, _adminPassword + Environment.NewLine, secretEncoding);
        if (walletFile is not null) await File.WriteAllTextAsync(walletFile, walletPassword + Environment.NewLine, secretEncoding);

        _nodeStatus = "Запускаю ноду…";
        UpdateStatus();
        BuildPage();
        try
        {
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
            AddArgument(start, "--rpc-bind", "127.0.0.1");
            AddArgument(start, "--rpc-port", "3033");
            AddArgument(start, "--rpc-user", "geocoin-start");
            AddArgument(start, "--rpc-pass-file", adminFile);
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
            _rpc = new GeoCoinRpcClient("geocoin-start", _adminPassword);

            bool ready = false;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (_nodeProcess.HasExited)
                    throw new InvalidOperationException("Нода завершила запуск с ошибкой. Подробности записаны в файл журнала.");
                try
                {
                    await _rpc.CallAsync("help");
                    ready = true;
                    break;
                }
                catch { await Task.Delay(1000); }
            }
            if (!ready) throw new TimeoutException("Нода запускается дольше обычного. Проверьте файл журнала и попробуйте ещё раз.");

            try { Directory.Delete(sessionDirectory, recursive: true); } catch { }
            _nodeStatus = "Нода подключена";
            _refreshTimer.Start();
            await RefreshStateAsync();
            if (!existingWallet)
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
            try { Directory.Delete(sessionDirectory, recursive: true); } catch { }
            _nodeStatus = "Нода не подключена";
            MessageBox.Show(this, ex.Message + "\n\nЖурнал: " + AppPaths.LogPath,
                "Не удалось запустить GeoCoin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        UpdateStatus();
        BuildPage();
    }

    private async Task StopNodeAsync()
    {
        if (_nodeProcess is null || _nodeProcess.HasExited) return;
        _nodeStatus = "Останавливаю ноду…";
        UpdateStatus();
        try
        {
            if (_miningActive && _rpc is not null)
            {
                try { await _rpc.CallAsync("setgenerate", new object[] { false }); } catch { }
                _miningActive = false;
            }
            if (_rpc is not null) await _rpc.CallAsync("stop");
            for (int i = 0; i < 200 && !_nodeProcess.HasExited; i++) await Task.Delay(100);
            if (!_nodeProcess.HasExited)
                throw new TimeoutException("Нода ещё завершает работу. Подождите немного и повторите.");
            CleanupNodeSession();
            _nodeStatus = "Нода остановлена";
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
            MessageBox.Show(this, "Зашифрованная резервная копия сохранена. Храните её отдельно от компьютера вместе с паролем кошелька.",
                "Копия готова", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось создать копию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ToggleMiningAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (!_miningActive)
        {
            string warning = $"Запустить майнинг на {(int)_threadCount.Value} потоках?\n\n" +
                             "Это увеличит нагрузку, нагрев и расход электричества. Майнинг будет идти на адрес основного кошелька.";
            if (MessageBox.Show(this, warning, "Начать майнинг", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
        }
        try
        {
            if (_miningActive)
            {
                await _rpc.CallAsync("setgenerate", new object[] { false });
                _miningActive = false;
            }
            else
            {
                await _rpc.CallAsync("setgenerate", new object[] { true, (int)_threadCount.Value });
                _miningActive = true;
            }
            BuildPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось изменить майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (_refreshing || !IsNodeRunning() || _rpc is null) return;
        _refreshing = true;
        try
        {
            JsonElement chain = await _rpc.CallAsync("getblockchaininfo");
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

            _nodeStatus = _headers > 0 && _height >= _headers ? "Синхронизация завершена" : "Синхронизация";
        }
        catch
        {
            if (_nodeProcess?.HasExited == true)
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
        }
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
        _statusLabel.Text = _nodeProcess is { HasExited: false } ? _nodeStatus : "Нода остановлена";
        _statusLabel.ForeColor = _nodeProcess is { HasExited: false }
            ? (_nodeStatus.Contains("завершена", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(44, 117, 69) : Accent)
            : Muted;
        _statusLabel.BackColor = _nodeProcess is { HasExited: false }
            ? Color.FromArgb(229, 235, 221) : Color.FromArgb(226, 228, 219);
    }

    private bool IsNodeRunning() => _nodeProcess is { HasExited: false } && _rpc is not null;

    private void CleanupNodeSession()
    {
        _refreshTimer.Stop();
        _rpc?.Dispose();
        _rpc = null;
        _adminPassword = null;
        _miningActive = false;
        _logWriter?.Dispose();
        _logWriter = null;
        if (_nodeProcess is { HasExited: true })
        {
            _nodeProcess.Dispose();
            _nodeProcess = null;
        }
    }

    private async void HandleClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose || !IsNodeRunning()) return;
        e.Cancel = true;
        if (_closePromptOpen) return;
        _closePromptOpen = true;
        var result = MessageBox.Show(this, "Перед выходом остановить ноду GeoCoin?", "Закрытие GeoCoin-Start",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            await StopNodeAsync();
            if (!IsNodeRunning())
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
                _logWriter?.WriteLine(line);
            }
        }
        catch { }
    }

    private static long ReadInt64(JsonElement value, string property)
        => value.TryGetProperty(property, out JsonElement element) && element.TryGetInt64(out long number) ? number : 0;

    private Panel AddCard(int x, int y, int width, int height)
    {
        var card = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
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
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10),
            AutoEllipsis = true
        });
    }

    private static void AddLabel(Control parent, string text, int x, int y, int width, int height, Color color, float size, bool bold = false)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            ForeColor = color,
            Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoEllipsis = true
        });
    }

    private static void StylePrimaryButton(Button button, Point location, Size size)
    {
        button.Location = location;
        button.Size = size;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button, Point location, Size size)
    {
        button.Location = location;
        button.Size = size;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(194, 198, 187);
        button.BackColor = Color.White;
        button.ForeColor = Ink;
        button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleTextBox(TextBox box, Point location, Size size)
    {
        box.Location = location;
        box.Size = size;
        box.Font = new Font("Segoe UI", 11);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Color.FromArgb(250, 250, 247);
        box.ForeColor = Ink;
    }

    private static void OpenLink(string address)
        => Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
}
