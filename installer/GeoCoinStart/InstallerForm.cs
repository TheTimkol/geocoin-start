using System.IO.Compression;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace GeoCoinStart;

internal sealed class InstallerForm : Form
{
    private sealed record InstallProgress(int Percent, string Message);

    private static readonly Dictionary<string, string> RussianToEnglish = new(StringComparer.Ordinal)
    {
        ["Установлена более новая версия"] = "A newer version is installed",
        ["Переустановить GeoCoin-Start"] = "Reinstall GeoCoin-Start",
        ["Версия на компьютере новее версии установщика."] = "The installed version is newer than this installer.",
        ["Эта версия уже установлена. Её можно переустановить."] = "This version is already installed. You can reinstall it.",
        ["Установка GeoCoin-Start"] = "Install GeoCoin-Start",
        ["просто и по шагам"] = "simple and step by step",
        ["Приложение уже установлено. Кнопка ниже обновит его до версии из этого установщика."] = "The app is already installed. The button below will update it to the version in this installer.",
        ["Один мастер установки. Официальный GeoCoin скачается автоматически."] = "One setup wizard. The official GeoCoin package will be downloaded automatically.",
        ["Для установки нужен интернет. Загрузчик сверит официальный пакет GeoCoin с контрольной суммой."] = "Internet is required. The installer will verify the official GeoCoin package checksum.",
        ["Дальше вас ждут обзор, майнинг, отправка и получение; файлы кошелька останутся на этом компьютере."] = "Next you can explore the overview, mine, send and receive coins. Wallet files stay on this computer.",
        ["Оригинальный проект: NoemaCrypt / GeoCoin ↗"] = "Original project: NoemaCrypt / GeoCoin ↗",
        ["Знак GeoCoin: geocoin.network ↗"] = "GeoCoin symbol: geocoin.network ↗",
        ["Редистрибьютор: TheTimkol ↗"] = "Redistributor: TheTimkol ↗",
        ["Готово к обновлению GeoCoin-Start"] = "Ready to update GeoCoin-Start",
        ["Готово к установке"] = "Ready to install",
        ["Обновить GeoCoin-Start"] = "Update GeoCoin-Start",
        ["Установить"] = "Install",
        ["Перед обновлением закройте GeoCoin-Start и остановите ноду. Затем запустите установщик снова."] = "Close GeoCoin-Start before updating. Stop the node too, then run the installer again.",
        ["Сначала закройте GeoCoin-Start"] = "Close GeoCoin-Start first",
        ["Запрашиваю штатное закрытие GeoCoin-Start…"] = "Asking GeoCoin-Start to close cleanly…",
        ["Эта версия GeoCoin-Start не поддерживает автоматическое закрытие. Щёлкните правой кнопкой по значку программы в области уведомлений, выберите «Выйти из GeoCoin-Start», затем нажмите «Обновить» ещё раз."] = "This GeoCoin-Start version cannot close automatically. Right-click its notification-area icon, choose Exit GeoCoin-Start, then click Update again.",
        ["Закройте GeoCoin-Start через значок в области уведомлений, затем нажмите «Обновить» ещё раз."] = "Close GeoCoin-Start from the notification area, then click Update again.",
        ["GeoCoin-Start ещё работает. Если появилось подтверждение, разрешите остановить ноду и майнер; затем установщик продолжит работу автоматически."] = "GeoCoin-Start is still running. If a confirmation appeared, allow the node and miner to stop; setup will continue automatically.",
        ["GeoCoin-Start не закрылся. Если вы отменили остановку, закройте клиент через значок в трее и повторите обновление."] = "GeoCoin-Start did not close. If you cancelled shutdown, close the app from the system tray and try again.",
        ["Подготавливаю установку…"] = "Preparing installation…",
        ["Установка завершена. Открываю приложение…"] = "Installation complete. Opening the app…",
        ["Установка завершена."] = "Installation complete.",
        ["Установка не завершена"] = "Installation did not complete",
        ["Не удалось установить GeoCoin. Проверьте, что в папке установки достаточно места, и попробуйте ещё раз."] = "Could not install GeoCoin. Check that the installation folder has enough free space, then try again.",
        ["Проверка целостности официального архива не пройдена."] = "Official archive integrity check failed.",
        ["В архиве обнаружен недопустимый путь."] = "The archive contains an invalid path.",
        ["Не удалось определить файл установщика."] = "Could not locate the installer file.",
        ["Скачиваю официальный пакет GeoCoin…"] = "Downloading the official GeoCoin package…",
        ["Проверяю официальный пакет…"] = "Verifying the official package…",
        ["Устанавливаю файлы GeoCoin…"] = "Installing GeoCoin files…",
        ["Завершаю установку…"] = "Finishing installation…",
        ["Удалить GeoCoin-Start"] = "Uninstall GeoCoin-Start",
        ["Простой интерфейс GeoCoin"] = "Simple GeoCoin interface",
        ["Файл приложения не найден."] = "Application file not found."
    };

    private readonly Label _title = new();
    private readonly PictureBox _logo = new();
    private readonly Label _subtitle = new();
    private readonly Label _notes = new();
    private readonly LinkLabel _originalCredit = new();
    private readonly LinkLabel _symbolCredit = new();
    private readonly LinkLabel _distributorCredit = new();
    private readonly ComboBox _languageCombo = new();
    private readonly Label _status;
    private readonly ProgressBar _progress;
    private readonly Button _installButton;
    private readonly bool _alreadyInstalled;
    private string _language;
    private bool _layingOut;

    private delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr windowHandle, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeoutMilliseconds, out IntPtr result);

    public InstallerForm()
    {
        _language = GetPreferredLanguage();
        _alreadyInstalled = File.Exists(AppPaths.InstallMarker) && File.Exists(AppPaths.ExecutablePath);
        Text = "Установка GeoCoin-Start";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        MaximizeBox = true;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(700, 540);
        BackColor = Color.FromArgb(20, 27, 24);
        ForeColor = Color.FromArgb(238, 239, 234);
        Font = new Font("Segoe UI", 10);

        _logo.Size = new Size(52, 52);
        _logo.SizeMode = PictureBoxSizeMode.Zoom;
        _logo.Image = (Icon ?? SystemIcons.Application).ToBitmap();
        Controls.Add(_logo);

        _title.Text = "GeoCoin\nпросто и по шагам";
        _title.Font = new Font("Segoe UI Semibold", 19, FontStyle.Bold);
        _title.ForeColor = Color.FromArgb(233, 208, 153);
        _title.AutoSize = true;
        Controls.Add(_title);

        _subtitle.Text = _alreadyInstalled
            ? "Приложение уже установлено. Кнопка ниже обновит его до версии из этого установщика."
            : "Один мастер установки. Официальный GeoCoin скачается автоматически.";
        _subtitle.AutoSize = true;
        _subtitle.ForeColor = Color.FromArgb(201, 208, 199);
        Controls.Add(_subtitle);

        _notes.Text = "Для установки нужен интернет. Загрузчик сверит официальный пакет GeoCoin с контрольной суммой.\n" +
                      "Дальше вас ждут обзор, майнинг, отправка и получение; файлы кошелька останутся на этом компьютере.";
        _notes.AutoSize = true;
        _notes.ForeColor = Color.FromArgb(161, 173, 162);
        Controls.Add(_notes);

        _originalCredit.Text = "Оригинальный проект: NoemaCrypt / GeoCoin ↗";
        _originalCredit.AutoSize = true;
        _originalCredit.LinkColor = Color.FromArgb(233, 208, 153);
        _originalCredit.ActiveLinkColor = Color.White;
        _originalCredit.LinkClicked += (_, _) => OpenLink(AppPaths.OriginalRepositoryUrl);
        Controls.Add(_originalCredit);

        _symbolCredit.Text = "Знак GeoCoin: geocoin.network ↗";
        _symbolCredit.AutoSize = true;
        _symbolCredit.LinkColor = Color.FromArgb(233, 208, 153);
        _symbolCredit.ActiveLinkColor = Color.White;
        _symbolCredit.LinkClicked += (_, _) => OpenLink(AppPaths.OfficialWebsiteUrl);
        Controls.Add(_symbolCredit);

        _distributorCredit.Text = "Редистрибьютор: TheTimkol ↗";
        _distributorCredit.AutoSize = true;
        _distributorCredit.LinkColor = Color.FromArgb(233, 208, 153);
        _distributorCredit.ActiveLinkColor = Color.White;
        _distributorCredit.LinkClicked += (_, _) => OpenLink(AppPaths.DistributorUrl);
        Controls.Add(_distributorCredit);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Items.AddRange(UiLocalization.LanguageNames.Cast<object>().ToArray());
        _languageCombo.SelectedIndex = Math.Max(0, Array.IndexOf(UiLocalization.LanguageCodes, _language));
        _languageCombo.Size = new Size(138, 30);
        _languageCombo.Font = new Font("Segoe UI", 9);
        _languageCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_languageCombo.SelectedIndex < 0) return;
            _language = UiLocalization.LanguageCodes[_languageCombo.SelectedIndex];
            ApplyInstallerLanguage();
        };
        Controls.Add(_languageCombo);

        _status = new Label
        {
            Text = _alreadyInstalled ? "Готово к обновлению GeoCoin-Start" : "Готово к установке",
            AutoSize = true,
            ForeColor = Color.FromArgb(201, 208, 199)
        };
        Controls.Add(_status);

        _progress = new ProgressBar
        {
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Size = new Size(600, 12)
        };
        Controls.Add(_progress);

        _installButton = new Button
        {
            Text = _alreadyInstalled ? "Обновить GeoCoin-Start" : "Установить",
            Size = new Size(190, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(182, 149, 86),
            ForeColor = Color.FromArgb(20, 27, 24),
            Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += InstallButtonClicked;
        Controls.Add(_installButton);

        Resize += (_, _) => LayoutContent();
        Load += (_, _) => LayoutContent();
        ApplyInstallerLanguage();
        LayoutContent();
    }

    private async void InstallButtonClicked(object? sender, EventArgs e)
    {
        if (CompareInstalledVersion() > 0) return;
        _installButton.Enabled = false;
        _languageCombo.Enabled = false;
        _status.Text = LocalizeInstallerText("Подготавливаю установку…");
        try
        {
            if (_alreadyInstalled)
            {
                List<int> runningAppIds = FindInstalledAppProcessIds();
                if (runningAppIds.Count > 0)
                {
                    _status.Text = LocalizeInstallerText("Запрашиваю штатное закрытие GeoCoin-Start…");
                    LayoutContent();
                    if (!RequestInstalledAppShutdown(runningAppIds))
                    {
                        bool exitedDuringRequest = await WaitForInstalledAppExitAsync(TimeSpan.FromSeconds(2));
                        if (!exitedDuringRequest)
                        {
                            _status.Text = LocalizeInstallerText("Закройте GeoCoin-Start через значок в области уведомлений, затем нажмите «Обновить» ещё раз.");
                            LayoutContent();
                            MessageBox.Show(this, LocalizeInstallerText("Эта версия GeoCoin-Start не поддерживает автоматическое закрытие. Щёлкните правой кнопкой по значку программы в области уведомлений, выберите «Выйти из GeoCoin-Start», затем нажмите «Обновить» ещё раз."),
                                LocalizeInstallerText("Сначала закройте GeoCoin-Start"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }

                    bool closedCleanly = await WaitForInstalledAppExitAsync(TimeSpan.FromSeconds(90));
                    if (!closedCleanly)
                    {
                        _status.Text = LocalizeInstallerText("GeoCoin-Start ещё работает. Если появилось подтверждение, разрешите остановить ноду и майнер; затем установщик продолжит работу автоматически.");
                        LayoutContent();
                        MessageBox.Show(this, LocalizeInstallerText("GeoCoin-Start не закрылся. Если вы отменили остановку, закройте клиент через значок в трее и повторите обновление."),
                            LocalizeInstallerText("Сначала закройте GeoCoin-Start"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }
            }

            _status.Text = LocalizeInstallerText("Подготавливаю установку…");
            LayoutContent();
            var progress = new Progress<InstallProgress>(state =>
            {
                _progress.Value = Math.Clamp(state.Percent, 0, 100);
                _status.Text = LocalizeInstallerText(state.Message);
                LayoutContent();
            });
            await InstallAsync(progress, _language);
            SaveAppLanguagePreference();
            _status.Text = LocalizeInstallerText("Установка завершена. Открываю приложение…");
            LayoutContent();
            LaunchInstalledApp();
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = LocalizeInstallerText("Установка не завершена");
            LayoutContent();
            MessageBox.Show(this,
                LocalizeInstallerText("Не удалось установить GeoCoin. Проверьте, что в папке установки достаточно места, и попробуйте ещё раз.") + "\n\n" + ex.Message,
                "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installButton.Enabled = true;
            _languageCombo.Enabled = true;
        }
        finally
        {
            if (!IsDisposed && !_installButton.Enabled)
            {
                _installButton.Enabled = true;
                _languageCombo.Enabled = true;
            }
        }
    }

    private static int CompareInstalledVersion()
    {
        if (!File.Exists(AppPaths.ExecutablePath)) return -1;
        var metadata = FileVersionInfo.GetVersionInfo(AppPaths.ExecutablePath);
        var installed = new Version(metadata.FileMajorPart, metadata.FileMinorPart,
            metadata.FileBuildPart, metadata.FilePrivatePart);
        return installed.CompareTo(Version.Parse(AppPaths.AppVersion + ".0"));
    }

    private static bool IsInstalledAppRunning()
        => FindInstalledAppProcessIds().Count > 0;

    private static List<int> FindInstalledAppProcessIds()
    {
        string expected = Path.GetFullPath(AppPaths.ExecutablePath);
        var processIds = new List<int>();
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(expected)))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited || process.MainModule?.FileName is not string actual) continue;
                    if (string.Equals(Path.GetFullPath(actual), expected, StringComparison.OrdinalIgnoreCase))
                        processIds.Add(process.Id);
                }
                catch { }
            }
        }
        return processIds;
    }

    private static bool RequestInstalledAppShutdown(IEnumerable<int> processIds)
    {
        bool acknowledged = false;
        foreach (int processId in processIds)
        {
            uint targetProcessId = (uint)processId;
            EnumWindowsCallback callback = (windowHandle, _) =>
            {
                GetWindowThreadProcessId(windowHandle, out uint ownerProcessId);
                if (ownerProcessId == targetProcessId)
                {
                    IntPtr delivered = SendMessageTimeout(windowHandle,
                        (uint)AppPaths.RequestInstallerShutdownMessage, IntPtr.Zero, IntPtr.Zero,
                        0x0002, 1500, out IntPtr response);
                    if (delivered != IntPtr.Zero && response != IntPtr.Zero)
                        acknowledged = true;
                }
                return true;
            };
            EnumWindows(callback, IntPtr.Zero);
            GC.KeepAlive(callback);
        }
        return acknowledged;
    }

    private static async Task<bool> WaitForInstalledAppExitAsync(TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!IsInstalledAppRunning()) return true;
            await Task.Delay(250);
        }
        return !IsInstalledAppRunning();
    }

    private string LocalizeInstallerText(string russianText)
    {
        if (_language == "ru") return russianText;
        if (russianText.Contains('\n'))
            return string.Join(Environment.NewLine, russianText.Split('\n').Select(line => LocalizeInstallerText(line.TrimEnd('\r'))));
        if (!RussianToEnglish.TryGetValue(russianText, out string? english))
        {
            var prefix = RussianToEnglish
                .Where(pair => russianText.StartsWith(pair.Key, StringComparison.Ordinal))
                .OrderByDescending(pair => pair.Key.Length)
                .FirstOrDefault();
            english = prefix.Key is null ? russianText : prefix.Value + russianText[prefix.Key.Length..];
        }
        return _language == "en" ? english : UiLocalization.TranslateFromEnglish(_language, english);
    }

    private void ApplyInstallerLanguage()
    {
        Text = LocalizeInstallerText("Установка GeoCoin-Start");
        _title.Text = LocalizeInstallerText("GeoCoin\nпросто и по шагам");
        _subtitle.Text = LocalizeInstallerText(_alreadyInstalled
            ? "Приложение уже установлено. Кнопка ниже обновит его до версии из этого установщика."
            : "Один мастер установки. Официальный GeoCoin скачается автоматически.");
        _notes.Text = LocalizeInstallerText("Для установки нужен интернет. Загрузчик сверит официальный пакет GeoCoin с контрольной суммой.\n" +
                                             "Дальше вас ждут обзор, майнинг, отправка и получение; файлы кошелька останутся на этом компьютере.");
        _originalCredit.Text = LocalizeInstallerText("Оригинальный проект: NoemaCrypt / GeoCoin ↗");
        _symbolCredit.Text = LocalizeInstallerText("Знак GeoCoin: geocoin.network ↗");
        _distributorCredit.Text = LocalizeInstallerText("Редистрибьютор: TheTimkol ↗");
        _status.Text = LocalizeInstallerText(_alreadyInstalled ? "Готово к обновлению GeoCoin-Start" : "Готово к установке");
        int versionOrder = CompareInstalledVersion();
        _installButton.Enabled = versionOrder <= 0;
        _installButton.Text = LocalizeInstallerText(versionOrder switch
        {
            > 0 => "Установлена более новая версия",
            0 => "Переустановить GeoCoin-Start",
            _ => _alreadyInstalled ? "Обновить GeoCoin-Start" : "Установить"
        });
        if (versionOrder > 0)
        {
            _subtitle.Text = LocalizeInstallerText("Версия на компьютере новее версии установщика.");
            _status.Text = _subtitle.Text;
        }
        else if (versionOrder == 0)
        {
            _subtitle.Text = LocalizeInstallerText("Эта версия уже установлена. Её можно переустановить.");
            _status.Text = _subtitle.Text;
        }
        LayoutContent();
    }

    private string GetPreferredLanguage()
    {
        try
        {
            if (File.Exists(AppPaths.UiSettingsPath))
            {
                UiPreferences? saved = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(AppPaths.UiSettingsPath));
                if (!string.IsNullOrWhiteSpace(saved?.Language)) return UiLocalization.NormalizeLanguage(saved.Language);
            }
        }
        catch { }

        string language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language switch
        {
            "en" => "en",
            "zh" => "zh-CN",
            "pt" => "pt-BR",
            "it" => "it",
            _ => "ru"
        };
    }

    private void SaveAppLanguagePreference()
    {
        Directory.CreateDirectory(AppPaths.DataRoot);
        UiPreferences preferences = new() { Language = _language };
        try
        {
            if (File.Exists(AppPaths.UiSettingsPath))
                preferences = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(AppPaths.UiSettingsPath)) ?? preferences;
        }
        catch { }
        preferences.Language = _language;
        File.WriteAllText(AppPaths.UiSettingsPath, JsonSerializer.Serialize(preferences), new System.Text.UTF8Encoding(false));
    }

    private void LayoutContent()
    {
        if (_layingOut || IsDisposed) return;
        _layingOut = true;
        try
        {
            const int margin = 36;
            int contentWidth = Math.Max(180, ClientSize.Width - margin * 2);
            int y = 26;

            _languageCombo.Location = new Point(Math.Max(margin, ClientSize.Width - margin - _languageCombo.Width), 20);

            _logo.Location = new Point(margin, y + 4);
            _title.MaximumSize = new Size(Math.Max(180, contentWidth - 68), 0);
            _title.Location = new Point(margin + 68, y);
            y = _title.Bottom + 14;

            _subtitle.MaximumSize = new Size(contentWidth, 0);
            _subtitle.Location = new Point(margin, y);
            y = _subtitle.Bottom + 16;

            _notes.MaximumSize = new Size(contentWidth, 0);
            _notes.Location = new Point(margin, y);
            y = _notes.Bottom + 16;

            _originalCredit.MaximumSize = new Size(contentWidth, 0);
            _originalCredit.Location = new Point(margin, y);
            y = _originalCredit.Bottom + 6;

            _symbolCredit.MaximumSize = new Size(contentWidth, 0);
            _symbolCredit.Location = new Point(margin, y);
            y = _symbolCredit.Bottom + 6;

            _distributorCredit.MaximumSize = new Size(contentWidth, 0);
            _distributorCredit.Location = new Point(margin, y);
            y = _distributorCredit.Bottom + 18;

            _status.MaximumSize = new Size(contentWidth, 0);
            _progress.Width = contentWidth;
            int minimumFooterHeight = _status.Height + 12 + _progress.Height + 20 + _installButton.Height + margin;
            int footerTop = Math.Max(y, ClientSize.Height - minimumFooterHeight);
            _status.Location = new Point(margin, footerTop);
            _progress.Location = new Point(margin, _status.Bottom + 12);
            _installButton.Location = new Point(
                Math.Max(margin, ClientSize.Width - margin - _installButton.Width),
                Math.Max(_progress.Bottom + 18, ClientSize.Height - margin - _installButton.Height));

            int contentBottom = _installButton.Bottom + margin;
            AutoScrollMinSize = contentBottom > ClientSize.Height
                ? new Size(ClientSize.Width, contentBottom)
                : Size.Empty;
        }
        finally
        {
            _layingOut = false;
        }
    }

    private static bool IsExcludedOfficialEntry(string archivePath)
    {
        string normalized = archivePath.Replace('\\', '/').TrimStart('/');
        return normalized.Equals("bin/geocoin-core.exe", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("/bin/geocoin-core.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InstallAsync(IProgress<InstallProgress> progress, string language)
    {
        string install = AppPaths.InstallDirectory;
        string parent = Path.GetDirectoryName(install)!;
        Directory.CreateDirectory(parent);
        using var installLock = File.Open(install + ".install.lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        string rollback = install + ".rollback";
        if (Directory.Exists(rollback))
        {
            if (Directory.Exists(install)) Directory.Delete(rollback, recursive: true);
            else Directory.Move(rollback, install);
        }
        if (CompareInstalledVersion() > 0)
            throw new InvalidOperationException("Версия на компьютере новее версии установщика.");
        string stage = install + ".stage-" + Guid.NewGuid().ToString("N");
        string officialStage = Path.Combine(stage, "official");
        Directory.CreateDirectory(officialStage);

        string tempArchive = Path.Combine(Path.GetTempPath(), "GeoCoin-Start-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            progress.Report(new InstallProgress(0, "Скачиваю официальный пакет GeoCoin…"));
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            using (HttpResponseMessage response = await http.GetAsync(
                AppPaths.OfficialArchiveUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using Stream source = await response.Content.ReadAsStreamAsync();
                await using var destination = new FileStream(tempArchive, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                byte[] buffer = new byte[81920];
                long copied = 0;
                long? total = response.Content.Headers.ContentLength;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory())) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    copied += read;
                    int percent = total is > 0 ? (int)Math.Min(40, copied * 40 / total.Value) : 20;
                    progress.Report(new InstallProgress(percent, "Скачиваю официальный пакет GeoCoin…"));
                }
            }

            progress.Report(new InstallProgress(42, "Проверяю официальный пакет…"));
            await using (var file = File.OpenRead(tempArchive))
            {
                byte[] digest = await SHA256.HashDataAsync(file);
                string actual = Convert.ToHexString(digest);
                if (!string.Equals(actual, AppPaths.OfficialSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Проверка целостности официального архива не пройдена.");
            }

            using (ZipArchive archive = ZipFile.OpenRead(tempArchive))
            {
                string root = Path.GetFullPath(officialStage) + Path.DirectorySeparatorChar;
                int total = Math.Max(archive.Entries.Count, 1);
                for (int index = 0; index < archive.Entries.Count; index++)
                {
                    ZipArchiveEntry entry = archive.Entries[index];
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string destination = Path.GetFullPath(Path.Combine(root, relative));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("В архиве обнаружен недопустимый путь.");

                    if (IsExcludedOfficialEntry(entry.FullName)) continue;
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(destination);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        await using Stream entryStream = entry.Open();
                        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        await entryStream.CopyToAsync(output);
                    }
                    int percent = 45 + (index + 1) * 48 / total;
                    progress.Report(new InstallProgress(percent, "Устанавливаю файлы GeoCoin…"));
                }
            }

            progress.Report(new InstallProgress(96, "Завершаю установку…"));
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                throw new InvalidOperationException("Не удалось определить файл установщика.");
            string installedExe = AppPaths.ExecutablePath;
            foreach (string requiredFile in new[] { AppPaths.NodePath, AppPaths.GpuMinerPath, AppPaths.ProductionManifestPath })
            {
                if (!File.Exists(Path.Combine(stage, Path.GetRelativePath(install, requiredFile))))
                    throw new InvalidDataException("Пакет GeoCoin неполный: " + Path.GetFileName(requiredFile));
            }
            File.Copy(currentExe, Path.Combine(stage, Path.GetFileName(installedExe)));

            string markerJson = JsonSerializer.Serialize(new
            {
                app = "GeoCoin-Start",
                version = AppPaths.AppVersion,
                official_version = AppPaths.OfficialVersion,
                official_sha256 = AppPaths.OfficialSha256
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(stage, Path.GetFileName(AppPaths.InstallMarker)), markerJson);
            if (CompareInstalledVersion() > 0)
                throw new InvalidOperationException("Версия на компьютере новее версии установщика.");
            if (Directory.Exists(install)) Directory.Move(install, rollback);
            try
            {
                Directory.Move(stage, install);
            }
            catch
            {
                if (Directory.Exists(rollback)) Directory.Move(rollback, install);
                throw;
            }
            try { Directory.Delete(rollback, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            RegisterInstallation(installedExe);
            TryCreateShortcut(installedExe, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GeoCoin-Start.lnk"));
            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "GeoCoin-Start");
            Directory.CreateDirectory(startMenu);
            TryCreateShortcut(installedExe, Path.Combine(startMenu, "GeoCoin-Start.lnk"), appUserModelId: AppPaths.ToastAppUserModelId);
            string uninstallerName = language == "ru" ? "Удалить GeoCoin-Start" : UiLocalization.TranslateFromEnglish(language, "Uninstall GeoCoin-Start");
            string shortcutDescription = language == "ru" ? "Простой интерфейс GeoCoin" : UiLocalization.TranslateFromEnglish(language, "Simple GeoCoin interface");
            TryCreateShortcut(installedExe, Path.Combine(startMenu, uninstallerName + ".lnk"), "--uninstall", shortcutDescription);
            progress.Report(new InstallProgress(100, "Установка завершена."));
        }
        finally
        {
            try { File.Delete(tempArchive); } catch { }
            try { Directory.Delete(stage, recursive: true); } catch { }
        }
    }

    private static void RegisterInstallation(string installedExe)
    {
        using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AppPaths.UninstallKey);
        key.SetValue("DisplayName", "GeoCoin-Start");
        key.SetValue("DisplayVersion", AppPaths.AppVersion);
        key.SetValue("Publisher", "TheTimkol");
        key.SetValue("InstallLocation", AppPaths.InstallDirectory);
        key.SetValue("DisplayIcon", installedExe);
        key.SetValue("UninstallString", $"\"{installedExe}\" --uninstall");
        key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
    }

    private static void TryCreateShortcut(string target, string shortcutPath, string? arguments = null, string? description = null,
        string? appUserModelId = null)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return;
            dynamic automation = shell;
            shortcut = automation.CreateShortcut(shortcutPath);
            dynamic link = shortcut;
            link.TargetPath = target;
            link.Arguments = arguments ?? "";
            link.WorkingDirectory = AppPaths.InstallDirectory;
            link.Description = description ?? "Простой интерфейс GeoCoin";
            link.Save();
            if (!string.IsNullOrWhiteSpace(appUserModelId))
                WindowsShellProperties.SetShortcutAppUserModelId(shortcutPath, appUserModelId);
        }
        catch { }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private void LaunchInstalledApp()
    {
        if (!File.Exists(AppPaths.ExecutablePath))
        {
            MessageBox.Show(this, "Файл приложения не найден.", "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.ExecutablePath,
            Arguments = "--installed",
            UseShellExecute = true,
            WorkingDirectory = AppPaths.InstallDirectory
        });
    }

    private static void OpenLink(string address)
    {
        Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
    }
}
