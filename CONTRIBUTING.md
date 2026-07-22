# Contributing

Contributions are welcome through pull requests.

Before opening a PR:

1. Keep runtime code dependency-free unless a dependency is essential and documented.
2. Never commit local `config.json`, `session.json`, logs, monitor identifiers, vault paths, or note data.
3. Run `dotnet test ObsidianDisplayGuard.slnx -c Release`.
4. Describe any behavior change involving window movement, recovery, startup, or fullscreen restoration.

Window-management changes should fail closed: when monitor or window identity is uncertain, restore Obsidian and stop moving windows.
