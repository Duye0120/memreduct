# 安装、管理员自启与更新

这是 Duye0120 自用 WinUI 版本的安装路线，与根目录历史 Win32 版本独立。程序版本来自 `modern/MemReduct.Fluent.csproj` 的三段 `Version`，首版为 `1.0.0`。

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

- `-BuildRoot` 默认 `D:\tmp\memreduct-tray-build`，复用既有 .NET 10、NuGet、obj 与固定预览输出。
- `-Compiler` 可显式指定；自动寻找工具链目录、Program Files、当前用户 Programs 下的 Inno Setup 7。
- 先检查编译器和运行实例，再发布到单一 `installer-stage`；缺少编译器时不会创建发布副本。
- 暂存目录每次仅重建此生成目录；遇到设置文件或符号链接立即停止，必须人工检查。预览设置在构建前后校验哈希。
- 默认结果在仓库同级 `MemReduct-安装包`，可用 `-OutputDirectory` 指定。
- 文件名固定 `MemReductFluent-1.0.0-win-x64-setup.exe`，旁边生成 `.sha256`。
- 打包排除用户设置、PDB、日志和更新缓存；包含运行依赖及 GPL 许可。
- 脚本只构建，不安装、不创建任务、不创建 PR、不推送标签、不发布 Release。

## 发布与更新契约

发布属于单独的用户授权操作。准备发布时：

1. 更新 `.csproj` 的 `Version`，验证代码并提交到个人仓库，构建安装包。
2. 在同一提交上创建 `fluent-vX.Y.Z` 标签，发布到 `Duye0120/memreduct`；附上对应源码提交与变更说明。
3. 上传精确命名的 `MemReductFluent-X.Y.Z-win-x64-setup.exe` 和 `.sha256`，设为正式、非草稿 Release。
4. 检查 GitHub Release API 的资产 `digest` 为 `sha256:<64位哈希>`；旧资产或没有 digest 的资产不会执行。该值与本地 `.sha256` 必须一致。
5. 应用“检查更新”只读取本仓库最新正式 Release。旧版标签、预发布、相同/更旧版本不提示安装。

更新只在用户点击时检查，不轮询、不后台静默安装。便携版提供发布页入口；安装版确认后通过已有 UAC 流程授权，重新读取发布信息，下载到受保护的 `Program Files\MemReduct Fluent\Updates` 单一缓存位置。来源固定为本人仓库的精确资产地址，重定向只允许 HTTPS GitHub 下载域，校验大小及 SHA-256 后才执行安装器并退出程序。

SHA-256 用于检查下载完整性，信任根是 GitHub 仓库/Release；它不等于代码签名。本版本未配置 Authenticode 证书。安装器会阻止降级、检查运行互斥量，并在安装前拒绝链接路径；不强杀程序、不改变用户配置。

## 当前验证状态与下一步

- 应用 Release/x64 编译通过；中英文自检通过，包含配置导入保留原文件、拒绝无效 JSON、任务身份/参数、安装路径与写入 ACL 边界、Release 来源/版本/哈希检查。
- Windows Task Scheduler COM 接受生成的 XML；仅设置内存中的 TaskDefinition，**未注册任务、未提权**。
- 构建脚本语法、缺少编译器时提前退出、XAML/资源以及预览设置保持检查通过。
- **未编译 `.iss`，未生成安装 EXE，未实际安装/升级/卸载或重启登录验证。** 当前机器缺少 Inno Setup，自动审批拒绝了编译器安装；用户选择稍后生成安装包，不重试绕过。
- 仓库目前没有正式 Release，因此尚无真实更新下载与安装验收；自检使用本地发布元数据及哈希样本，不伪造发布。

编译器准备好后的验收：先生成安装包，在隔离 Windows 环境验证首次安装/导入、UAC 取消、同用户授权、退出后重新登录自动管理员运行、更新保留设置及任务、降级拦截、卸载任务移除且设置保留。确认后再对用户本机安装和正式发布。

参考：[Windows 任务安全上下文](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks)、[GitHub Release API](https://docs.github.com/en/rest/releases/releases)、[Inno Setup 文档](https://jrsoftware.org/ishelp/)。
