# 0.0.3 candidate acceptance

This is a local acceptance candidate. It has not been installed over the user's existing application or published to GitHub Releases. Henry++ and upstream contributor attribution and the GPL license are retained.

## Candidate identity

- Source: `60c32b6b9ce9b9dba7d56dcbeedb20f43d8beb06`.
- Installer: `MemReductFluent-0.0.3-win-x64-setup.exe`, 46,303,220 bytes (44.16 MiB).
- SHA-256: `0d4fc1956fa1bdcb7fe030cbfa1a368caae6c667d82794ee2e073eedd5e8b744`.
- Both installer-stage language checks passed all 29 groups and 48 layout cases after final packaging.
- Installed and portable settings hashes match the pre-iteration baseline.

## Changes since 0.0.2

- Reopening an installed application reuses an existing authorized startup task, with a bounded wait and an ordinary-permission fallback when it fails. Permission text distinguishes current-session consent from persistent startup configuration.
- Manual elevation validates the requesting account before reading settings. The original window remains open until the elevated process passes that check, reads its settings, and completes a two-way handoff. Cancellation, timeout and account mismatch do not authorize delayed cleanup; preferences and duplicate actions are locked during transfer. Cross-account elevation is unsupported.
- An explicit startup cleanup request blocks automatic scheduling until window loading consumes it once. An already-due automatic cleanup cannot preempt that request and cause manual-only selections to be skipped by reentry protection.
- A five-minute physical/committed memory chart uses bounded in-memory samples, shows sampling gaps, and stops preparing geometry while the overview is hidden, minimized or outside the scroll viewport.
- Recent cleanup results retain five actual attempts for the current session, including duration and partial/failed outcomes. Available-memory changes are observations, not guaranteed reclaimed amounts.
- Cleanup privileges are scoped to each selected operation. A Win32 failure is retained for that operation while later operations continue. If none completes without errors, the result uses failure feedback, keeps the previous completion timestamp and does not attribute unrelated memory changes to cleanup. Fully successful zero-byte results remain valid, and a later success dismisses an earlier cleanup-error banner.
- Single-instance mutexes are retained only after successful acquisition. Contention and invalid waits cannot expose an unowned mutex to shutdown; abandoned-owner recovery retains Windows-granted ownership.
- The settings page groups additional cleanup areas under an expander; the two starred areas remain excluded from automatic cleaning.
- Settings controls are created on the first visit and retained afterwards. Tray preferences continue to work before that visit, and the page initializes from the current preferences.
- An optional process overview shows eight working sets on demand. It never terminates processes, scans in the background, saves process names, or adds shared working sets into a misleading system total.
- Update downloads expose progress and cancellation. Both native window closing and tray Exit cancel pending release/download work and prevent late callbacks from updating disposed windows or starting cleanup.
- User settings and downloaded update files are accessed with the same account's ordinary permissions. Settings writes use exclusive random temporary files and atomic replacement; failed replacement keeps the original, and reads reject files larger than 64 KiB.
- Card/control corners remain 12/10 physical pixels, navigation restores scroll position, and Chinese/English layouts retain the existing input-height checks.
- Global errors and permission notices stay beneath navigation while pages scroll or change. Long messages scroll within a bounded notification area, closing an error reclaims its space, and failed/partial cleanup outcomes use their own error/warning severity. Layout refreshes and queued navigation clicks preserve elevation and large-text restrictions.
- Stronger installer compression reduces download size without removing runtime files or moving dependencies into a separate download.
- Failed sampling labels retained values as last readings, provides the last successful read time, replaces the tray percentage with a red question mark and suspends automatic dispatch. Recovery restores live readings without dismissing unrelated errors. Tray drawing failures have their own error path.

## Evidence and limits

The fixed preview builds without compiler warnings. Chinese and English self-checks exercise 29 groups and 48 layout cases each. Checks include deferred settings initialization, elevation request identity/parameter validation and event handoff, mutex ownership, explicit startup-cleanup priority, per-operation cleanup failure dispatch and result presentation, persistent notification layout and severity, sampling failure/recovery and unknown tray readings, real read-only memory/process APIs, task XML/query contracts, release parsing and verified-copy cancellation, atomic settings replacement and failure preservation, ordinary-token identity across asynchronous continuations, tray handle lifetime, input editing, text scaling and offscreen rendering. Fixture screenshots use synthetic history, failures and process names. Passing installer-stage evidence is in `staged-monitor-recheck` (Chinese) and `staged-monitor-final-en-US` under the local verification directory.

The first parallel staged checks failed the tray pixel-coverage assertion before producing screenshots. Sequential checks of the same unchanged staged binary subsequently passed in both languages. A diagnostic-only source change now includes the character, icon size and actual coverage in assertion failures; the corresponding preview passed both languages concurrently. The initial failure remains unexplained and is retained in `staged-monitor-state-zh-CN` and `staged-monitor-state-en-US`; checks were not weakened. The packaged binary has the source identity above and does not include that later assertion-message change. Inspect the real tray during acceptance rather than treating the later passes as proof that the intermittent failure is resolved.

The [independent Windows CI build for this source](https://github.com/Duye0120/memreduct-winui/actions/runs/39878964414) passed with .NET 10.0.402, locked NuGet dependencies and resource-key parity checks. CI produces a portable build artifact with seven-day retention; no desktop acceptance or installation is implied.

A separate ordinary-permission process was launched with a handoff request and no cleanup intent; it exited with the expected rejection code 2 before loading settings. The in-process event checks cover readiness, transfer confirmation, timeout and expired handles. Neither check establishes that an elevated process can access the original process's events across integrity levels; actual UAC consent and transfer remain desktop acceptance items.

A local import-table inspection of 150 PE binaries found 167 unique imported modules. Non-API-set imports resolve either within staging or in the development machine's System32; no external `MSVCP`, `VCRUNTIME`, `VCCORLIB` or `CONCRT` DLL imports were found. This includes delay imports but does not cover dynamic loading or prove compatibility with a clean Windows 10 machine.

A short real-process tray measurement is recorded in [the product roadmap](product-roadmap.md). CPU cost was low on the development machine, but the WinUI runtime's roughly 172 MiB working set is not comparable to the tiny native upstream application. Deferred settings creation does not demonstrate a substantial memory reduction. A smaller trimmed build crashed and was rejected; normal candidates remain untrimmed.

The preceding staged source `e5ecc1f` completed a 320.3-second tray observation after a five-second warmup, crossing the five-minute history capacity. On this 22-logical-processor machine, it used 531.25 ms CPU (0.166% of one core / 0.00754% of the machine), ending at 170.7 MiB working set and 89.6 MiB private commit. Across sixteen samples, GDI objects stayed at 82, USER objects ranged from 25 to 27, and kernel handles from 835 to 850, without sustained handle growth. Raw evidence is `tray-soak-e5ecc1f.json` in the local verification directory. This was an isolated, ordinary-permission process with automatic cleanup off, no settings file, no open settings page and no actual cleanup. The observation does not establish overnight leak freedom, active-window performance or a material reduction in runtime memory.

UI revision `bfaae17` was also observed for 30.06 seconds using the same isolated tray procedure: 93.75 ms CPU (0.312% of one core / 0.0142% of this machine), 171.69 MiB final working set, 89.39 MiB private commit and 886 handles at each of three samples. Evidence is `tray-idle-bfaae17.json`. Different observation durations and ordinary runtime variation prevent a performance-improvement claim. These measurements predate the final startup scheduling fix; that fix has local functional checks but no separate idle benchmark.

Both the installed and portable user settings are checked against their pre-iteration hashes. Builds and self-checks do not register startup tasks, run real cleanup, install software, or alter those settings.

Final packaged source `60c32b6` completed a 30.06-second isolated ordinary-permission tray observation: 156.25 ms CPU (0.520% of one core / 0.0236% of this 22-processor machine), 170.6 MiB final working set and 89.1 MiB private commit. Handles ranged from 864 to 867 across three samples. Raw evidence is `tray-idle-60c32b6.json`. This short check supports low background CPU on this machine, not a material memory reduction or long-duration leak freedom. The process was stopped afterwards; automatic cleanup was off and no settings file was created.

A read-only local check still found installed version 0.0.1. Task Scheduler returned `ERROR_FILE_NOT_FOUND` for this application's current-user task. No task was created and the installed application was not replaced; persistent consent is therefore not configured on this machine. This observation does not validate the candidate's real elevated relaunch path.

## Desktop acceptance still required

1. Install or upgrade to a protected directory; verify the chosen path and existing preferences.
2. Enable elevated startup once using the same signed-in administrator account. Exit from the tray, reopen normally, and verify that the existing task supplies permission without another UAC prompt. Disable the task and verify ordinary startup still works.
3. Cancel a first-time elevation and retry; verify the same-account two-way handoff and that a different administrator account is rejected while the original window remains open. A normal one-time UAC consent alone does not authorize future processes; user-writable custom directories intentionally cannot host the persistent elevated task.
4. Exercise actual tray left/right clicks, cleanup, full-screen protection and language restart with physical keyboard input and the user's display settings.
   Verify that an explicit startup manual-clean request takes priority over an already-due automatic cleanup and is not replayed by another loading event.
5. Exercise a real released update through download, cancellation, permission, installer completion and reopening. There is no new public Release for this candidate yet.
6. Verify fresh installation, upgrade and uninstall on a clean Windows machine. The local self-contained build is not proof of clean-machine dependency compatibility or a signed installer.
7. Verify settings save and update download from a same-account elevated process. Current identity checks ran from an ordinary process. If a same-account limited token is unavailable (including UAC-disabled configurations), file access intentionally fails instead of falling back to elevated user-data writes.

Do not weaken path/ACL checks, enable destructive automatic cleaning, or overwrite settings to make an acceptance step appear successful.
