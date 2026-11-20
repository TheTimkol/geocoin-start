using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GeoCoinStart;

internal sealed class UninstallerForm : Form
{
    private readonly CheckBox _removeData = new();
    private readonly Button _removeButton = new();

    public UninstallerForm()
    {
        Text = "Удаление GeoCoin-Start";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(600, 365);
        BackColor = Color.FromArgb(241, 241, 235);
        ForeColor = Color.FromArgb(31, 39, 34);
        Font = new Font("Segoe UI", 10);

        var logo = new PictureBox
        {
            Location = new Point(28, 28),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = (Icon ?? SystemIcons.Application).ToBitmap()
        };
        Controls.Add(logo);
        Controls.Add(new Label
        {
            Text = "Удаление GeoCoin-Start",
            Location = new Point(92, 30),
            Size = new Size(470, 38),
            Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 39, 34)
        });
        Controls.Add(new Label
        {
            Text = "Будут удалены приложение и файлы официальной ноды GeoCoin. Кошелёк и блокчейн останутся на компьютере.",
            Location = new Point(30, 100),
            Size = new Size(540, 64),
            ForeColor = Color.FromArgb(104, 115, 105)
        });

        _removeData.Text = "Также удалить кошелёк и данные сети GeoCoin";
        _removeData.Location = new Point(30, 186);
        _removeData.Size = new Size(530, 30);
        _removeData.AutoSize = false;
        Controls.Add(_removeData);

        var removeDataWarning = new Label
        {
            Text = "Это действие безвозвратно удалит кошелёк, резервные копии и загруженную историю блоков. Сначала убедитесь, что у вас есть отдельная копия кошелька.",
            Location = new Point(52, 221),
            Size = new Size(518, 58),
            ForeColor = Color.FromArgb(161, 70, 53),
            Visible = false
        };
        Controls.Add(removeDataWarning);
        _removeData.CheckedChanged += (_, _) => removeDataWarning.Visible = _removeData.Checked;

        var cancel = new Button
        {
            Text = "Отмена",
            Location = new Point(310, 310),
            Size = new Size(100, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(31, 39, 34)
        };
        cancel.Click += (_, _) => Close();
        Controls.Add(cancel);

        _removeButton.Text = "Удалить программу";
        _removeButton.Location = new Point(420, 310);
        _removeButton.Size = new Size(150, 36);
        _removeButton.FlatStyle = FlatStyle.Flat;
        _removeButton.FlatAppearance.BorderSize = 0;
        _removeButton.BackColor = Color.FromArgb(166, 129, 63);
        _removeButton.ForeColor = Color.White;
        _removeButton.Click += RemoveButtonClicked;
        Controls.Add(_removeButton);
        AcceptButton = _removeButton;
        CancelButton = cancel;
    }

    private void RemoveButtonClicked(object? sender, EventArgs e)
    {
        if (IsProgramInUse(out string? processName))
        {
            MessageBox.Show(this,
                $"Сначала закройте работающую программу «{processName}», затем запустите удаление снова.",
                "GeoCoin-Start ещё работает", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string message = _removeData.Checked
            ? "Удалить приложение, кошелёк, резервные копии и все данные сети GeoCoin? Восстановить их после удаления будет нельзя."
            : "Удалить приложение и файлы ноды GeoCoin? Кошелёк, резервные копии и данные сети останутся на компьютере.";
        if (MessageBox.Show(this, message, "Подтверждение удаления", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                throw new InvalidOperationException("Не удалось найти файл приложения для завершения удаления.");
            string helperPath = Path.Combine(Path.GetTempPath(), "GeoCoin-Start-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(currentExe, helperPath, overwrite: false);
            var start = new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--finish-uninstall");
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add(_removeData.Checked ? "true" : "false");
            if (Process.Start(start) is null) throw new InvalidOperationException("Не удалось запустить завершение удаления.");
            _removeButton.Enabled = false;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось начать удаление.\n\n" + ex.Message,
                "Ошибка удаления", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsProgramInUse(out string? processName)
    {
        string[] names = { "GeoCoin-Start", "geocoin-node", "geocoin-gpu-miner", "geocoin-rpc-miner" };
        int currentId = Environment.ProcessId;
        foreach (string name in names)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (process.Id == currentId) continue;
                    try
                    {
                        if (!process.HasExited)
                        {
                            processName = name;
                            return true;
                        }
                    }
                    catch { }
                }
            }
        }
        processName = null;
        return false;
    }

    internal static void FinishUninstall(string[] args)
    {
        if (args.Length < 3 || !int.TryParse(args[1], out int processId)) return;
        bool removeData = string.Equals(args[2], "true", StringComparison.OrdinalIgnoreCase);
        try
        {
            using Process? previous = Process.GetProcessById(processId);
            previous?.WaitForExit();
        }
        catch (ArgumentException) { }
        catch { }

        string helperPath = Environment.ProcessPath ?? "";
        string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "GeoCoin-Start");
        string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GeoCoin-Start.lnk");
        try { Directory.Delete(AppPaths.InstallDirectory, recursive: true); } catch { }
        try { Directory.Delete(AppPaths.NodeSessionDirectory, recursive: true); } catch { }
        try { Directory.Delete(startMenu, recursive: true); } catch { }
        try { File.Delete(desktopShortcut); } catch { }
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(AppPaths.UninstallKey, throwOnMissingSubKey: false); } catch { }
        if (removeData)
        {
            string dataParent = Path.GetDirectoryName(AppPaths.DataRoot)!;
            try { Directory.Delete(dataParent, recursive: true); } catch { }
        }

        if (!string.IsNullOrWhiteSpace(helperPath))
            MoveFileEx(helperPath, null, MoveFileDelayUntilReboot);
    }

    private const int MoveFileDelayUntilReboot = 0x00000004;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);
}
