# Windows 11 UI refresh

This fork refreshes the native Win32 main window. Henry++ remains the author of
Mem Reduct; the original GPL license and notices are preserved.

- A larger physical-memory card and two compact secondary cards show the same
  usage, available-memory and total-memory statistics as the original window.
- Rounded surfaces, Segoe UI typography and usage bars establish a clear hierarchy.
- View > Follow system theme tracks Windows app colors; the existing dark-mode
  command remains available as a manual override.
- Cleaning commands, tray behavior, preferences and elevation requirements remain
  in place. The Settings button also opens the existing settings dialog.

## Preview

The Windows UI build workflow compiles x64 with Visual Studio 2022 on Windows 2025.
It checks out Henry++'s routine dependency at
`1acd9394395b7cee9e1ea7b985fb54e76be86b11` into a sibling directory, as expected by
the original project. No web runtime or new UI framework is used.

The artifact contains a portable INI file, so preferences are kept beside the
preview executable. Automatic update checks are disabled in that preview to avoid
replacing a custom build with the official upstream release. The current installed
application and its startup task are not replaced by this package.

The preview is unsigned. Compilation alone does not establish correct rendering,
screen-reader behavior, or elevated cleanup on every Windows/DPI configuration.
