# GeoCoin-Start — Windows

Multilingual installer and simplified GeoCoin wallet for Windows 10/11 x64. Russian, English, Simplified Chinese, Brazilian Portuguese, and Italian are available in both the installer and wallet. The wallet interface also supports light and dark themes; both selections are saved locally.

## What it includes

- A single self-contained `GeoCoin-Start-Setup.exe`; no separate .NET runtime or administrator rights are required.
- Manual update checks use the latest GitHub release, verify the setup executable's SHA-256 checksum, and start the installer after the app closes.
- The official GeoCoin v0.1.3 Windows package is downloaded from the author's release and verified with SHA-256 before installation.
- If the Windows profile path contains Cyrillic, the node and miner use a short ASCII-only path alias. Wallet data stays under the current user's `%LOCALAPPDATA%` folder.
- Eight sections: Overview, Mining, Remote GPU Subminer, Node and Log, Send, Receive, Transactions, and Wallet.
- Node health page with process status, RPC response, peer count, live node/miner log, and LAN-only miner RPC credentials.
- Remote GPU subminer mode for connecting this computer to a GeoCoin node on another PC. It is exclusive with the local node mode and uses miner-role credentials plus a public reward address.
- One mining page with independent CPU and GPU controls. Both can run together, with separate hashrates; GPU rewards go to an existing address in the current wallet.
- Mined-block counts for today, the current or most recent session, and the wallet lifetime, plus mature and immature wallet balances. Session and lifetime counts persist across restarts.
- Transaction history showing date, type, amount, fee, confirmations, address, and copyable transaction ID.
- Send page shows a current network fee estimate, can import a recipient address (and optional amount) from a QR image, and explains that the node recalculates the final fee from selected inputs.
- Receive page can export the selected address QR code as a PNG image. QR imports are decoded from image files.
- Receive, history, and wallet pages explain what is missing and link back to the overview when the node or wallet is unavailable.
- Password-protected wallets with full-wallet `.dat` backup export and import from existing GeoCoin wallet files or backups. Import asks for the file password when it is protected and does not change the source file.
- Receive page can choose an existing wallet address or create a new one, and displays a copyable address QR code containing the raw GeoCoin address.
- Import/export of a 64-character hexadecimal private key for an individual address. GeoCoin has no mnemonic seed phrase; use the `.dat` wallet backup for a full copy.
- Private-key `.txt` exports are unencrypted and must be kept secret.
- A Windows uninstaller that keeps wallet and node data by default.

Running CPU and GPU mining together can put a heavy load on the computer. GPU mining requires a supported graphics card and its CUDA, HIP, or OpenCL driver. GeoCoin-Start does not install drivers.

GeoCoin runs one node process with RPC enabled; RPC is an interface to that node, not a second node. Remote miner RPC uses plain HTTP. Use only a trusted LAN or VPN, allow private networks only if Windows Firewall asks, and do not expose/forward port 3033 to the internet. The miner password is held only while the application is open and changes after it closes. Never use the wallet private key or admin RPC password for a subminer.

## Install and remove

Run `GeoCoin-Start-Setup.exe` and choose **Install**. An internet connection is required the first time. The official GeoCoin package and the application are installed under `%LOCALAPPDATA%\Programs\GeoCoin-Start`; main network data is stored under `%LOCALAPPDATA%\GeoCoin-Start\data`.

The installer creates shortcuts in the Start menu and on the desktop. Remove GeoCoin-Start through **Settings → Apps → Installed apps** or the GeoCoin-Start uninstall shortcut in the Start menu. The uninstaller preserves the wallet, encrypted backups, and node data unless you explicitly select their removal.

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
