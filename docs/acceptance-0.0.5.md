# 0.0.5 release acceptance

This version changes installed cleanup authorization to a protected small helper. The UI stays in the registered installation folder with ordinary permissions; settings paths and formats are unchanged. The package was prepared locally, then published at the user's request for installed-app acceptance. Publication does not establish that installed login or privilege reuse has passed.

## Publication

- Published on October 23, 2026 at 10:30:56 China Standard Time as the latest stable [memreduct-winui 0.0.5 release](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v0.0.5).
- Release ID: `421391274`; annotated tag `fluent-v0.0.5` resolves to source commit `2f201dcab7e76206b91799e7222bc2e1f1fe642b`.
- [Independent Windows build](https://github.com/Duye0120/memreduct-winui/actions/runs/39889649919) passed for that source, including locked restore, x64 publish, resource parity and the small helper's contract check.
- GitHub's latest-release endpoint confirms `fluent-v0.0.5`, with draft and prerelease both false. The release body matches the English-first/Chinese-second notes in `release-notes-0.0.5.md`.
- Both uploaded assets report the expected byte counts and server-side SHA-256 digests. The installer digest matches the package identity below; the checksum sidecar digest is `3c91919ed837a2eb70fd4941e6d85090fab7915cbee6aa1ef424c79d1ddc50c2`.
- The public installer URL returns HTTP 200 with the expected 46,321,648-byte content length; the publicly downloaded checksum file exactly matches the local sidecar.
- Publishing uploaded existing artifacts only; it did not run setup, alter personal settings, or create a pull request.

## Package identity

- File: `MemReductFluent-0.0.5-win-x64-setup.exe`
- Size: 46,321,648 bytes (44.18 MiB).
- SHA-256: `2462a2b637ec852b7c6b53cbe7c83b8c969ea0833cfca5b6adfa86d3353fe434`.
- Protected helper EXE: 44,032 bytes (43 KiB); no second bundled runtime.
- Installer compilation completed successfully with Inno Setup 7.1.0. Checksum sidecar matches; installed and portable settings hashes both match the pre-work baseline.

## Implementation

- Fixed helper location: `Program Files\MemReduct Helper`, with administrator/SYSTEM maintenance and Users read/execute access. Installer checks links, protects its own folder and files, replaces known payloads without overwriting pre-existing hard links, and verifies protection after copying.
- Per-user `MemReduct.Fluent.Helper.<SID>` task runs only that protected executable. On-demand authorization survives closing the UI. Startup separately controls the helper logon trigger and the ordinary UI Run entry.
- Protocol accepts only Ping and the existing cleanup mask, automatic/manual flag and language. Both sides verify the same SID/session; the UI also checks the helper's elevated token and fixed protected image. Ordinary clients get read/write pipe access without permission to create server instances.
- Starred areas are filtered again by the helper. Native cleanup and privilege restoration remain shared with `MemoryEngine.cs`; no arbitrary execution, path operation or profile write is exposed through the pipe.
- Upgrade migration is limited to a matching old task for the current account. Packaging does not install anything, modify startup state or copy settings.

## Verification

- Incremental Release build: zero warnings/errors. Actual Framework helper builds from shared sources and passes its read-only contract check, using the Windows Framework 4.8 runtime without bundling another .NET/WinUI runtime.
- Final Chinese and English WinUI self-checks each passed 30 groups and 48 layout combinations. These are hidden-window programmatic checks, not physical desktop acceptance.
- A temporary disabled ordinary-permission task, never run, confirmed Windows rewrites the trigger identity and omits default enabled nodes. Production matching accepts the normalized identity only after resolving the same SID. The task was removed afterward.
- An isolated ordinary transport host exercised the actual server loop and bounded protocol: same-account client accepted, ordinary counterfeit server rejected by the client, automatic starred-only requests became no-ops, unknown requests disconnected, and stalled clients timed out without stopping the server. This fixture grants its ordinary host its own pipe-creation right; it does not prove the production elevated task or production pipe ACL end to end.
- The first isolated elevated task probe stopped before IPC because it exposed the XML-normalization mismatch. That mismatch was fixed and checked with Windows-normalized XML. The elevated task was removed and its dedicated protected probe folder was removed. A subsequent diagnostic retry was rejected by automatic approval with no specific reason, so it was not retried through another privileged path.
- No native memory cleanup, real app installation, user-setting change, actual login trigger or production task migration was performed.

## Acceptance still required

1. Exit the installed app and upgrade in its existing D-drive folder; confirm settings are retained.
2. Enable startup with the same signed-in administrator account, then clean manually. Confirm the UI itself remains ordinary and the helper is elevated.
3. Exit and reopen the app; clean again and confirm authorization is reused.
4. Sign out/in when convenient; confirm the tray app/helper start and automatic cleanup respects game mode, cooldown and starred exclusions.
5. Disable startup and verify login startup stops while manual on-demand authorization still works. Confirm uninstall removes only this product's tasks and retains settings.

Application-internal update installation from a writable UI directory remains under the pre-existing protected-install restriction; use the release installer manually for that case. This candidate does not widen the updater trust boundary.
