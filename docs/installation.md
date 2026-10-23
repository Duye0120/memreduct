# 安装、管理员自启与更新

这是 Duye0120 自用 WinUI 版本的安装路线，与根目录历史 Win32 版本独立。程序版本来自 `modern/MemReduct.Fluent.csproj` 的三段 `Version`，当前按用户要求从 `0.0.1` 重新编号；旧 `1.0.0`–`1.0.2` 仅作为历史版本。

## 用户行为

1. 从 0.0.2 起安装向导显示目录选择，默认 `Program Files\MemReduct Fluent`，支持其他盘符、空格及中文本地目录。安装仍要求管理员权限，拒绝磁盘根目录、网络路径及符号链接。升级通过 `UsePreviousAppDir` 沿用已有目录；迁移需先卸载再安装，避免遗留自启任务或双份卸载记录。
2. 首次安装可选择便携版的 `settings.json`。安装完成后勾选打开程序时导入：只复制且不覆盖已有配置；取消打开则不导入，源文件始终保留。
3. 安装版设置位于 `%LOCALAPPDATA%\MemReductFluent\settings.json`。更新、重装、卸载均保留该目录；便携预览仍用 EXE 旁的配置。
4. 从 0.0.5 起，安装版主程序保持普通权限。小型助手固定安装在 `Program Files\MemReduct Helper`，首次清理授权后注册 `MemReduct.Fluent.Helper.<SID>` 最高权限任务；后续通过该固定任务按需启动助手，无需重新提权主程序。
5. 开机启动同时控制普通权限 UI 的当前用户 Run 项和助手任务的登录触发器；关闭时删除 UI Run 项并禁用触发器，保留按需授权。修改任务仍需当前账户同意 UAC。升级迁移精确匹配的旧任务，卸载只移除本产品匹配任务；Henry++ 原版文件、INI、任务不受影响。

管理员自启要求当前登录账户本身属于管理员；用另一管理员账户的凭据替普通账户授权会被拒绝，避免创建到错误账户。程序不是 SYSTEM 服务，不保存登录密码。安装和升级自身仍可能需要 UAC，不能保证所有未来操作都完全免确认。

安装身份读取 HKLM 64 位卸载项 `{5B9D5C89-F01C-4AB6-A593-DA69B3835832}_is1` 的 `InstallLocation`，不读取用户可写的路径标记。设置位置不随安装目录移动。助手目录、文件和祖先必须通过 ACL、所有者及非链接检查；D 盘 UI 的文件不进入清理或更新提权入口。0.0.7 起可写的主程序目录也支持应用内更新；0.0.6 及以下的旧程序需先手动覆盖安装一次修复版。安装器仅收紧自己的固定助手目录，绝不修改 `D:\apps` 或其他父目录 ACL。

### 0.0.8 退出主程序后助手占用修复

2026-10-23 用户反馈主程序退出后安装 0.0.7 仍提示退出。实机仅见清理助手运行，对应任务的账号名直接比较 SID 为 false，解析为相同 SID 后为 true；安装器未停止该任务，助手文件删除失败后误用了主程序退出文案。原因与只读验证见 [开发指南](development.md#退出后安装器仍提示退出008)。

已发布 [fluent-v0.0.8](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v0.0.8) 并设为 latest，源码与标签提交 `310d1ee1bd6e6aaa3fe6ec928b7191a8e2386459`。Inno Setup 7.1.0 编译的安装包大小 46,322,039 字节，SHA-256：`886db3ec66448559bc9cf613836b31b9fb751216350b45402ad8157fd33f8810`；两个资产的服务端 digest、大小与本地一致，公开 latest API 确认 0.0.8。

实际 Inno 身份匹配代码的四项只读检查通过，助手自身检查及安装包编译通过；未例行重跑未改动的主界面检查。安装版、便携版设置前后哈希保持一致。没有替用户停止管理员助手、覆盖安装或执行卸载，仍由用户重试安装验收。

### 0.0.7 D 盘应用内更新修复

2026-10-23 已发布 [fluent-v0.0.7](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v0.0.7) 并设为 latest，源码与标签提交为 `9a0dec53e2fcf81c2d52d28100661e334f0e9e9d`。更新日志见 [0.0.7](release-notes-0.0.7.md)，英文在上、中文在下。

中英文各通过 31 组现有检查与 48 组布局检查，.NET Framework 助手自身检查通过。实际使用同版本 Framework 运行时读取 GitHub 发布信息并解析通过，编译的助手明确声明 .NET Framework 4.8 目标，避免旧目标默认 HTTPS 行为。

Inno Setup 7.1.0 实编译的 `MemReductFluent-0.0.7-win-x64-setup.exe` 为 46,327,607 字节，SHA-256：`537ee7f1e06d074f4684c9203702605826128802f9242d6403a16ecdbf1436dc`。两个上传资产的服务端 digest、大小均与本地一致，公开 latest API 确认 0.0.7。通过修复版真实下载函数完整读取正式发布资产，流式校验大小与 SHA-256 一致，没有另存下载副本或写入用户更新缓存；版本比较和同版不重复提示验证通过。

安装版和便携版设置前后哈希一致，未改启动任务、D 盘 ACL 或已安装文件。真实 UAC、安装/取消向导及普通令牌重开尚未执行；旧版在可写目录仍需手动覆盖安装本修复版一次。

### 0.0.6 清理动画与状态显示

2026-10-23 已按用户授权发布 [fluent-v0.0.6](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v0.0.6) 并设为 latest，源码与标签提交为 `bc1bc54a5b24cdceabd42b132c691fba00fbaca6`。更新日志见 [0.0.6](release-notes-0.0.6.md)，完整英文在上、中文在下。

中英文各通过 31 组现有检查与 48 组布局检查。Inno Setup 7.1.0 生成 `MemReductFluent-0.0.6-win-x64-setup.exe`，大小 46,306,510 字节，SHA-256 为 `bd9d0c3243845b652b1242d13ed88f1953172c175fdc73534f46fb4d50f5838c`；安装包和校验文件的 GitHub 服务端 digest、大小均与本地一致。公开 latest API 返回 0.0.6，通过已安装的 0.0.5 更新解析逻辑确认能识别 0.0.6，且同版本不会重复提示。

安装版与便携版设置文件前后哈希一致，未替用户安装、执行真实清理或修改启动任务。当前 `D:\apps` 和主程序目录仍允许普通用户写入，既有应用内更新会在提供下载前拦截并提示手动安装；完整下载、授权、安装和自动重新打开流程仍需后续验收。

### 0.0.5 受保护助手

- `HelperTrust.cs`、`HelperTasks.cs`、`HelperProtocol.cs` 为 UI 与助手共用的权限、任务和通信约束；`helper/Program.cs` 不构造 WinUI、不加载个人配置、不执行任意命令或路径。
- `MemoryEngine.cs` 的原生清理操作、特权恢复与卷缓存逻辑共用源文件。助手独立过滤星号自动清理项，返回逐项错误与实际完成位掩码。
- 管道只允许本机同账户同会话；双向核对进程 SID/会话，UI 还验证服务端管理员令牌和固定受保护映像。请求固定 12 字节，响应最多 64 KiB；错误文本有长度上限，连接/读取有超时。
- 计划任务只运行固定助手 `--serve <SID>`，不保存密码，不使用 SYSTEM；当前账户只有任务读取/执行权限，不能编辑高权限动作。
- Windows 注册 XML 后会把登录触发器中的 SID 转为账户名，并省略值为 true 的 Enabled 节点；匹配先解析为相同 SID，遵循默认启用语义，同时保留精确动作/目录/参数检查。
- `build-helper.ps1` 使用现有 .NET SDK 的 Roslyn 编译器、Windows Framework 引用与系统 .NET Framework 4.8 运行时；助手不再携带一套 .NET/WinUI DLL。开发和交付都复用固定输出/缓存目录。
- 安装时保护助手自身目录，停止精确匹配的助手任务并移除已知旧 EXE/config 后复制新文件，再核对保护；不覆盖任何 settings.json。旧 UI 最高权限登录任务仅在账户、动作、参数均匹配后迁移。其他账户的旧任务保留，不批量删除。

### 0.0.2 验证记录

源码提交 `23d1678adc60fb3b158ba9af4f9c4ff36aa4c9e3`。Release/x64 编译成功；中英文各 17 组自检、48 组布局检查通过，新增不同盘符、中文路径、大小写、相邻路径、非法路径和祖先目录权限边界断言。证据位于仓库同级 `MemReduct-Fluent-验证/custom-install-zh-CN` 与 `custom-install-en-US`。

Inno Setup 7.1.0 编译生成 `MemReductFluent-0.0.2-win-x64-setup.exe`（47,107,420 字节），SHA-256：`c76b1c84f49e77193080475f51d07e8c25438dc4c632b0874f90f02541d5a68e`。安装版与便携版设置文件哈希未变化。打包时运行中的已安装版位于 Program Files，与预览和暂存目录独立，未被覆盖或关闭。尚未执行真实自定义目录安装、覆盖升级、卸载、UAC 或登录自启验证。

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
3. 上传精确命名的 `MemReductFluent-X.Y.Z-win-x64-setup.exe` 和 `.sha256`，设为正式、非草稿 Release。更新日志先写完整英文，再写内容一致的完整中文；提交信息与 Release 标题仍使用英文。
4. 检查 GitHub Release API 的资产 `digest` 为 `sha256:<64位哈希>`；旧资产或没有 digest 的资产不会执行。该值与本地 `.sha256` 必须一致。
5. 应用“检查更新”只读取本仓库最新正式 Release。旧版标签、预发布、相同/更旧版本不提示安装。

更新日志采用版本号标题和分类列表，完整英文在上、完整中文在下：

- **Features / 功能**：新增的用户功能。
- **Improvements / 改进**：已有功能的体验、性能或交互变化。
- **Bug fixes / 错误修复**：指出原来的触发场景和现在修正后的行为。

空分类省略，中英文逐项对应。每条优先用一句话说明用户可感知的变化，只列实际完成的内容，不直接粘贴提交信息。内部实现、自检数量、CI 与详细验收证据保留在开发文档；必要的安装说明或已知限制可在更新日志后简短补充。

更新只在用户点击时检查，不轮询、不后台静默安装。便携版提供发布页入口；安装版确认后在当前进程后台下载到 `%LOCALAPPDATA%\MemReductFluent\Updates\update.exe`，期间不提权、不重启。来源固定为本人仓库的精确资产地址，重定向只允许 HTTPS GitHub 下载域，下载校验大小及 SHA-256。

点击「安装更新」才通过 UAC 启动固定的 `Program Files\MemReduct Helper\MemReduct.Helper.exe --install-update <SID>`。助手不创建主窗口、托盘或实例互斥量，不读写设置；重新读取 GitHub 正式发布元数据，以当前账户普通令牌打开用户缓存，将其复制到受保护的 `Program Files\MemReduct Helper\Updates\update.exe`，再次校验大小和 SHA-256，再启动安装器。目标目录仅来自 HKLM 卸载项，通过 `/DIR` 保留，UI 不向助手传递路径、URL、哈希或命令。用户缓存从不直接执行。原进程仅在助手成功后退出；取消 UAC 则保持运行和可重试状态，验证失败恢复检查更新入口。两处缓存均复用固定文件名。

应用内升级要求同一管理员账户，避免读取或启动到另一账户的配置。普通账户需由管理员手动下载安装包完成安装；管理员凭据是 Windows 要求，应用不存储密码。`/FROMAPP=1` 安装结束或取消时，安装器的 `DeinitializeSetup` 只调用一次受保护助手的 `--launch-ui`，后者核对同账户 shell 普通令牌，经 `DuplicateTokenEx` 和 `CreateProcessWithTokenW` 重开 UI；取不到普通令牌则失败，绝不回退管理员启动。普通手动安装保留原有启动勾选项。

实现依据：[Inno Setup 退出事件](https://jrsoftware.org/ishelp/topic_scriptevents.htm)、[CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw)。即使使用 `runasoriginaluser`，已由提权进程启动的安装器也不能保证降权，因此应用内更新改由固定助手明确选择普通令牌。

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

历史归档：下述 1.0.1、1.0.2 Release 及附件已于 2026-10-22 按用户要求清理；源码提交与 Git 标签保留。当前下载入口为文末的 0.0.1 Release。

仓库已更名为 `Duye0120/memreduct-winui`，应用源码链接、发布页、更新 API 与安装器元数据同步迁移。安装标识、安装路径、设置路径和启动任务名称保持不变。版本提升到 1.0.1，包含持续高占用自动清理修复。

应用发布与 Inno Setup 编译通过；改名后的中文自检通过 15 组检查、48 组布局，两语言清理回归见 [问题核查记录](bug-audit.md)。便携设置 SHA-256 保持不变。

安装包：`MemReductFluent-1.0.1-win-x64-setup.exe`（47,074,654 字节）。SHA-256：`2a7f4c8a57f67c9479ff66dd6b7bb60589d9e7492b36d420e661ad11cc9720df`。

已发布 [fluent-v1.0.1](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v1.0.1)，标签对应源码提交 `45748e972cb5ff56236578374028ebc60de0d9ea`。GitHub latest API 返回正式版本，两项附件上传完成，安装包的服务端 digest、大小与本地一致。通过实际发布元数据调用应用的版本解析逻辑，确认 1.0.0 可识别 1.0.1，1.0.1 不会提示同版本更新；未执行下载后的安装升级。

更名前的本地 1.0.0 使用旧 API 地址且不跟随 API 重定向，因此此次通过 Release 手动安装 1.0.1，后续检查更新使用新仓库地址。

后续在隔离 Windows 环境验证首次安装/导入、UAC 取消、同用户授权、退出后重新登录自动管理员运行、更新保留设置及任务、降级拦截、卸载任务移除且设置保留。确认后再对用户本机安装和正式发布。

参考：[Windows 任务安全上下文](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks)、[GitHub Release API](https://docs.github.com/en/rest/releases/releases)、[Inno Setup 文档](https://jrsoftware.org/ishelp/)。

### 1.0.2 修复包验证

修复首次安装没有登录任务时的读取误报，整理关于页；原因与回归证据见 [开发指南](development.md#102-安装版自启状态与关于布局)。中英文各 15 组自检、48 组布局通过，包含真实 COM 只读查询。旧版 1.0.1 安装问题由用户截图反馈；本次未执行覆盖安装、UAC 注册或注销登录验收。

`MemReductFluent-1.0.2-win-x64-setup.exe` 已经 Inno Setup 7.1.0 实编译，大小 47,137,366 字节，SHA-256 为 `2fc7d8b4ae7dfe1a6814dac6311eec311a84fde31c15303d1dc385374ed7a02c`。构建前后安装版与便携版设置文件哈希均一致，未修改用户登录任务或原版安装目录。

经用户确认已发布 [fluent-v1.0.2](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v1.0.2)，源码提交 `11152aef1920e97800703a9a864f28aca77b6f02`。两项附件上传成功，服务端 digest 和大小与本地一致，latest API 指向 1.0.2。使用真实发布 JSON 调用应用 `ParseRelease`，确认当前 1.0.1 可识别更新，当前 1.0.2 不提示重复安装；未代用户安装。

仓库已脱离 GitHub fork 网络，默认分支和当前本地维护分支均为 `master`。原有分支、标签和 1.0.1 Release 仍可访问，README 保留 Henry++ 来源署名。

### 0.0.1 版本重置与后台更新

2026-10-22 按用户要求将当前开发版本重置为 0.0.1，保留之前的修复、来源署名及设置。下载与安装拆为两步：后台下载不重启，点击安装才授权；验证失败或取消授权不退出主进程。托盘右键改用独立的零尺寸工具窗口持有菜单，不再激活主窗口。

中英文各 16 组自检、48 组布局通过，证据位于仓库同级 `MemReduct-Fluent-验证/update-fix-zh-CN` 与 `update-fix-en-US`。包含隐藏主窗口后激活菜单宿主仍保持隐藏、下载副本哈希/长度/取消/已有文件保护、旧 1.0.x 标签过滤，以及安装就绪文案的布局检查。没有触发真实清理、修改任务或写入用户设置。

Inno Setup 7.1.0 实编译生成 `MemReductFluent-0.0.1-win-x64-setup.exe`，随后补入首次托盘清理引导及确认状态菜单，覆盖同一个未发布安装包。当前包为 47,105,541 字节，SHA-256：`02e536c8a60c8b9e7a41a2b00dc62103fa078a7bd197077248433baffebe8e6f`。安装版与便携版设置文件前后哈希均一致。已删除本次被替代的、未发布的 1.0.3 安装包及校验文件，复用固定预览和 D 盘构建缓存。

首次引导补充验证：中英文各 17 组自检、48 组布局通过，包含取消不改配置、选择记忆和设置开关同步；详见 [开发指南](development.md#托盘首次清理引导001)。权限路线沿用现有实现，未更改任何开机任务。

经用户明确授权，2026-10-22 已发布 [fluent-v0.0.1](https://github.com/Duye0120/memreduct-winui/releases/tag/fluent-v0.0.1) 并显式设为 latest。标签对应源码提交 `5c7db3d7d76e6772bf1a4348cfef2a83581544f2`，两项附件的服务端 SHA-256 与大小均与本地一致；latest API 和发布列表均确认 0.0.1 为唯一当前 Release。确认新包可用后删除旧的 1.0.1、1.0.2 发布及附件，保留源码历史与 Git 标签。

待验证边界：真实托盘鼠标操作、UAC 取消、旧版覆盖安装、完整下载—提权—安装—自动打开流程。未代用户安装或修改设置；旧 1.0.x 必须手动下载 0.0.1 安装一次，之后按新版本序列更新。
