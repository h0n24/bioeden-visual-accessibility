# Installation

The release ZIP is the easiest option for regular players. It does not require Git, the .NET SDK or a mod loader.

1. Close BioEden.
2. Download the ZIP from the repository's **Releases** page and extract it.
3. Run `install.ps1` with PowerShell, or double-click `Install.cmd` after placing the extracted folder next to the `BioEden` folder.
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
