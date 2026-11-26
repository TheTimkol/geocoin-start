using System.Diagnostics;

namespace GeoCoinStart;

internal static class UpdateSignature
{
    public static async Task VerifyAsync(string currentExecutable, string installer)
    {
        const string verify = """
            $ErrorActionPreference = 'Stop'
            $current = Get-AuthenticodeSignature -LiteralPath $env:GEOCOIN_CURRENT_EXE
            $update = Get-AuthenticodeSignature -LiteralPath $env:GEOCOIN_UPDATE_EXE
            if ($current.Status -ne 'Valid' -or $update.Status -ne 'Valid') { exit 1 }
            if ($current.SignerCertificate.Thumbprint -ne $update.SignerCertificate.Thumbprint) { exit 2 }
            exit 0
            """;
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(verify);
        start.Environment["GEOCOIN_CURRENT_EXE"] = currentExecutable;
        start.Environment["GEOCOIN_UPDATE_EXE"] = installer;
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить проверку подписи.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill();
            throw new TimeoutException("Проверка подписи обновления не завершилась.");
        }
        if (process.ExitCode != 0)
            throw new InvalidDataException("Для обновления требуется действительная подпись того же издателя. Используйте подписанный установщик из официального релиза.");
    }
}
