# Mem Reduct Fluent 开发与交接指南

当前应用为 C# / WinUI 3 桌面程序，项目文件是 [MemReduct.Fluent.csproj](../modern/MemReduct.Fluent.csproj)。
请先读仓库 [README](../README.md) 和 [AGENTS.md](../AGENTS.md)。
本文中的本机路径是 2026-10-22 的交接快照，执行前检查存在性和当前 Git 状态；
在其他机器上使用通用构建命令，不要照搬本机绝对路径。

## 当前待办与已知问题

### D 盘安装版应用内更新（0.0.7）

- 根因是下载页面的安装目录保护检查，以及安装阶段提权主程序自身。先用实际启动信息复现提权目标为 `D:\apps\MemReduct Fluent\MemReduct.Fluent.exe`；回归断言改为只能提权固定受保护助手后通过。
- 主程序与 .NET Framework 助手共用 `UpdatePackage.cs` 的固定发布源、资产名/大小/哈希校验和可取消的复制逻辑。助手重新获取发布信息，以自己的受保护版本检查升级，不信任 UI 或 D 盘 DLL。
- `HelperUpdates.cs` 以普通令牌读取用户下载缓存，验证后只执行助手目录中的保护副本；不接受路径参数，不改主程序目录或祖先 ACL。取消 UAC 或校验失败时原 UI 保持运行。
- 更新安装结束或取消后，安装器只调用固定助手重开一次；`UserDataAccess.StartInstalledUi` 使用同账户普通 shell 令牌，避免把可写的 D 盘 UI 以管理员权限打开。取令牌失败会提示手动打开，不回退高权限启动。
- 中英文各通过 31 组现有检查与 48 组布局检查，助手自身覆盖发布信息解析、正确副本与篡改拒绝。既有动画检查改为等待完成状态，避免固定等待时长在高负载下误报；产品动画时长未改。
- 在助手的 .NET Framework 4.8 运行时实测访问公开 GitHub API 并调用共用解析器。实测发现直接 Roslyn 编译缺少目标框架属性会沿用旧 HTTPS 行为，已在生成程序集声明 4.8 目标，重新联网通过；不修改系统 TLS 或代理配置。依据见 [微软 TLS 指南](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls)。
- 旧版下载拦截逻辑无法靠发布信息修改，0.0.6 及以下位于可写目录时，需要手动覆盖安装 0.0.7 一次。真实 UAC、安装、取消向导与普通令牌重开仍需桌面验收，不把构建或自检当作完整升级通过。

### 三栏界面的清理动画（0.0.6）

- 按用户最新截图保留原三栏主界面；圆环设计草稿已在本机另存补丁，不进入当前实现。主题、语言资源、设置页及托盘配色保持已发布版本；“最近清理”改为默认折叠，新增记录仅显示标题，不自动展开，也不清空已有记录。
- 右上角正常状态不再显示“实时”和绿点；读取异常时才显示状态，恢复后自动收起。保留错误横幅、上次读数及托盘不可用标记。
- 用量压力使用普通状态文字，保留对应颜色，移除白底、描边与圆角。
- 仅在通过确认和授权、真正开始执行清理后，数字和三条进度条以 250 ms ease-out 回落到零；完成后以 850 ms 恢复到最新采样。快速完成也会走完短暂回落；失败恢复最近读数。动画值不写入历史、阈值判断或托盘。
- 隐藏、最小化、采样失败、关闭系统动画或高对比度时停止过渡，显示真实采样；关闭窗口后停止计时器。单一 UI 定时器仅在过渡期间运行。
- 既有自检覆盖同步归零、清理期间的新读数、快速完成、失败/缺失缓存及恢复；不执行原生清理。`--self-check <独立目录> --visual-preview` 可打开独立动画演示，点击“清理内存”仅演示显示变化，不授权、不读写个人设置、不执行清理。其录制帧来自真实 WinUI 控件。
- 0.0.6 发布前中英文各通过 31 组现有检查与 48 组布局检查，证据位于仓库同级 `MemReduct-Fluent-验证/release-0.0.6-zh-CN` 和 `release-0.0.6-en-US`；安装、真实清理和应用内完整升级由用户验收。

### WinUI 持续集成

`.github/workflows/winui.yml` 在 `master` 的 `modern/` 改动时执行，也支持手动触发。独立的 Windows 2025 runner 使用固定 .NET 10.0.402 和锁定依赖，检查中英文资源键一致性，再发布 x64 自包含构建。产物附带源码提交号和 SDK 版本，不包含个人设置或 PDB，保留 7 天；不会上传 Release 或安装到用户机器。

该流程验证干净环境中的还原和编译，不把构建成功当作真实 UI、管理员令牌、UAC、安装与自动更新验收。现有本地自检仍用于窗口、输入与文件行为验证。

持续改进范围与验收标准见 [产品改进计划](product-roadmap.md)。不要把单次编译或自检通过当成整个目标完成。

### 清理完成与失败反馈（下一版本）

- `CleanupResult.CompletedAreas` 只记录没有报告错误的完整操作。权限申请放在逐项执行范围内；一项的 Win32 权限或原生错误会标记该项并继续执行其他已选操作。
- 所有操作均未完整完成时显示“清理未完成”，托盘反馈失败颜色，保留原有上次完成时间；不把这段时间的可用内存波动写成清理成果。部分完成、全部完成和真实的 0 MB 变化分别处理。
- 最近五次记录保留失败原因和耗时；后续成功会收起旧的清理错误横幅，不收起保存设置等其他错误。
- 现有自检通过注入失败的执行回调验证真实分发循环，不调用原生清理或修改令牌。另验证界面结果、历史与时间戳；不能替代管理员环境下的实际清理和特权恢复验收。
- 屏幕外截图中的复选框动画曾显示横线；诊断时原控件为 `CheckedNormal`、图标为 `NormalOn`，五项未选控件为 `UncheckedNormal` / `NormalOff`。未发现配置半选错误，也未据此替换 WinUI 模板；实际桌面动画仍待验收。

### 全局提示与导航状态（下一版本）

- 错误和权限提示固定在导航下方，不再放在页面滚动内容里。原来的 `SelectPage` 会对页面调用 `StartBringIntoView`，从而把页面之前的提示滚出视口；自检已复现该问题。
- 提示区域高度受限，长消息可独立滚动，主页面仍可操作；关闭错误提示后折叠控件，收回间距。失败使用错误色，部分完成保留警告色；每次报告显式设置严重级别，不继承上一条提示。
- 导航切换同时服从授权交接和字号限制；`RefreshOptions` 与 `ReflowMetrics` 共享当前布局是否支持侧栏的状态，排队的点击回调也检查限制。
- 现有自检覆盖翻页/滚动后的提示位置、长错误与小窗口、关闭回收、12 屏幕像素圆角、正常/大字号恢复、授权期间重排与点击；截图包含中英文浅深色失败状态。未请求实际 UAC 或修改用户设置。

### 启动清理的执行顺序（下一版本）

- `--clean` 请求在窗口加载后消费一次；消费前禁止自动计划抢先执行。原来构造函数中的首次 `Sample` 可能先触发已经到期的自动清理，随后加载事件中的手动请求会被 `_cleaning` 重入保护跳过，漏掉仅限手动的区域。
- 周期采样通过 `CanStartAutomaticCleanup` 检查待处理请求、退出、清理、重启提示和提权状态；手动请求进入 `CleanAsync(manual: true)` 后继续使用原有重入与完成冷却。
- 自检复现了待处理请求仍被调度门放行的问题，并验证请求消费后恢复普通计划；检查模式仍在原生调用前返回。真实管理员启动与清理顺序需要桌面验收。

### 管理员首次保存设置（0.0.4）

- 复现步骤：管理员实例首次托盘左键清理，取消“以后手动清理前仍需确认”的勾选，点击“清理”。保存确认偏好失败，显示 `JsonSerializer` 类型初始化异常，后续可能一直重复引导。
- 原因是将 `TokenLinkedToken` 返回的 Identification 令牌当作可进行文件 I/O 的普通令牌。真实管理员隔离检查复现首次加载 JSON 失败，移出 JSON 转换后继续复现文件写入 `ERROR_BAD_IMPERSONATION_LEVEL (1346)`。原自检只比较 SID 和管理员组、提前初始化 JSON，未覆盖这个条件。[Windows 模拟级别](https://learn.microsoft.com/en-us/windows/win32/secauthz/impersonation-levels)区分身份查询与本机访问权限。
- 管理员文件访问改为从当前桌面 Shell 获取普通主令牌，只请求进程查询和令牌查询/复制权限；使用前验证 SID 与调用者一致、非管理员且不是 Identification/Anonymous。Shell 不存在、账户不符或令牌不可用时仍拒绝访问，不回退管理员文件写入。普通进程沿用自身令牌；不读取其他账户配置、不启用额外特权、不注册任务。
- JSON 序列化/解析在文件访问作用域外执行；设置读取、原子写入、导入及更新缓存仍通过 `UserDataAccess`。保留 64 KiB 上限、源文件保留、已存在目标不覆盖与失败时旧文件不变。
- 冷启动设置保存移到现有自检第一步，早于其他 JSON 诊断；权限检查增加实际模拟级别和 await 后文件写入。另以独立新进程分别检查普通/管理员的首次保存、读取、导入，不把预热后的通过当作冷启动证明。

### 采样中断与旧读数（下一版本）

- 采样失败后，顶部状态和趋势终点不再显示“实时 / 现在”；保留的数值标记为“上次读数”，状态提示显示上次读取时间。首次读取失败时明确显示尚无读数。
- 托盘用可空读数表示不可用状态，绘制红色 `?`，不伪装成 0% 或继续显示旧百分比。清理仍在执行时保留旋转动画和清理状态，同时说明监控读数不可用。
- 自动计划在采样故障期间暂停，恢复有效读取后恢复原计划。恢复只关闭采样自己的错误提示，不关闭保存设置等其他错误。
- 托盘刷新有独立错误处理，停止失败的动画更新，不再把托盘绘制错误误报为内存读取错误；正常状态不重复重写实时标签。旧数值颜色在故障期间也跟随主题变化。
- 现有自检注入采样错误，再通过真实的只读 `Sample` 路径恢复；验证首次无数据、旧读数标记、时间、自动调度门、清理动画组合状态、其他错误保留和问号图形的 GDI 句柄释放。检查窗口使用独立原生标题，避免正常实例的窗口查找命中它。

### D 盘主程序与受保护清理助手（0.0.5）

主程序、托盘和设置继续在普通权限进程运行，清理通过固定受保护目录中的 Framework 4.8 小助手执行。实现、任务迁移及通信边界见 [安装说明](installation.md#005-受保护助手)。首次授权创建按需任务，后续重新打开可直接启动该任务；开机启动另外控制登录触发器和普通 UI Run 项。任务只运行助手，不再把用户可写的 D 盘 UI 加入管理员登录动作。设置格式、文件路径与原子保存方式保持兼容。

新增助手也从 `MemoryEngine.cs` 编译原生操作，并复用 `UserDataAccess.cs` 的同账户普通令牌访问；Framework 目标排除现代 UI DTO/异步 impersonation API。共享源保持 C# 6 语法，由现有 SDK 的 Roslyn 编译，不安装额外框架或编译器。`build-helper.ps1` 随增量构建、安装包构建和 CI 执行只读协议自检。

### 手动重新打开与管理员授权（0.0.3–0.0.4 历史实现）

- 普通安装版的无参数启动和语言重启会尝试复用用户已开启且校验通过的管理员登录任务；不自动创建任务，不改变自启选择。
- 任务启动前仍校验实际安装路径、ACL、用户 SID 与任务动作。启动进程在等待期间不占用单实例互斥量；只有目标窗口应答自己处于管理员权限才完成交接。
- 最长等待 10 秒；任务不可用或无响应时回退普通启动，并显示原因。已提权、便携、自检、最小化、设置导入、显式清理和更新助手均不走这条转发路径。
- 构建脚本共用输出占用检查：只阻止实际使用预览/暂存目录的进程；独立安装目录中运行的实例不再阻止编译。无法读取进程路径时继续阻止覆盖。
- 实际登录任务交接、退出后重新打开及 UAC 行为仍需安装版验证；自检不创建或执行真实任务。

### 托盘首次清理引导（0.0.1）

- 首次托盘左键通过 `CleanAsync(fromTray: true)` 展示引导，复用已有清理确认弹窗；选择提交前不修改配置，取消不会清理，也不会标记引导已完成。主窗口按钮和自动清理不会触发这份引导。
- `TrayCleanupHintShown` 默认 false，旧配置保留已有 `ConfirmCleanup` 选择；只有用户确认首次引导时才记住完成状态。保存失败恢复原选择并停止本次清理。
- 托盘“手动清理前确认”的勾选状态直接读取 `ConfirmCleanup`，切换走设置页的同一个处理函数；提示文字也区分需要确认与直接清理。取消业务确认不代表取消 Windows 权限要求。
- 沿用现有管理员会话和安装版最高权限登录任务，不创建新的永久提权入口、不改变用户自启选择。便携版重新打开可能需要 UAC；安装版只有用户开启管理员自启并授权后才能在之后登录时免重复确认。
- 中英文各 17 组自检与 48 组布局通过；新增首次引导渲染、取消不改配置、选择持久化及设置开关联动检查。证据：仓库同级 `MemReduct-Fluent-验证/tray-onboarding-zh-CN` / `tray-onboarding-en-US`。未执行实际清理、注册任务或验证重新登录；真实托盘鼠标操作仍由用户验收。

### 1.0.2 安装版自启状态与关于布局

- 首次安装未创建本产品登录任务时，`ITaskFolder.GetTask` 返回 `0x80070002`。在实际 WinUI 进程中，.NET 将它映射成 `FileNotFoundException`，旧代码只捕获 `COMException`，导致黄色“无法读取开机启动状态”提示（显示的异常消息可能是 ClassFactory）。按准确 HRESULT 处理“任务不存在”为关闭状态，其他错误继续向上传递。参见 [.NET COM 异常映射](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/handling-com-interop-exceptions)。
- 从安装分支提取只读 `IsInstalledStartupEnabled`，让便携自检也调用真实任务查询；修复前 `startup-repro/self-check.json` 失败，修复后中英文均通过。只读现有任务，不注册、删除或运行任务。
- 关于卡片按版本/更新、项目链接、署名/退出分行；窄窗口和放大字体改为纵向排列。现有 48 组布局检查新增行边界、重叠和堆叠对齐断言，中英文通过，并输出 `update-settings.png`。
- 证据：仓库同级 `MemReduct-Fluent-验证/startup-fix-zh-CN`、`startup-fix-en-US`。每个语言 15 组检查、48 组布局；真实 UAC 注册、重新登录自启和覆盖安装仍需验收。
- 个人仓库已脱离 fork，`master` 从原分支快进并设为默认；源码入口链接指向仓库首页。历史分支、1.0.1 Release 和作者署名保留。

原版公开问题、本改版适用性与本次自动清理修复见 [问题核查记录](bug-audit.md)。自动清理完成后若仍超阈值，阈值重试退避 5 分钟；回落后恢复，独立定时与手动清理不受该退避影响。

**输入框高度问题已定位并修复（2026-10-22），等用户在实际窗口验收。**

![输入框高度问题](../images/winui-numberbox-height-known-issue.png)

- 用户指出的是高度，不是黑色焦点下划线。黑色底边是 WinUI 原生聚焦样式
  （`TextControlBorderThemeThicknessFocused = 1,1,1,2` + 黑白强调色），本次保留未动。
- 根因（自检实测确认）：`ThresholdNumber` / `IntervalNumber` 在水平 `StackPanel` 里默认
  `VerticalAlignment=Stretch`，被行内更高的 `ToggleSwitch` 拉到 40 DIP；TextBox 文字顶对齐渲染，
  文字贴顶、框底留白约 8 DIP，聚焦黑边画在留白之下、看起来脱离文字。与 DPI、文字缩放、模板无关。
- 修复：两个 `NumberBox` 加 `VerticalAlignment="Center"`（`MainWindow.xaml`），高度回到内容自撑的
  32 DIP；`ReflowMetrics` 仍只管宽度，清理逻辑与用户设置不变。
- 自检（`SelfCheck.cs`）新增断言：聚焦/失焦高度一致（≤1.2 DIP）、上下边缘不动（≤0.5 DIP）、
  文字中心偏差 ≤1 DIP、两行等高，并在 100/150/200% 文字缩放的布局循环中复检。
  中英文自检通过：高度 32、中心偏差 0.33 DIP、聚焦前后边缘完全一致。
- 证据：`..\MemReduct-Fluent-验证\height-before-zh-CN`（修复前，40 DIP、偏差 -3.67、断言失败）
  与 `height-zh-CN` / `height-en-US`（修复后，含 `threshold-focused.png` 等截图）。
- 自检在隐藏窗口的 150% DPI 下进行；实体键盘输入、其他 DPI 与缩放组合仍需用户实际验收。

**DPI 一致性加固（2026-10-22）**：窗口最小尺寸改为随当前显示器 DPI 重算（`UpdatePresenterConstraints`，
挂入 `XamlRoot.Changed`，此前只在启动时按启动 DPI 设置一次）；`UpdateCornerRadii` 增加 `XamlRoot` 判空并在
构造函数提前应用（消除首帧使用 App.xaml 内 DIP 圆角的窗口期）；托盘初始图标不再硬编码 32px，改用任务栏
DPI 的 `SM_CXSMICON`。布局、间距、字体仍是 DIP（设计如此），对话框圆角在创建时读取当前 scale，均未改动。
自检新增：全部 12 个自定义控件 10 屏幕像素圆角断言与最小尺寸随 DPI 断言（双语言在 150% 通过，
证据 `..\MemReduct-Fluent-验证\dpi-zh-CN` / `dpi-en-US`）。跨显示器拖动的实际 DPI 切换无法在隐藏窗口自检中
模拟，需用户多显示器验收。

`TrayIcon.ShowExistingWindow()` 查找的原生窗口标题 `Mem Reduct Fluent` 与 `MainWindow.xaml` 的
`Window.Title` 一致；自定义标题栏显示 `Mem Reduct`。重复启动唤回窗口仍需实际运行验证。

## 源码地图

### 托盘交互与游戏保护

- `TrayIcon` 左键/键盘激活进入现有手动清理入口，右键保留三项菜单。按成功协商的托盘版本分发消息，避免 `WM_LBUTTONUP` 与 `NIN_SELECT` 重复清理；短时间连点也被合并。
- 手动确认开启时先显示主窗口再弹确认；关闭确认可直接执行。仍沿用管理员授权、重入保护和错误处理。
- 正常用量绿色、70% 橙色、90% 红色；清理中使用 12 帧、100ms/帧的旋转弧线，数字仍表示用量而非进度。完成或异常时停动画，结果色保留 5 秒；高对比度使用系统前景色并保留文字状态。
- `GameModeMonitor` 用 Direct3D 全屏状态和前台窗口/显示器边界检测全屏，排除自身窗口、桌面与任务栏。每秒检查，不注入游戏、不扫描进程名单、不修改系统游戏模式。
- `GameMode` 默认 true；全屏视频、自动隐藏任务栏时占满屏幕的应用也可能触发保护。普通窗口化游戏不识别；多显示器只判断前台应用。切出全屏重置自动清理计时，至少 30 秒缓冲，间隔模式从此重新计时；已开始的原生清理不会被强行中断。
- `NotifyAfterCleanup` 默认 false，通过设置页保存。成功且开关开启才请求静音气泡通知；自动任务在全屏保护期间不发。Windows 通知策略可能抑制实际展示，应用不延迟排队过期通知。
- 内置自检覆盖颜色阈值、点击消息去重规则、动画帧变化与句柄释放、通知条件、游戏保护开关/配置兼容/退出缓冲，以及负坐标显示器几何判断。自检不创建真实托盘、不清理内存、不发送通知；真实游戏、鼠标点击及系统通知显示仍需交互验收。

| 文件 | 职责与入口 |
| --- | --- |
| [App.xaml](../modern/App.xaml) | 浅深色资源、黑白强调色、字体、圆角与共享间距 |
| [App.xaml.cs](../modern/App.xaml.cs) | 参数、单实例互斥、加载设置与语言、自检启动 |
| [MainWindow.xaml](../modern/MainWindow.xaml) | 主窗口、顶部/侧栏导航、概览与设置页宿主 |
| [SettingsView.xaml](../modern/SettingsView.xaml) | 首次进入设置时才创建的设置控件；由主窗口绑定现有事件 |
| [MainWindow.xaml.cs](../modern/MainWindow.xaml.cs) | 1 秒采样、增量刷新、布局、设置事件、清理/UAC/语言重启 |
| [AppSettings.cs](../modern/AppSettings.cs) | 默认值、数值范围、选中区域、自动计划、原子替换保存 |
| [MemoryEngine.cs](../modern/MemoryEngine.cs) | 只读采样、原生清理、权限与逐项错误收集 |
| [TrayIcon.cs](../modern/TrayIcon.cs) / [TrayGlyph.cs](../modern/TrayGlyph.cs) | 托盘消息、百分比数字图标、DPI/颜色、句柄释放 |
| [StartupRegistration.cs](../modern/StartupRegistration.cs) | 便携版 Run 项；安装版同用户最高权限交互式登录任务 |
| [Installation.cs](../modern/Installation.cs) / [UpdateService.cs](../modern/UpdateService.cs) | 已登记安装路径及 ACL 校验、个人设置路径、本人仓库的 Release 检查与验证下载 |
| [安装与发布指南](installation.md) | Inno Setup 安装、升级、卸载及发布流程与验证边界 |
| [UiText.cs](../modern/UiText.cs) / [Strings/](../modern/Strings/) | 启动时语言选择、ResourceLoader、简体中文与英文 `.resw` |
| [SelfCheck.cs](../modern/SelfCheck.cs) | 内置开发者自检、隐藏窗口布局测量、PNG/JSON 导出 |
| [app.manifest](../modern/app.manifest) | Windows 兼容声明、PerMonitorV2 DPI |

## 依赖与系统

以项目文件和 `packages.lock.json` 为准，以下为当前锁定配置：

| 项目 | 配置 |
| --- | --- |
| SDK / 目标框架 | .NET 10；`net10.0-windows10.0.26100.0` |
| 声明的最低 Windows | `10.0.19041.0`；目前只在本机 Windows 11 验证过 |
| 架构 | 仅 `x64` / `win-x64` |
| WinUI 包 | `Microsoft.WindowsAppSDK.WinUI` 2.3.9 |
| 窗口交互包 | `Microsoft.WindowsAppSDK.InteractiveExperiences` 2.1.9 |
| 文字渲染包 | `Microsoft.WindowsAppSDK.DWrite` 2.1.0 |
| SDK 构建工具 | `Microsoft.Windows.SDK.BuildTools` 10.0.26100.9169 |
| 发布方式 | 非 MSIX；自包含 .NET 与 Windows App SDK；未启用裁剪 |

构建不需要 Visual Studio，也不依赖旧 C 项目的兄弟目录 `routine` / `builder`。
不要为了修界面重装全套 SDK、引入 WebView 或更换 UI 框架。包升级应是独立任务。

## 本机位置

| 用途 | 当前路径 |
| --- | --- |
| Git 仓库 / Claude Code 工作目录 | `C:\Users\user\Documents\Codex\2026-10-22\c-users-user-appdata-roaming-mihomo\outputs\memreduct` |
| 正在使用的便携预览 | 仓库同级 `..\MemReduct-WinUI3-多语言预览\MemReduct.Fluent.exe` |
| 历史验证证据 | 仓库同级 `..\MemReduct-Fluent-验证\`，最新输入检查在 `editing-*` |
| SDK / 依赖 / 编译缓存 | `D:\tmp\memreduct-tray-build\` 下的 `dotnet`、`nuget`、`cli`、`obj` |
| 固定编译输出 | 同上面的便携预览目录；开发时不再生成 `bin` 和 `publish` 两份运行副本 |

本机 SDK 为 10.0.402，可直接调用 `D:\tmp\memreduct-tray-build\dotnet\dotnet.exe`。
C 盘空间有限，优先复用 D 盘环境；不要在 C 盘重复下载工具链、创建多份预览或 ZIP。
上述目录是本机文件，不包含在 Git 克隆中。

## 构建与运行

日常只使用 [build-dev.ps1](../modern/build-dev.ps1)，从仓库根目录执行：

```powershell
pwsh -File .\modern\build-dev.ps1
```

脚本默认复用 D 盘工具链与 `obj`，固定 Release/x64、固定输出到仓库同级的
`MemReduct-WinUI3-多语言预览`；使用 `dotnet build --no-restore`，不执行 publish、Clean 或 Rebuild。
EXE、DLL、PRI 未变化时不会重写，设置文件在构建前后校验哈希，不自动启动程序。
运行中的实例（含无法读取路径的管理员实例）会使脚本在改文件前停止；请从托盘退出后重试。

有意修改依赖时才显式还原；缺少还原资产会自动执行锁定还原：

```powershell
pwsh -File .\modern\build-dev.ps1 -Restore
```

其他机器用 `-BuildRoot` 指定工具链根目录，该目录下应已有 `dotnet\dotnet.exe`：

```powershell
pwsh -File .\modern\build-dev.ps1 -BuildRoot 'E:\dev\memreduct-build'
```

编译成功后按需运行固定目录中的 EXE：

```powershell
& '..\MemReduct-WinUI3-多语言预览\MemReduct.Fluent.exe'
```

直接打开 GUI EXE 会创建托盘并读取该目录旁的设置。不要用 `dotnet MemReduct.Fluent.dll`
替代 EXE 验证 WinUI 激活。启动其他副本前检查现有实例；全目录运行，不要只拷贝 EXE。
现有 `.github/workflows/windows-ui.yml` 构建的是旧 Win32 版本，不是这份项目。

仅需正式交付给其他机器时再单独 `dotnet publish`，交付目录不要与开发输出混用；不要每次改 UI 都发布。
SDK、NuGet 与 obj 是下一次增量构建所需缓存，不属于重复安装包，不能每次清空。

`--minimized` 用于登录后留在托盘；`--restart` 用于语言重启并等待旧实例退出。
`--elevated` / `--clean` 是内部提权流程参数，普通 UI 验证不要传 `--clean`，它会请求真实手动清理。

手动提权还携带 `--elevation-handoff <SID> <request-id>`。新进程在读取设置前校验当前管理员 SID，成功读取设置后通过会话内的随机命名事件通知原进程，并等待原进程确认交接；取消或超时不能继续清理。原窗口仅在收到确认后退出，等待期间锁定设置和重复操作。事件不传递设置或任意命令，允许的操作仅为打开程序和已明确请求的 `--clean`。跨账户提权不支持，不能通过读取另一账户的设置代替当前用户的选择。普通权限自检验证身份/参数契约与真实事件握手，但不代表真实 UAC、跨完整性级别事件访问或管理员启动成功。

单实例锁由 `App.AcquireInstanceMutex` 取得，只有持有所有权的 Mutex 才能进入退出路径。争用返回空值，等待异常释放本地句柄后向上传播；原持有线程异常退出时，保留 Windows 已转移的所有权。`SelfCheck.Lifecycle.cs` 使用独立随机名称和线程验证这些情况，不操作正在运行的应用锁。

## 功能、配置与原生行为

每秒采样一次；只在文本或进度变化时更新现有控件，避免刷新时替换控件或整页重绘。
“已提交内存”来自 `GetPerformanceInfo.CommitTotal / CommitLimit`，不等于分页文件已用空间。
系统文件缓存采样失败时可显示不可用值，不应编造数字。

| 设置 / 清理项 | 默认值 | 行为或原生入口 |
| --- | --- | --- |
| `CleanWorkingSets` / 进程工作集 | 开 | NT 内存列表命令 2 |
| `CleanSystemCache` / 系统文件缓存 | 开 | `SetSystemFileCacheSize` |
| `CleanLowPriorityStandby` / 低优先级备用列表 | 开 | NT 内存列表命令 5 |
| `CleanModifiedFileCache` / 已修改文件缓存 | 关 | 枚举磁盘卷并 `FlushFileBuffers`，不锁卷、不卸载、不写原始扇区 |
| `CleanModifiedPages` / 已修改页面列表 * | 关 | NT 内存列表命令 3；仅手动 |
| `CleanStandby` / 全部备用列表 * | 关 | NT 内存列表命令 4；仅手动 |
| `CleanRegistryCache` / 注册表缓存 | 关 | NT 信息类 155 |
| `CombineMemoryPages` / 合并重复内存页 | 关 | NT 信息类 130；x64 结构 24 字节 |
| `AutoThreshold` / `Threshold` | 关 / 90 | 允许范围 50–99% |
| `AutoInterval` / `IntervalMinutes` | 关 / 30 | 允许范围 1–1440 分钟 |
| `ConfirmCleanup` / `CloseToTray` | 开 / 开 | 手动清理确认；关闭窗口默认隐藏到托盘 |
| `TrayCleanupHintShown` | 关 | 首次托盘清理引导是否已确认，不改变旧配置中的确认偏好 |
| `AlwaysOnTop` / `SidebarExpanded` | 关 / 关 | 默认不置顶、顶部导航；可切换侧栏 |
| `Theme` / `Language` | `System` / `System` | 主题可实时切换；语言通过重启完整生效 |

星号过滤同时存在于 `AppSettings.AutomaticAreas` / 计划判断和 `MemoryEngine.GetOperations(automatic: true)`，
不得仅在 UI 隐藏。自动清理还受管理员权限、30 秒冷却、重入保护和语言重启弹窗状态控制。
除用户明确启用的安装版登录任务外，普通权限的自动清理不会自行请求提权；原生失败保留逐项错误，释放量只报告观测到的可用内存增加。

便携版 `settings.json` 在 EXE 旁，安装版在 `%LOCALAPPDATA%\MemReductFluent`；`SettingsStore.Save` 先写 `.tmp` 再替换，旧配置缺少新字段时使用默认值。
解析损坏配置时应保留原文件，不用默认设置静默覆盖。新增设置不应改变已有用户选择。
不读取 Henry++ 原版 INI，不修改其 `Mem Reduct - Logon` 任务。
便携版开机启动使用当前用户 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的
`MemReduct.Fluent` 项，命令为当前 EXE 加 `--minimized`；移动目录后需重新设置。
本项目安装版在用户授权后使用同账户的最高权限交互式登录任务，不保存密码；首次启用会切换到管理员托盘实例。
具体安装路径、设置导入、升级和卸载约定见 [安装维护说明](installation.md)。

尚未移植全局快捷键和托盘字体/颜色手动定制；当前数字托盘按内存阈值及清理状态变色，并适配任务栏主题和高对比度。

## 界面约定

- 参照 magpie：浅灰背景、白色分组、细分隔线、克制的黑白强调色；避免重新使用用户拒绝的蓝色按钮与旧多级菜单。
- 卡片圆角 12 **屏幕像素**、按钮与输入框 10 **屏幕像素**；`UpdateCornerRadii` 按 `RasterizationScale` 换算 DIP，不能直接把逻辑值当屏幕像素。
- 间距集中在 `App.xaml` 的 `Space8/12/16/20`、`PageInset`、`RowInset`、`MetricInset`；优先复用。
- 字体资源为 `Segoe UI Variable`，中文由系统回退。不要单独给某一页换字体。
- 导航偏好持久化；文字缩放超过 125% 时暂用顶部导航，保留侧栏偏好。
- 主题使用 WinUI 资源；焦点、悬停、禁用和高对比度需要分别检查，不只看静态白底截图。
- 文本和可访问名称同时维护 `Strings/zh-CN/Resources.resw`、`Strings/en-US/Resources.resw`；保留 `x:Uid` 映射。
- 语言在启动前设置覆盖值。当前采用保存后弹窗“稍后重启 / 立即重启”，默认稍后；重启参数只带 `--restart`，不能重放 `--clean`。

跟随系统语言时，`zh-*` 使用简体中文，其余使用英文；发布只保留中英文 WinUI 运行库语言资源。

## 验证与自检

文档修改不需要构建。代码修改按风险使用现有检查，不新增测试框架或例行执行整套构建。
需要验证 WinUI 渲染时，在增量构建后执行现有自检：

```powershell
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path '..\MemReduct-WinUI3-多语言预览\MemReduct.Fluent.exe').Path
foreach ($language in @('zh-CN', 'en-US')) {
    $proofPath = [IO.Path]::GetFullPath("..\MemReduct-Fluent-验证\dev-$language")
    $run = Start-Process -FilePath $exe -ArgumentList @('--self-check', ('"' + $proofPath + '"'), '--check-language', $language) -WindowStyle Hidden -PassThru -Wait
    $report = Get-Content -LiteralPath (Join-Path $proofPath 'self-check.json') -Raw | ConvertFrom-Json
    if ($run.ExitCode -ne 0 -or !$report.passed) { throw "Self-check failed: $language" }
    $report | Select-Object passed, language, @{Name='LayoutCases';Expression={$_.layouts.Count}}
}
```

自检不读取真实用户设置，不创建托盘或启动项，不启动自动清理计时器，不执行真实原生清理。
它会读取内存、在屏幕外创建自己的 WinUI 窗口，并导出 PNG 与 `self-check.json` / `input-editing.json`。

既有检查覆盖：中英文资源/可访问名称、配置兼容、星号过滤、时间计划与冷却、原生 ABI、
数字托盘句柄、重复采样不重写文本、中英文各 48 个宽度/字号/导航/页面组合，
以及通过程序化设置真实 `TextBox.Text`、移动焦点检查输入和失焦保存。
**已断言输入框焦点前后等高、文字居中及两行对齐；不覆盖实体键盘按键注入和跨显示器 DPI 切换，实际外观仍需用户验收。**

交付时分开报告：编译通过、只读采样、自检、程序化输入、真实桌面观感、用户键盘操作、
UAC/真实清理、语言实际重启、登录自启。不能相互替代；本次新增清理、实际语言重启与登录自启尚无完整验收。

## 更新现有便携预览

固定预览目录就是编译输出，不再经过 D 盘发布暂存或人工复制步骤。
明确区分 Git 源码、D 盘缓存、用户正在用的预览、原版安装目录。

1. 检查运行进程的准确 `ExecutablePath`。管理员实例可能读不到路径、也无法从普通权限结束；此时请用户从托盘“退出”，不要把查不到路径当作没有进程。
2. 预览只有窗口 × 时通常只是隐藏。不要覆盖仍占用的 DLL；不要同时运行多个普通实例进行对比。
3. 执行 `modern/build-dev.ps1`；脚本对设置做前后哈希校验，MSBuild 只更新需要更新的编译输出。
4. 根据改动风险执行现有检查；检查失败时先修复，不启动尚未通过检查的版本。
5. 保留设置和不明文件，不回滚用户设置；不再每次复制整个发布目录或新建逐次备份。
6. 预览根目录曾有外部目录句柄，整目录改名/交换失败过；优先原地更新，不创建一堆重复目录。

必要发布载荷约 170 MiB；当前本机预览还有约 56 MiB 旧 SDK 残留，先前删除被自动审批拦截。
**不要为了本次接手重试删除、改名绕过或删除整个预览目录。** 旧 `work/` 脚本不属于本仓库，不能假设可用或直接重跑。

## 许可与历史说明

保留 [LICENSE](../LICENSE) 与 Henry++ 署名。`src/`、根 `.sln`、`UI-NOTES.md` 属于早期 Win32 路线，
其中旧设置、多级菜单、紫色按钮和 Windows 7 支持等说明不适用于本 WinUI 实现。
