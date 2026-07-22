# Security policy

## Supported version

The latest published release receives security fixes.

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting for this repository. Do not include vault contents, note titles, monitor serial numbers, or other personal data in a public issue.

## Security model

Obsidian Display Guard:

- runs without administrator rights;
- has no network client, telemetry, updater, driver, or service;
- does not read or modify note contents;
- stores configuration and bounded logs only under the current user's Local AppData;
- validates crash-recovery records against the window handle, process ID, and process start time before restoring a window.

The application cannot control the secure UAC desktop and does not promise to reposition elevated windows from a lower-integrity process.
