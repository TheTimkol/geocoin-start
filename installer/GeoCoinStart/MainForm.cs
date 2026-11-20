using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Drawing.Imaging;
using QRCoder;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace GeoCoinStart;

internal sealed class MainForm : Form
{
    private sealed record GpuDevice(string Backend, string Id, string Name)
    {
        public override string ToString() => $"{Name} — {Backend.ToUpperInvariant()} · {Id}";
    }

    private sealed record FeeEstimate(decimal ApproximateGc, decimal MinimumGc, decimal RateGcPerKb, int AssumedBytes);

    private sealed record MinedBlockRecord(string BlockId, string TransactionId, long Timestamp, string Source);
    private sealed class MinedBlockLedger
    {
        public List<MinedBlockRecord> Blocks { get; set; } = new();
        public long? SessionStartedAtUnix { get; set; }
        public long? SessionEndedAtUnix { get; set; }
        public List<string> SessionBlockIds { get; set; } = new();
        public List<string> LifetimeBlockIds { get; set; } = new();
        public Dictionary<string, string> TransactionToBlockIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool WalletHistoryBackfilled { get; set; }
    }

    private static readonly Color LightCanvas = Color.FromArgb(241, 241, 235);
    private static readonly Color LightInk = Color.FromArgb(31, 39, 34);
    private static readonly Color LightMuted = Color.FromArgb(104, 115, 105);
    private static readonly Color LightRail = Color.FromArgb(22, 31, 26);
    private static readonly Color DarkCanvas = Color.FromArgb(30, 35, 32);
    private static readonly Color DarkSurface = Color.FromArgb(39, 46, 41);
    private static readonly Color DarkInput = Color.FromArgb(47, 55, 49);
    private static readonly Color DarkInk = Color.FromArgb(235, 238, 231);
    private static readonly Color DarkMuted = Color.FromArgb(174, 185, 173);
    private static readonly Color DarkRail = Color.FromArgb(17, 23, 20);
    private static readonly Color DarkAccent = Color.FromArgb(184, 145, 76);
    private Color Canvas => _darkTheme ? DarkCanvas : LightCanvas;
    private Color Surface => _darkTheme ? DarkSurface : Color.White;
    private Color InputSurface => _darkTheme ? DarkInput : Color.FromArgb(250, 250, 247);
    private Color Ink => _darkTheme ? DarkInk : LightInk;
    private Color Muted => _darkTheme ? DarkMuted : LightMuted;
    private Color Accent => _darkTheme ? DarkAccent : Color.FromArgb(166, 129, 63);
    private Color Rail => _darkTheme ? DarkRail : LightRail;
    private Color Divider => _darkTheme ? Color.FromArgb(61, 68, 62) : Color.FromArgb(220, 220, 214);
    private Color Success => _darkTheme ? Color.FromArgb(118, 194, 133) : Color.FromArgb(53, 119, 74);
    private Color Error => _darkTheme ? Color.FromArgb(240, 137, 116) : Color.FromArgb(161, 70, 53);

    private static readonly Dictionary<string, string> RussianToEnglish = new(StringComparer.Ordinal)
    {
        ["GeoCoin-Start — простой кошелёк"] = "GeoCoin-Start — simple wallet",
        ["ПРОСТОЙ КОШЕЛЁК"] = "SIMPLE WALLET",
        ["Обзор"] = "Overview", ["Майнинг"] = "Mining", ["Подмайнер"] = "Subminer", ["Нода"] = "Node",
        ["Отправить"] = "Send", ["Получить"] = "Receive", ["История"] = "History", ["Кошелёк"] = "Wallet",
        ["Нода и журнал"] = "Node and log", ["Отправить монеты"] = "Send coins", ["Получить монеты"] = "Receive coins",
        ["История транзакций"] = "Transaction history", ["Управление кошельком"] = "Wallet management",
        ["Управление GeoCoin без лишних настроек"] = "Manage GeoCoin without unnecessary settings",
        ["Оригинальный проект ↗"] = "Original project ↗", ["Редистрибьютор: TheTimkol ↗"] = "Redistributor: TheTimkol ↗",
        ["Тёмная тема"] = "Dark theme", ["Светлая тема"] = "Light theme",
        ["Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду."] = "View your wallet and sync status here. Start the node to begin.",
        ["БАЛАНС КОШЕЛЬКА"] = "WALLET BALANCE", ["СОСТОЯНИЕ СЕТИ"] = "NETWORK STATUS", ["ДОБЫЧА И НАГРАДЫ"] = "MINING AND REWARDS",
        ["Кошелёк ещё не создан"] = "Wallet has not been created",
        ["Запустите ноду, чтобы начать подключение"] = "Start the node to connect",
        ["Добытые блоки за всё время: "] = "Blocks mined all time: ", ["Зрелые монеты: "] = "Mature coins: ", ["Незрелые монеты: "] = "Immature coins: ",
        ["Зрелые монеты: —"] = "Mature coins: —", ["Незрелые монеты: —"] = "Immature coins: —",
        ["Запускаю ноду…"] = "Starting node…", ["Запустить ноду"] = "Start node", ["Остановить ноду"] = "Stop node",
        ["Создать кошелёк"] = "Create wallet", ["Импортировать кошелёк"] = "Import wallet", ["Импортировать другой"] = "Import another wallet",
        ["Экспортировать кошелёк (.dat)"] = "Export wallet (.dat)", ["Экспортировать весь кошелёк (.dat)"] = "Export full wallet (.dat)",
        ["Сделайте резервную копию кошелька и храните её отдельно от компьютера."] = "Back up your wallet and store the copy away from this computer.",
        ["Создайте кошелёк или импортируйте существующий файл GeoCoin. Пароль хранится только у вас."] = "Create a wallet or import an existing GeoCoin file. Only you know its password.",
        ["CPU и GPU настраиваются в одном окне. Их можно запускать по отдельности или одновременно."] = "Set up CPU and GPU mining here. Run either one or both at the same time.",
        ["ДОБЫТЫЕ БЛОКИ"] = "MINED BLOCKS", ["Майнинг на процессоре · CPU"] = "CPU mining", ["Потоки"] = "Threads",
        ["CPU-майнинг включён · потоков: "] = "CPU mining running · threads: ", ["CPU-майнинг остановлен"] = "CPU mining stopped",
        ["Скорость CPU: "] = "CPU hashrate: ", ["Остановить CPU"] = "Stop CPU", ["Запустить CPU"] = "Start CPU",
        ["Майнинг на видеокарте · GPU"] = "GPU mining", ["Выберите видеокарту. Награда поступит на уже существующий адрес этого кошелька."] = "Choose a GPU. Rewards will go to an existing address in this wallet.",
        ["Найти"] = "Find", ["GPU-майнинг остановлен"] = "GPU mining stopped", ["GPU-майнинг запущен. Ожидаю данные о скорости…"] = "GPU mining started. Waiting for hashrate…",
        ["Скорость GPU: —"] = "GPU hashrate: —", ["Скорость GPU: ожидание данных…"] = "GPU hashrate: waiting for data…",
        ["Адрес награды будет взят из текущего кошелька."] = "The reward address will come from the current wallet.", ["Адрес награды: "] = "Reward address: ",
        ["Перейти к подмайнеру"] = "Open subminer", ["Остановить GPU"] = "Stop GPU", ["Запустить GPU"] = "Start GPU",
        ["Сначала запустите ноду на странице «Обзор»."] = "Start the node on the Overview page first.",
        ["Сначала создайте или импортируйте кошелёк на странице «Обзор»."] = "Create or import a wallet on the Overview page first.",
        ["Один процесс GeoCoin выполняет работу ноды. RPC — интерфейс управления этой же нодой; отдельно запускать вторую RPC-ноду не нужно."] = "One GeoCoin process runs the node. RPC controls that same node; a second RPC node is not needed.",
        ["СОСТОЯНИЕ УЗЛА"] = "NODE STATUS", ["ДОСТУП ДЛЯ GPU-ПОДМАЙНЕРА"] = "GPU SUBMINER ACCESS", ["Адрес прослушивания"] = "Listen address",
        ["127.0.0.1 · только этот компьютер"] = "127.0.0.1 · this computer only",
        ["Для подключения с другого ПК выберите частный IP этого компьютера. Используйте только доверенную сеть или VPN; не настраивайте проброс порта из интернета."] = "To connect another PC, choose this computer's private IP. Use a trusted LAN or VPN; do not expose this port to the internet.",
        ["Имя пользователя майнера"] = "Miner username", ["Пароль майнера"] = "Miner password",
        ["Показать"] = "Show", ["Скрыть"] = "Hide", ["Скопировать данные для подмайнера"] = "Copy subminer connection details",
        ["Пароль не сохраняется после закрытия приложения. Для другого ПК выберите частный IP и запустите ноду."] = "The password is not saved after the app closes. Choose a private IP and start the node to connect another PC.",
        ["Пароль действует, пока приложение открыто. Если появится запрос брандмауэра, разрешите только частные сети."] = "The password is kept only while the app is open. If Windows Firewall asks, allow private networks only.",
        ["ЖУРНАЛ НОДЫ И МАЙНЕРА"] = "NODE AND MINER LOG", ["Открыть файл журнала"] = "Open log file",
        ["Этот компьютер будет работать как GPU-подмайнер и подключится к уже запущенной ноде на другом ПК. Локальную ноду запускать не нужно."] = "This computer will act as a GPU subminer connected to a node on another PC. No local node is needed.",
        ["ПОДКЛЮЧЕНИЕ К НОДЕ"] = "CONNECT TO NODE", ["Адрес RPC-ноды"] = "Node RPC address", ["Пароль роли майнера"] = "Miner role password",
        ["Адрес для награды"] = "Reward address", ["Видеокарта"] = "GPU", ["Подключаюсь…"] = "Connecting…", ["Подключить и начать GPU-майнинг"] = "Connect and start GPU mining",
        ["На узле нужен miner RPC-пользователь и пароль; admin RPC и пароль кошелька сюда не вводите. Подключайтесь только по доверенной LAN/VPN: RPC не шифрует пароль при передаче."] = "Use the miner RPC username and password from the node; do not enter the admin RPC or wallet password here. Connect only over a trusted LAN or VPN: RPC does not encrypt the password.",
        ["Адрес получателя"] = "Recipient address", ["Адрес GeoCoin получателя"] = "GeoCoin recipient address", ["Введите сумму больше нуля, используя точку или запятую."] = "Enter an amount greater than zero, using a dot or comma.",
        ["Сумма, GC"] = "Amount, GC", ["Например, 1,25"] = "For example, 1.25", ["Пароль кошелька"] = "Wallet password",
        ["Комиссия рассчитается автоматически. Отправленную транзакцию нельзя отменить."] = "The fee is calculated automatically. A sent transaction cannot be reversed.", ["Проверить и отправить"] = "Review and send",
        ["Выберите существующий адрес или создайте новый. Покажите его отправителю или дайте отсканировать QR-код."] = "Choose an existing address or create a new one. Share it with the sender or let them scan the QR code.",
        ["АДРЕС ДЛЯ ПОЛУЧЕНИЯ"] = "RECEIVE ADDRESS", ["Новый адрес появится здесь"] = "A new address will appear here", ["В кошельке пока нет адреса"] = "This wallet has no address yet",
        ["Создать новый адрес"] = "Create new address", ["Копировать адрес"] = "Copy address", ["QR содержит только адрес"] = "QR code contains the address only",
        ["Сохранить QR-код"] = "Save QR code", ["Импортировать QR"] = "Import QR", ["Открыть обзор"] = "Open overview",
        ["На этой странице пока нет данных."] = "There is no data on this page yet.", ["Кошелёк не открыт"] = "Wallet is not open",
        ["Чтобы показать адреса и QR-код, сначала запустите ноду. Затем создайте или откройте кошелёк."] = "Start the node to view addresses and QR codes, then create or open a wallet.",
        ["Чтобы получить монеты, создайте новый кошелёк или импортируйте существующий на странице «Обзор»."] = "To receive coins, create a wallet or import an existing one on the Overview page.",
        ["История появится после запуска ноды и подключения кошелька. Запустите ноду на странице «Обзор»."] = "History will appear after you start the node and connect a wallet. Start it on the Overview page.",
        ["Создайте или импортируйте кошелёк на странице «Обзор», чтобы загрузить историю переводов и наград."] = "Create or import a wallet on the Overview page to load transfers and mining rewards.",
        ["Для управления кошельком запустите ноду на странице «Обзор»."] = "Start the node on the Overview page to manage the wallet.",
        ["Создайте или импортируйте кошелёк на странице «Обзор», чтобы управлять резервными копиями и ключами."] = "Create or import a wallet on the Overview page to manage backups and keys.",
        ["Нода остановлена. Можно подготовить адрес и сумму; для отправки сначала запустите ноду на странице «Обзор»."] = "The node is stopped. You can prepare the address and amount; start the node on the Overview page to send.",
        ["Откройте или создайте кошелёк на странице «Обзор», чтобы отправлять монеты."] = "Open or create a wallet on the Overview page to send coins.",
        ["Введите адрес получателя или загрузите QR-код. Перед отправкой проверьте сумму и оценку комиссии."] = "Enter the recipient address or load a QR code. Check the amount and fee estimate before sending.",
        ["Рассчитываю оценку комиссии…"] = "Calculating fee estimate…", ["Комиссия появится после запуска ноды."] = "Fee estimate will appear when the node is running.",
        ["Сумма ориентировочная; GeoCoin рассчитывает комиссию по размеру транзакции."] = "This is an estimate; GeoCoin calculates the final fee from the transaction size.",
        ["Оценка комиссии рассчитана для обычной транзакции и может отличаться от точной суммы."] = "The estimate uses a typical transaction and may differ from the final fee.",
        ["Сохранить QR-код адреса"] = "Save address QR code", ["Выберите изображение с QR-кодом GeoCoin"] = "Choose an image containing a GeoCoin QR code",
        ["QR-код сохранён как изображение PNG."] = "QR code saved as a PNG image.", ["Адрес загружен из QR-кода. Перед отправкой проверьте адрес и сумму."] = "Address loaded from QR code. Check the address and amount before sending.",
        ["В выбранном изображении не найден QR-код."] = "No QR code was found in the selected image.", ["QR-код не содержит корректный адрес GeoCoin."] = "The QR code does not contain a valid GeoCoin address.",
        ["Этот QR-код содержит адрес другой сети или неверный адрес."] = "This QR code contains an invalid address or an address for another network.", ["QR-код не содержит адрес GeoCoin."] = "The QR code does not contain a GeoCoin address.",
        ["Сначала запустите ноду, чтобы рассчитать комиссию."] = "Start the node to calculate the fee.", ["Нода не сообщила минимальную комиссию."] = "The node did not provide a minimum fee.",
        ["Не удалось получить оценку комиссии."] = "Could not get a fee estimate.",
        ["Адрес создан. Его можно скопировать или отсканировать QR-код."] = "Address created. Copy it or scan the QR code.", ["Адрес скопирован. Теперь его можно отправить отправителю."] = "Address copied. You can now share it with the sender.",
        ["Загружаю адреса текущего кошелька…"] = "Loading addresses from this wallet…", ["Выберите адрес для перевода или создайте новый. QR-код содержит только адрес."] = "Choose an address or create one. The QR code contains the address only.",
        ["В кошельке нет адресов. Создайте новый адрес для получения монет."] = "This wallet has no addresses. Create one to receive coins.", ["Не удалось загрузить адреса кошелька: "] = "Could not load wallet addresses: ",
        ["История входящих переводов, отправок и наград за майнинг."] = "History of received and sent transfers and mining rewards.", ["Обновить"] = "Refresh",
        ["Дата"] = "Date", ["Операция"] = "Operation", ["Сумма"] = "Amount", ["Комиссия"] = "Fee", ["Состояние"] = "Status", ["Адрес"] = "Address",
        ["Получение"] = "Received", ["Отправка"] = "Sent", ["Награда"] = "Reward", ["Отменена"] = "Reversed", ["Ожидает зрелости"] = "Immature", ["Ожидает"] = "Pending",
        ["Выберите запись, чтобы посмотреть номер транзакции."] = "Select a record to view its transaction ID.", ["Копировать ID"] = "Copy ID", ["Обновляю историю кошелька…"] = "Updating wallet history…",
        ["Создайте или импортируйте кошелёк, чтобы увидеть историю."] = "Create or import a wallet to view its history.", ["Не удалось загрузить историю: "] = "Could not load history: ",
        ["Резервная копия сохраняет весь кошелёк. Приватный ключ относится только к одному адресу."] = "A backup saves the full wallet. A private key applies to one address only.",
        ["Полная копия кошелька"] = "Full wallet backup", ["Файл .dat включает весь текущий кошелёк и подходит для его восстановления."] = "The .dat file contains the whole wallet and can restore it.",
        ["Приватный ключ одного адреса"] = "Private key for one address", ["В GeoCoin нет мнемонической фразы. Приватный ключ из 64 hex-символов открывает один адрес."] = "GeoCoin has no recovery phrase. A 64-character hexadecimal private key unlocks one address.",
        ["Адрес для экспорта ключа"] = "Address to export", ["Сохранить ключ выбранного адреса"] = "Save key for selected address", ["Импорт ключа в текущий кошелёк"] = "Import key into this wallet",
        ["64 шестнадцатеричных символа"] = "64 hexadecimal characters", ["Импортировать ключ"] = "Import key",
        ["Ключи — секретные данные. Не отправляйте их другим людям и не храните в общедоступных файлах."] = "Keys are secret. Do not share them or store them in public files.",
        ["Синхронизация завершена"] = "Synchronization complete", ["Нода остановлена"] = "Node stopped", ["Нода не запущена"] = "Node is not running",
        ["Синхронизация"] = "Synchronizing", ["Нода завершилась"] = "Node exited", ["Нода запускается…"] = "Node is starting…", ["Подключаю GPU к удалённой ноде…"] = "Connecting GPU to remote node…",
        ["Подключаюсь к сети GeoCoin…"] = "Connecting to the GeoCoin network…",
        ["Локальная нода остановлена · работает режим подмайнера"] = "Local node stopped · subminer is running", ["RPC отвечает"] = "RPC is responding",
        ["Процесс запущен · проверяю RPC…"] = "Process started · checking RPC…", ["RPC не запущен"] = "RPC is not running", ["Сеть: —"] = "Network: —",
        ["Не удалось проверить видеокарту."] = "Could not check the GPU.", ["GPU-майнер не найден. Переустановите GeoCoin-Start."] = "GPU miner not found. Reinstall GeoCoin-Start.",
        ["GPU-майнинг работает"] = "GPU mining is running", ["GPU-подмайнер остановлен."] = "GPU subminer stopped.", ["GPU-майнинг остановлен."] = "GPU mining stopped.",
        ["Не удалось запустить GPU-майнинг: "] = "Could not start GPU mining: ", ["Не удалось подключиться: "] = "Could not connect: ",
        ["Проверяю miner RPC и адрес награды…"] = "Checking miner RPC and reward address…", ["Подготавливаю подключение к удалённой ноде…"] = "Preparing connection to remote node…",
        ["Не удалось загрузить адреса: "] = "Could not load addresses: ", ["Не удалось импортировать ключ: "] = "Could not import key: ",
        ["Не удалось экспортировать ключ: "] = "Could not export key: ", ["В кошельке нет доступных адресов."] = "No available addresses in this wallet.",
        ["Ключ добавлен. История адреса восстановлена."] = "Key imported. Address history restored.",
        ["Адреса появятся после запуска ноды и открытия кошелька."] = "Addresses will appear after the node starts and the wallet is opened.",
        ["Совместимая видеокарта не найдена. Проверьте драйверы."] = "No compatible GPU found. Check the drivers.",
        ["Кошелёк создан. Сейчас сделайте резервную копию на странице «Обзор»."] = "Wallet created. Make a backup on the Overview page now.",
        ["Проверьте ключ: нужно ровно 64 шестнадцатеричных символа 0–9, A–F."] = "Check the key: it must contain exactly 64 hexadecimal characters, 0–9 or A–F.",
        ["Ключ сохранён в выбранный файл. Храните его отдельно и никому не передавайте."] = "Key saved to the selected file. Store it separately and do not share it.",
        ["Ключ сохранён"] = "Key saved", ["Не удалось создать адрес"] = "Could not create address",
        ["В истории пока нет операций. После синхронизации обновите список."] = "No transactions yet. Refresh the list after synchronization.",
        ["Проверьте адрес получателя."] = "Check the recipient address.", ["Сумма может содержать не больше 8 знаков после запятой."] = "The amount can have no more than 8 decimal places.",
        ["Введите пароль кошелька."] = "Enter the wallet password.", ["Отправляю перевод…"] = "Sending transfer…",
        ["ID транзакции скопирован."] = "Transaction ID copied.", ["Не удалось скопировать ID: "] = "Could not copy ID: ",
        ["Сегодня: "] = "Today: ", ["Текущая сессия: "] = "Current session: ", ["Последняя сессия: "] = "Last session: ", ["Сессия: "] = "Session: ",
        ["Блоков: "] = "Blocks: ", ["Узлов сети: "] = "Network peers: ", ["подключений: "] = "connections: ", ["Скорость: "] = "Hashrate: ",
        ["Ожидает данных…"] = "Waiting for data…", ["Остановить подмайнер"] = "Stop subminer", ["Не удалось создать адрес: "] = "Could not create address: ",
        ["Адрес скопирован"] = "Address copied", ["Зрелые монеты"] = "Mature coins", ["Незрелые монеты"] = "Immature coins"
    };
    private static readonly Dictionary<string, string> EnglishToRussian = RussianToEnglish
        .GroupBy(entry => entry.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    private readonly Panel _pagePanel = new();
    private readonly Panel _headerPanel = new();
    private readonly Label _pageTitle = new();
    private readonly Label _statusLabel = new();
    private readonly ComboBox _languageCombo = new();
    private readonly Button _themeButton = new();
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
    private readonly Label _lifetimeMinedBlocksLabel = new();
    private readonly Label _matureCoinsLabel = new();
    private readonly Label _immatureCoinsLabel = new();
    private readonly TextBox _nodeLogBox = new();
    private readonly NumericUpDown _threadCount = new();
    private readonly ComboBox _gpuDeviceCombo = new();
    private readonly ComboBox _remoteGpuDeviceCombo = new();
    private readonly ComboBox _rpcBindCombo = new();
    private readonly ComboBox _walletAddressCombo = new();
    private readonly ComboBox _receiveAddressCombo = new();
    private readonly PictureBox _receiveQrCode = new();
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
    private readonly Label _receiveQrCaption = new();
    private readonly Label _sendFeeEstimate = new();
    private readonly Label _sendFeeHint = new();
    private readonly Label _overviewHint = new();
    private readonly Button _nodeButton = new();
    private readonly Button _walletButton = new();
    private readonly Button _walletRestoreButton = new();
    private readonly Button _mineButton = new();
    private readonly Button _gpuMineButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _receiveButton = new();
    private readonly Button _receiveCopyButton = new();
    private readonly Button _saveReceiveQrButton = new();
    private readonly Button _importPaymentQrButton = new();
    private readonly Button[] _navigation = new Button[8];
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };
    private readonly System.Windows.Forms.Timer _sendFeeRefreshTimer = new() { Interval = 450 };
    private readonly object _minedBlocksLock = new();
    private readonly List<MinedBlockRecord> _minedBlockRecords = new();
    private readonly HashSet<string> _miningSessionBlockIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lifetimeMinedBlockIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _minedBlockIdByTransactionId = new(StringComparer.OrdinalIgnoreCase);

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
    private decimal _matureCoins;
    private decimal _immatureCoins;
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
    private bool _loadingReceiveAddresses;
    private bool _walletHistoryBackfilled;
    private double _cpuHashrateValue;
    private string _gpuHashrateText = "Скорость: —";
    private string _gpuStatusText = "GPU-майнинг остановлен";
    private string? _gpuLastError;
    private readonly List<GpuDevice> _gpuDevices = new();
    private readonly List<string> _walletAddresses = new();
    private string? _selectedTransactionId;
    private bool _refreshing;
    private bool _allowClose;
    private bool _closePromptOpen;
    private bool _darkTheme;
    private string _uiLanguage = "ru";
    private bool _englishUi => _uiLanguage == "en";
    private bool _refreshingSendFee;
    private int _lastPageViewportWidth;
    private int _selectedThreads = Math.Max(1, Environment.ProcessorCount / 2);

    public MainForm()
    {
        LoadUiPreferences();
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
        ApplyThemeToTree(this);
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
        _receiveAddressCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingReceiveAddresses || _receiveAddressCombo.SelectedItem is not string address) return;
            _lastAddress = address;
            UpdateReceiveAddressDisplay();
        };
        _sendButton.Click += async (_, _) => await SendAsync();
        _receiveButton.Click += async (_, _) => await CreateReceiveAddressAsync();
        _receiveCopyButton.Click += (_, _) => CopyReceiveAddress();
        _saveReceiveQrButton.Click += (_, _) => SaveReceiveQrCode();
        _importPaymentQrButton.Click += (_, _) => ImportPaymentQrFromImage();
        _sendAmount.TextChanged += (_, _) =>
        {
            _sendFeeRefreshTimer.Stop();
            _sendFeeRefreshTimer.Start();
        };
        _sendFeeRefreshTimer.Tick += async (_, _) =>
        {
            _sendFeeRefreshTimer.Stop();
            await RefreshSendFeeEstimateAsync();
        };
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
            ApplyUiLanguage(this);
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

        var rail = new Panel { Name = "navigationRail", Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Rail };
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
            Name = "railFooter",
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

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Items.AddRange(UiLocalization.LanguageNames.Cast<object>().ToArray());
        _languageCombo.SelectedIndex = Math.Max(0, Array.IndexOf(UiLocalization.LanguageCodes, _uiLanguage));
        _languageCombo.Font = new Font("Segoe UI", 9);
        _languageCombo.Size = UiSize(112, 28);
        _languageCombo.Location = UiPoint(0, 8);
        _headerPanel.Controls.Add(_languageCombo);
        _languageCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_languageCombo.SelectedIndex < 0) return;
            string language = UiLocalization.LanguageCodes[_languageCombo.SelectedIndex];
            if (_uiLanguage == language) return;
            _uiLanguage = language;
            SaveUiPreferences();
            BuildPage();
            ApplyUiLanguage(this);
            UpdateStatus();
        };

        StyleSecondaryButton(_themeButton, new Point(0, 8), new Size(112, 28));
        _themeButton.Click += (_, _) =>
        {
            _darkTheme = !_darkTheme;
            SaveUiPreferences();
            BuildPage();
            UpdatePreferenceControlText();
            ApplyThemeToTree(this);
            ApplyUiLanguage(this);
            UpdateStatus();
        };
        _headerPanel.Controls.Add(_themeButton);
        UpdatePreferenceControlText();

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
        int themeWidth = ScaleUi(112);
        int controlGap = ScaleUi(8);
        int right = _headerPanel.ClientSize.Width - margin;
        _themeButton.Location = new Point(Math.Max(margin, right - themeWidth), ScaleUi(8));
        _languageCombo.Location = new Point(Math.Max(margin, right - themeWidth - controlGap - ScaleUi(112)), ScaleUi(8));
        _statusLabel.Location = new Point(Math.Max(margin, right - width), ScaleUi(43));
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
            _navigation[i].BackColor = selected
                ? (_darkTheme ? Color.FromArgb(48, 61, 52) : Color.FromArgb(44, 59, 48))
                : Rail;
            _navigation[i].ForeColor = selected ? Color.FromArgb(241, 218, 173) : Color.FromArgb(206, 215, 205);
        }
        BuildPage();
        if (page == "transactions" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshTransactionsAsync();
        if (page == "wallet" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshWalletAddressesAsync();
        if (page == "receive" && IsNodeRunning() && File.Exists(AppPaths.WalletPath))
            _ = RefreshReceiveAddressesAsync();
        if ((page == "mining" || page == "subminer") && !_gpuDevicesScanned && !_checkingGpuDevices && File.Exists(AppPaths.GpuMinerPath))
            _ = RefreshGpuDevicesAsync();
        if (page == "node") UpdateNodePage();
        ApplyUiLanguage(this);
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
        ApplyThemeToTree(this);
        ApplyUiLanguage(this);
        UpdateStatus();
    }

    private void BuildOverviewPage()
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("Здесь видно состояние кошелька и синхронизации. Для начала запустите ноду.", 22, 12, cardWidth, 44);

        Panel balanceCard = AddCard(22, 72, cardWidth, 162);
        AddLabel(balanceCard, "БАЛАНС КОШЕЛЬКА", 24, 20, 250, 20, Muted, 9, bold: true);
        _balanceValue.Text = _nodeProcess is null ? "— GC" : FormatGcAmount(_balance);
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

        Panel miningSummary = AddCard(22, 412, cardWidth, 120);
        AddLabel(miningSummary, "ДОБЫЧА И НАГРАДЫ", 24, 17, 260, 20, Muted, 9, bold: true);
        _lifetimeMinedBlocksLabel.Location = UiPoint(24, 47);
        _lifetimeMinedBlocksLabel.Size = UiSize(cardWidth - 60, 24);
        _lifetimeMinedBlocksLabel.Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold);
        _lifetimeMinedBlocksLabel.ForeColor = Ink;
        miningSummary.Controls.Add(_lifetimeMinedBlocksLabel);
        _matureCoinsLabel.Location = UiPoint(24, 80);
        _matureCoinsLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 32), 22);
        _matureCoinsLabel.ForeColor = Muted;
        miningSummary.Controls.Add(_matureCoinsLabel);
        _immatureCoinsLabel.Location = UiPoint(Math.Max(250, cardWidth / 2), 80);
        _immatureCoinsLabel.Size = UiSize(Math.Max(220, cardWidth / 2 - 32), 22);
        _immatureCoinsLabel.ForeColor = Muted;
        miningSummary.Controls.Add(_immatureCoinsLabel);
        UpdateOverviewMiningSummary();

        _nodeButton.Text = _startingNode ? "Запускаю ноду…" : !IsNodeProcessRunning() ? "Запустить ноду" : "Остановить ноду";
        StylePrimaryButton(_nodeButton, new Point(22, 550), new Size(192, 42));
        _nodeButton.Enabled = !_remoteGpuMining && !_startingRemoteGpu && !_startingNode;
        _pagePanel.Controls.Add(_nodeButton);

        bool hasWallet = File.Exists(AppPaths.WalletPath);
        _walletButton.Text = hasWallet ? "Экспортировать кошелёк (.dat)" : "Создать кошелёк";
        StyleSecondaryButton(_walletButton, new Point(232, 550), new Size(230, 42));
        _walletButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletButton);

        _walletRestoreButton.Text = hasWallet ? "Импортировать другой" : "Импортировать кошелёк";
        StyleSecondaryButton(_walletRestoreButton, new Point(232, 600), new Size(230, 42));
        _walletRestoreButton.Enabled = IsNodeRunning();
        _pagePanel.Controls.Add(_walletRestoreButton);

        _overviewHint.Text = File.Exists(AppPaths.WalletPath)
            ? "Сделайте резервную копию кошелька и храните её отдельно от компьютера."
            : "Создайте кошелёк или импортируйте существующий файл GeoCoin. Пароль хранится только у вас.";
        _overviewHint.Location = UiPoint(24, 658);
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
        _miningState.ForeColor = _cpuMiningActive ? Success : Muted;
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
            BackColor = Divider
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
        _gpuMiningState.ForeColor = _gpuMiningActive ? Success : Muted;
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
        _rpcBindCombo.Items.Add(_englishUi ? "127.0.0.1 · this computer only" : "127.0.0.1 · только этот компьютер");
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
        _gpuMiningState.ForeColor = _remoteGpuMining ? Success : Muted;
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
        _nodeProcessStatus.ForeColor = running ? Success : Muted;
        _nodeRpcStatus.Text = running ? (_rpcHealthy ? "RPC отвечает" : "Процесс запущен · проверяю RPC…") : "RPC не запущен";
        _nodeRpcStatus.ForeColor = _rpcHealthy ? Success : Muted;
        _nodePeerStatus.Text = running ? $"Блоков: {_height:N0} · подключений: {_connections}" : "Сеть: —";
        string runTime = "";
        if (_nodeProcess is { } process && IsNodeProcessRunning())
        {
            try { runTime = $"PID {process.Id} · запущена в {process.StartTime.ToString("HH:mm:ss", UiLocalization.Culture(_uiLanguage))}"; }
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
            ShowLocalizedMessage("Для подключения другого ПК сначала запустите ноду с частным IP на странице «Нода». Не публикуйте RPC в интернете.",
                "Нет удалённого доступа", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string profile = $"RPC URL: http://{_rpcBindAddress}:3033/{Environment.NewLine}" +
                         $"Пользователь майнера: {_minerRpcUsername}{Environment.NewLine}" +
                         $"Пароль майнера: {_minerPassword}{Environment.NewLine}" +
                         "Вставьте эти данные в GeoCoin-Start → Подмайнер на другом компьютере.";
        Clipboard.SetText(profile);
        ShowLocalizedMessage("Данные роли майнера скопированы. Передавайте их только доверенному компьютеру в своей LAN/VPN.",
            "Профиль скопирован", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BuildSendPage()
    {
        int cardWidth = PageCardWidth(700);
        int fieldWidth = cardWidth - 60;
        string pageHint = !IsNodeRunning()
            ? "Нода остановлена. Можно подготовить адрес и сумму; для отправки сначала запустите ноду на странице «Обзор»."
            : !File.Exists(AppPaths.WalletPath)
                ? "Откройте или создайте кошелёк на странице «Обзор», чтобы отправлять монеты."
                : "Введите адрес получателя или загрузите QR-код. Перед отправкой проверьте сумму и оценку комиссии.";
        AddParagraph(pageHint, 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, 454);
        AddLabel(card, "Адрес получателя", 26, 18, fieldWidth, 22, Ink, 11, bold: true);
        int addressInputWidth = Math.Max(220, fieldWidth - 160);
        StyleTextBox(_sendAddress, new Point(26, 47), new Size(addressInputWidth, 36));
        _sendAddress.PlaceholderText = "Адрес GeoCoin получателя";
        card.Controls.Add(_sendAddress);
        _importPaymentQrButton.Text = "Импортировать QR";
        StyleSecondaryButton(_importPaymentQrButton, new Point(cardWidth - 164, 47), new Size(138, 36));
        _importPaymentQrButton.Enabled = true;
        card.Controls.Add(_importPaymentQrButton);

        AddLabel(card, "Сумма, GC", 26, 99, 200, 22, Ink, 11, bold: true);
        StyleTextBox(_sendAmount, new Point(26, 128), new Size(Math.Min(220, fieldWidth), 36));
        _sendAmount.PlaceholderText = "Например, 1,25";
        card.Controls.Add(_sendAmount);

        _sendFeeEstimate.Text = IsNodeRunning() ? "Рассчитываю оценку комиссии…" : "Комиссия появится после запуска ноды.";
        _sendFeeEstimate.Location = UiPoint(26, 177);
        _sendFeeEstimate.Size = UiSize(fieldWidth, 24);
        _sendFeeEstimate.ForeColor = Ink;
        _sendFeeEstimate.Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold);
        card.Controls.Add(_sendFeeEstimate);
        _sendFeeHint.Text = "Сумма ориентировочная; GeoCoin рассчитывает комиссию по размеру транзакции.";
        _sendFeeHint.Location = UiPoint(26, 202);
        _sendFeeHint.Size = UiSize(fieldWidth, 42);
        _sendFeeHint.ForeColor = Muted;
        _sendFeeHint.Font = new Font("Segoe UI", 9);
        card.Controls.Add(_sendFeeHint);

        AddLabel(card, "Пароль кошелька", 26, 249, 220, 22, Ink, 11, bold: true);
        StyleTextBox(_sendPassword, new Point(26, 278), new Size(Math.Min(320, fieldWidth), 36));
        _sendPassword.UseSystemPasswordChar = true;
        _sendPassword.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendPassword);

        _sendButton.Text = "Проверить и отправить";
        StylePrimaryButton(_sendButton, new Point(26, 329), new Size(228, 42));
        _sendButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_sendButton);

        _sendMessage.Location = UiPoint(26, 379);
        _sendMessage.Size = UiSize(fieldWidth, 48);
        _sendMessage.ForeColor = Muted;
        card.Controls.Add(_sendMessage);
        if (IsNodeRunning() && File.Exists(AppPaths.WalletPath)) _ = RefreshSendFeeEstimateAsync();
    }

    private void BuildReceivePage()
    {
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "Чтобы показать адреса и QR-код, сначала запустите ноду. Затем создайте или откройте кошелёк."
                    : "Чтобы получить монеты, создайте новый кошелёк или импортируйте существующий на странице «Обзор».");
            return;
        }

        int cardWidth = PageCardWidth(700);
        bool compact = cardWidth < 650;
        int addressWidth = Math.Max(220, cardWidth - 260);
        AddParagraph("Выберите существующий адрес или создайте новый. Покажите его отправителю или дайте отсканировать QR-код.", 22, 12, cardWidth, 48);
        Panel card = AddCard(22, 78, cardWidth, compact ? 390 : 322);
        AddLabel(card, "АДРЕС ДЛЯ ПОЛУЧЕНИЯ", 26, 20, addressWidth, 22, Muted, 9, bold: true);
        _receiveAddressCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _receiveAddressCombo.Location = UiPoint(26, 50);
        _receiveAddressCombo.Size = UiSize(addressWidth, 34);
        _receiveAddressCombo.Font = new Font("Segoe UI", 9.5f);
        _receiveAddressCombo.Enabled = !_loadingReceiveAddresses && IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_receiveAddressCombo);

        _receiveAddress.Location = UiPoint(26, 96);
        _receiveAddress.Size = UiSize(addressWidth, 60);
        _receiveAddress.AutoEllipsis = true;
        _receiveAddress.ForeColor = Ink;
        _receiveAddress.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        _receiveAddress.TextAlign = ContentAlignment.MiddleLeft;
        card.Controls.Add(_receiveAddress);

        _receiveButton.Text = "Создать новый адрес";
        StylePrimaryButton(_receiveButton, new Point(26, 168), new Size(190, 42));
        _receiveButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        card.Controls.Add(_receiveButton);

        _receiveCopyButton.Text = "Копировать адрес";
        StyleSecondaryButton(_receiveCopyButton,
            new Point(compact ? 26 : 230, compact ? 218 : 168), new Size(180, 42));
        _receiveCopyButton.Enabled = !string.IsNullOrWhiteSpace(_lastAddress);
        card.Controls.Add(_receiveCopyButton);

        _saveReceiveQrButton.Text = "Сохранить QR-код";
        StyleSecondaryButton(_saveReceiveQrButton,
            new Point(26, compact ? 268 : 218), new Size(180, 38));
        _saveReceiveQrButton.Enabled = !string.IsNullOrWhiteSpace(_lastAddress);
        card.Controls.Add(_saveReceiveQrButton);

        _receiveMessage.Text = IsNodeRunning() && File.Exists(AppPaths.WalletPath)
            ? "Загружаю адреса текущего кошелька…"
            : "Адреса появятся после запуска ноды и открытия кошелька.";
        _receiveMessage.Location = UiPoint(26, compact ? 318 : 270);
        _receiveMessage.Size = UiSize(addressWidth, compact ? 56 : 42);
        _receiveMessage.ForeColor = Muted;
        card.Controls.Add(_receiveMessage);

        _receiveQrCode.Location = UiPoint(cardWidth - 190, 54);
        _receiveQrCode.Size = UiSize(150, 150);
        _receiveQrCode.SizeMode = PictureBoxSizeMode.Zoom;
        _receiveQrCode.BackColor = Color.White;
        card.Controls.Add(_receiveQrCode);
        _receiveQrCaption.Text = "QR содержит только адрес";
        _receiveQrCaption.Location = UiPoint(cardWidth - 196, 211);
        _receiveQrCaption.Size = UiSize(162, 38);
        _receiveQrCaption.TextAlign = ContentAlignment.TopCenter;
        _receiveQrCaption.ForeColor = Muted;
        _receiveQrCaption.Font = new Font("Segoe UI", 8.5f);
        card.Controls.Add(_receiveQrCaption);
        UpdateReceiveAddressDisplay();
    }

    private void BuildTransactionsPage()
    {
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "История появится после запуска ноды и подключения кошелька. Запустите ноду на странице «Обзор»."
                    : "Создайте или импортируйте кошелёк на странице «Обзор», чтобы загрузить историю переводов и наград.");
            return;
        }

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

    private void BuildUnavailableState(string heading, string explanation)
    {
        int cardWidth = PageCardWidth(700);
        AddParagraph("На этой странице пока нет данных.", 22, 12, cardWidth, 40);
        Panel card = AddCard(22, 66, cardWidth, 226);
        AddLabel(card, heading, 26, 28, cardWidth - 64, 34, Ink, 18, bold: true);
        AddLabel(card, explanation, 26, 76, cardWidth - 64, 58, Muted, 10);
        var openOverview = new Button { Text = "Открыть обзор" };
        StylePrimaryButton(openOverview, new Point(26, 154), new Size(192, 42));
        openOverview.Click += (_, _) => SetPage("overview");
        card.Controls.Add(openOverview);
    }

    private void BuildWalletPage()
    {
        if (!IsNodeRunning() || !File.Exists(AppPaths.WalletPath))
        {
            BuildUnavailableState(
                !IsNodeRunning() ? "Нода остановлена" : "Кошелёк не открыт",
                !IsNodeRunning()
                    ? "Для управления кошельком запустите ноду на странице «Обзор»."
                    : "Создайте или импортируйте кошелёк на странице «Обзор», чтобы управлять резервными копиями и ключами.");
            return;
        }

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
            BackColor = Divider
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
            ShowLocalizedMessage("Сначала остановите GPU-подмайнер. В этом режиме приложение подключено к ноде на другом компьютере.",
                "Сначала остановите подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.NodePath))
        {
            ShowLocalizedMessage("Файл ноды GeoCoin не найден. Запустите установщик ещё раз.",
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
            ShowLocalizedMessage("Имя miner RPC должно быть непустым и не содержать пробелов или двоеточия.",
                "Проверьте имя пользователя", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.Equals(_minerRpcUsername, "geocoin-start", StringComparison.OrdinalIgnoreCase))
        {
            ShowLocalizedMessage("Для подмайнера задайте отдельное имя пользователя, отличное от admin RPC.",
                "Имя занято", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (minerPassword.Length < 16 || minerPassword.Contains('\r') || minerPassword.Contains('\n'))
        {
            ShowLocalizedMessage("Задайте для miner RPC пароль не короче 16 символов без переводов строки.",
                "Проверьте пароль", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_rpcBindAddress != "127.0.0.1" && !GetPrivateIpv4Addresses().Contains(_rpcBindAddress, StringComparer.Ordinal))
        {
            ShowLocalizedMessage("Выбранный сетевой адрес больше не активен. Откройте страницу «Нода», выберите актуальный частный IP и запустите снова.",
                "Обновите адрес RPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _rpcBindAddress = "127.0.0.1";
            return;
        }
        if (_rpcBindAddress != "127.0.0.1" && ShowLocalizedMessage($"Открыть miner RPC на частном адресе {_rpcBindAddress}:3033?\n\n" +
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
                var result = ShowLocalizedMessage("Нода подключена. Создать новый защищённый кошелёк?\n\n" +
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
            ShowLocalizedMessage(ex.Message + "\n\nЖурнал: " + AppPaths.LogPath,
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
            ShowLocalizedMessage(ex.Message + "\n\nОставьте GeoCoin-Start открытым и попробуйте остановить ноду ещё раз.",
                "GeoCoin ещё работает", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        UpdateStatus();
    }

    private async Task CreateWalletAsync()
    {
        if (!IsNodeRunning() || _rpc is null) return;
        if (File.Exists(AppPaths.WalletPath))
        {
            ShowLocalizedMessage("В папке GeoCoin уже есть кошелёк. Он не будет перезаписан.",
                "Кошелёк уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string? password = PasswordDialog.AskToCreate(this);
        if (password is null) return;
        try
        {
            JsonElement result = await _rpc.CallAsync("createwallet", new[] { password });
            _lastAddress = result.TryGetProperty("address", out JsonElement address) ? address.GetString() : null;
            ResetWalletHistoryBackfill();
            _receiveMessage.Text = "Кошелёк создан. Сейчас сделайте резервную копию на странице «Обзор».";
            ShowLocalizedMessage("Кошелёк создан и защищён паролем.\n\n" +
                "Сначала сохраните пароль, затем создайте зашифрованную резервную копию. Храните копию отдельно от компьютера.",
                "Кошелёк готов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось создать кошелёк", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            ShowLocalizedMessage("Выберите новый файл. Существующую резервную копию приложение не перезаписывает.",
                "Файл уже существует", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            await _rpc.CallAsync("backupwallet", new[] { dialog.FileName });
            ShowLocalizedMessage("Копия кошелька сохранена. Храните её отдельно от компьютера; для защищённого файла также сохраните пароль.",
                "Копия готова", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось создать копию", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (ShowLocalizedMessage("Перед импортом закройте оригинальный клиент GeoCoin, если выбранный кошелёк открыт в нём. Исходный файл останется без изменений.",
                "Импорт кошелька", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
            return;

        string sourcePath = Path.GetFullPath(dialog.FileName);
        if (string.Equals(sourcePath, Path.GetFullPath(AppPaths.WalletPath), StringComparison.OrdinalIgnoreCase))
        {
            ShowLocalizedMessage("Этот файл уже выбран как текущий кошелёк.", "Кошелёк уже открыт",
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
        if (replacingWallet && ShowLocalizedMessage("Текущий кошелёк будет заменён. Перед этим GeoCoin-Start создаст его резервную копию и сохранит исходный файл отдельно. Продолжить?",
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
            ResetWalletHistoryBackfill();
            await RefreshStateAsync();
            ShowLocalizedMessage("Кошелёк импортирован и открыт. Исходный файл не изменён. История операций будет синхронизирована с сетью.",
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
                    ShowLocalizedMessage("Импорт не завершился. Исходный кошелёк сохранён здесь:\n" + (preservedWallet ?? recoveryCopy ?? "") +
                        "\n\n" + rollbackError.Message,
                        "Проверьте файл кошелька", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            ShowLocalizedMessage("Не удалось импортировать файл кошелька. Убедитесь, что выбран файл GeoCoin и введён его пароль.\n\n" + ex.Message +
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
                ShowLocalizedMessage(ex.Message, "Проверка GPU", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                ShowLocalizedMessage(ex.Message, "Не удалось остановить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        string warning = $"Запустить майнинг на {(int)_threadCount.Value} потоках процессора?\n\n" +
                         "Это увеличит нагрузку, нагрев и расход электричества. Если GPU-майнинг уже запущен, обе нагрузки будут работать одновременно.";
        if (ShowLocalizedMessage(warning, "Начать майнинг", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
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
            ShowLocalizedMessage(ex.Message, "Не удалось запустить CPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            ShowLocalizedMessage("Сейчас GPU-процесс подключён к удалённой ноде. Управляйте им на странице «Подмайнер».",
                "Работает подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!IsNodeRunning() || _rpc is null || _minerPassFile is null || !File.Exists(_minerPassFile)) return;
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            ShowLocalizedMessage("GPU-майнер не найден. Переустановите GeoCoin-Start, чтобы получить официальный пакет целиком.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_gpuDeviceCombo.SelectedItem is not GpuDevice device)
        {
            ShowLocalizedMessage("Сначала нажмите «Найти» и выберите видеокарту.",
                "Видеокарта не выбрана", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string warning = $"Начать майнинг на видеокарте «{device.Name}»?\n\n" +
                         "Видеокарта будет работать с высокой нагрузкой и может нагреваться. Награда поступит на существующий адрес текущего кошелька.";
        if (ShowLocalizedMessage(warning, "Начать GPU-майнинг", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _gpuLastError = null;
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
            miner.ErrorDataReceived += (_, e) => HandleGpuMinerErrorLine(e.Data, remote: false);
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
                        int exitCode = 0;
                        try { exitCode = miner.ExitCode; } catch { }
                        _gpuStatusText = DescribeGpuMinerExit(exitCode, remote: false);
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
            _gpuLastError = ex.Message;
            _gpuStatusText = "Не удалось запустить GPU-майнинг: " + ex.Message;
            ShowLocalizedMessage(ex.Message, "Не удалось запустить GPU-майнинг", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            ShowLocalizedMessage("GPU-майнер уже запущен. Остановите текущий процесс перед сменой режима.",
                "GPU уже занят", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (IsNodeProcessRunning())
        {
            ShowLocalizedMessage("Сначала остановите локальную ноду. Подмайнер работает отдельно и подключается к ноде на другом ПК.",
                "Остановите локальную ноду", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(AppPaths.GpuMinerPath))
        {
            ShowLocalizedMessage("Официальный GPU-майнер не найден. Переустановите GeoCoin-Start.",
                "GPU-майнинг недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!Uri.TryCreate(_remoteNodeUrlInput.Text.Trim(), UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttp ||
            !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            ShowLocalizedMessage("Введите полный адрес RPC-ноды, например http://192.168.1.10:3033/. Не добавляйте имя пользователя или пароль в адрес.",
                "Проверьте адрес ноды", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string username = _remoteMinerUserInput.Text.Trim();
        string password = _remoteMinerPasswordInput.Text;
        string address = _remoteRewardAddressInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Contains(':') || username.Any(char.IsWhiteSpace) || password.Length < 16 || string.IsNullOrWhiteSpace(address))
        {
            ShowLocalizedMessage("Укажите имя miner RPC без пробелов, пароль не короче 16 символов и публичный адрес для награды.",
                "Заполните данные подмайнера", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_remoteGpuDeviceCombo.SelectedItem is not GpuDevice device)
        {
            ShowLocalizedMessage("Нажмите «Найти» и выберите видеокарту.", "Видеокарта не выбрана",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (ShowLocalizedMessage("Подключить видеокарту к удалённой RPC-ноде?\n\n" +
                "Передача miner RPC-пароля по обычному HTTP не шифруется. Продолжайте только в своей доверенной локальной сети или VPN. Никогда не вводите здесь admin RPC пароль или пароль кошелька.",
                "Запустить GPU-подмайнер", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        string? passFile = null;
        _gpuLastError = null;
        _startingRemoteGpu = true;
        _gpuStatusText = "Проверяю miner RPC и адрес награды…";
        BuildPage();
        try
        {
            using (var rpcCheck = new GeoCoinRpcClient(username, password, endpoint.ToString()))
            {
                JsonElement template = await rpcCheck.CallAsync("getblocktemplate", new object[]
                {
                    new Dictionary<string, string> { ["address"] = address }
                });
                if (template.ValueKind != JsonValueKind.Object || !template.TryGetProperty("blockhex", out _))
                    throw new InvalidOperationException("Нода не вернула шаблон блока. Проверьте синхронизацию ноды и miner RPC-доступ.");
            }

            _gpuStatusText = "Подготавливаю подключение к удалённой ноде…";
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
            miner.ErrorDataReceived += (_, e) => HandleGpuMinerErrorLine(e.Data, remote: true);
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
                        int exitCode = 0;
                        try { exitCode = miner.ExitCode; } catch { }
                        _gpuStatusText = DescribeGpuMinerExit(exitCode, remote: true);
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
            _gpuLastError = ex.Message;
            _gpuStatusText = "Не удалось подключиться: " + ex.Message;
            if (passFile is not null)
            {
                try { Directory.Delete(Path.GetDirectoryName(passFile)!, recursive: true); } catch { }
            }
            ShowLocalizedMessage(ex.Message, "Не удалось запустить подмайнер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private void HandleGpuMinerErrorLine(string? line, bool remote)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        string message = line.Trim();
        string prefix = remote ? "[GPU-подмайнер] " : "[GPU] ";
        WriteNodeLog(prefix + message);
        _gpuLastError = message;
        _gpuStatusText = (remote ? "Ошибка GPU-подмайнера: " : "Ошибка GPU: ") + message;
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (_currentPage is "mining" or "subminer") _gpuMiningState.Text = _gpuStatusText;
            }));
        }
        catch (InvalidOperationException) { }
    }

    private string DescribeGpuMinerExit(int exitCode, bool remote)
    {
        string name = remote ? "GPU-подмайнер" : "GPU-майнер";
        if (exitCode == 0) return remote ? "GPU-подмайнер остановлен." : "GPU-майнинг остановлен.";
        return string.IsNullOrWhiteSpace(_gpuLastError)
            ? $"{name} завершился с кодом {exitCode}. Откройте вкладку «Нода», чтобы посмотреть журнал."
            : $"{name}: {_gpuLastError} (код {exitCode}). Подробности — во вкладке «Нода».";
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
                HandleGpuMinerErrorLine(message, remote);
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
                if (ledger?.LifetimeBlockIds is not null)
                    foreach (string blockId in ledger.LifetimeBlockIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                        _lifetimeMinedBlockIds.Add(blockId);
                if (ledger?.TransactionToBlockIds is not null)
                    foreach ((string txid, string blockId) in ledger.TransactionToBlockIds)
                        if (!string.IsNullOrWhiteSpace(txid) && !string.IsNullOrWhiteSpace(blockId))
                            _minedBlockIdByTransactionId[txid] = blockId;
                _walletHistoryBackfilled = ledger?.WalletHistoryBackfilled ?? false;
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
                _lifetimeMinedBlockIds.Add(record.BlockId);
                if (!string.IsNullOrWhiteSpace(record.TransactionId))
                    _minedBlockIdByTransactionId[record.TransactionId] = record.BlockId;
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
                    UpdateMinedBlockFromWalletEntryLocked(entry);

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

    private async Task RefreshLifetimeWalletMinedBlocksAsync()
    {
        GeoCoinRpcClient? rpc = _rpc;
        if (_walletHistoryBackfilled || rpc is null || !File.Exists(AppPaths.WalletPath)) return;

        try
        {
            JsonElement history = await rpc.CallAsync("listsinceblock", new object[] { "", 1, true, true, true });
            if (!ReferenceEquals(rpc, _rpc) || history.ValueKind != JsonValueKind.Object ||
                !history.TryGetProperty("transactions", out JsonElement transactions) || transactions.ValueKind != JsonValueKind.Array)
                return;

            lock (_minedBlocksLock)
            {
                foreach (JsonElement entry in transactions.EnumerateArray())
                    UpdateMinedBlockFromWalletEntryLocked(entry);
                if (history.TryGetProperty("removed", out JsonElement removed) && removed.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement entry in removed.EnumerateArray())
                        RemoveMinedBlockLocked(ReadString(entry, "blockhash"), ReadString(entry, "txid"));

                _walletHistoryBackfilled = true;
                PruneMinedBlockRecordsLocked();
                SaveMinedBlockRecordsLocked();
            }
            RefreshMinedBlockCountersOnUi();
        }
        catch
        {
            // Keep retrying after the wallet finishes syncing or RPC becomes available.
        }
    }

    private void ResetWalletHistoryBackfill()
    {
        lock (_minedBlocksLock)
        {
            _walletHistoryBackfilled = false;
            SaveMinedBlockRecordsLocked();
        }
    }

    private void UpdateMinedBlockFromWalletEntryLocked(JsonElement entry)
    {
        string category = ReadString(entry, "category").ToLowerInvariant();
        string txid = ReadString(entry, "txid");
        string blockId = ReadString(entry, "blockhash");
        if (category == "orphan")
        {
            RemoveMinedBlockLocked(blockId, txid);
            return;
        }
        if (category is not ("immature" or "generate") || string.IsNullOrWhiteSpace(txid)) return;

        long timestamp = ReadInt64(entry, "blocktime");
        if (timestamp <= 0) timestamp = ReadInt64(entry, "time");
        if (timestamp <= 0 || timestamp > DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()) return;
        if (string.IsNullOrWhiteSpace(blockId)) blockId = txid;
        UpsertMinedBlockLocked(new MinedBlockRecord(blockId, txid, timestamp, "local-wallet"));
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
        _lifetimeMinedBlockIds.Add(record.BlockId);
        if (!string.IsNullOrWhiteSpace(record.TransactionId))
            _minedBlockIdByTransactionId[record.TransactionId] = record.BlockId;
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
        var blockIdsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(blockId)) blockIdsToRemove.Add(blockId);
        if (!string.IsNullOrWhiteSpace(transactionId) &&
            _minedBlockIdByTransactionId.TryGetValue(transactionId, out string? mappedBlockId))
            blockIdsToRemove.Add(mappedBlockId);
        foreach (MinedBlockRecord record in _minedBlockRecords.Where(record =>
                     (!string.IsNullOrWhiteSpace(blockId) && string.Equals(record.BlockId, blockId, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(transactionId) && string.Equals(record.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase))).ToArray())
        {
            _minedBlockRecords.Remove(record);
            _miningSessionBlockIds.Remove(record.BlockId);
            blockIdsToRemove.Add(record.BlockId);
        }
        foreach (string id in blockIdsToRemove) _lifetimeMinedBlockIds.Remove(id);
        if (!string.IsNullOrWhiteSpace(transactionId)) _minedBlockIdByTransactionId.Remove(transactionId);
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
            var ledger = new MinedBlockLedger
            {
                Blocks = _minedBlockRecords.ToList(),
                SessionStartedAtUnix = _miningSessionStartedAt?.ToUnixTimeSeconds(),
                SessionEndedAtUnix = _miningSessionEndedAt?.ToUnixTimeSeconds(),
                SessionBlockIds = _miningSessionBlockIds.ToList(),
                LifetimeBlockIds = _lifetimeMinedBlockIds.ToList(),
                TransactionToBlockIds = new Dictionary<string, string>(_minedBlockIdByTransactionId, StringComparer.OrdinalIgnoreCase),
                WalletHistoryBackfilled = _walletHistoryBackfilled
            };
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
            try { BeginInvoke((Action)(() =>
            {
                UpdateMinedBlockCounters();
                if (_currentPage == "overview") UpdateOverviewMiningSummary();
            })); } catch (InvalidOperationException) { }
            return;
        }
        UpdateMinedBlockCounters();
        if (_currentPage == "overview") UpdateOverviewMiningSummary();
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
            await RefreshReceiveAddressesAsync(_lastAddress);
            _receiveMessage.Text = "Адрес создан. Его можно скопировать или отсканировать QR-код.";
            UpdateReceiveAddressDisplay();
            BuildPage();
        }
        catch (Exception ex)
        {
            ShowLocalizedMessage(ex.Message, "Не удалось создать адрес", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyReceiveAddress()
    {
        string? address = _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
        if (string.IsNullOrWhiteSpace(address))
        {
            ShowLocalizedMessage("В текущем кошельке пока нет адресов. Создайте новый адрес.", "Адрес ещё не создан",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _lastAddress = address;
        Clipboard.SetText(address);
        _receiveMessage.Text = "Адрес скопирован. Теперь его можно отправить отправителю.";
    }

    private void SaveReceiveQrCode()
    {
        if (_receiveQrCode.Image is not Image qrImage || _receiveAddressCombo.SelectedItem is not string) return;
        using var dialog = new SaveFileDialog
        {
            Title = TranslateUiText("Сохранить QR-код адреса"),
            FileName = "GeoCoin-address.png",
            Filter = "Изображение PNG (*.png)|*.png",
            AddExtension = true,
            DefaultExt = "png",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            qrImage.Save(dialog.FileName, ImageFormat.Png);
            _receiveMessage.Text = "QR-код сохранён как изображение PNG.";
            _receiveMessage.ForeColor = Success;
            ApplyUiLanguage(_receiveMessage);
        }
        catch (Exception ex)
        {
            _receiveMessage.Text = "Не удалось сохранить QR-код: " + ex.Message;
            _receiveMessage.ForeColor = Error;
            ApplyUiLanguage(_receiveMessage);
        }
    }

    private async void ImportPaymentQrFromImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = TranslateUiText("Выберите изображение с QR-кодом GeoCoin"),
            Filter = _uiLanguage switch
            {
                "en" => "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                "zh-CN" => "图像 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件 (*.*)|*.*",
                "pt-BR" => "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos os arquivos (*.*)|*.*",
                "it" => "Immagini (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tutti i file (*.*)|*.*",
                _ => "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы (*.*)|*.*"
            },
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string payload;
            using (Image source = Image.FromFile(dialog.FileName))
            using (var bitmap = new Bitmap(source))
            {
                var reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new DecodingOptions { TryHarder = true }
                };
                payload = reader.Decode(bitmap)?.Text?.Trim() ?? "";
            }
            if (payload.Length == 0)
                throw new InvalidOperationException("В выбранном изображении не найден QR-код.");

            string address = payload;
            string? amountText = null;
            if (payload.StartsWith("geocoin:", StringComparison.OrdinalIgnoreCase))
            {
                string body = payload["geocoin:".Length..].TrimStart('/');
                int queryIndex = body.IndexOf('?');
                if (queryIndex >= 0)
                {
                    string query = body[(queryIndex + 1)..];
                    body = body[..queryIndex];
                    foreach (string parameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pair = parameter.Split('=', 2);
                        if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]).Equals("amount", StringComparison.OrdinalIgnoreCase))
                            amountText = Uri.UnescapeDataString(pair[1]);
                    }
                }
                address = Uri.UnescapeDataString(body.TrimEnd('/'));
            }
            if (address.Length < 20 || address.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("QR-код не содержит корректный адрес GeoCoin.");

            if (_rpc is not null && IsNodeRunning())
            {
                JsonElement validation = await _rpc.CallAsync("validateaddress", new object[] { address });
                if (validation.ValueKind != JsonValueKind.Object ||
                    !validation.TryGetProperty("isvalid", out JsonElement isValid) || isValid.ValueKind != JsonValueKind.True ||
                    !validation.TryGetProperty("isgeocoin", out JsonElement isGeoCoin) || isGeoCoin.ValueKind != JsonValueKind.True)
                    throw new InvalidOperationException("Этот QR-код содержит адрес другой сети или неверный адрес.");
            }
            else if (!address.StartsWith("GEO", StringComparison.OrdinalIgnoreCase) || address.Any(character => !char.IsLetterOrDigit(character)))
            {
                throw new InvalidOperationException("QR-код не содержит адрес GeoCoin.");
            }

            _sendAddress.Text = address;
            if (!string.IsNullOrWhiteSpace(amountText))
            {
                string normalizedAmount = amountText.Trim().Replace(',', '.');
                if (decimal.TryParse(normalizedAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) &&
                    amount > 0 && decimal.Round(amount, 8) == amount)
                    _sendAmount.Text = amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));
            }
            _sendMessage.Text = "Адрес загружен из QR-кода. Перед отправкой проверьте адрес и сумму.";
            _sendMessage.ForeColor = Success;
            ApplyUiLanguage(_sendMessage);
        }
        catch (Exception ex)
        {
            _sendMessage.Text = "Не удалось импортировать QR-код: " + ex.Message;
            _sendMessage.ForeColor = Error;
            ApplyUiLanguage(_sendMessage);
        }
    }

    private async Task RefreshReceiveAddressesAsync(string? preferredAddress = null)
    {
        if (_loadingReceiveAddresses || _rpc is null || _currentPage != "receive" || !File.Exists(AppPaths.WalletPath)) return;
        _loadingReceiveAddresses = true;
        _receiveAddressCombo.Enabled = false;
        _receiveMessage.Text = "Загружаю адреса текущего кошелька…";
        try
        {
            string? previous = preferredAddress ?? _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
            List<string> addresses = await GetWalletAddressesAsync();
            if (!string.IsNullOrWhiteSpace(preferredAddress) && !addresses.Contains(preferredAddress, StringComparer.Ordinal))
                addresses.Add(preferredAddress);

            _receiveAddressCombo.BeginUpdate();
            _receiveAddressCombo.Items.Clear();
            foreach (string address in addresses) _receiveAddressCombo.Items.Add(address);
            int selected = previous is null ? -1 : addresses.FindIndex(address => address == previous);
            _receiveAddressCombo.SelectedIndex = selected >= 0 ? selected : (addresses.Count > 0 ? 0 : -1);
            _receiveAddressCombo.EndUpdate();

            if (_receiveAddressCombo.SelectedItem is string selectedAddress)
            {
                _lastAddress = selectedAddress;
                _receiveMessage.Text = "Выберите адрес для перевода или создайте новый. QR-код содержит только адрес.";
            }
            else
            {
                _lastAddress = null;
                _receiveMessage.Text = "В кошельке нет адресов. Создайте новый адрес для получения монет.";
            }
            UpdateReceiveAddressDisplay();
        }
        catch (Exception ex)
        {
            _receiveMessage.Text = "Не удалось загрузить адреса кошелька: " + ex.Message;
            UpdateReceiveAddressDisplay();
        }
        finally
        {
            _loadingReceiveAddresses = false;
            if (_currentPage == "receive" && !IsDisposed)
                _receiveAddressCombo.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);
        }
    }

    private void UpdateReceiveAddressDisplay()
    {
        string? address = _receiveAddressCombo.SelectedItem as string ?? _lastAddress;
        _receiveAddress.Text = string.IsNullOrWhiteSpace(address) ? "В кошельке пока нет адреса" : address;
        _receiveCopyButton.Enabled = !string.IsNullOrWhiteSpace(address);
        _saveReceiveQrButton.Enabled = !string.IsNullOrWhiteSpace(address);

        Image? oldImage = _receiveQrCode.Image;
        _receiveQrCode.Image = null;
        oldImage?.Dispose();
        if (string.IsNullOrWhiteSpace(address))
        {
            _receiveQrCode.Visible = false;
            _receiveQrCaption.Visible = false;
            return;
        }

        try
        {
            using QRCodeData data = QRCodeGenerator.GenerateQrCode(address, QRCodeGenerator.ECCLevel.Q);
            using var renderer = new PngByteQRCode(data);
            byte[] png = renderer.GetGraphic(8);
            using var stream = new MemoryStream(png);
            using Image image = Image.FromStream(stream);
            _receiveQrCode.Image = new Bitmap(image);
            _receiveQrCode.Visible = true;
            _receiveQrCaption.Visible = true;
            _saveReceiveQrButton.Enabled = true;
        }
        catch (Exception ex)
        {
            _receiveQrCode.Visible = false;
            _receiveQrCaption.Visible = false;
            _saveReceiveQrButton.Enabled = false;
            WriteNodeLog("Не удалось подготовить QR-код адреса: " + ex.Message);
        }
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
                    fee == 0 ? "—" : Math.Abs(fee).ToString("0.########", UiLocalization.Culture(_uiLanguage)) + " GC", status, address);
                DataGridViewRow row = grid.Rows[rowIndex];
                row.Tag = txid;
                row.Cells[5].ToolTipText = address;
                row.Cells[2].Style.ForeColor = amount < 0 ? Error : Success;
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
        if (ShowLocalizedMessage("Этот ключ будет добавлен в текущий кошелёк. Импорт восстановит историю адреса и может занять время. Продолжить?",
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
            ResetWalletHistoryBackfill();
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
        if (ShowLocalizedMessage("Ключ даёт полный контроль над монетами этого адреса. Он будет записан в незашифрованный текстовый файл. Любой, кто получит файл, сможет потратить эти монеты. Продолжить?",
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
            ShowLocalizedMessage("Выберите новый файл. Экспорт приватного ключа не перезаписывает существующие файлы.",
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
            ShowLocalizedMessage("Приватный ключ сохранён. Он открывает только выбранный адрес; файл не зашифрован.",
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

    private async Task<FeeEstimate> CalculateSendFeeEstimateAsync()
    {
        if (!IsNodeRunning() || _rpc is null)
            throw new InvalidOperationException("Сначала запустите ноду, чтобы рассчитать комиссию.");

        JsonElement feeInfo = await _rpc.CallAsync("getfeerate");
        decimal minimum = ReadDecimal(feeInfo, "min_tx_fee_gc");
        decimal ratePerKbGc = ReadDecimal(feeInfo, "recommended_fee_rate_per_kb_gc");
        if (ratePerKbGc <= 0 && feeInfo.TryGetProperty("recommended_fee_rate_per_kb", out JsonElement angleRate) &&
            angleRate.TryGetDecimal(out decimal rateAngles))
            ratePerKbGc = rateAngles / 100_000_000m;
        if (minimum <= 0 && feeInfo.TryGetProperty("min_tx_fee", out JsonElement minimumAngles) &&
            minimumAngles.TryGetDecimal(out decimal minimumAngleValue))
            minimum = minimumAngleValue / 100_000_000m;
        if (minimum <= 0)
            throw new InvalidOperationException("Нода не сообщила минимальную комиссию.");

        // GeoCoin's wallet estimate uses 40 base + 1,800 per input + 60 per output.
        // Assume one input and recipient plus change outputs, matching a typical send.
        const int typicalSerializedBytes = 1960;
        decimal estimated = Math.Max(minimum, ratePerKbGc * typicalSerializedBytes / 1000m);
        estimated = decimal.Ceiling(estimated * 100_000_000m) / 100_000_000m;
        return new FeeEstimate(estimated, minimum, Math.Max(0, ratePerKbGc), typicalSerializedBytes);
    }

    private string FormatSendFeeAmount(decimal amount)
        => amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));

    private string FormatSendFeeEstimate(FeeEstimate estimate)
    {
        string approximate = FormatSendFeeAmount(estimate.ApproximateGc);
        string minimum = FormatSendFeeAmount(estimate.MinimumGc);
        string rate = FormatSendFeeAmount(estimate.RateGcPerKb);
        if (_uiLanguage == "en")
        {
            _sendFeeHint.Text = $"Minimum {minimum} GC · rate {rate} GC/kB · typical transaction about {estimate.AssumedBytes} bytes.";
            return $"Estimated fee: about {approximate} GC";
        }
        if (_uiLanguage == "zh-CN")
        {
            _sendFeeHint.Text = $"最低 {minimum} GC · 费率 {rate} GC/千字节 · 按约 {estimate.AssumedBytes} 字节的普通交易估算。";
            return $"预计手续费：约 {approximate} GC";
        }
        if (_uiLanguage == "pt-BR")
        {
            _sendFeeHint.Text = $"Mínimo {minimum} GC · taxa {rate} GC/kB · transação típica de cerca de {estimate.AssumedBytes} bytes.";
            return $"Taxa estimada: cerca de {approximate} GC";
        }
        if (_uiLanguage == "it")
        {
            _sendFeeHint.Text = $"Minimo {minimum} GC · tariffa {rate} GC/kB · transazione tipica di circa {estimate.AssumedBytes} byte.";
            return $"Commissione stimata: circa {approximate} GC";
        }
        _sendFeeHint.Text = $"Минимум {minimum} GC · ставка {rate} GC/кБ · обычная транзакция около {estimate.AssumedBytes} байт.";
        return $"Ориентировочная комиссия: {approximate} GC";
    }

    private async Task RefreshSendFeeEstimateAsync()
    {
        if (_refreshingSendFee || _currentPage != "send" || !IsNodeRunning() || _sendFeeEstimate.IsDisposed) return;
        _refreshingSendFee = true;
        try
        {
            FeeEstimate estimate = await CalculateSendFeeEstimateAsync();
            if (_currentPage != "send" || _sendFeeEstimate.IsDisposed) return;
            _sendFeeEstimate.Text = FormatSendFeeEstimate(estimate);
            ApplyUiLanguage(_sendFeeEstimate);
            ApplyUiLanguage(_sendFeeHint);
        }
        catch (Exception ex)
        {
            if (_currentPage == "send" && !_sendFeeEstimate.IsDisposed)
            {
                _sendFeeEstimate.Text = "Не удалось получить оценку комиссии.";
                _sendFeeHint.Text = ex.Message;
                ApplyUiLanguage(_sendFeeEstimate);
                ApplyUiLanguage(_sendFeeHint);
            }
        }
        finally
        {
            _refreshingSendFee = false;
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
            _sendMessage.ForeColor = Error;
            return;
        }
        if (!decimal.TryParse(rawAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
        {
            _sendMessage.Text = "Введите сумму больше нуля, используя точку или запятую.";
            _sendMessage.ForeColor = Error;
            return;
        }
        if (decimal.Round(amount, 8) != amount)
        {
            _sendMessage.Text = "Сумма может содержать не больше 8 знаков после запятой.";
            _sendMessage.ForeColor = Error;
            return;
        }
        string password = _sendPassword.Text;
        if (password.Length == 0)
        {
            _sendMessage.Text = "Введите пароль кошелька.";
            _sendMessage.ForeColor = Error;
            return;
        }

        FeeEstimate feeEstimate;
        try
        {
            feeEstimate = await CalculateSendFeeEstimateAsync();
            _sendFeeEstimate.Text = FormatSendFeeEstimate(feeEstimate);
            ApplyUiLanguage(_sendFeeEstimate);
            ApplyUiLanguage(_sendFeeHint);
        }
        catch (Exception ex)
        {
            _sendMessage.Text = "Не удалось рассчитать комиссию: " + ex.Message;
            _sendMessage.ForeColor = Error;
            return;
        }

        string amountForReview = amount.ToString("0.########", UiLocalization.Culture(_uiLanguage));
        string review = _uiLanguage switch
        {
            "en" => $"Send {amountForReview} GC?\n\nRecipient:\n{address}\n\n" +
                    $"Fee: about {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                    $"Minimum: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. This estimate assumes a typical {feeEstimate.AssumedBytes}-byte transaction; the final fee depends on the selected inputs and may be higher.\n\n" +
                    "A confirmed transfer cannot be canceled.",
            "zh-CN" => $"发送 {amountForReview} GC？\n\n收款地址：\n{address}\n\n" +
                       $"手续费：约 {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                       $"最低：{FormatSendFeeAmount(feeEstimate.MinimumGc)} GC。估算按约 {feeEstimate.AssumedBytes} 字节的普通交易计算；实际费用取决于所选输入，可能更高。\n\n" +
                       "确认后的转账无法撤销。",
            "pt-BR" => $"Enviar {amountForReview} GC?\n\nDestinatário:\n{address}\n\n" +
                       $"Taxa: cerca de {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                       $"Mínimo: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. A estimativa considera uma transação típica de {feeEstimate.AssumedBytes} bytes; a taxa final depende das entradas selecionadas e pode ser maior.\n\n" +
                       "Uma transferência confirmada não pode ser cancelada.",
            "it" => $"Inviare {amountForReview} GC?\n\nDestinatario:\n{address}\n\n" +
                    $"Commissione: circa {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                    $"Minimo: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. La stima considera una transazione tipica di {feeEstimate.AssumedBytes} byte; la commissione finale dipende dagli input selezionati e potrebbe essere maggiore.\n\n" +
                    "Una transazione confermata non può essere annullata.",
            _ => $"Отправить {amountForReview} GC?\n\nПолучатель:\n{address}\n\n" +
                 $"Комиссия: примерно {FormatSendFeeAmount(feeEstimate.ApproximateGc)} GC\n" +
                 $"Минимум: {FormatSendFeeAmount(feeEstimate.MinimumGc)} GC. Расчёт сделан для обычной транзакции около {feeEstimate.AssumedBytes} байт; точная сумма зависит от выбранных входов и может быть выше.\n\n" +
                 "После подтверждения перевод нельзя отменить."
        };
        string reviewTitle = _uiLanguage switch { "en" => "Review transfer", "zh-CN" => "确认转账", "pt-BR" => "Revise a transferência", "it" => "Conferma il trasferimento", _ => "Проверьте перевод" };
        if (ShowLocalizedMessage(review, reviewTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _sendButton.Enabled = false;
        _sendMessage.Text = "Отправляю перевод…";
        _sendMessage.ForeColor = Muted;
        try
        {
            // The node recalculates the fee from selected inputs and applies at least this estimate.
            var parameters = new object[] { address, amount, feeEstimate.ApproximateGc, password };
            JsonElement result = await _rpc.CallAsync("sendtoaddress", parameters);
            string txid = result.ValueKind == JsonValueKind.String ? result.GetString() ?? "" : result.ToString();
            _sendMessage.Text = "Перевод принят нодой. Номер: " + txid;
            _sendMessage.ForeColor = Success;
            _sendPassword.Clear();
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            _sendMessage.Text = ex.Message;
            _sendMessage.ForeColor = Error;
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
                if (_currentPage is "overview" or "send" or "receive" or "transactions" or "wallet") BuildPage();
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
                try
                {
                    JsonElement walletInfo = await _rpc.CallAsync("getwalletinfo");
                    _matureCoins = ReadDecimal(walletInfo, "balance");
                    _immatureCoins = ReadDecimal(walletInfo, "immature_balance");
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

            await RefreshLifetimeWalletMinedBlocksAsync();
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
                    ? FormatGcAmount(_balance)
                    : "Кошелёк ещё не создан";
                _nodeState.Text = _nodeStatus;
                _syncState.Text = SyncDescription();
                _peerState.Text = $"Блоков: {_height:N0}     Узлов сети: {_connections}";
                UpdateOverviewMiningSummary();
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
        _miningState.ForeColor = _cpuMiningActive ? Success : Muted;
        _cpuHashrate.Text = _cpuMiningActive
            ? $"Скорость CPU: {FormatHashrate(_cpuHashrateValue)}"
            : "Скорость CPU: —";
        _threadCount.Enabled = !_cpuMiningActive;
        _mineButton.Text = _cpuMiningActive ? "Остановить CPU" : "Запустить CPU";
        _mineButton.Enabled = IsNodeRunning() && File.Exists(AppPaths.WalletPath);

        _gpuMiningState.Text = _gpuStatusText;
        _gpuMiningState.ForeColor = _gpuMiningActive ? Success : Muted;
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

    private void UpdateOverviewMiningSummary()
    {
        if (_lifetimeMinedBlocksLabel.IsDisposed || _matureCoinsLabel.IsDisposed || _immatureCoinsLabel.IsDisposed) return;
        int lifetimeCount;
        lock (_minedBlocksLock) lifetimeCount = _lifetimeMinedBlockIds.Count;
        _lifetimeMinedBlocksLabel.Text = $"Добыто блоков за всё время: {lifetimeCount:N0}";
        if (IsNodeRunning() && File.Exists(AppPaths.WalletPath))
        {
            _matureCoinsLabel.Text = "Зрелые монеты: " + FormatGcAmount(_matureCoins);
            _immatureCoinsLabel.Text = "Незрелые монеты: " + FormatGcAmount(_immatureCoins);
        }
        else
        {
            _matureCoinsLabel.Text = "Зрелые монеты: —";
            _immatureCoinsLabel.Text = "Незрелые монеты: —";
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
        bool processRunning = IsNodeProcessRunning();
        _statusLabel.Text = processRunning ? _nodeStatus : "Нода остановлена";
        _statusLabel.Text = TranslateUiText(_statusLabel.Text);
        _statusLabel.ForeColor = processRunning
            ? (_nodeStatus.Contains("завершена", StringComparison.OrdinalIgnoreCase)
                ? Success : Accent)
            : Muted;
        _statusLabel.BackColor = processRunning
            ? (_darkTheme ? Color.FromArgb(48, 67, 53) : Color.FromArgb(229, 235, 221))
            : (_darkTheme ? Color.FromArgb(48, 55, 50) : Color.FromArgb(226, 228, 219));
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
            ShowLocalizedMessage("Дождитесь завершения запуска узла или подмайнера, затем повторите выход.",
                "Выполняется запуск", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool gpuRunning = _gpuMinerProcess is { HasExited: false };
        if (_allowClose || (!IsNodeRunning() && !gpuRunning)) return;
        e.Cancel = true;
        if (_closePromptOpen) return;
        _closePromptOpen = true;
        var result = ShowLocalizedMessage(gpuRunning && !IsNodeRunning()
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

    private string FormatGcAmount(decimal amount, bool includePositiveSign = false)
    {
        string prefix = includePositiveSign && amount > 0 ? "+" : "";
        return prefix + amount.ToString("0.########", UiLocalization.Culture(_uiLanguage)) + " GC";
    }

    private string FormatTransactionTime(JsonElement entry)
    {
        long timestamp = ReadInt64(entry, "time");
        if (timestamp <= 0) timestamp = ReadInt64(entry, "timereceived");
        if (timestamp <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("g", UiLocalization.Culture(_uiLanguage)); }
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

    private string FormatHashrate(double value)
    {
        string[] units = { "хеш/с", "кХеш/с", "МХеш/с", "ГХеш/с", "ТХеш/с", "ПХеш/с" };
        int unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        return value.ToString(value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00", UiLocalization.Culture(_uiLanguage)) + " " + units[unit];
    }

    private Panel AddCard(int x, int y, int width, int height)
    {
        var card = new Panel
        {
            Location = UiPoint(x, y),
            Size = UiSize(width, height),
            BackColor = Surface,
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
        button.FlatAppearance.BorderColor = _darkTheme ? Color.FromArgb(78, 88, 79) : Color.FromArgb(194, 198, 187);
        button.BackColor = Surface;
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
        box.BackColor = InputSurface;
        box.ForeColor = Ink;
    }

    private void LoadUiPreferences()
    {
        try
        {
            if (!File.Exists(AppPaths.UiSettingsPath)) return;
            UiPreferences? settings = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(AppPaths.UiSettingsPath));
            _uiLanguage = UiLocalization.NormalizeLanguage(settings?.Language);
            _darkTheme = settings?.DarkTheme ?? false;
        }
        catch
        {
            _uiLanguage = "ru";
            _darkTheme = false;
        }
    }

    private void SaveUiPreferences()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            string json = JsonSerializer.Serialize(new UiPreferences
            {
                Language = _uiLanguage,
                DarkTheme = _darkTheme
            });
            File.WriteAllText(AppPaths.UiSettingsPath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            WriteNodeLog("Не удалось сохранить настройки интерфейса: " + ex.Message);
        }
    }

    private void UpdatePreferenceControlText()
    {
        _themeButton.Text = _darkTheme ? "Светлая тема" : "Тёмная тема";
        int selectedIndex = Array.IndexOf(UiLocalization.LanguageCodes, _uiLanguage);
        _languageCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private DialogResult ShowLocalizedMessage(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        => MessageBox.Show(this, TranslateUiText(message), TranslateUiText(caption), buttons, icon, defaultButton);

    private string TranslateUiText(string value)
    {
        string english = NormalizeUiTextToEnglish(value);
        if (_uiLanguage == "en") return english;
        if (_uiLanguage == "ru")
        {
            if (EnglishToRussian.TryGetValue(english, out string? russian)) return russian;
            if (UiLocalization.TryNormalizeEnglishToRussian(english, out russian)) return russian;
            return TranslateCommonPrefixes(english, english: false);
        }
        return UiLocalization.TranslateFromEnglish(_uiLanguage, english);
    }

    private string NormalizeUiTextToEnglish(string value)
    {
        if (RussianToEnglish.TryGetValue(value, out string? english)) return english;
        english = TranslateCommonPrefixes(value, english: true);
        if (!string.Equals(english, value, StringComparison.Ordinal)) return english;
        if (UiLocalization.TryNormalizeRussianToEnglish(value, out english)) return english;
        foreach (string language in UiLocalization.LanguageCodes.Skip(2))
            if (UiLocalization.TryNormalizeToEnglish(language, value, out english)) return english;
        if (EnglishToRussian.TryGetValue(value, out string? russian) && RussianToEnglish.TryGetValue(russian, out english))
            return english;
        return value;
    }

    private static string TranslateCommonPrefixes(string value, bool english)
    {
        (string Russian, string English)[] prefixes =
        {
            ("Блоков: ", "Blocks: "), ("Узлов сети: ", "Network peers: "), ("подключений: ", "connections: "),
            ("Скорость CPU: ", "CPU hashrate: "), ("Скорость GPU: ", "GPU hashrate: "), ("Скорость: ", "Hashrate: "),
            ("Сегодня: ", "Today: "), ("Текущая сессия: ", "Current session: "), ("Последняя сессия: ", "Last session: "),
            ("Сессия: ", "Session: "), ("Добыто блоков за всё время: ", "Blocks mined all time: "),
            ("Найдено адресов: ", "Addresses found: "), ("Найдено видеокарт: ", "GPUs found: "),
            ("Синхронизировано ", "Synced "), ("Подключаюсь к ", "Connecting to "),
            ("Ключ добавлен, но история восстановлена не полностью (", "Key imported, but address history is incomplete ("),
            ("Зрелые монеты: ", "Mature coins: "), ("Незрелые монеты: ", "Immature coins: "),
            ("Ориентировочная комиссия: ", "Estimated fee: "), ("Не удалось рассчитать комиссию: ", "Could not calculate the fee: "),
            ("Не удалось импортировать QR-код: ", "Could not import QR code: "), ("Не удалось сохранить QR-код: ", "Could not save QR code: "),
            ("Адрес награды: ", "Reward address: "), ("ID транзакции: ", "Transaction ID: "),
            ("Перевод принят нодой. Номер: ", "Transfer accepted by the node. ID: "),
            ("Не удалось загрузить адреса кошелька: ", "Could not load wallet addresses: "),
            ("Не удалось загрузить адреса: ", "Could not load addresses: "),
            ("Не удалось импортировать ключ: ", "Could not import key: "), ("Не удалось экспортировать ключ: ", "Could not export key: "),
            ("Не удалось загрузить историю: ", "Could not load history: "),
            ("Не удалось подключиться: ", "Could not connect: "), ("Не удалось запустить GPU-майнинг: ", "Could not start GPU mining: "),
            ("Ошибка GPU-подмайнера: ", "GPU subminer error: "), ("Ошибка GPU: ", "GPU error: ")
        };
        foreach ((string source, string target) in prefixes)
        {
            string from = english ? source : target;
            if (value.StartsWith(from, StringComparison.Ordinal))
            {
                string remainder = value[from.Length..];
                if (english)
                    remainder = remainder.Replace(" блоков", " blocks", StringComparison.Ordinal)
                        .Replace(" блока", " blocks", StringComparison.Ordinal).Replace(" блок", " block", StringComparison.Ordinal)
                        .Replace(". Экспорт ключа создаёт незашифрованный текстовый файл.", ". Exporting a key creates an unencrypted text file.", StringComparison.Ordinal);
                else
                    remainder = remainder.Replace(" blocks", " блоков", StringComparison.Ordinal)
                        .Replace(" block", " блок", StringComparison.Ordinal)
                        .Replace(". Exporting a key creates an unencrypted text file.", ". Экспорт ключа создаёт незашифрованный текстовый файл.", StringComparison.Ordinal);
                return (english ? target : source) + remainder;
            }
        }
        if (english)
        {
            value = value.Replace("     Узлов сети: ", "     Network peers: ", StringComparison.Ordinal)
                .Replace(" · подключений: ", " · connections: ", StringComparison.Ordinal);
        }
        else
        {
            value = value.Replace("     Network peers: ", "     Узлов сети: ", StringComparison.Ordinal)
                .Replace(" · connections: ", " · подключений: ", StringComparison.Ordinal);
        }
        return value;
    }

    private void ApplyUiLanguage(Control root)
    {
        if (root is TextBox textBox)
            textBox.PlaceholderText = TranslateUiText(textBox.PlaceholderText);
        else if (root is not ComboBox)
            root.Text = TranslateUiText(root.Text);

        if (root is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = TranslateUiText(column.HeaderText);
            foreach (DataGridViewRow row in grid.Rows)
                foreach (DataGridViewCell cell in row.Cells)
                    if (cell.Value is string value) cell.Value = TranslateUiText(value);
        }

        foreach (Control child in root.Controls)
            ApplyUiLanguage(child);
    }

    private void ApplyThemeToTree(Control root)
    {
        if (root == this || root == _headerPanel || root == _pagePanel)
            root.BackColor = Canvas;
        else if (root.Name is "navigationRail" or "railFooter")
            root.BackColor = Rail;
        else if (root == _receiveQrCode)
            root.BackColor = Color.White;
        else if (root is TextBox textBox)
        {
            if (textBox != _nodeLogBox)
            {
                textBox.BackColor = InputSurface;
                textBox.ForeColor = Ink;
            }
        }
        else if (root is ComboBox combo)
        {
            combo.BackColor = InputSurface;
            combo.ForeColor = Ink;
        }
        else if (root is Button button && button.Parent?.Name == "navigationRail")
        {
            bool selected = string.Equals(button.Tag as string, _currentPage, StringComparison.Ordinal);
            button.BackColor = selected
                ? (_darkTheme ? Color.FromArgb(48, 61, 52) : Color.FromArgb(44, 59, 48))
                : Rail;
            button.ForeColor = selected ? Color.FromArgb(241, 218, 173) : Color.FromArgb(206, 215, 205);
        }
        else if (root is Panel panel)
        {
            Color background = panel.BackColor;
            if (background == LightCanvas || background == DarkCanvas) panel.BackColor = Canvas;
            else if (background == Color.White || background == DarkSurface) panel.BackColor = Surface;
            else if (background == Color.FromArgb(220, 220, 214) || background == Color.FromArgb(61, 68, 62)) panel.BackColor = Divider;
        }

        if (root is Label label)
        {
            if (label.ForeColor == LightInk || label.ForeColor == DarkInk) label.ForeColor = Ink;
            else if (label.ForeColor == LightMuted || label.ForeColor == DarkMuted) label.ForeColor = Muted;
        }
        if (root is Button nonRailButton && nonRailButton.Parent?.Name != "navigationRail")
        {
            Color background = nonRailButton.BackColor;
            if (background == Color.White || background == DarkSurface) nonRailButton.BackColor = Surface;
            else if (background == Color.FromArgb(166, 129, 63) || background == DarkAccent) nonRailButton.BackColor = Accent;
            if (nonRailButton.ForeColor == LightInk || nonRailButton.ForeColor == DarkInk) nonRailButton.ForeColor = Ink;
            nonRailButton.FlatAppearance.BorderColor = _darkTheme ? Color.FromArgb(78, 88, 79) : Color.FromArgb(194, 198, 187);
        }
        if (root is DataGridView dataGrid)
        {
            dataGrid.BackgroundColor = Surface;
            dataGrid.DefaultCellStyle.BackColor = Surface;
            dataGrid.DefaultCellStyle.ForeColor = Ink;
            dataGrid.DefaultCellStyle.SelectionBackColor = _darkTheme ? Color.FromArgb(65, 78, 67) : Color.FromArgb(226, 233, 222);
            dataGrid.DefaultCellStyle.SelectionForeColor = Ink;
            dataGrid.ColumnHeadersDefaultCellStyle.BackColor = Canvas;
            dataGrid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            dataGrid.GridColor = Divider;
        }
        foreach (Control child in root.Controls)
            ApplyThemeToTree(child);
    }

    private static void OpenLink(string address)
        => Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
}
