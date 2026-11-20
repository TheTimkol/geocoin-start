using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GeoCoinStart;

/// <summary>
/// Gives native GeoCoin processes an ASCII-only alias for paths under the current
/// user's Local AppData directory. The alias exists only in this logon session.
/// </summary>
internal sealed class WindowsAsciiPathAlias : IDisposable
{
    private const uint DefineDosDeviceRawTargetPath = 0x00000001;
    private const uint DefineDosDeviceRemoveDefinition = 0x00000002;
    private const uint DefineDosDeviceExactMatchOnRemove = 0x00000004;
    private const uint DefineDosDeviceNoBroadcastSystem = 0x00000008;

    private readonly string _localAppDataRoot;
    private readonly string? _deviceName;
    private readonly string? _targetPath;
    private bool _disposed;

    private WindowsAsciiPathAlias(string localAppDataRoot)
    {
        _localAppDataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localAppDataRoot));
        if (!_localAppDataRoot.Any(character => character > 0x7f)) return;

        var inUse = new HashSet<string>(Environment.GetLogicalDrives(), StringComparer.OrdinalIgnoreCase);
        int lastError = 85;
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            string candidate = $"{letter}:\\";
            if (inUse.Contains(candidate)) continue;

            string deviceName = $"{letter}:";
            string targetPath = @"\??\" + _localAppDataRoot;
            uint flags = DefineDosDeviceRawTargetPath | DefineDosDeviceNoBroadcastSystem;
            if (DefineDosDevice(flags, deviceName, targetPath))
            {
                _deviceName = deviceName;
                _targetPath = targetPath;
                return;
            }
            lastError = Marshal.GetLastWin32Error();
        }

        throw new InvalidOperationException(
            "Не удалось создать короткий ASCII-путь для ноды. Освободите букву диска и запустите GeoCoin-Start снова.",
            new Win32Exception(lastError));
    }

    public static WindowsAsciiPathAlias CreateForLocalAppData()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            throw new InvalidOperationException("Windows не сообщил путь к папке данных пользователя.");

        return new WindowsAsciiPathAlias(localAppData);
    }

    public string Map(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string fullPath = Path.GetFullPath(path);
        if (_deviceName is null) return fullPath;

        if (fullPath.Equals(_localAppDataRoot, StringComparison.OrdinalIgnoreCase))
            return _deviceName + "\\";

        string rootPrefix = _localAppDataRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Путь файла ноды находится вне папки данных пользователя.");

        return _deviceName + fullPath[_localAppDataRoot.Length..];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_deviceName is null || _targetPath is null) return;

        uint flags = DefineDosDeviceRemoveDefinition | DefineDosDeviceExactMatchOnRemove |
                     DefineDosDeviceRawTargetPath | DefineDosDeviceNoBroadcastSystem;
        _ = DefineDosDevice(flags, _deviceName, _targetPath);
    }

    [DllImport("kernel32.dll", EntryPoint = "DefineDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DefineDosDevice(uint flags, string deviceName, string targetPath);
}
