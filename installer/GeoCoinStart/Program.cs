namespace GeoCoinStart;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        bool launchedByInstaller = args.Contains("--installed", StringComparer.OrdinalIgnoreCase);
        bool isInstalledCopy = string.Equals(
            Path.GetFullPath(Environment.ProcessPath ?? string.Empty),
            Path.GetFullPath(AppPaths.ExecutablePath),
            StringComparison.OrdinalIgnoreCase);

        Application.Run(launchedByInstaller || isInstalledCopy
            ? new MainForm()
            : new InstallerForm());
    }
}

internal static class AppPaths
{
    public const string OfficialVersion = "v0.1.3";
    public const string OfficialSha256 = "D97D655E3B45CD1B80BA4553BD5415F2B4FDD0FE370E50BA30158188D0C0EF99";
    public const string OfficialArchiveUrl = "https://github.com/NoemaCrypt/GeoCoin/releases/download/v0.1.3/GeoCoin-windows-x64-static.zip";
    public const string OriginalRepositoryUrl = "https://github.com/NoemaCrypt/GeoCoin";
    public const string DistributorUrl = "https://t.me/TheTimkol";

    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "GeoCoin-Start");
    public static string ExecutablePath => Path.Combine(InstallDirectory, "GeoCoin-Start.exe");
    public static string OfficialDirectory => Path.Combine(InstallDirectory, "official");
    public static string DataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoCoin-Start", "data");
    public static string WalletPath => Path.Combine(DataRoot, "wallet.dat");
    public static string NodePath => Path.Combine(OfficialDirectory, "bin", "geocoin-node.exe");
    public static string ProductionManifestPath => Path.Combine(
        OfficialDirectory, "config", "activation-manifest.production.json");
    public static string InstallMarker => Path.Combine(InstallDirectory, "installed.json");
    public static string LogPath => Path.Combine(DataRoot, "geocoin-start.log");
}
