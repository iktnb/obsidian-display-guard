# Obsidian Display Guard

Turn a secondary Windows monitor into a protected, borderless Obsidian display.

Obsidian Display Guard is a small Windows tray application. It keeps one interactive Obsidian window fullscreen on a selected monitor and returns other normal application windows to the primary display. It does not install a driver, run a Windows service, require administrator access, collect telemetry, or modify an Obsidian vault.

## Safety first

- Press **Ctrl+Alt+Shift+F12** at any time to pause or resume protection.
- Use **Exit & restore** from the tray menu to restore the original Obsidian window style and position.
- If the protected monitor disappears, Guard fails closed: Obsidian is restored and no windows are moved.
- Start with `--safe` to open the tray app without enforcing protection.
- Start with `--restore` to perform recovery and exit.

Guard intentionally runs as the signed-in user. Elevated windows and the secure UAC desktop are outside its protection guarantee.

## Quick start

1. Download `ObsidianDisplayGuard.exe` from the latest GitHub Release.
2. Put it in a stable location, for example `%USERPROFILE%\Apps\ObsidianDisplayGuard\`.
3. Run it. On first launch, confirm the Obsidian executable, vault name, and protected monitor.
4. Use **Preview monitor** before selecting **Save and apply**.

The tray menu provides protection on/off, timed pauses, immediate restore, settings, per-user Windows startup, and safe exit.

### Швидкий старт українською

1. Завантаж `ObsidianDisplayGuard.exe` з останнього Release.
2. Поклади його у стабільну папку, наприклад `%USERPROFILE%\Apps\ObsidianDisplayGuard\`.
3. Запусти та перевір шлях до Obsidian, назву vault і малий монітор.
4. Натисни **Preview monitor**, а потім **Save and apply**.

Аварійна клавіша — **Ctrl+Alt+Shift+F12**. Вона миттєво ставить захист на паузу та повертає Obsidian у звичайний режим.

## What it does

- Identifies the selected monitor by hardware/device identity instead of only `DISPLAY1` or `DISPLAY2`.
- Saves the original Obsidian window placement and style before applying borderless fullscreen.
- Uses out-of-process WinEvent hooks plus a low-frequency fallback check.
- Preserves the maximized/restored state of windows returned to the primary monitor.
- Relaunches the configured Obsidian vault after an accidental close.
- Stores versioned configuration under `%LocalAppData%\ObsidianDisplayGuard\config.json`.
- Keeps bounded local logs without note contents, vault contents, or window titles.

Virtual Desktop pinning is deliberately not implemented because the required Windows interfaces are undocumented and brittle.

## Verify a release

Each release includes a SHA-256 checksum and GitHub build-provenance attestation.

```powershell
Get-FileHash .\ObsidianDisplayGuard.exe -Algorithm SHA256
gh attestation verify .\ObsidianDisplayGuard.exe -R iktnb/obsidian-display-guard
```

Version `0.1.0` is not Authenticode-signed. Windows SmartScreen may warn about a newly downloaded executable even when its checksum and provenance are valid.

## Build and test

Requirements: Windows x64 and the .NET 10 SDK.

```powershell
dotnet restore ObsidianDisplayGuard.slnx
dotnet build ObsidianDisplayGuard.slnx -c Release --no-restore
dotnet test ObsidianDisplayGuard.slnx -c Release --no-build
dotnet publish src\ObsidianDisplayGuard\ObsidianDisplayGuard.csproj -c Release -r win-x64 --self-contained true -o artifacts\win-x64
```

## Uninstall

1. Open the tray menu and clear **Start with Windows**.
2. Choose **Exit & restore**.
3. Delete `ObsidianDisplayGuard.exe`.
4. Optionally delete `%LocalAppData%\ObsidianDisplayGuard\` to remove local settings and logs.

## License

MIT
