# GeoCoin-Start — Windows

Russian-language installer and simplified GeoCoin wallet for Windows 10/11 x64.

## What it includes

- A single self-contained `GeoCoin-Start-Setup.exe`; no separate .NET runtime or administrator rights are required.
- The official GeoCoin v0.1.3 Windows package is downloaded from the author's release and verified with SHA-256 before installation.
- Four sections: Overview, Mining, Send, and Receive.
- CPU mining and GPU mining with compatible device detection using the official GeoCoin GPU miner.
- Password-protected wallets with encrypted backup export and restore/import.
- A Windows uninstaller that keeps wallet and node data by default.

GPU mining requires a supported graphics card and its CUDA, HIP, or OpenCL driver. GeoCoin-Start does not install drivers.

## Install and remove

Run `GeoCoin-Start-Setup.exe` and choose **Install**. An internet connection is required the first time. The official GeoCoin package and the application are installed under `%LOCALAPPDATA%\Programs\GeoCoin-Start`; main network data is stored under `%LOCALAPPDATA%\GeoCoin-Start\data`.

The installer creates shortcuts in the Start menu and on the desktop. Remove GeoCoin-Start through **Settings → Apps → Installed apps** or the Start menu shortcut **Удалить GeoCoin-Start**. The uninstaller preserves the wallet, encrypted backups, and node data unless you explicitly select their removal.

## Attribution

- Original project: [NoemaCrypt / GeoCoin](https://github.com/NoemaCrypt/GeoCoin)
- Original release: [GeoCoin v0.1.3](https://github.com/NoemaCrypt/GeoCoin/releases/tag/v0.1.3)
- Official symbol: [geocoin.network](https://geocoin.network/)
- Redistributor: [TheTimkol on Telegram](https://t.me/TheTimkol)

## Build

The build script creates a self-contained Windows x64 executable. The installer downloads the official package directly from the author's release and checks it against the pinned SHA-256 value:

```powershell
powershell -ExecutionPolicy Bypass -File installer\GeoCoinStart\InstallerBuild.ps1
```
