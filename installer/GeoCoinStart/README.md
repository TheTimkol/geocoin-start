# GeoCoin-Start — Windows

Russian-language installer and simplified GeoCoin wallet for Windows 10/11 x64.

## What it includes

- A single self-contained `GeoCoin-Start-Setup.exe`; no separate .NET runtime or administrator rights are required.
- The official GeoCoin v0.1.3 Windows package is downloaded from the author's release and verified with SHA-256 before installation.
- Four sections: Overview, Mining, Send, and Receive.
- A local GeoCoin node; wallet passwords are not saved by the application.
- Encrypted wallet backups.

## Install

Run `GeoCoin-Start-Setup.exe` and choose **Install**. An internet connection is required the first time. The official GeoCoin package and the application are installed under `%LOCALAPPDATA%\Programs\GeoCoin-Start`; main network data is stored under `%LOCALAPPDATA%\GeoCoin-Start\data`.

The installer does not start mining or create a wallet without your action. On first use, start the node, create a password-protected wallet, save the password, and make a backup.

## Build

The build script creates a self-contained Windows x64 executable. The installer downloads the official package directly from the author's release and checks it against the pinned SHA-256 value:

```powershell
powershell -ExecutionPolicy Bypass -File installer\GeoCoinStart\InstallerBuild.ps1
```

## Attribution

- Original project: [NoemaCrypt / GeoCoin](https://github.com/NoemaCrypt/GeoCoin)
- Original release: [GeoCoin v0.1.3](https://github.com/NoemaCrypt/GeoCoin/releases/tag/v0.1.3)
- Redistributor: [TheTimkol on Telegram](https://t.me/TheTimkol)

The application exposes CPU mining only. GPU controls are not included in this simplified interface.
