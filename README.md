# GeoCoin-Start

A simple multilingual GeoCoin installer and wallet for Windows 10/11 x64. GeoCoin-Start provides a guided setup and a small interface for people who are new to running a node or using a cryptocurrency wallet.

## Download

[Download GeoCoin-Start-Setup.exe](https://github.com/TheTimkol/geocoin-start/releases/latest/download/GeoCoin-Start-Setup.exe) · [Verify its SHA-256 checksum](https://github.com/TheTimkol/geocoin-start/releases/latest/download/SHA256SUMS.txt)

Run the setup file and choose **Install**. An internet connection is required: the installer downloads the official GeoCoin v0.1.3 Windows package from the author's release, verifies its SHA-256 checksum, and installs it for the current Windows user. Administrator rights and a separate .NET installation are not required.

## Features

- Overview of the local node, synchronization, and wallet balance.
- An in-client first-run guide that walks a new user through starting the node, creating or importing a wallet, making a backup, and opening mining or receive tools.
- Russian, English, Simplified Chinese, Brazilian Portuguese, and Italian interface in both the installer and wallet, plus light and dark themes. The selections are saved on this computer.
- Optional 4- or 6-digit app PIN, ten-minute inactivity lock, and Windows Hello unlock on supported Windows 11 systems. The lock screen keeps wallet balance, mined-block counts, hashrate, and node/network health visible. The PIN is protected with Windows DPAPI and does not replace the wallet password or encrypt wallet data.
- Optional Windows Notification Center alerts for node connection loss, completed synchronization, new mining rewards, incoming transfers, and outgoing transfers accepted by the node. The alert types can be changed in Settings; an accepted outgoing transfer is not necessarily confirmed in a block yet.
- A Settings page for PIN, Windows Hello, inactivity lock, notification preferences, and reopening the first-run guide.
- Optional per-user Windows startup. Enabling it opens the client at sign-in; it does not start the node or mining.
- A manual update check that downloads the latest GitHub release, verifies the installer SHA-256 checksum, and starts the per-user installer after the app shuts down.
- A short ASCII-only path alias for the node and miner when a Windows profile path contains Cyrillic; wallet data stays in the current user's private `%LOCALAPPDATA%` folder.
- A node page with live RPC health, process status, and an expandable peer list with active inbound/outbound connections and recent refusals separated from other connection failures. It also explains how to accept mainnet TCP 3030 peers without exposing RPC 3033.
- Pause the on-screen node log while reading it and copy a sanitized diagnostic summary that excludes wallet addresses, passwords, and local paths.
- A miner-only RPC profile for a trusted LAN/VPN and a remote GPU subminer page. The remote worker uses a public reward address and miner-role credentials, never a wallet private key or admin RPC password.
- CPU and GPU mining controls on one page. Start either or both at once; the app shows a separate hashrate for each. GPU rewards use an existing address from the current wallet.
- Mined-block count for the current day, current or most recent mining session, and wallet lifetime, plus mature and immature coin balances. Session and lifetime counts are retained across app restarts.
- Transaction history with newest entries first, 15/30/50 rows per page, dates, operation type, amount, fee, confirmation state, address, and a copyable transaction ID. Mainnet transaction hashes link to the GeoCoin Explorer when the record contains a valid hash.
- Sending GeoCoin with a network fee estimate, an image-based QR import for recipient address (and amount when present), and a confirmation screen. The node recalculates the final fee from the selected inputs.
- Receiving GeoCoin using any existing wallet address or a newly created address, with copy and QR-code PNG export. The QR code contains the raw GeoCoin address.
- Clear empty states on receive, history, and wallet pages when the node or wallet is unavailable, with a link back to the overview.
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

To sign the setup executable, install a trusted code-signing certificate with its private key in the current Windows user's `Personal` certificate store, then set its thumbprint before running the build:

```powershell
$env:GEOCOIN_SIGNING_CERT_THUMBPRINT = "YOUR_CERTIFICATE_THUMBPRINT"
powershell -ExecutionPolicy Bypass -File installer\GeoCoinStart\InstallerBuild.ps1
```

The build verifies the certificate chain and signature. Without a trusted code-signing certificate, the installer remains unsigned and Windows may show an unknown-publisher warning. The certificate subject is the Authenticode publisher; the application attribution remains TheTimkol as redistributor.
