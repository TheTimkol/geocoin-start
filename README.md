# GeoCoin-Start

A simple Russian-language GeoCoin installer and wallet for Windows 10/11 x64. GeoCoin-Start provides a guided setup and a small interface for people who are new to running a node or using a cryptocurrency wallet.

## Download

[Download GeoCoin-Start-Setup.exe](https://github.com/TheTimkol/geocoin-start/releases/latest/download/GeoCoin-Start-Setup.exe) · [Verify its SHA-256 checksum](https://github.com/TheTimkol/geocoin-start/releases/latest/download/SHA256SUMS.txt)

Run the setup file and choose **Install**. An internet connection is required: the installer downloads the official GeoCoin v0.1.3 Windows package from the author's release, verifies its SHA-256 checksum, and installs it for the current Windows user. Administrator rights and a separate .NET installation are not required.

## Features

- Overview of the local node, synchronization, and wallet balance.
- CPU mining with a thread selector, plus GPU mining with device detection through the official GeoCoin GPU miner.
- Sending GeoCoin with an automatically calculated fee and a confirmation screen.
- Receiving GeoCoin with a new address and a copy button.
- Password-protected wallet creation, encrypted backup export, and restore/import from an encrypted GeoCoin backup.
- A Windows uninstaller in the Start menu and Installed Apps. Wallet and node data are kept by default; removal of those data is an explicit option.

Mining starts only after the user chooses a device and confirms. GPU mining requires a supported graphics card and working CUDA, HIP, or OpenCL drivers. The app does not install graphics drivers.

Wallet and node data stay on this computer under `%LOCALAPPDATA%`. When replacing an existing wallet, GeoCoin-Start first creates an encrypted safety backup and preserves the original wallet file in the local backup folder.

## Attribution

- Original project and developer: [NoemaCrypt / GeoCoin](https://github.com/NoemaCrypt/GeoCoin)
- Official GeoCoin v0.1.3 release: [release page](https://github.com/NoemaCrypt/GeoCoin/releases/tag/v0.1.3)
- Official GeoCoin symbol: [geocoin.network](https://geocoin.network/)
- Redistributor and GeoCoin-Start developer: [TheTimkol on Telegram](https://t.me/TheTimkol)

GeoCoin-Start is an unofficial community-made application and is not maintained by NoemaCrypt. It downloads the original node, wallet, and miners unchanged from the official GeoCoin release, then provides a simpler interface and setup flow.

## Build

The Windows x64 build requires the .NET 10 SDK. From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File installer\GeoCoinStart\InstallerBuild.ps1
```

The setup executable is not signed with an Authenticode publisher certificate, so Windows may show an unknown-publisher warning.
