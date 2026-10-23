# 0.0.4 settings hotfix acceptance

This is a local installer candidate, not a published Release. It includes the preceding 0.0.3 features and preserves Henry++ attribution and GPL terms. The user's installed copy and startup tasks were not replaced or changed.

## Identity

- Source: `70d8c871aa84d96f4a587fa7f05e29954462f8b3`.
- Installer: `MemReductFluent-0.0.4-win-x64-setup.exe`, 46,307,463 bytes (44.16 MiB).
- SHA-256: `9ee56d10ccffc2a7d628d258545bb0b266b9035337f54705fb0086d3069e1e2f`.
- [Independent Windows build](https://github.com/Duye0120/memreduct-winui/actions/runs/39880854465): passed for this source.

## Reproduction and cause

In an elevated instance, left-clicking the tray opens the first-cleanup explanation. Clearing the future-confirmation checkbox and choosing Clean first saves `ConfirmCleanup=false` and `TrayCleanupHintShown=true`. That save failed with a `JsonSerializer` initialization error.

A fresh isolated process linking the actual settings and user-access source reproduced the elevated first-save failure. Inspecting the linked token showed `Identification` rather than a usable file-access level. Moving JSON work outside that callback exposed the underlying native file-write failure, error 1346 (`ERROR_BAD_IMPERSONATION_LEVEL`). Identity/group checks alone had missed it; prior UI checks also warmed JSON before invoking settings persistence.

## Fix

- Elevated user-data operations obtain the current desktop Shell's ordinary primary token using process-query and token-query/duplicate access only. They reject another SID, an administrator token, and anonymous/identification tokens. Missing or inaccessible Shell access fails closed. No extra privileges are enabled, and elevated writes are never a fallback.
- Ordinary instances continue using their own token. The same shared helper protects synchronous settings and asynchronous update-cache access.
- JSON conversion is outside the file-access callback. Bounded reads, unique temporary files, atomic replacement, preservation on failure and non-overwriting imports remain enforced.
- Settings errors expose the underlying message rather than only a type-initializer wrapper.
- The first built-in self-check now cold-saves and reloads the exact tray-confirmation preference before other JSON diagnostics. Identity checks assert a usable impersonation level and perform actual file I/O after an await.

## Verified

- Separate fresh ordinary and elevated processes passed first save, first load and first import using the actual production sources and isolated fixtures.
- The staged executable passed Chinese checks from a real elevated process: 30 groups and 48 layouts. Its recorded callback identities were non-admin both synchronously and after await.
- The same staged executable passed English checks from an ordinary process: 30 groups and 48 layouts. Atomic-save failure preservation, import behavior and cold tray-preference persistence remain covered.
- A separate elevated read-only probe loaded the user's existing installed profile and compared all normalized settings with the original file. No writes were requested.
- Installed and portable settings hashes match the pre-fix baseline. No cleanup, startup registration, installation or update execution was performed.

Local evidence directories are `settings-fix-staged-zh-CN` and `settings-fix-staged-en-US` under the verification folder. The cold-process probes are development scratch tools and do not ship in the application.

## Remaining acceptance

Install the hotfix and repeat the user's actual tray checkbox/clean action, then exit and reopen to confirm the choice persists. The programmatic checks do not claim physical tray-click acceptance, a real cleanup, a full upgrade transaction or a successful released-update flow. Custom or unavailable desktop Shells remain an explicit unsupported permission case rather than silently allowing administrator writes.
