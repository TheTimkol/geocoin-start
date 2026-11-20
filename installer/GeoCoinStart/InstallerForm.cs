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

    private readonly Label _title = new();
    private readonly PictureBox _logo = new();
    private readonly Label _subtitle = new();
    private readonly Label _notes = new();
    private readonly LinkLabel _originalCredit = new();
    private readonly LinkLabel _symbolCredit = new();
    private readonly LinkLabel _distributorCredit = new();
    private readonly Label _status;
    private readonly ProgressBar _progress;
    private readonly Button _installButton;
    private readonly bool _alreadyInstalled;
    private bool _layingOut;

    public InstallerForm()
    {
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
        LayoutContent();
    }

    private async void InstallButtonClicked(object? sender, EventArgs e)
    {
        if (_alreadyInstalled && IsInstalledAppRunning())
        {
            MessageBox.Show(this,
                "Перед обновлением закройте GeoCoin-Start и остановите ноду. Затем запустите установщик снова.",
                "Сначала закройте GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _installButton.Enabled = false;
        _status.Text = "Подготавливаю установку…";
        try
        {
            var progress = new Progress<InstallProgress>(state =>
            {
                _progress.Value = Math.Clamp(state.Percent, 0, 100);
                _status.Text = state.Message;
                LayoutContent();
            });
            await InstallAsync(progress);
            _status.Text = "Установка завершена. Открываю приложение…";
            LayoutContent();
            LaunchInstalledApp();
            Close();
        }
        catch (Exception ex)
        {
            _status.Text = "Установка не завершена";
            LayoutContent();
            MessageBox.Show(this,
                "Не удалось установить GeoCoin. Проверьте, что в папке установки достаточно места, и попробуйте ещё раз.\n\n" + ex.Message,
                "GeoCoin-Start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installButton.Enabled = true;
        }
    }

    private static bool IsInstalledAppRunning()
    {
        string expected = Path.GetFullPath(AppPaths.ExecutablePath);
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(expected)))
        {
            using (process)
            {
                try
                {
                    if (process.HasExited || process.MainModule?.FileName is not string actual) continue;
                    if (string.Equals(Path.GetFullPath(actual), expected, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }
            }
        }
        return false;
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

    private static async Task InstallAsync(IProgress<InstallProgress> progress)
    {
        Directory.CreateDirectory(AppPaths.InstallDirectory);
        Directory.CreateDirectory(AppPaths.OfficialDirectory);

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
                string root = Path.GetFullPath(AppPaths.OfficialDirectory) + Path.DirectorySeparatorChar;
                int total = Math.Max(archive.Entries.Count, 1);
                for (int index = 0; index < archive.Entries.Count; index++)
                {
                    ZipArchiveEntry entry = archive.Entries[index];
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string destination = Path.GetFullPath(Path.Combine(root, relative));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("В архиве обнаружен недопустимый путь.");

                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(destination);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        await using Stream entryStream = entry.Open();
                        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
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
            if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(currentExe, installedExe, overwrite: true);

            string markerJson = JsonSerializer.Serialize(new
            {
                app = "GeoCoin-Start",
                version = AppPaths.AppVersion,
                official_version = AppPaths.OfficialVersion,
                official_sha256 = AppPaths.OfficialSha256
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AppPaths.InstallMarker, markerJson);
            RegisterInstallation(installedExe);
            TryCreateShortcut(installedExe, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GeoCoin-Start.lnk"));
            string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "GeoCoin-Start");
            Directory.CreateDirectory(startMenu);
            TryCreateShortcut(installedExe, Path.Combine(startMenu, "GeoCoin-Start.lnk"));
            TryCreateShortcut(installedExe, Path.Combine(startMenu, "Удалить GeoCoin-Start.lnk"), "--uninstall");
            progress.Report(new InstallProgress(100, "Установка завершена."));
        }
        finally
        {
            try { File.Delete(tempArchive); } catch { }
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

    private static void TryCreateShortcut(string target, string shortcutPath, string? arguments = null)
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
            link.Description = "Простой интерфейс GeoCoin";
            link.Save();
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
