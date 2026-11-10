// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Desktop.ViewModels;
using HeliumTodo.Desktop.Views;
using HeliumTodo.Domain;
using HeliumTodo.Application.Configuration;
using HeliumTodo.Application.Services;
using HeliumTodo.Application.Ipc;
using HeliumTodo.Domain.Lifecycle;
using HeliumTodo.Domain.Networking;
using HeliumTodo.Domain.Services;
using HeliumTodo.Domain.Theming;
using HeliumTodo.Infrastructure;
using HeliumTodo.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HeliumTodo.Application.Abstractions;
using HeliumTodo.Application.Exceptions;

namespace HeliumTodo.Desktop;

/// <summary>
/// 应用程序本体：负责「组装」整个程序。
///
/// 组装顺序（这也是理解这个项目的最佳入口）：
///   1. 读配置              → 知道用户上次设置成什么样
///   2. 建依赖注入容器      → 把各个服务、仓储登记好，谁需要什么自动给
///   3. 准备数据库          → 建表 + 首次运行灌示例数据
///   4. 应用主题            → 把配置里的主题套上去
///   5. 加载插件            → 扫描 plugins 文件夹
///   6. 开窗口
/// </summary>
public partial class App : Avalonia.Application
{
    /// <summary>全局的服务容器（谁需要什么服务，从这里拿）。</summary>
    public static ServiceProvider Services { get; private set; } = null!;

    /// <summary>主窗口引用，退出时要用。</summary>
    private MainWindow? _mainWindow;

    /// <summary>托盘图标（关掉主界面后靠它把窗口叫回来、或真正退出）。</summary>
    private TrayService? _tray;

    /// <summary>后台提醒扫描。</summary>
    private ReminderService? _reminders;

    /// <summary>
    /// 是不是真的要走退出流程。
    ///
    /// 点主窗口的关闭按钮时这个值是 false —— 于是窗口只是藏起来；
    /// 托盘菜单点「退出」才把它置 true，这时候关闭才真的结束进程。
    /// </summary>
    private bool _exitRequested;

    /// <summary>
    /// 单实例守卫：同一时间只允许一份在跑；用户再双击一次程序时，
    /// 由它把"请把窗口显示出来"转发给已经在跑的这一份。
    /// 它同时是托盘失效时的退路 —— 所以"关闭 = 藏到后台"可以无条件执行。
    /// </summary>
    private SingleInstanceGuard? _singleInstance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // ── 注意：这里以前有一行 RemoveDuplicateValidationPlugins()，迁移到 Avalonia 12 时删掉了 ──
        //
        // 那行代码的作用是：把 Avalonia 自带的「用反射找属性」的校验插件从
        // BindingPlugins.DataValidators 里摘掉，因为我们全用编译期绑定，
        // 两种方式同时开会让同一个属性被通知两次。
        //
        // Avalonia 12 做了两件事，让这段代码既编译不过、也没必要存在了：
        //   1. BindingPlugins 这个静态类被删掉了（连同 DataAnnotationsValidationPlugin）；
        //   2. 那些校验插件默认就是不启用的，只在真正需要时才会注册。
        // 所以直接删掉即可，不需要任何替代写法。

        // ── ① 配置 ──
        //
        // HELIUM_CONFIG_DIR 是给自检工具留的口子：
        // 无头渲染 / 备份自检会真的写文件，必须在一个临时目录里做，
        // 否则「测试导入」就等于往用户自己的数据库里灌一遍数据。
        //
        // 平时这个环境变量不存在，走正常的 %AppData% 或便携模式。
        var configDirectoryOverride = Environment.GetEnvironmentVariable("HELIUM_CONFIG_DIR");
        var configService = string.IsNullOrWhiteSpace(configDirectoryOverride)
            ? new ConfigService()
            : new ConfigService(configDirectoryOverride);
        configService.Load();

        // ── ② 主题注册表（插件稍后还能往里加主题） ──
        var themeRegistry = new ThemeRegistry();

        // ── ③ 依赖注入容器 ──
        Services = BuildServiceProvider(configService, themeRegistry);

        // ── ④ 数据库：建表 / 灌示例数据 / 自动备份 ──
        InitializeDatabase(configService);

        // ── ⑤ 应用上次选的主题 ──
        themeRegistry.Apply(configService.Current.ThemeId);

        var themeService = Services.GetRequiredService<ThemeService>();
        var wallpaper = Services.GetRequiredService<WallpaperService>();

        // 壁纸播放过程中每换一帧都会发 Changed。
        // 强调色只跟壁纸的主色调有关（解码时就算好了，不随帧变），
        // 所以重复调用是安全的 —— ThemeService 内部会比对参数，没变就直接返回。
        wallpaper.Changed += (_, _) => themeService.ApplyFor(configService.Current, wallpaper);

        themeService.ApplyFor(configService.Current, wallpaper);

        // 蒙版只有一层：颜色随明暗（亮白 / 暗黑），强度由用户那条滑块决定
        var isDark = themeRegistry.Current.Kind == HeliumTodo.Domain.Theming.ThemeKind.Dark;
        ApplyWallpaperScrim(configService.Current.WallpaperScrimOpacity, isDark);

        // 卡片（功能区）的玻璃浓度
        ApplyCardOpacity(configService.Current.CardOpacity, isDark);
        ApplyCardShadow(isDark);

        // ── ⑤b 加载壁纸（后台解码，不阻塞启动） ──
        if (configService.Current.WallpaperEnabled
            && !string.IsNullOrWhiteSpace(configService.Current.WallpaperPath))
        {
            var wallpaperPath = configService.Current.WallpaperPath;
            var blur = configService.Current.WallpaperBlurRadius;
            var kind = themeRegistry.Current.Kind;

            // 故意不 await：壁纸可能是一张几十帧的动图，等它解完再开窗口
            // 会让启动卡一两秒。先开窗口，壁纸好了自己会显示出来。
            //
            // ⚠️ 这里【不能】包一层 Task.Run：
            //    包了以后 LoadAsync 的后续代码会跑在线程池上，
            //    而它结尾要改界面绑定的属性，那就成了跨线程访问，界面会崩。
            //    直接调用的话，await 之后会回到界面线程（同步上下文），才对。
            _ = LoadWallpaperAsync(wallpaper, wallpaperPath, blur, kind);
        }

        // ── ⑥ 插件：0.5.0-alpha 起暂时关闭（见 DEVELOPMENT_SPEC.md §9） ──
        // 只探测目录是否存在并记一行日志，绝不加载任何外部 dll。
        var pluginDirectory = Path.Combine(configService.ConfigDirectory, "plugins");
        if (Directory.Exists(pluginDirectory))
        {
            LogBackground("[插件] 检测到 plugins 目录，本版本不加载");
        }

        // ── ⑦ 开窗口 ──
        //
        // 注意这个类型判断：
        //   正常运行时是「桌面生命周期」，要开主窗口；
        //   而界面渲染自检（HeliumTodo.Verification）用的是无头模式，
        //   它会自己创建窗口来渲染，这里就不该再开窗口了 ——
        //   不然无头模式下没有桌面，一开窗口就崩。
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ── ⓪ 单实例 ──
            //
            // 这个程序关掉主界面是留在后台跑的，所以"再双击一次程序"是很自然的动作。
            // 如果不拦着，第二次启动会开出第二份进程、第二个数据库连接、第二个托盘图标，
            // 用户会以为程序被开了两遍 —— 而后台那份还占着数据库文件。
            // 这里的做法：第二个实例只负责喊一声"把窗口显示出来"，然后自己退出。
            // app id 默认是 HeliumTodo；自检/探针会用 HELIUM_APP_ID 换成别的名字，
            // 这样同一台机器上能并排拉起互不相干的两份实例（见 AppIdentity）。
            _singleInstance = new SingleInstanceGuard(HeliumTodo.Domain.Lifecycle.AppIdentity.ResolveFromEnvironment());
            if (!_singleInstance.IsFirstInstance)
            {
                _singleInstance.RequestShow();
                LogBackground("[单实例] 已经有一份在跑，已请它把窗口显示出来，本进程退出");

                // 这里**不能**直接 desktop.Shutdown()。
                // 此刻还站在 OnFrameworkInitializationCompleted 里，Avalonia 的消息循环
                // 要到这一句返回之后才由 StartCore 起来；现在把 Dispatcher 关掉，
                // StartCore 再进 MainLoop 就会抛
                //   InvalidOperationException: Cannot perform requested operation because the Dispatcher shut down
                // 进程以 0xE0434352（未处理异常）退出 —— 用户看到的就是"程序是异常结束的"。
                // 所以把"关"排进消息循环：等它转起来再正常收尾，退出码是 0。
                Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());

                base.OnFrameworkInitializationCompleted();
                return;
            }

            _mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<ShellViewModel>(),
            };

            // 并发冲突（两个窗口同时改同一行）是正常业务分支，不该把进程带崩：
            // Infrastructure 把 DbUpdateConcurrencyException 翻译成 ConcurrencyConflictException，
            // 这里在 UI 线程兜底，用壳层警告条告诉用户，然后继续运行。
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                if (e.Exception is ConcurrencyConflictException conflict &&
                    _mainWindow?.DataContext is ShellViewModel shell)
                {
                    shell.ShowAlert(conflict.Message);
                    e.Handled = true;
                }
            };

            desktop.MainWindow = _mainWindow;

            // 数据库没起来时，壳层一进来就把原因说清楚（页面自己也会再报一次）。
            if (DatabaseStartupNotice is { Length: > 0 } startupNotice &&
                _mainWindow.DataContext is ShellViewModel startupShell)
            {
                startupShell.ShowAlert(startupNotice);
            }

            StartBackgroundServices(desktop);

            // 退出时把配置写回磁盘
            desktop.ShutdownRequested += (_, _) =>
            {
                _reminders?.Stop();
                _tray?.Dispose();
                _singleInstance?.Dispose();
                OnExit();
            };

            // 走完 ShutdownRequested 之后才真正退出。把退出码记一行日志：
            // 排查"程序是正常结束还是被外面杀掉的"时，这行是唯一能对得上的证据 ——
            // 0xFFFFFFFF 这类码不是我们返回的（我们的每条退出路径都返回 0）。
            desktop.Exit += (_, e) =>
                LogBackground($"[退出] 进程正常结束，退出码 {e.ApplicationExitCode}");
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 托盘 + 后台提醒 + 「关掉主界面不退出」。
    ///
    /// 这里就是需求里说的那一套生命周期：
    ///   ① 启动时主界面正常显示，同时把托盘图标挂上、提醒开始扫；
    ///   ② 点关闭按钮 → 拦下来，只把窗口藏起来，进程和提醒都还在；
    ///   ③ 托盘菜单点「退出」→ 才真的关窗口、结束进程。
    ///
    /// 注意：托盘起不来（极少数环境）时不要拦关闭按钮，
    /// 否则窗口一关就再也叫不回来了 —— 那样用户只能去任务管理器杀进程。
    /// </summary>
    private void StartBackgroundServices(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_mainWindow is not { } window) return;

        var notifications = Services.GetRequiredService<INotificationService>();

        _reminders = new ReminderService(Services, notifications);
        _reminders.Trace = LogBackground;
        _reminders.Start();
        LogBackground("[提醒] 后台扫描已启动（每 30 秒一次）");

        _tray = new TrayService(
            tooltip: "Helium Todo",
            onShow: () => Avalonia.Threading.Dispatcher.UIThread.Post(() => RestoreMainWindow()),
            onBackup: () => _ = BackupOnceAsync(),
            onExit: () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _exitRequested = true;
                _reminders?.Stop();
                _tray?.Dispose();
                window.Close();
                desktop.Shutdown();
            }));

        LogBackground($"[环境] 会话={System.Diagnostics.Process.GetCurrentProcess().SessionId}"
                      + $" 管理员={IsElevated()}"
                      + $" 资源管理器={DescribeExplorer()}"
                      + $" 渲染后端={Application.Configuration.RenderBackendSettings.DisplayName(HeliumTodo.Desktop.Services.RenderBackendChoice.ActiveKey)}"
                      + $"{(_hasEnvironmentOverride ? "（环境变量指定）" : string.Empty)}");

        _tray.Trace = LogBackground;

        // 自检开关：这台机器上没有可交互的通知区域（无头会话），
        // 托盘图标加不上，于是"关窗口变后台"这条链路没法用真的托盘验证。
        // 置 HELIUM_FAKE_TRAY=1 就当作托盘已经起来了 —— 只影响验证，不影响正常使用。
        var fakeTray = Environment.GetEnvironmentVariable("HELIUM_FAKE_TRAY") == "1";

        var trayReady = fakeTray || _tray.Start();
        if (!trayReady)
        {
            // 托盘起不来（错误码 5 之类）时不再"放弃后台"，只记一笔：
            // 窗口照样藏，用户再双击一次程序就能叫回来 —— 那条路不经过托盘。
            LogBackground("[托盘] 启动失败：没有托盘图标，但关闭仍会留在后台"
                          + "（再双击一次程序即可唤回窗口）");
            _tray = null;
        }
        else
        {
            LogBackground(fakeTray
                ? "[托盘] 自检模式：假装托盘已就绪（没有真的图标）"
                : "[托盘] 已启动，关闭按钮改为藏窗口");
        }

        // 第二位启动的进程会往这个事件里塞一个信号，我们就把它转成"显示窗口"
        _singleInstance?.Listen(() =>
            Avalonia.Threading.Dispatcher.UIThread.Post(RestoreMainWindow));

        // 关闭一律只藏窗口，进程和提醒都继续跑。
        // 能不能藏得回来由两条路保证：托盘图标、以及"再双击一次程序"（单实例守卫）。
        // 所以这里不需要再判断托盘是否成功 —— 判断反而会让"托盘恰好失败"变成"后台丢不掉"。
        window.Closing += (_, e) =>
        {
            if (_exitRequested) return;

            e.Cancel = true;
            window.FinishAnimationsImmediately();
            window.Hide();

            // 窗口藏起来以后没人看得到壁纸，但动图的定时器不会自己停。
            // 不停的话，后台会一直按帧解码 + 上传纹理 —— 一个只在托盘里待着的
            // 程序却在烧 CPU，这跟"后台常驻"的初衷正好相反。
            Services.GetRequiredService<WallpaperService>().Stop();
            LogBackground(trayReady
                ? "[窗口] 关闭按钮被拦截，已隐藏到托盘（进程继续运行）"
                : "[窗口] 关闭按钮被拦截，已隐藏（没有托盘图标，再双击程序可唤回）");
        };
    }

    /// <summary>
    /// 后台服务的诊断日志。
    ///
    /// 为什么写文件：程序是 WinExe（没有控制台），而这些事发生在
    /// "窗口已经关掉"之后 —— 出问题时除了日志没有任何地方能看。
    /// 日志落到配置目录里，跟数据库、备份放在一起，用户找得到。
    /// </summary>
    /// <summary>
    /// 当前进程是不是以管理员身份跑的。
    /// 这个信息对"托盘图标不显示""打不开资源管理器"很关键：
    /// 提权进程和资源管理器不在同一个完整性级别，某些外壳交互会被系统挡掉。
    /// </summary>
    private static bool IsElevated()
    {
        // WindowsIdentity 只在 Windows 上有；虽然本程序是 Windows 桌面程序，
        // 但项目 TFM 是 net10.0，分析器要求显式判断一次（否则报 CA1416）。
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>资源管理器跑没跑、在哪个会话 —— 托盘图标最终是它画的。</summary>
    private static string DescribeExplorer()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("explorer");
            if (processes.Length == 0)
            {
                return "没有运行";
            }

            var sessions = processes.Select(p => p.SessionId).Distinct().OrderBy(id => id);
            return $"PID {string.Join(',', processes.Select(p => p.Id))} 会话 {string.Join(',', sessions)}";
        }
        catch
        {
            return "查不到";
        }
    }

    /// <summary>这次的后端是不是环境变量硬点的（环境变量优先于设置页，界面上要提示一句）。</summary>
    private static bool _hasEnvironmentOverride =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HELIUM_RENDER"));

    internal static void LogBackground(string message)
    {
        try
        {
            var directory = Services?.GetService(typeof(ConfigService)) is ConfigService config
                ? config.ConfigDirectory
                : AppContext.BaseDirectory;

            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "background.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志写不进去也不能影响功能
        }
    }

    /// <summary>从托盘把主界面叫回来。</summary>
    private void RestoreMainWindow()
    {
        if (_mainWindow is not { } window) return;

        // 先打招呼：这次恢复的动画由下面显式播，窗口自己别再播一遍。
        window.BeginTrayRestore();

        window.Show();
        if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
        {
            window.WindowState = Avalonia.Controls.WindowState.Normal;
        }

        window.Activate();

        // 窗口回来了，动图接着放（静态图调用没有副作用）。
        Services.GetRequiredService<WallpaperService>().Start();

        // Hide/Show 不会触发 Opened，所以这里的出现动画要自己叫一下，
        // 否则"从托盘回来"是硬生生蹦出来的，和第一次打开的手感不一致。
        window.PlayShowAnimation();
    }

    /// <summary>
    /// 托盘菜单上的「立即备份一次」。
    ///
    /// 这就是「后台进程处理必要的数据」里最值得做的一件：
    /// 用户可能很久不打开窗口，但数据每天在变。
    /// </summary>
    private async Task BackupOnceAsync()
    {
        try
        {
            var backup = Services.GetRequiredService<DataBackupService>();
            var path = await Task.Run(() => backup.BackupAutomatically(keepCount: 7));
            System.Diagnostics.Debug.WriteLine(path is null
                ? "[备份] 这次没做成"
                : $"[备份] 已生成 {path}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[备份] 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 把「蒙版不透明度」写进应用资源。
    ///
    /// 为什么用资源而不是直接绑到 ViewModel 属性上？
    ///   因为这个值改变时只需要界面重画，不涉及任何业务逻辑；
    ///   走资源的话，设置页改完立刻生效，不用再想办法通知主窗口。
    ///   强调色也是同样的做法（见 ThemeService）。
    /// </summary>
    public static void ApplyWallpaperScrim(int percent, bool isDark)
    {
        if (Avalonia.Application.Current?.Resources is not { } resources) return;

        // 强度换算（暗色 ×1.6、上限 0.95）在 Core 里，单测锁着；
        // 这里只负责把结果和颜色写进资源。
        resources["WallpaperScrimOpacity"] =
            HeliumTodo.Domain.Theming.WallpaperScrim.ComputeStrength(percent, isDark);

        var hex = HeliumTodo.Domain.Theming.WallpaperScrim.ColorHex(isDark);
        resources["WallpaperScrimBrush"] =
            new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));
    }

    /// <summary>
    /// 把「功能区不透明度」写进应用资源（`CardSurfaceBrush`）。
    ///
    /// 【为什么不直接绑 ViewModel 属性？】
    ///   卡片背景是 `Border.card` 样式里的一个画刷，被几十个控件共用。
    ///   让每个用到它的控件都去绑一次 ViewModel 不现实；
    ///   写进应用资源，样式里的 `DynamicResource` 会自己跟着变。
    ///
    /// 【为什么基础色要跟着明暗走？】
    ///   亮色下"半透明"= 白里透出壁纸；暗色下必须是深灰透出壁纸。
    ///   暗色还用白色的话，卡片会变成一块发光的磨砂玻璃，字反而看不清。
    /// </summary>
    public static void ApplyCardOpacity(int percent, bool isDark)
    {
        if (Avalonia.Application.Current?.Resources is not { } resources) return;

        var alpha = (byte)Math.Round(Math.Clamp(percent, 0, 100) * 255 / 100.0);
        var baseColor = isDark
            ? Avalonia.Media.Color.FromRgb(0x1C, 0x1C, 0x1C)
            : Avalonia.Media.Colors.White;

        resources["CardSurfaceBrush"] = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }

    /// <summary>卡片阴影当前是不是跟着暗色在走（压制结束后要按这个恢复）。</summary>
    private static bool _cardShadowIsDark;

    /// <summary>阴影是否被临时压制（窗口动画 / 拖拽期间）。</summary>
    private static bool _cardShadowSuppressed;

    /// <summary>
    /// 按明暗写卡片阴影（`CardShadow`）。
    ///
    /// 为什么阴影也要走代码、而不是留在主题字典里：
    ///   窗口动画和拖边改大小的那 180 毫秒里要把它临时换成"没有阴影"
    ///   （软件渲染下阴影是整片半透明模糊，实测每帧要吃十几毫秒，见 WindowResizeGrips）。
    ///   而主题字典里的同名 key 优先级高于应用级资源，代码赋值会静默失效 ——
    ///   这个坑在 `CardSurfaceBrush` 上已经踩过一次，所以这里一律走应用级资源。
    /// </summary>
    public static void ApplyCardShadow(bool isDark)
    {
        _cardShadowIsDark = isDark;

        if (_cardShadowSuppressed) return;

        WriteCardShadow(isDark ? DarkCardShadow : LightCardShadow);
    }

    /// <summary>
    /// 压制 / 恢复卡片阴影。窗口正在动的时候压制，动完恢复。
    /// 用引用计数的语义由调用方（InteractionQuality）保证，这里只做开关。
    /// </summary>
    public static void SetCardShadowSuppressed(bool suppressed)
    {
        _cardShadowSuppressed = suppressed;
        WriteCardShadow(suppressed ? default : (_cardShadowIsDark ? DarkCardShadow : LightCardShadow));
    }

    private static readonly Avalonia.Media.BoxShadows LightCardShadow =
        Avalonia.Media.BoxShadows.Parse("0 8 24 0 #14000000");

    private static readonly Avalonia.Media.BoxShadows DarkCardShadow =
        Avalonia.Media.BoxShadows.Parse("0 8 24 0 #40000000");

    private static void WriteCardShadow(Avalonia.Media.BoxShadows shadow)
    {
        if (Avalonia.Application.Current?.Resources is { } resources)
        {
            resources["CardShadow"] = shadow;
        }
    }

    /// <summary>
    /// 加载壁纸的独立方法，只为把异常吞掉（壁纸坏了不该影响程序启动）。
    /// </summary>
    private static async Task LoadWallpaperAsync(
        WallpaperService wallpaper,
        string path,
        int blurRadius,
        HeliumTodo.Domain.Theming.ThemeKind kind)
    {
        try
        {
            if (!await wallpaper.LoadAsync(path, blurRadius, kind))
            {
                System.Diagnostics.Debug.WriteLine($"[壁纸] 加载失败：{wallpaper.Error}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[壁纸] 加载异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 建依赖注入容器。
    ///
    /// 这里就是「易扩展」的另一个体现：
    ///   想换数据库实现？改 AddHeliumData 那一行。
    ///   想换网络实现（联机自习）？改 IPeerTransport 那一行。
    ///   其它代码全都不用动。
    /// </summary>
    private static ServiceProvider BuildServiceProvider(
        ConfigService configService,
        ThemeRegistry themeRegistry)
    {
        var services = new ServiceCollection();

        // ── 日志（输出到「调试 → 输出」窗口） ──
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // ── 核心服务 ──
        services.AddSingleton(configService);
        services.AddSingleton(themeRegistry);
        services.AddSingleton<ThemeService>();
        services.AddSingleton<FocusTimerService>();

        // 壁纸服务：负责解码后的播放与"当前该显示哪一帧"
        services.AddSingleton<WallpaperService>();

        // 自习室的网络层：现在是「不联网」的占位实现。
        // 【暂缓】以后实现无中心服务器联机时，只要把下面这行换成真正的 P2P 实现即可。
        // 联机传输层：局域网点对点（无中心服务器）。
        // 想换回纯本地（比如调试界面时不想开端口），把这行换成 LocalOnlyPeerTransport 即可 ——
        // 界面和业务层都只依赖 IPeerTransport，换实现不用改其它代码。
        services.AddSingleton<IPeerTransport>(sp =>
        {
            var config = sp.GetRequiredService<ConfigService>();

            return new LanPeerTransport(PeerEndpoint.DefaultPort, config.Current.DisplayName);
        });
        services.AddSingleton<StudyRoomService>();

        // ── 数据库（PostgreSQL）──
        var connectionString = PostgresConnection.Resolve(configService.GetStorageSettings());
        services.AddHeliumData(connectionString, enableSensitiveDataLogging: false);

        // ── 仓储 ──
        services.AddSingleton<ScheduleRepository>();
        services.AddSingleton<TodoRepository>();
        services.AddSingleton<FocusRepository>();
        services.AddSingleton<StudyRoomRepository>();

        // ── 备份 / 恢复（统一走 .zip） ──
        // 启动时的自动备份和设置页里手动导出用的是同一个服务、同一种格式。
        services.AddSingleton<DataBackupService>();

        // 打开文件夹优先交给 Avalonia 的 Launcher（系统外壳），失败再退回自己起 explorer.exe
        services.AddSingleton<IFolderLauncher, AvaloniaFolderLauncher>();

        // 系统通知（Windows 上落到通知中心）。没有托盘时自动降级成"什么都不做"
        services.AddSingleton<INotificationService>(sp =>
            new WindowsToastNotifier(sp.GetService<TrayService>()));

        // 进程间通信：现在只注册一个空实现，契约在 Core 层（见 Ipc/）
        services.AddSingleton<IIpcChannel, NullIpcChannel>();

        // ── 界面状态与主窗口 ──
        services.AddSingleton<AppState>();
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>迁移到最新、灌示例数据、启动时自动备份（.zip）。</summary>
    /// <summary>数据库没起来时留给壳层的提示；null 表示一切正常。</summary>
    internal static string? DatabaseStartupNotice { get; private set; }

    private static void InitializeDatabase(ConfigService configService)
    {
        try
        {
            var storage = configService.GetStorageSettings();
            var masked = PostgresConnection.Mask(PostgresConnection.Resolve(storage));

            // 先做 TCP 预检：连不上就别让 EF 的重试策略白等 6 秒。
            var probe = PostgresConnection.Probe(storage);
            if (probe is not null)
            {
                DatabaseStartupNotice = DatabaseErrorText.DescribeProbeFailure(probe, masked);
                Console.Error.WriteLine($"[启动] 数据库预检失败：{probe}（连接：{masked}）");
                return;
            }

            var factory = Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<HeliumDbContext>>();
            using var db = factory.CreateDbContext();

            // 先迁移再备份：旧结构下用新模型读表会直接报「列不存在」，
            // 备份放在迁移后至少能拿到一份结构正确的快照。
            var seeded = DatabaseInitializer.Initialize(db, seedIfEmpty: true);

            System.Diagnostics.Debug.WriteLine(seeded
                ? "[启动] 首次运行，已写入示例数据"
                : "[启动] 数据库就绪");

            // 启动时备份一份（.zip，保留最近 7 份），万一数据被改坏还能找回来。
            // 备份失败（磁盘满、临时目录不让写……）不该影响使用。
            if (configService.Current.AutoBackup)
            {
                try
                {
                    var backup = Services.GetRequiredService<DataBackupService>().BackupAutomatically();
                    if (backup is not null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[启动] 已自动备份：{backup}");
                    }
                }
                catch (Exception backupException)
                {
                    System.Diagnostics.Debug.WriteLine($"[启动] 自动备份失败（不影响使用）：{backupException.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // 数据库连不上也要让程序能启动，界面会显示空数据
            System.Diagnostics.Debug.WriteLine($"[启动] 数据库初始化失败：{ex}");

            var masked = PostgresConnection.Mask(
                PostgresConnection.Resolve(configService.GetStorageSettings()));
            DatabaseStartupNotice = DatabaseErrorText.Describe(ex, masked);
            Console.Error.WriteLine($"[启动] 数据库初始化失败：{ex.Message}（连接：{masked}）");
        }
    }


    /// <summary>
    /// 重启程序（切换渲染后端后调用）。返回结果见 <see cref="RestartOutcome"/>。
    ///
    /// 第一件事是判断"是不是在调试器里跑"：调试器有自己的子进程，
    /// 我们从里面再起一个，调试器就管不到新进程了 —— 新旧两个进程一起抓着
    /// <c>bin\Debug\net10.0\HeliumTodo.Desktop.exe</c>，下一次编译直接
    /// <c>MSB3027 文件正由另一进程使用</c>。所以调试器在场时一律不自己重启，
    /// 交给调试器的「重新启动」（Ctrl+Shift+F5）。
    ///
    /// 顺序很讲究，换一下就会出问题：
    ///   ① 先 <c>_exitRequested = true</c> —— 否则关窗口会被"藏到后台"那条逻辑拦住，
    ///      用户会看到窗口消失又出现，而进程根本没退；
    ///   ② 再放掉单实例锁 —— 新进程启动时会先抢这把锁，锁还在我们手里的话，
    ///      它会认为自己"不是第一个"，于是只喊一声"显示窗口"就自己退出了，
    ///      表现就是"点了重启，界面闪一下，后端还是旧的"；
    ///   ③ 然后才启动新进程，最后关掉自己。
    ///
    /// 为什么用 <c>UseShellExecute = true</c>：走 shell 启动更接近"用户双击图标"，
    /// 将来的 AppUserModelID / 通知身份等也会更一致。
    /// </summary>
    public RestartOutcome TryRestart()
    {
        // 自检里没法真的挂调试器，用 HELIUM_FAKE_DEBUGGER=1 模拟"调试器在场"。
        var debuggerAttached = System.Diagnostics.Debugger.IsAttached
            || Environment.GetEnvironmentVariable("HELIUM_FAKE_DEBUGGER") == "1";

        var decision = RestartPolicy.Decide(debuggerAttached);
        if (decision == RestartOutcome.BlockedByDebugger)
        {
            LogBackground("[重启] 检测到调试器，放弃自动重启："
                          + "请用调试器的「重新启动」(Ctrl+Shift+F5)，"
                          + "否则新进程不受调试器管理，还会占住 exe 让下次编译失败");
            return decision;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            LogBackground("[重启] 拿不到当前可执行文件路径，放弃自动重启");
            return RestartOutcome.Failed;
        }

        _exitRequested = true;

        // ② 放锁（Dispose 里会 ReleaseMutex）
        try
        {
            _singleInstance?.Dispose();
        }
        catch (Exception ex)
        {
            LogBackground($"[重启] 释放单实例锁失败（不影响继续重启）：{ex.Message}");
        }
        finally
        {
            _singleInstance = null;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
            LogBackground($"[重启] 已启动新进程：{exePath}");
        }
        catch (Exception ex)
        {
            LogBackground($"[重启] 启动新进程失败：{ex.Message}");
            // 锁已经放掉了，窗口还能用，只是"后台常驻"这条链会退化成可以再开一个实例
            return RestartOutcome.Failed;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }

        return RestartOutcome.Started;
    }

    /// <summary>
    /// 程序退出时的收尾：把配置写回磁盘。
    /// （插件的关闭通知在 <see cref="OnFrameworkInitializationCompleted"/> 里
    /// 通过 desktop.ShutdownRequested 事件处理，那里更早、更可靠。）
    /// </summary>
    public void OnExit()
    {
        try
        {
            Services?.GetService<ConfigService>()?.Save();
        }
        catch
        {
            // 退出时出错就别弹窗了
        }
    }
}
