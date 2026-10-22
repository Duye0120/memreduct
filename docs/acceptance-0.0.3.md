# 0.0.3 candidate acceptance

This is a local acceptance candidate. It has not been installed over the user's existing application or published to GitHub Releases. Henry++ and upstream contributor attribution and the GPL license are retained.

## Changes since 0.0.2

- Reopening an installed application reuses an existing authorized startup task, with a bounded wait and an ordinary-permission fallback when it fails. Permission text distinguishes current-session consent from persistent startup configuration.
- A five-minute physical/committed memory chart uses bounded in-memory samples, shows sampling gaps, and stops preparing geometry while the overview is hidden.
- Recent cleanup results retain five actual attempts for the current session, including duration and partial/failed outcomes. Available-memory changes are observations, not guaranteed reclaimed amounts.
- The settings page groups additional cleanup areas under an expander; the two starred areas remain excluded from automatic cleaning.
- An optional process overview shows eight working sets on demand. It never terminates processes, scans in the background, saves process names, or adds shared working sets into a misleading system total.
- Update downloads expose progress and cancellation. Closing the app cancels pending release/download work and prevents late callbacks from updating disposed windows or starting cleanup.
- Card/control corners remain 12/10 physical pixels, navigation restores scroll position, and Chinese/English layouts retain the existing input-height checks.
- Stronger installer compression reduces download size without removing runtime files or moving dependencies into a separate download.

## Evidence and limits

The fixed preview builds without compiler warnings. Chinese and English self-checks exercise 21 groups and 48 layout cases each. Checks include real read-only memory/process APIs, task XML/query contracts, release parsing and verified-copy cancellation, settings import preservation, tray handle lifetime, input editing, text scaling and offscreen rendering. Fixture screenshots use synthetic history and process names.

A short real-process tray measurement is recorded in [the product roadmap](product-roadmap.md). CPU cost was low on the development machine, but the WinUI runtime's roughly 171 MiB working set is not comparable to the tiny native upstream application. A smaller trimmed build crashed and was rejected; normal candidates remain untrimmed.

Both the installed and portable user settings are checked against their pre-iteration hashes. Builds and self-checks do not register startup tasks, run real cleanup, install software, or alter those settings.

## Desktop acceptance still required

1. Install or upgrade to a protected directory; verify the chosen path and existing preferences.
2. Enable elevated startup once using the same signed-in administrator account. Exit from the tray, reopen normally, and verify that the existing task supplies permission without another UAC prompt. Disable the task and verify ordinary startup still works.
3. Cancel a first-time elevation and retry. A normal one-time UAC consent alone does not authorize future processes; user-writable custom directories intentionally cannot host the persistent elevated task.
4. Exercise actual tray left/right clicks, cleanup, full-screen protection and language restart with physical keyboard input and the user's display settings.
5. Exercise a real released update through download, cancellation, permission, installer completion and reopening. There is no new public Release for this candidate yet.
6. Verify fresh installation, upgrade and uninstall on a clean Windows machine. The local self-contained build is not proof of clean-machine dependency compatibility or a signed installer.

Do not weaken path/ACL checks, enable destructive automatic cleaning, or overwrite settings to make an acceptance step appear successful.
