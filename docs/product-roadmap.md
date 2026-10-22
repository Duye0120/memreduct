# Product improvement plan

This is an ongoing improvement program, not a declaration that the product is complete. Henry++ and upstream contributor attribution, GPL terms, user settings, and the existing memory-cleaning safety checks remain requirements throughout.

## Acceptance targets

| Area | Required outcome | Evidence needed |
| --- | --- | --- |
| Permissions | Previously authorized users can reopen without repeated UAC; first-time consent, revocation, unsafe paths and failed task launches remain explicit | Installed-app exit/reopen, disabled/missing task, non-admin account and writable-path checks |
| Installation | Custom locations, upgrades, relocation instructions, uninstall and settings preservation work consistently | Real fresh install and upgrade on a second drive, settings hashes, task and uninstall registration |
| Size | Reduce download, installed and build-cache footprints without breaking clean-machine startup or hiding dependency downloads | Measured before/after size, clean-machine startup, both languages, configuration and cleanup paths |
| Interface | Compact Magpie-inspired layout, 10–12 physical-pixel corners, readable controls and coherent states | Light/dark, Chinese/English, narrow windows, 100–200% scaling, keyboard and input-focus checks |
| Cleanup | Accurate results, visible busy/failure state, no overlapping cleanups, conservative automatic behavior | Existing engine checks plus separately authorized real cleanup and fullscreen-game scenarios |
| Updates | Background download, clear progress, safe cancellation/retry, one restart at installation and preserved settings | Download interruption, digest mismatch, upgrade cancellation and real successful update |
| Maintenance | Reproducible build, private author identity, upstream credit and documented release evidence | Clean build, scoped English commits, release asset verification and source/tag consistency |

## Confirmed priorities

1. Manual reopen currently does not reuse the already authorized login task. Connect this path and wait for the destination window to acknowledge that it is elevated. Never replay cleanup/import/update helper arguments through the task.
2. Build scripts currently block any running copy, including an installed copy outside the generated output. Limit the guard to output directories, while rejecting inaccessible process paths.
3. Installation size is primarily bundled runtime code. The 0.0.2 staging directory includes a roughly 25 MiB Windows SDK projection, 15 MiB .NET core library and 15 MiB WinUI native DLL. Measure a supported trimming strategy before changing release defaults; do not delete runtime DLLs manually.
4. A custom user-writable installation currently loses persistent elevated startup and automatic update installation. Investigate a small protected helper design that preserves user-chosen UI locations without granting elevated execution to replaceable files. Current permission checks must remain until a complete design and validation replace them.
5. Inspect the current real UI and consolidate permission/update status text. Validate readability rather than treating successful compilation as visual approval.

## Current validation boundary

Existing self-checks can exercise pure path/ACL rules, task XML, settings import, memory-engine contracts and offscreen layout. They do not prove real UAC consent, scheduled task activation, installed-app relaunch, actual cleanup, upgrade rollback, or clean-machine dependency availability. Keep those items open until tested at the appropriate level.

Only publish a release after its scoped changes and assets have been verified and release authorization is present. Intermediate improvements can be committed and pushed to the personal repository without creating upstream pull requests.
