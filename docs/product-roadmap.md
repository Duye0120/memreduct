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

1. Manual reopen now reuses an existing authorized login task and waits for an elevated window to respond. Routing and timeout checks pass; real installed-app consent/reopen validation remains pending. Cleanup/import/update helper arguments are excluded.
2. Build scripts now limit running-process guards to output directories, while rejecting inaccessible process paths. The separate installed copy no longer blocks the fixed preview build.
3. Installation size is primarily bundled runtime code. The 0.0.2 staging directory includes a roughly 25 MiB Windows SDK projection, 15 MiB .NET core library and 15 MiB WinUI native DLL. Measure a supported trimming strategy before changing release defaults; do not delete runtime DLLs manually.
4. A custom user-writable installation currently loses persistent elevated startup and automatic update installation. Investigate a small protected helper design that preserves user-chosen UI locations without granting elevated execution to replaceable files. Current permission checks must remain until a complete design and validation replace them.
5. Inspect the current real UI and consolidate permission/update status text. Validate readability rather than treating successful compilation as visual approval.

## Implemented in the next-version source

- Five-minute physical/committed memory trend with bounded storage (301 samples), session-only retention, accessible text summary, and explicit gaps after interrupted sampling.
- Usage-state label, stronger secondary-text contrast, restrained 12px grouping cards and 10px controls. The cleaning action remains above the chart and visible in the default-size window.
- Returning from scrolled settings resets the overview to its top after layout/focus changes settle. Input callbacks stop during window teardown.
- The history fixture images use synthetic data exclusively inside the developer self-check. Regular UI always uses native measurements.

Next priorities: asynchronous shutdown boundaries, actual installed permission reuse, and measured idle runtime costs. Keep new diagnostics read-only by default and avoid process-killing features.

The settings hierarchy now keeps the three original cleanup areas visible and groups the five additional areas under an expander with a selected count. Existing enabled advanced choices expand automatically on launch. Download progress is bounded to percentage changes, reports size, and exposes cancellation without leaving an executable partial download. Permission descriptions distinguish session-only consent from an authorized startup task.

Settings/update checks passed in Chinese and English, including 48 layout cases with the longer installed-permission descriptions. Real file-copy checks cover intermediate progress and cancellation after a partial write. The final shutdown guard and end-of-copy cancellation check were rebuilt and rechecked in Chinese. Download-state screenshots use a fixture; no network transfer or installer execution is implied.

Initial microbenchmark on the development machine: native sampling averaged 0.104 ms per call; a populated 282-sample graph required 5.060 ms and approximately 335,751 managed bytes per geometry preparation. This excludes composition and is not an idle CPU benchmark. Reduce graph geometry allocations before adding more per-second visuals.

The graph now uses one polyline per uninterrupted series instead of one WinRT segment per sample. The same check measured 0.651 ms and 82,926 managed bytes per frame (approximately 87% less preparation time and 75% fewer managed allocations). The Chinese self-check passed all 18 groups and 48 layout cases after the change; data-gap assertions still pass. Timing is machine/load dependent.

Recent cleanups now retain the last five native attempts in memory for the current session. They show manual/automatic origin, duration, the observed available-memory increase, and partial/failed outcomes. Confirmation or UAC cancellation does not create a fake cleanup record. Chinese and English checks passed 19 groups and 48 layout cases, including bounded retention and failure rendering. Fixture images were inspected; no real cleanup was invoked.

## Size experiment boundary

An isolated .NET 10.0.402 partial-trimming experiment reduced the publish folder from 178,340,120 to 87,032,681 bytes. It was **rejected for delivery**: the executable exited with `0xC0000005` before producing a self-check report. Analysis also warned about dynamic COM, reflection-based serialization and WinRT projections. Normal builds remain untrimmed and unchanged; do not enable trimming just to reproduce the smaller number. See Microsoft's [known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities).

The experiment log and size/exit-code manifest were preserved in local work files. Automatic approval rejected cleanup of `D:\tmp\memreduct-tray-build\trim-experiment` with only `blocked by policy`; that directory remains and must not be deleted through an alternate mechanism. It contains temporary build outputs, not user settings. This cleanup restriction does not block normal development.

Validation for this iteration: the incremental build completed with zero warnings/errors. Chinese and English self-checks passed 18 check groups and 48 layout cases each, including scrolling back from settings and a visible primary action at the default window size. Offscreen light/dark renders were generated; Chinese and English overview renders were inspected. The installed and portable settings hashes match the pre-iteration baseline. No real cleanup, task mutation, install or release occurred during these checks.

## Current validation boundary

The local 0.0.3 candidate installer compiled successfully with Inno Setup 7.1.0. Both staged-executable self-checks passed 19 groups and 48 layouts. The installer is 46,284,158 bytes, compared with 47,107,420 bytes for 0.0.2 (1.75% smaller despite the added UI). LZMA2 ultra compression reduces download size only; it uses a 32 MiB installer decompression dictionary instead of the default 8 MiB. The normal application remains untrimmed. Installed and portable settings hashes remain unchanged. This is a local candidate, not a published release or proof of successful installation.

Existing self-checks can exercise pure path/ACL rules, task XML, settings import, memory-engine contracts and offscreen layout. They do not prove real UAC consent, scheduled task activation, installed-app relaunch, actual cleanup, upgrade rollback, or clean-machine dependency availability. Keep those items open until tested at the appropriate level.

Only publish a release after its scoped changes and assets have been verified and release authorization is present. Intermediate improvements can be committed and pushed to the personal repository without creating upstream pull requests.
