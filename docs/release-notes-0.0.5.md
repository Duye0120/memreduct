# 0.0.5

## Features

- A protected cleanup helper lets the main app keep ordinary permissions, including when installed on another drive.

## Improvements

- Reuse cleanup authorization when reopening the installed app. Startup launches the ordinary tray app and its authorized helper at sign-in.
- Keep the selected installation folder and existing settings during the upgrade.

## Bug fixes

- Fix startup authorization failing when the main app is installed in a user-writable folder such as `D:\apps`.

---

# 0.0.5 中文更新日志

## 功能

- 新增受保护的清理助手，主程序保持普通权限，支持安装在其他盘符。

## 改进

- 重新打开安装版可复用清理授权；开机启动在登录时启动普通权限托盘程序和已授权的助手。
- 升级沿用已选安装目录，并保留现有设置。

## 错误修复

- 修复主程序安装在 `D:\apps` 等普通用户可写目录时，开机启动授权失败的问题。
