# GeoCoin-Start for macOS

## Requirements

- Apple silicon Mac (ARM64).
- macOS 14 or later.
- Internet access for the node to connect to the GeoCoin network.

Intel Macs are not supported by this build. Download the DMG from the [latest release](https://github.com/TheTimkol/geocoin-start/releases/latest).

## Install

1. Download `GeoCoin-Start-macos-arm64.dmg` from the [latest release](https://github.com/TheTimkol/geocoin-start/releases/latest).
2. Verify its SHA-256 value against the [release checksum file](https://github.com/TheTimkol/geocoin-start/releases/latest/download/SHA256SUMS.txt):

   ```bash
   shasum -a 256 GeoCoin-Start-macos-arm64.dmg
   ```

3. Open the DMG in Finder and drag `GeoCoin-Start.app` to **Applications**.
4. Eject the mounted GeoCoin-Start disk image, then open the app from **Applications**.

This build is ad hoc signed and is not notarized by Apple. macOS may block its first launch. Only if you have verified the checksum and trust the source, you can approve the app in **System Settings → Privacy & Security → Open Anyway**. Read [Apple’s guidance for apps that are not notarized](https://support.apple.com/en-us/102445) before overriding this warning. If macOS reports the app as damaged or malicious, do not open it.

## First run

1. Start GeoCoin-Start and select the **degree** network.
2. Start the node from the Overview page. The Node page shows the startup log, synchronization height, and peer status.
3. Wait for synchronization before relying on the balance or sending funds. Closing the window can leave the node running in the menu bar. Choose **Quit** to stop the client and node.
4. Create a wallet or open/import an existing wallet. Wallet and chain data are kept on this Mac under:

   `~/Library/Application Support/TheTimkol/GeoCoin-Start/degree`

5. Back up the wallet file and keep the backup somewhere private. A wallet password protects the wallet file; an app PIN is only a local screen lock and does not replace the wallet password.

## Using the wallet

- **Send:** enter a recipient or import a QR image, review the amount and fee, then confirm the transfer.
- **Receive:** choose or create a wallet address and copy it or export its QR code.
- **History:** use search, filters, and paging to review transactions.
- **Node:** inspect live startup messages, synchronization, and peers. Do not expose RPC port 3033 to the public internet; P2P uses port 3030.
- **Appearance:** choose Classic or Modern layout and light or dark theme. Glass transparency is adjustable in Modern layout.
- **App lock:** an optional 4- or 6-digit PIN can lock the client. It does not encrypt the wallet or data directory.

GeoCoin-Start is an unofficial community project. See [README.md](README.md#attribution) for attribution.
