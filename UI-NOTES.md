# Windows 11 UI refresh

This fork refreshes the native Win32 main window. Henry++ remains the author of
Mem Reduct; the original GPL license and notices are preserved.

- A compact three-column panel shows physical memory, pagefile and system working
  set, with the original usage, available-memory and total-memory statistics.
- The Magpie-inspired layout uses a light gray canvas, a white rounded panel,
  subtle dividers, restrained purple accents and compact controls.
- More > View > Follow system theme tracks Windows app colors; the existing dark-mode
  command remains available as a manual override.
- Cleaning commands, tray behavior, preferences and elevation requirements remain
  in place. Settings opens the existing settings dialog; More retains the original
  File, View, Settings and Help menus. F1/F2 shortcuts remain; F10 opens More.

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
