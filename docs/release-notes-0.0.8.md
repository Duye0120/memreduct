# 0.0.8

## Bug fixes

- Fix upgrades and uninstalls failing after the main app has exited while the background cleanup helper is still running. Setup now recognizes and stops the matching helper task automatically.
- Show separate errors for a running main app, a helper that cannot stop, and a helper file that remains locked.

## Installation

Run the installer in the existing installation folder. Your settings and startup preferences are preserved. Versions 0.0.6 and earlier installed in a user-writable folder still need this manual upgrade once.

---

# 0.0.8 中文更新日志

## 错误修复

- 修复主程序已经退出、后台清理助手仍在运行时，升级或卸载失败的问题。安装器现在会正确识别并自动停止对应的助手任务。
- 分别提示主程序仍在运行、助手无法停止和助手文件仍被占用，避免混用退出托盘的提示。

## 安装说明

在原安装目录运行安装包，保留现有设置和开机启动选项。0.0.6 及更早版本安装在普通用户可写目录时，仍需先手动覆盖升级这一次。
