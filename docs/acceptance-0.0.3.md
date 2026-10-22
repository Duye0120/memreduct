# 0.0.3 candidate acceptance

This is a local acceptance candidate. It has not been installed over the user's existing application or published to GitHub Releases. Henry++ and upstream contributor attribution and the GPL license are retained.

## Candidate identity

- Source: `e509b55c5b3d45d7f806d2166e28ba326c11d318`.
- Installer: `MemReductFluent-0.0.3-win-x64-setup.exe`, 46,272,654 bytes (44.13 MiB).
- SHA-256: `860838fe8c3648e1a486a1f236e81984ab896ff73ab3818c019bafcf5e8358b8`.
- Both installer-stage language checks passed all 23 groups and 48 layout cases after final packaging; the fixed preview uses the same source.
- Installed and portable settings hashes match the pre-iteration baseline.

## Changes since 0.0.2

- Reopening an installed application reuses an existing authorized startup task, with a bounded wait and an ordinary-permission fallback when it fails. Permission text distinguishes current-session consent from persistent startup configuration.
- A five-minute physical/committed memory chart uses bounded in-memory samples, shows sampling gaps, and stops preparing geometry while the overview is hidden, minimized or outside the scroll viewport.
- Recent cleanup results retain five actual attempts for the current session, including duration and partial/failed outcomes. Available-memory changes are observations, not guaranteed reclaimed amounts.
- The settings page groups additional cleanup areas under an expander; the two starred areas remain excluded from automatic cleaning.
- An optional process overview shows eight working sets on demand. It never terminates processes, scans in the background, saves process names, or adds shared working sets into a misleading system total.
- Update downloads expose progress and cancellation. Both native window closing and tray Exit cancel pending release/download work and prevent late callbacks from updating disposed windows or starting cleanup.
- User settings and downloaded update files are accessed with the same account's ordinary permissions. Settings writes use exclusive random temporary files and atomic replacement; failed replacement keeps the original, and reads reject files larger than 64 KiB.
- Card/control corners remain 12/10 physical pixels, navigation restores scroll position, and Chinese/English layouts retain the existing input-height checks.
- Stronger installer compression reduces download size without removing runtime files or moving dependencies into a separate download.

## Evidence and limits

The fixed preview builds without compiler warnings. Chinese and English self-checks exercise 23 groups and 48 layout cases each. Checks include real read-only memory/process APIs, task XML/query contracts, release parsing and verified-copy cancellation, atomic settings replacement and failure preservation, ordinary-token identity across asynchronous continuations, tray handle lifetime, input editing, text scaling and offscreen rendering. Fixture screenshots use synthetic history and process names.

The [independent Windows CI build](https://github.com/Duye0120/memreduct-winui/actions/runs/39781122005) passed for this source with .NET 10.0.402, locked NuGet dependencies and resource-key parity checks. It produced a portable build artifact with seven-day retention; no desktop acceptance or installation is implied.

A local import-table inspection of 150 PE binaries found 167 unique imported modules. Non-API-set imports resolve either within staging or in the development machine's System32; no external `MSVCP`, `VCRUNTIME`, `VCCORLIB` or `CONCRT` DLL imports were found. This includes delay imports but does not cover dynamic loading or prove compatibility with a clean Windows 10 machine.

A short real-process tray measurement is recorded in [the product roadmap](product-roadmap.md). CPU cost was low on the development machine, but the WinUI runtime's roughly 173 MiB working set is not comparable to the tiny native upstream application. A smaller trimmed build crashed and was rejected; normal candidates remain untrimmed.

Both the installed and portable user settings are checked against their pre-iteration hashes. Builds and self-checks do not register startup tasks, run real cleanup, install software, or alter those settings.

## Desktop acceptance still required

1. Install or upgrade to a protected directory; verify the chosen path and existing preferences.
2. Enable elevated startup once using the same signed-in administrator account. Exit from the tray, reopen normally, and verify that the existing task supplies permission without another UAC prompt. Disable the task and verify ordinary startup still works.
3. Cancel a first-time elevation and retry. A normal one-time UAC consent alone does not authorize future processes; user-writable custom directories intentionally cannot host the persistent elevated task.
4. Exercise actual tray left/right clicks, cleanup, full-screen protection and language restart with physical keyboard input and the user's display settings.
5. Exercise a real released update through download, cancellation, permission, installer completion and reopening. There is no new public Release for this candidate yet.
6. Verify fresh installation, upgrade and uninstall on a clean Windows machine. The local self-contained build is not proof of clean-machine dependency compatibility or a signed installer.
7. Verify settings save and update download from a same-account elevated process. Current identity checks ran from an ordinary process. If a same-account limited token is unavailable (including UAC-disabled configurations), file access intentionally fails instead of falling back to elevated user-data writes.

Do not weaken path/ACL checks, enable destructive automatic cleaning, or overwrite settings to make an acceptance step appear successful.
