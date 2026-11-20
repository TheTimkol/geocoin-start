# GeoCoin-Start

A simple Russian-language GeoCoin installer and wallet for Windows 10/11 x64. GeoCoin-Start provides a guided setup and a small interface for people who are new to running a node or using a cryptocurrency wallet.

## Download

[Download GeoCoin-Start-Setup.exe](https://github.com/TheTimkol/geocoin-start/releases/latest/download/GeoCoin-Start-Setup.exe) · [Verify its SHA-256 checksum](https://github.com/TheTimkol/geocoin-start/releases/latest/download/SHA256SUMS.txt)

Run the setup file and choose **Install**. An internet connection is required: the installer downloads the official GeoCoin v0.1.3 Windows package from the author's release, verifies its SHA-256 checksum, and installs it for the current Windows user. Administrator rights and a separate .NET installation are not required.

## Features

- Overview of the local node, synchronization, and wallet balance.
- A node page with live RPC health, process status, connected peers, and a rolling log that can also be opened as a file.
- A miner-only RPC profile for a trusted LAN/VPN and a remote GPU subminer page. The remote worker uses a public reward address and miner-role credentials, never a wallet private key or admin RPC password.
- CPU and GPU mining controls on one page. Start either or both at once; the app shows a separate hashrate for each. GPU rewards use an existing address from the current wallet.
- Accepted blocks mined today and in the current or most recent CPU/GPU mining session. Both counts are retained across app restarts; the daily count uses local time.
- Transaction history with dates, operation type, amount, fee, confirmation state, address, and a copyable transaction ID.
- Sending GeoCoin with an automatically calculated fee and a confirmation screen.
- Receiving GeoCoin with a new address and a copy button.
- Password-protected wallet creation, full-wallet `.dat` backup export, and import from an existing GeoCoin wallet file or backup. Import asks for the wallet file's password when it is protected and keeps the source file unchanged.
- Import and export of a 64-character hexadecimal private key for one address. GeoCoin does not use a mnemonic seed phrase; use the `.dat` backup to save the whole wallet.
- A private-key `.txt` export is unencrypted and must be kept secret.
- A Windows uninstaller in the Start menu and Installed Apps. Wallet and node data are kept by default; removal of those data is an explicit option.

Mining starts only after the user confirms. Running CPU and GPU mining together can put a heavy load on the computer. GPU mining requires a supported graphics card and working CUDA, HIP, or OpenCL drivers. The app does not install graphics drivers.

GeoCoin has one node process; RPC is an interface on that process. Remote GPU mining binds RPC to a private network address and uses the miner role. The RPC connection uses plain HTTP, so use a trusted LAN or VPN only, allow private networks only in Windows Firewall, and do not forward RPC ports from the internet. The miner password is held only while GeoCoin-Start is open and is regenerated after the app closes.

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
