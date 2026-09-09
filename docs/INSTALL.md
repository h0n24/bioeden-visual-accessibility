# Installation

The release ZIP is the easiest option for regular players. It does not require Git, the .NET SDK or a mod loader.

1. Close BioEden.
2. Download the ZIP from the repository's **Releases** page and extract it.
3. Double-click `Install.cmd`. If the game is not found, enter the folder containing `BioEden.exe` when prompted. The ZIP can be extracted anywhere.
4. Start the game normally.

If the game is not detected automatically, pass its folder explicitly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -GamePath "D:\SteamLibrary\steamapps\common\BioEden"
```

The folder must contain `BioEden.exe`. The installer verifies SHA-256 hashes, keeps immutable `.NoDOF.original` backups beside the two patched game libraries, and refuses unknown game or runtime versions.

To check or remove the mod:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -Status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
```

The mod is currently tested for BioEden 1.2.0.0 on Windows. The game itself is not redistributed.

To upgrade, close the game, extract the newest release ZIP into a fresh folder and run its Install.cmd. Do not run an installer from an older download. Uninstalling first is unnecessary. Settings → Accessibility shows the installed Mod version and offers a manual GitHub update check (select Check updates and Confirm). Beta releases are included; updates are never installed automatically.
