using System.Runtime.InteropServices;
using System.Text.Json;

namespace GeoCoinStart;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelId(AppPaths.ToastAppUserModelId));
        }
        catch { }
        ApplicationConfiguration.Initialize();
        ApplyColorMode(ReadSavedDarkTheme());

        if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(new UninstallerForm());
            return;
        }
        if (args.Contains("--finish-uninstall", StringComparer.OrdinalIgnoreCase))
        {
            UninstallerForm.FinishUninstall(args);
            return;
        }

        bool launchedByInstaller = args.Contains("--installed", StringComparer.OrdinalIgnoreCase);
        bool isInstalledCopy = string.Equals(
            Path.GetFullPath(Environment.ProcessPath ?? string.Empty),
            Path.GetFullPath(AppPaths.ExecutablePath),
            StringComparison.OrdinalIgnoreCase);

        if (launchedByInstaller || isInstalledCopy)
        {
            try { WindowsShellProperties.EnsureStartMenuShortcutAppUserModelId(); }
            catch { }
        }

        if (launchedByInstaller || isInstalledCopy)
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            FileStream instanceLock;
            try
            {
                instanceLock = File.Open(Path.Combine(AppPaths.DataRoot, "client.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                MessageBox.Show("GeoCoin-Start уже запущен или его каталог данных недоступен.");
                return;
            }
            using (instanceLock)
            {
                if (Directory.Exists(AppPaths.NodeSessionDirectory))
                {
                    foreach (string session in Directory.EnumerateDirectories(AppPaths.NodeSessionDirectory))
                    {
                        if (!Guid.TryParseExact(Path.GetFileName(session), "N", out _) ||
                            File.GetAttributes(session).HasFlag(FileAttributes.ReparsePoint)) continue;
                        try { File.Delete(Path.Combine(session, "wallet.pass")); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            MessageBox.Show("Не удалось удалить файл пароля предыдущей сессии: " + ex.Message);
                        }
                    }
                }
                Application.Run(new MainForm());
            }
        }
        else
        {
            Application.Run(new InstallerForm());
        }
    }

    internal static void ApplyColorMode(bool darkTheme)
    {
        bool darkModeSupported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
#pragma warning disable WFO5001
        Application.SetColorMode(darkTheme && darkModeSupported
            ? SystemColorMode.Dark
            : SystemColorMode.Classic);
#pragma warning restore WFO5001
    }

    private static bool ReadSavedDarkTheme()
    {
        try
        {
            if (!File.Exists(AppPaths.UiSettingsPath)) return false;
            using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(AppPaths.UiSettingsPath));
            return settings.RootElement.TryGetProperty(nameof(UiPreferences.DarkTheme), out JsonElement value) &&
                value.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelId(string appId);
}

internal static class AppPaths
{
    public const string AppVersion = "0.3.12";
    public const string OfficialVersion = "v0.1.3";
    public const string OfficialSha256 = "D97D655E3B45CD1B80BA4553BD5415F2B4FDD0FE370E50BA30158188D0C0EF99";
    public const string OfficialArchiveUrl = "https://github.com/NoemaCrypt/GeoCoin/releases/download/v0.1.3/GeoCoin-windows-x64-static.zip";
    public const string OriginalRepositoryUrl = "https://github.com/NoemaCrypt/GeoCoin";
    public const string DistributorUrl = "https://t.me/TheTimkol";
    public const string ToastAppUserModelId = "TheTimkol.GeoCoinStart";
    public const int RequestInstallerShutdownMessage = 0x8000 + 0x351;
    public const string NetworkId = "degree";
    public const string TransactionExplorerBaseUrl = "https://explorer.geocoin.network";
    public const string LatestReleasePageUrl = "https://github.com/TheTimkol/geocoin-start/releases/latest";
    public const string OfficialWebsiteUrl = "https://geocoin.network/";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GeoCoin-Start";

    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "GeoCoin-Start");
    public static string ExecutablePath => Path.Combine(InstallDirectory, "GeoCoin-Start.exe");
    public static string OfficialDirectory => Path.Combine(InstallDirectory, "official");
    public static string DataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoCoin-Start", "data");
    public static string WalletPath => Path.Combine(DataRoot, "wallet.dat");
    public static string MinedBlocksPath => Path.Combine(DataRoot, "mined-blocks.json");
    public static string UiSettingsPath => Path.Combine(DataRoot, "ui-settings.json");
    public static string NodeSessionDirectory => Path.Combine(DataRoot, "sessions");
    public static string WalletBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoCoin-Start", "wallet-backups");
    public static string NodePath => Path.Combine(OfficialDirectory, "bin", "geocoin-node.exe");
    public static string GpuMinerPath => Path.Combine(OfficialDirectory, "bin", "geocoin-gpu-miner.exe");
    public static string ProductionManifestPath => Path.Combine(
        OfficialDirectory, "config", "activation-manifest.production.json");
    public static string InstallMarker => Path.Combine(InstallDirectory, "installed.json");
    public static string LogPath => Path.Combine(DataRoot, "geocoin-start.log");
}
