# 安装、管理员自启与更新

这是 Duye0120 自用 WinUI 版本的安装路线，与根目录历史 Win32 版本独立。程序版本来自 `modern/MemReduct.Fluent.csproj` 的三段 `Version`，当前按用户要求从 `0.0.1` 重新编号；旧 `1.0.0`–`1.0.2` 仅作为历史版本。

## 用户行为

1. 安装到固定 `Program Files\MemReduct Fluent`。安装器要求管理员权限，不能通过 `/DIR` 切换到用户可写目录。
2. 首次安装可选择便携版的 `settings.json`。安装完成后勾选打开程序时导入：只复制且不覆盖已有配置；取消打开则不导入，源文件始终保留。
3. 安装版设置位于 `%LOCALAPPDATA%\MemReductFluent\settings.json`。更新、重装、卸载均保留该目录；便携预览仍用 EXE 旁的配置。
4. 在安装版设置中开启开机启动，使用当前登录的管理员账户授权一次。注册 `MemReduct.Fluent.<SID>` 最高权限交互式登录任务，并启动管理员托盘实例；以后登录不再弹 UAC。关闭该设置需要有修改任务的权限。
5. 更新保留既有任务，卸载只移除名称与执行目标都匹配的本产品任务。原 Henry++ 安装文件、INI、任务不受影响。

管理员自启要求当前登录账户本身属于管理员；用另一管理员账户的凭据替普通账户授权会被拒绝，避免创建到错误账户。程序不是 SYSTEM 服务，不保存登录密码。安装和升级自身仍可能需要 UAC，不能保证所有未来操作都完全免确认。

## 构建安装包

日常使用 `modern/build-dev.ps1`。只有要交付时才使用 Inno Setup 7 和下面的脚本：

```powershell
pwsh -File .\modern\build-installer.ps1 -Compiler 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

当前用户机器已安装到 D 盘，使用：

```powershell
pwsh -File .\modern\build-installer.ps1 -Compiler 'D:\apps\Inno Setup 7\ISCC.exe'
```

- `-BuildRoot` 默认 `D:\tmp\memreduct-tray-build`，复用既有 .NET 10、NuGet、obj 与固定预览输出。
- `-Compiler` 可显式指定；自动寻找工具链目录、Program Files、当前用户 Programs 下的 Inno Setup 7。
- 先检查编译器和运行实例，再发布到单一 `installer-stage`；缺少编译器时不会创建发布副本。
- 暂存目录每次仅重建此生成目录；遇到设置文件或符号链接立即停止，必须人工检查。预览设置在构建前后校验哈希。
- 默认结果在仓库同级 `MemReduct-安装包`，可用 `-OutputDirectory` 指定。
- 文件名遵循 `MemReductFluent-X.Y.Z-win-x64-setup.exe`，旁边生成 `.sha256`。
- 打包排除用户设置、PDB、日志和更新缓存；包含运行依赖及 GPL 许可。
- 脚本只构建，不安装、不创建任务、不创建 PR、不推送标签、不发布 Release。

## 发布与更新契约

发布属于单独的用户授权操作。准备发布时：

1. 更新 `.csproj` 的 `Version`，验证代码并提交到个人仓库，构建安装包。
2. 在同一提交上创建 `fluent-vX.Y.Z` 标签，发布到 `Duye0120/memreduct-winui`；附上对应源码提交与变更说明。
3. 上传精确命名的 `MemReductFluent-X.Y.Z-win-x64-setup.exe` 和 `.sha256`，设为正式、非草稿 Release。
4. 检查 GitHub Release API 的资产 `digest` 为 `sha256:<64位哈希>`；旧资产或没有 digest 的资产不会执行。该值与本地 `.sha256` 必须一致。
5. 应用“检查更新”只读取本仓库最新正式 Release。旧版标签、预发布、相同/更旧版本不提示安装。

更新只在用户点击时检查，不轮询、不后台静默安装。便携版提供发布页入口；安装版确认后在当前进程后台下载到 `%LOCALAPPDATA%\MemReductFluent\Updates\update.exe`，期间不提权、不重启。来源固定为本人仓库的精确资产地址，重定向只允许 HTTPS GitHub 下载域，下载校验大小及 SHA-256。

点击「安装更新」才通过 UAC 启动固定受保护安装目录中的 `--install-update <SID>` 助手。助手不创建主窗口、托盘或实例互斥量，不读写设置；重新读取 GitHub 正式发布元数据，将缓存复制到受保护的 `Program Files\MemReduct Fluent\Updates\update.exe` 并再次校验大小和 SHA-256，再启动安装器。用户缓存从不直接执行。原进程仅在助手成功后退出；取消 UAC 则保持运行和可重试状态，验证失败恢复检查更新入口。两处缓存均复用固定文件名。

应用内升级要求同一管理员账户，避免读取或启动到另一账户的配置。普通账户需由管理员手动下载安装包完成安装；管理员凭据是 Windows 要求，应用不存储密码。`/FROMAPP=1` 安装成功后自动启动一次，普通手动安装保留原有启动勾选项。

从历史 `1.0.0`–`1.0.2` 迁移到 `0.0.1` 需要手动下载安装包一次：旧版只接受更大的版本号，无法通过远程发布修改这个判断。0.x 安装器仅对这三个历史版本放行迁移；其他较高版本仍拒绝降级。新版更新检查忽略三个旧 Release 标签，避免把旧代码当作升级。安装标识、路径、设置和启动任务名称不变；后续按 `0.0.2`、`0.0.3` 等递增，发布时需显式设为 latest。

SHA-256 用于检查下载完整性，信任根是 GitHub 仓库/Release；它不等于代码签名。本版本未配置 Authenticode 证书。安装器会阻止降级、检查运行互斥量，并在安装前拒绝链接路径；不强杀程序、不改变用户配置。

## 当前验证状态与下一步

- 应用 Release/x64 编译通过；中英文自检通过，包含配置导入保留原文件、拒绝无效 JSON、任务身份/参数、安装路径与写入 ACL 边界、Release 来源/版本/哈希检查。
- Windows Task Scheduler COM 接受生成的 XML；仅设置内存中的 TaskDefinition，**未注册任务、未提权**。
- 构建脚本语法、缺少编译器时提前退出、XAML/资源以及预览设置保持检查通过。
- 2026-10-22 用户自行安装 Inno Setup 7.1.0 后，发布与 `.iss` 编译通过，生成约 44.93 MiB 的 `MemReductFluent-1.0.0-win-x64-setup.exe` 及 `.sha256`；未包含用户设置。实编译时修复了 Pascal 的 `Type` 保留字冲突，卸载仍仅按本产品任务名称与执行目标匹配移除。
- **未实际安装/升级/卸载或重启登录验证。** 安装包尚未签名。
- 内置自检使用本地发布元数据及哈希样本；发布文件校验与实际升级安装验收需分别记录。

### 1.0.1 发布记录

仓库已更名为 `Duye0120/memreduct-winui`，应用源码链接、发布页、更新 API 与安装器元数据同步迁移。安装标识、安装路径、设置路径和启动任务名称保持不变。版本提升到 1.0.1，包含持续高占用自动清理修复。

应用发布与 Inno Setup 编译通过；改名后的中文自检通过 15 组检查、48 组布局，两语言清理回归见 [问题核查记录](bug-audit.md)。便携设置 SHA-256 保持不变。

安装包：`MemReductFluent-1.0.1-win-x64-setup.exe`（47,074,654 字节）。SHA-256：`2a7f4c8a57f67c9479ff66dd6b7bb60589d9e7492b36d420e661ad11cc9720df`。

已发布 [fluent-v1.0.1](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v1.0.1)，标签对应源码提交 `dad47ada37d72d70175bd0e53bb09163b16f4587`。GitHub latest API 返回正式版本，两项附件上传完成，安装包的服务端 digest、大小与本地一致。通过实际发布元数据调用应用的版本解析逻辑，确认 1.0.0 可识别 1.0.1，1.0.1 不会提示同版本更新；未执行下载后的安装升级。

更名前的本地 1.0.0 使用旧 API 地址且不跟随 API 重定向，因此此次通过 Release 手动安装 1.0.1，后续检查更新使用新仓库地址。

后续在隔离 Windows 环境验证首次安装/导入、UAC 取消、同用户授权、退出后重新登录自动管理员运行、更新保留设置及任务、降级拦截、卸载任务移除且设置保留。确认后再对用户本机安装和正式发布。

参考：[Windows 任务安全上下文](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks)、[GitHub Release API](https://docs.github.com/en/rest/releases/releases)、[Inno Setup 文档](https://jrsoftware.org/ishelp/)。

### 1.0.2 修复包验证

修复首次安装没有登录任务时的读取误报，整理关于页；原因与回归证据见 [开发指南](development.md#102-安装版自启状态与关于布局)。中英文各 15 组自检、48 组布局通过，包含真实 COM 只读查询。旧版 1.0.1 安装问题由用户截图反馈；本次未执行覆盖安装、UAC 注册或注销登录验收。

`MemReductFluent-1.0.2-win-x64-setup.exe` 已经 Inno Setup 7.1.0 实编译，大小 47,137,366 字节，SHA-256 为 `2fc7d8b4ae7dfe1a6814dac6311eec311a84fde31c15303d1dc385374ed7a02c`。构建前后安装版与便携版设置文件哈希均一致，未修改用户登录任务或原版安装目录。

经用户确认已发布 [fluent-v1.0.2](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v1.0.2)，源码提交 `3134e8356dab23b2ddb577197cc020cf7a503e07`。两项附件上传成功，服务端 digest 和大小与本地一致，latest API 指向 1.0.2。使用真实发布 JSON 调用应用 `ParseRelease`，确认当前 1.0.1 可识别更新，当前 1.0.2 不提示重复安装；未代用户安装。

仓库已脱离 GitHub fork 网络，默认分支和当前本地维护分支均为 `master`。原有分支、标签和 1.0.1 Release 仍可访问，README 保留 Henry++ 来源署名。

### 0.0.1 版本重置与后台更新

2026-10-22 按用户要求将当前开发版本重置为 0.0.1，保留之前的修复、来源署名及设置。下载与安装拆为两步：后台下载不重启，点击安装才授权；验证失败或取消授权不退出主进程。托盘右键改用独立的零尺寸工具窗口持有菜单，不再激活主窗口。

中英文各 16 组自检、48 组布局通过，证据位于仓库同级 `MemReduct-Fluent-验证/update-fix-zh-CN` 与 `update-fix-en-US`。包含隐藏主窗口后激活菜单宿主仍保持隐藏、下载副本哈希/长度/取消/已有文件保护、旧 1.0.x 标签过滤，以及安装就绪文案的布局检查。没有触发真实清理、修改任务或写入用户设置。

Inno Setup 7.1.0 实编译生成 `MemReductFluent-0.0.1-win-x64-setup.exe`，47,128,984 字节，SHA-256：`6576cfc0cc9ffb1e1b714b09d3a9cb1a4f6f844a9aeebfa9aace7857128ae6c6`。安装版与便携版设置文件前后哈希均一致。已删除本次被替代的、未发布的 1.0.3 安装包及校验文件，复用固定预览和 D 盘构建缓存。

待验证边界：真实托盘鼠标操作、UAC 取消、旧版覆盖安装、完整下载—提权—安装—自动打开流程。尚未执行本机安装，也未发布 0.0.1 Release；发布需明确设置为 latest，旧版必须手动迁移一次。
