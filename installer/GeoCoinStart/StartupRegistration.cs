using Microsoft.Win32;

namespace GeoCoinStart;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GeoCoin-Start";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? command = key?.GetValue(ValueName) as string;
        return string.Equals(command, BuildCommand(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!File.Exists(AppPaths.ExecutablePath))
                throw new FileNotFoundException("The installed GeoCoin-Start executable was not found.", AppPaths.ExecutablePath);

            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new IOException("Could not open the current user's startup settings.");
            key.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
            return;
        }

        using RegistryKey? existingKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        existingKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static void Remove() => SetEnabled(false);

    private static string BuildCommand() => $"\"{Path.GetFullPath(AppPaths.ExecutablePath)}\" --installed";
}
