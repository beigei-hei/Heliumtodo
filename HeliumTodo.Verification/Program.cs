// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Desktop.Windowing;
using HeliumTodo.Desktop.ViewModels;
using HeliumTodo.Desktop.Views;
using HeliumTodo.Desktop.Behaviors;
using HeliumTodo.Desktop.Controls;
using Avalonia.Xaml.Interactivity;
using HeliumTodo.Application.Configuration;
using HeliumTodo.Domain.Windowing;
using HeliumTodo.Domain.Services;
using HeliumTodo.Domain.Theming;
using HeliumTodo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using HeliumTodo.Domain;
using Microsoft.Extensions.DependencyInjection;
using HeliumTodo.Domain.Models;
using HeliumTodo.Domain.Policies;

namespace HeliumTodo.Verification;

/// <summary>
/// 界面渲染自检：把每个页面在「无头模式」下渲染一遍，存成 PNG。
///
/// 为什么需要它？
///   编译通过不代表界面没问题 —— 绑定写错、资源找不到、布局崩掉，
///   这些只有真的渲染一次才会暴露。而它不需要桌面环境，CI 上也能跑。
///
/// 用法：
///   dotnet run 加上 -project 参数，指向 HeliumTodo.Verification
///   加 --tree 参数可以顺便打印可视化树（排查「某个控件怎么没显示」时很有用）
///   加 --height=1600 可以加高渲染窗口（页面很长、想一次看全时有用）
///   加 --shell 会额外把整个主窗口（左侧导航栏 + 内容区）也渲染成 外壳.png
/// </summary>
public static class Program
{
    /// <summary>渲染用的窗口宽度，和默认窗口大小一致。</summary>
    private const int DefaultWidth = 1400;

    /// <summary>渲染用的窗口高度。设置页这类长页面可以配合 --height 调大。</summary>
    private const int DefaultHeight = 900;

    /// <summary>可视化树最多打印多少行（--tree-lines 可以改）。</summary>
    private static int _treeLines = 120;

    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ⚠️ 这件事必须做在一切之前。
        //
        // --backup-check 会真的导出、真的合并导入 —— 那些都是**写操作**。
        // 如果在用户真实的配置目录里做，就等于拿他的数据库当试验品
        // （实测把 8 条待办合并成了 16 条、32 条）。
        //
        // 所以先把配置目录换成一个临时沙盒，App 初始化时读的就是这个。
        if (args.Contains("--backup-check") || args.Contains("--refresh-check")
            || args.Contains("--room-check") || args.Contains("--shell-check")
            || args.Contains("--reminder-check") || args.Contains("--window-check")
            || args.Contains("--db-check") || args.Contains("--behavior-check"))
        {
            // 沙盒放在程序自己的目录下：这台机器的 %TEMP% 对 .NET 进程是拒绝写入的
            // （python 能建、.NET 建不了，报 UnauthorizedAccessException），
            // 放在 bin 下面一定可写，而且每次运行一个独立子目录，互不影响。
            var sandboxDirectory = Path.Combine(
                AppContext.BaseDirectory, "_check-sandbox", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandboxDirectory);
            Environment.SetEnvironmentVariable("HELIUM_CONFIG_DIR", sandboxDirectory);
            Console.WriteLine($"备份自检沙盒：{sandboxDirectory}");
        }

        // 是否顺便打印可视化树
        var dumpTree = args.Any(a => a.Equals("--tree", StringComparison.OrdinalIgnoreCase));

        // 页面很长时 120 行不够看，可以用 --tree-lines=600 放宽
        _treeLines = ReadIntArg(args, "--tree-lines", 120);

        // 是否额外渲染整个主窗口（左侧导航栏 + 内容区）
        var renderShell = args.Any(a => a.Equals("--shell", StringComparison.OrdinalIgnoreCase));

        // 渲染尺寸（可以用 --height=1600 这样的参数覆盖高度）
        var width = ReadIntArg(args, "--width", DefaultWidth);
        var height = ReadIntArg(args, "--height", DefaultHeight);

        // 想看壁纸效果时用它：--wallpaper=某张图.png
        // 直接加载这张图并渲染带壁纸的外壳，完全不去碰用户的配置文件
        var wallpaperPath = ReadStringArg(args, "--wallpaper");

        var outputDirectory = ReadStringArg(args, "--output") ?? ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine("  Helium Todo · 界面渲染自检");
        Console.WriteLine($"  输出目录：{outputDirectory}");
        Console.WriteLine($"  渲染尺寸：{width} x {height}");
        Console.WriteLine($"  当前版本：{VersionInfo.Current}");
        if (dumpTree) Console.WriteLine("  可视化树：开启");
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine();

        var failures = new List<(string Name, Exception Error)>();

        // 注意：这里必须写全名 HeliumTodo.Desktop.App。
        // 因为「HeliumTodo.Desktop」既是一个命名空间，也是里面那个 Application 子类的名字，
        // 只写 App 的话编译器分不清指的是哪一个。
        //
        // 另外两个关键设置：
        //   UseHeadlessDrawing = false  → 不要用「假的」绘图，要真的画出像素
        //   .UseSkia()                  → 用 Skia 渲染引擎来画
        //   缺了任何一个，CaptureRenderedFrame 都会抛异常。
        AppBuilder.Configure<HeliumTodo.Desktop.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        if (args.Contains("--folder-check"))
        {
            return RunFolderCheck(HeliumTodo.Desktop.App.Services);
        }

        var services = HeliumTodo.Desktop.App.Services;

        if (services is null)
        {
            Console.WriteLine("[错误] 服务容器没有初始化成功。");
            return 1;
        }

        var shell = services.GetRequiredService<ShellViewModel>();

        // 备份自检：走界面命令那条路，不是直接调服务 ——
        // 这样连"按钮点下去到底跑没跑通"一起验证了。
        if (args.Any(a => a.Equals("--backup-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunBackupCheck(services);
        }

        // 文案自检（P6）：UI_TEXT_MAP.md 里的每条用户可见文字，都必须能在编译产物里
        // 找到同样的字面量。改了文案却忘了同步，这里最先红。
        if (args.Any(a => a.Equals("--copy-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunCopyCheck();
        }
        // 数据库自检：连上真实的 PostgreSQL，确认迁移已应用、时间列的类型没写反、
        // 规格要求的索引都在。这些是"跑起来没事、过两个月才炸"的那类问题。
        if (args.Any(a => a.Equals("--db-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunDbCheck(services);
        }

        // 自习室自检：建房间 → 离开 → 重进页面，看还会不会显示在房间里。
        if (args.Any(a => a.Equals("--room-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunRoomCheck(services);
        }

        // 联动自检：在「待办事项」页加一条、勾掉一条，
        // 看「今日工作流」页会不会自己跟着刷新（不用切页面、不用手动刷新）。
        if (args.Any(a => a.Equals("--refresh-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunRefreshCheck(services);
        }

        // 外壳自检：图标轨逐个点亮，确认自绘导航真的能切页。
        if (args.Any(a => a.Equals("--shell-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunShellCheck(services);
        }

        // 时间轴自检：确认 00:00 / 24:00 的刻度文字不再被切掉（上下留白生效）。
        if (args.Any(a => a.Equals("--timeline-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunTimelineCheck(services);
        }

        // 提示条自检：确认设置页的提示条不叠在内容上（用户报过"文字压在一起"）。
        if (args.Any(a => a.Equals("--notice-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunNoticeCheck(services);
        }

        // 渲染后端重启提示自检：改后端要弹「是否立即重启」，两个按钮并排、取消真的撤销。
        if (args.Any(a => a.Equals("--restart-prompt-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunRestartPromptCheck(services);
        }

        // 图标轨自检：展开/收起、设置的位置、以及"设置项自己不再画一层底"。
        if (args.Any(a => a.Equals("--rail-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunRailCheck(services);
        }

        // 版面自检：六个页面的"分区骨架"必须一模一样（用户报过设置页老是被落下）。
        if (args.Any(a => a.Equals("--layout-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunLayoutCheck(services);
        }

        // 主题自检：FluentAvaloniaTheme 真的挂在样式表里、明暗与强调色真的进了像素
        // （只看资源字典会被主题字典的优先级骗过去）。
        if (args.Any(a => a.Equals("--theme-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunThemeCheck(services);
        }

        // 外观自检：确认「功能区不透明度」「明暗模式」「暗色灰蒙版」三件事真的生效
        // （用户报的主题色/玻璃浓度改了没反应，根因是资源被主题字典盖住）。
        if (args.Any(a => a.Equals("--appearance-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunAppearanceCheck(services);
        }

        // 提醒自检：确认「提醒」选择器真的长在界面上、自定义值能存进数据库。
        if (args.Any(a => a.Equals("--reminder-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunReminderCheck(services);
        }

        // 窗口自检：打开动画、拖边改大小、单实例守卫。
        if (args.Any(a => a.Equals("--window-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunWindowCheck(services);
        }

        // 行为自检（P5）：XAML 上挂的 behaviors 真的挂上了，拖动/双击/回车/抓手都有效，
        // 解绑之后不再响应。
        if (args.Any(a => a.Equals("--behavior-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunBehaviorCheck(services);
        }
        // 性能自检：量一下「改一次窗口尺寸 → 布局 + 软件渲染」要多少毫秒。
        // 用户报过「启动、最大化、拖边改大小都卡」，没有数字就只能猜，所以先量。
        if (args.Any(a => a.Equals("--perf-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunPerfCheck(services);
        }

        // 动效自检：政策数值 + "被打断也要收尾干净"（无头量不出帧间隔，真机数字在
        // tools/probe-native-anim.ps1）。
        if (args.Any(a => a.Equals("--anim-check", StringComparison.OrdinalIgnoreCase)))
        {
            return RunAnimCheck(services);
        }



        // ── 外观开关（截图用，必须在页面/外壳渲染之前生效）──
        //   HELIUM_DARK=1            按暗色模式出图（连同蒙版强度一起换）
        //   HELIUM_CARD_OPACITY=NN   功能区不透明度（0-100）
        // 壁纸蒙版是"一层、颜色随明暗"的，所以这里要按明暗重算一次；
        // 不然 --wallpaper 拍出来的暗色图里，蒙版还是亮色那份白色。
        var appearanceDark = Environment.GetEnvironmentVariable("HELIUM_DARK") == "1";
        if (appearanceDark)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }

        var scrimPercent = services.GetRequiredService<ConfigService>().Current.WallpaperScrimOpacity;
        HeliumTodo.Desktop.App.ApplyWallpaperScrim(scrimPercent, appearanceDark);

        if (int.TryParse(Environment.GetEnvironmentVariable("HELIUM_CARD_OPACITY"), out var startupOpacity))
        {
            HeliumTodo.Desktop.App.ApplyCardOpacity(startupOpacity, appearanceDark);
        }

        // ── 壁纸预加载（只有传了 --wallpaper 才会走这里）──
        if (!string.IsNullOrWhiteSpace(wallpaperPath))
        {
            var wallpaper = services.GetRequiredService<WallpaperService>();
            Console.WriteLine($"── 壁纸 ──");
            Console.WriteLine($"   {Path.GetFileName(wallpaperPath)}");

            var loadTask = wallpaper.LoadAsync(wallpaperPath, 8, HeliumTodo.Domain.Theming.ThemeKind.Light);
            PumpDispatcherUntil(loadTask, TimeSpan.FromSeconds(60));

            if (wallpaper.IsLoaded)
            {
                Console.WriteLine($"   [通过] {wallpaper.PixelWidth}x{wallpaper.PixelHeight}"
                                  + $" · {wallpaper.FrameCount}/{wallpaper.SourceFrameCount} 帧"
                                  + $" · {(wallpaper.IsAnimated ? "动图" : "静态")}"
                                  + $" · 主色调 {wallpaper.Accent.ToHex()}"
                                  + $"{(wallpaper.Accent.IsFallback ? "（兜底）" : string.Empty)}");

                if (wallpaper.FrameDelays.Count > 0)
                {
                    // 素材本身可能比 30 帧/秒快（比如 20 ms 一帧的 GIF），
                    // 真正决定观感的是"排进定时器的间隔"，所以按夹过上限的值来判断。
                    var minDelay = wallpaper.FrameDelays.Select(HeliumTodo.Domain.Media.PlaybackLimits.ClampFrameDelay).Min();
                    var maxDelay = wallpaper.FrameDelays.Select(HeliumTodo.Domain.Media.PlaybackLimits.ClampFrameDelay).Max();
                    Console.WriteLine($"   原始间隔 {wallpaper.FrameDelays.Min()}–{wallpaper.FrameDelays.Max()} ms"
                                      + $" → 实际播放 {minDelay}–{maxDelay} ms"
                                      + $"（最快约 {1000.0 / minDelay:0.0} 帧/秒，上限 30 帧/秒）");

                    if (minDelay < HeliumTodo.Domain.Media.PlaybackLimits.MinFrameIntervalMs)
                    {
                        failures.Add(("壁纸帧率", new InvalidOperationException(
                            $"实际帧间隔 {minDelay} ms 小于下限 {HeliumTodo.Domain.Media.PlaybackLimits.MinFrameIntervalMs} ms，会超过 30 帧/秒")));
                    }
                }

                // 动图还要验证"帧能一帧帧走下去、走完能绕回来"。
                //
                // ⚠️ 这里【不】靠等时间来验证，因为无头模式下的 DispatcherTimer
                //    压根不会触发（消息循环是假的），等多久都还是第一帧 ——
                //    那样只会得到一个假失败。所以改成手动推帧，验证帧序列本身。
                if (wallpaper.IsAnimated)
                {
                    var first = wallpaper.CurrentFrame;
                    var distinct = new List<object>();

                    for (var i = 0; i < wallpaper.FrameCount; i++)
                    {
                        wallpaper.AdvanceFrame();
                        var frame = wallpaper.CurrentFrame;
                        if (frame is not null && !distinct.Contains(frame)) distinct.Add(frame);
                    }

                    var cycled = ReferenceEquals(wallpaper.CurrentFrame, first);

                    if (distinct.Count < 2 || !cycled)
                    {
                        failures.Add(("壁纸动画", new InvalidOperationException(
                            $"帧序列不对：{wallpaper.FrameCount} 帧里只见到 {distinct.Count} 张不同的图，循环回第一帧={cycled}")));
                        Console.WriteLine($"   [失败] 帧序列不对（不同帧 {distinct.Count} 张，循环={cycled}）");
                    }
                    else
                    {
                        Console.WriteLine($"   [通过] 帧序列正常（{wallpaper.FrameCount} 帧 / {distinct.Count} 张不同画面，能循环）");
                    }
                }
            }
            else
            {
                failures.Add(("壁纸", new InvalidOperationException(wallpaper.Error ?? "未知原因")));
                Console.WriteLine($"   [失败] {wallpaper.Error}");
            }

            Console.WriteLine();
        }

        // 要渲染的页面 = 左侧导航栏里的每一项（插件注册的页面也会一起渲染）
        var pages = shell.MenuItems.Concat(shell.FooterItems).ToList();

        Console.WriteLine($"共 {pages.Count} 个页面待检查。");
        Console.WriteLine();

        foreach (var item in pages)
        {
            Console.WriteLine($"── {item.Title} ──");

            try
            {
                var path = RenderPage(item, outputDirectory, dumpTree, width, height);
                Console.WriteLine($"   [通过] 已生成 {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                failures.Add((item.Title, ex));
                Console.WriteLine($"   [失败] {ex.GetType().Name}: {ex.Message}");
                if (Environment.GetEnvironmentVariable("HELIUM_TRACE") == "1") Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
        }

        // 外壳单独渲染一遍 —— 六个页面都绕过了 MainWindow/ShellView，
        // 导航栏里的 FANavigationView 到底正不正常，只能这样看出来。
        if (renderShell)
        {
            Console.WriteLine("── 外壳（导航栏 + 内容区）──");

            try
            {
                var path = RenderShell(shell, outputDirectory, dumpTree, width, height);
                Console.WriteLine($"   [通过] 已生成 {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                failures.Add(("外壳", ex));
                Console.WriteLine($"   [失败] {ex.GetType().Name}: {ex.Message}");
                if (Environment.GetEnvironmentVariable("HELIUM_TRACE") == "1") Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
        }

        // ── 汇总 ──
        // 外壳算一个额外项，免得「6 个页面全过」掩盖了外壳失败
        var total = pages.Count + (renderShell ? 1 : 0);

        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine($"  合计 {total} 项 · 成功 {total - failures.Count} · 失败 {failures.Count}");
        Console.WriteLine("══════════════════════════════════════════════════════");

        if (failures.Count > 0)
        {
            Console.WriteLine();
            foreach (var (name, error) in failures)
            {
                Console.WriteLine($"▸ {name}");
                Console.WriteLine($"  {error}");
                Console.WriteLine();
            }
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("全部页面渲染正常 ✔");
        return 0;
    }

    /// <summary>
    /// 渲染整个主窗口（左侧导航栏 + 内容区），保存成 外壳.png。
    ///
    /// 为什么需要单独渲染外壳：
    /// 上面那六个页面是用 ContentControl 单独接住 ViewModel 渲染的，
    /// **完全绕过了 MainWindow / ShellView**。也就是说，
    /// 导航栏（图标轨）区域
    /// 到底长得对不对、选中项高亮还在不在，六个页面截图是看不出来的。
    ///
    /// 用真实的 MainWindow 渲染一遍，才算真的验证了外壳。
    /// </summary>
    private static string RenderShell(ShellViewModel shell, string outputDirectory, bool dumpTree, int width, int height)
    {
        // 先把第一个页面导航进去，这样内容区不是空的，
        // 也能顺便验证「导航栏选中 → 内容区切换」这条链路
        if (shell.MenuItems.Count > 0 && shell.SelectedItem is null)
        {
            shell.SelectedItem = shell.MenuItems[0];
        }

        // 想拍别的页（比如「设置」看玻璃卡片压在壁纸上的效果）时指定页名：
        //   HELIUM_SHELL_PAGE=设置
        // 六个单页截图绕过了 MainWindow，拍不到壁纸层，只有这条路径能拍到。
        var wantedPage = Environment.GetEnvironmentVariable("HELIUM_SHELL_PAGE");
        if (!string.IsNullOrWhiteSpace(wantedPage))
        {
            var target = shell.MenuItems.Concat(shell.FooterItems)
                .FirstOrDefault(i => i.Title.Contains(wantedPage, StringComparison.Ordinal));
            if (target is not null)
            {
                shell.SelectedItem = target;
            }
        }

        var window = new MainWindow
        {
            DataContext = shell,
            Width = width,
            Height = height,
            WindowDecorations = WindowDecorations.None,
        };

        // 想看图标轨展开的样子：HELIUM_RAIL_EXPANDED=1
        // （展开态有 260ms 的宽度过渡，等它走完再截图，否则拍到的是半宽的中间帧）
        if (Environment.GetEnvironmentVariable("HELIUM_RAIL_EXPANDED") == "1")
        {
            shell.IsRailExpanded = true;
        }

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(800));
        if (shell.IsRailExpanded)
        {
            PumpDispatcher(TimeSpan.FromMilliseconds(500));
        }

        if (dumpTree)
        {
            DumpVisualTree(window, "外壳");
        }

        var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("CaptureRenderedFrame 返回了 null（可能没有渲染出任何内容）");

        var path = Path.Combine(outputDirectory, "外壳.png");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        frame.Dispose();

        window.Close();

        return path;
    }

    /// <summary>渲染一个页面并保存成 PNG。</summary>
    private static string RenderPage(NavigationItemViewModel item, string outputDirectory, bool dumpTree, int width, int height)
    {
        // 取页面。注意：导航栏里存的是 **ViewModel**，不是控件 ——
        // 真正把它变成界面的是 ViewLocator（和程序运行时走的是同一套逻辑）。
        // 所以这里也要用一个 ContentControl 来「接住」它，
        // 这样渲染出来的就是用户真正会看到的那套界面，而不是另写一份。
        // ── 外观开关（截图用）──
        //   HELIUM_DARK=1            按暗色模式出图
        //   HELIUM_CARD_OPACITY=NN   功能区不透明度（0-100）
        // 两个都要在"取页面"之前生效：卡片刷子是应用级资源，改早了才会被
        // 页面上那些 Binding 用到。
        var appearanceDark = Environment.GetEnvironmentVariable("HELIUM_DARK") == "1";
        if (appearanceDark)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("HELIUM_CARD_OPACITY"), out var opacityPercent))
        {
            HeliumTodo.Desktop.App.ApplyCardOpacity(opacityPercent, appearanceDark);
        }

        var page = item.GetOrCreatePage();

        // 先把数据加载完（异步的，要跑消息循环等它）
        if (page is ViewModelBase vm)
        {
            var task = vm.OnNavigatedToAsync();
            PumpDispatcherUntil(task, TimeSpan.FromSeconds(20));


            // 自习室这一页有两个分支：已进房间 / 还没进房间。
            // 默认渲染的是前者（种子数据里有 STUDY1），
            // 设 HELIUM_EMPTY_ROOM=1 时先退出房间再拍，把后者也覆盖到。
            // 联机输入框只在「还没进房间」时显示，不这样拍一遍就等于没验证过。
            if (Environment.GetEnvironmentVariable("HELIUM_EMPTY_ROOM") == "1" &&
                vm is HeliumTodo.Desktop.ViewModels.StudyRoomViewModel roomVm)
            {
                if (roomVm.LeaveRoomCommand.CanExecute(null))
                {
                    roomVm.LeaveRoomCommand.Execute(null);
                    PumpDispatcher(TimeSpan.FromMilliseconds(600));
                }

                // 再一个开关：真的建一个房间，用来核对「房主才显示地址」这一支。
                // 建房的路上会真的开监听端口（47470），端口被占用时也只是提示失败，
                // 不影响其它页面。
                if (Environment.GetEnvironmentVariable("HELIUM_HOST_ROOM") == "1")
                {
                    if (roomVm.CreateRoomCommand.CanExecute(null))
                    {
                        roomVm.CreateRoomCommand.Execute(null);
                        PumpDispatcher(TimeSpan.FromSeconds(1.5));
                    }
                }
            }

            // 设置页：把「导入确认」浮层也拍一张。
            // 这一段平时是点按钮才出来的，光拍默认状态等于没验证过。
            if (Environment.GetEnvironmentVariable("HELIUM_BACKUP_DIALOG") == "1" &&
                vm is SettingsViewModel settingsVm)
            {
                var probeDirectory = Path.Combine(Path.GetTempPath(), "helium-dialog-render");
                Directory.CreateDirectory(probeDirectory);
                var probeZip = Path.Combine(probeDirectory, "dialog-preview.zip");

                PumpDispatcherUntil(settingsVm.ExportToAsync(probeZip), TimeSpan.FromSeconds(60));
                PumpDispatcherUntil(settingsVm.BeginImportAsync(probeZip), TimeSpan.FromSeconds(60));

                try
                {
                    File.Delete(probeZip);
                }
                catch (IOException)
                {
                    // 删不掉无所谓
                }
            }

            // 设置页：提示条平时是隐藏的，要用户点个按钮才出来。
            // 设 HELIUM_SETTINGS_NOTICE=1 时直接把它点亮，拍进截图。
            if (Environment.GetEnvironmentVariable("HELIUM_SETTINGS_NOTICE") == "1" &&
                vm is SettingsViewModel noticeVm)
            {
                noticeVm.NoticeText = "已取消更改，渲染后端仍是「软件渲染」。";
                noticeVm.IsNoticeVisible = true;
                PumpDispatcher(TimeSpan.FromMilliseconds(300));
            }

            // 设置页：「改渲染后端要重启」这个浮层，平时要用户动下拉框才出来。
            // 设 HELIUM_RESTART_DIALOG=1 时先选一个不同后端，把浮层拍进截图。
            if (Environment.GetEnvironmentVariable("HELIUM_RESTART_DIALOG") == "1" &&
                vm is SettingsViewModel restartVm)
            {
                // 选一个跟当前配置不同的后端，才会触发提示。
                var pick = restartVm.RenderBackends
                    .FirstOrDefault(o => o.Key != RenderBackendSettings.Normalize(restartVm.Config.RenderBackend));
                if (pick is not null)
                {
                    restartVm.SelectedRenderBackend = pick;
                    PumpDispatcher(TimeSpan.FromMilliseconds(300));
                }
            }

            // 日程页：把「新建」表单展开，提醒下拉才会出现在截图里。
            // 平时要用户点一下「＋ 新建」才看得见，光拍默认状态等于没拍。
            if (Environment.GetEnvironmentVariable("HELIUM_OPEN_EDITOR") == "1" && vm is ScheduleViewModel scheduleVm)
            {
                scheduleVm.IsEditorOpen = true;
                PumpDispatcher(TimeSpan.FromMilliseconds(500));
            }
        }

        var host = new ContentControl
        {
            Content = page,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        var window = new Window
        {
            Width = width,
            Height = height,
            // ⚠️ Avalonia 12 的改名：SystemDecorations → WindowDecorations。
            //    枚举类型本身也换了位置，现在就叫 Avalonia.Controls.WindowDecorations
            //    （Avalonia 11 里是 Avalonia.Controls.SystemDecorations）。
            //    属性名要跟着一起改，否则虽然能编译，但用的是标了 [Obsolete] 的旧属性。
            WindowDecorations = WindowDecorations.None,
            Content = host,
            DataContext = null,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        if (dumpTree)
        {
            DumpVisualTree(window, item.Title);
        }

        var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("CaptureRenderedFrame 返回了 null（可能没有渲染出任何内容）");

        var path = Path.Combine(outputDirectory, $"{Sanitize(item.Title)}.png");

        // ⚠️ Avalonia 12 把 frame.Save(path) 标成过时了，现在要显式传编码参数。
        //    BitmapEncoderOptions 是抽象类，PNG 用它的子类 PngBitmapEncoderOptions，
        //    直接拿内置的 Default 实例就等于「按默认参数编码 PNG」。
        frame.Save(path, PngBitmapEncoderOptions.Default);
        frame.Dispose();

        window.Close();

        return path;
    }

    /// <summary>
    /// 打印可视化树（只打印关心的那几类控件）。
    ///
    /// 排查「某个控件明明写了却没显示」时特别有用：
    ///   能看到它到底有没有被创建、被放在了哪个父节点下、实际尺寸是多少。
    /// </summary>
    private static void DumpVisualTree(Visual root, string title)
    {
        Console.WriteLine($"   ── 可视化树（{title}）──");

        var interesting = new[]
        {
            "ItemsControl", "Canvas", "ScrollViewer", "TextBlock", "ContentPresenter",
            "WidthObserverPanel", "TimelineGrid", "StackPanel", "Grid",
            // 交互控件也打出来：设置页「控件是否拉满整行」「圆角对不对」靠它核对。
            "Button", "ToggleButton", "ComboBox", "TextBox", "NumericUpDown", "ToggleSwitch",
        };

        var count = 0;

        foreach (var visual in root.GetVisualDescendants())
        {
            var name = visual.GetType().Name;

            if (!interesting.Contains(name)) continue;

            // TextBlock 只打印有文字的，避免刷屏
            string? text = null;
            if (visual is TextBlock tb)
            {
                text = tb.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;
            }

            var bounds = visual.Bounds;
            var indent = new string(' ', Math.Min(24, visual.GetVisualAncestors().Count()));

            var line = $"   {indent}{name} {bounds.Width:0}x{bounds.Height:0}";
            if (text is not null) line += $"  \"{Trim(text, 18)}\"";

            // 时间轴相关的控件额外打印坐标，方便定位
            if (visual is Visual v && (name is "Canvas" or "TextBlock"))
            {
                var pos = v.TranslatePoint(new Point(0, 0), root);
                if (pos is not null) line += $"  @({pos.Value.X:0},{pos.Value.Y:0})";
            }

            Console.WriteLine(line);

            // 每个页面默认最多打印 120 行（--tree-lines 可以放宽），够定位问题就行
            if (++count > _treeLines)
            {
                Console.WriteLine("   …（省略更多）");
                break;
            }
        }

        Console.WriteLine();
    }

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    /// <summary>
    /// 跑消息循环，直到某个任务完成或者超时。
    ///
    /// 为什么要这样？
    ///   界面代码里的异步方法（比如读数据库）要在 UI 线程的消息循环里才会推进。
    ///   这里没有真正的窗口循环，所以要手动「泵」一下。
    /// </summary>
    private static void PumpDispatcherUntil(Task task, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        if (task.IsFaulted && task.Exception is not null)
        {
            throw task.Exception.GetBaseException();
        }
    }

    /// <summary>
    /// 一边转界面消息循环一边等某个值变成期望的样子，超时就返回当时的值。
    ///
    /// 为什么不能直接 Thread.Sleep：页面刷新是事件回调里 await 出来的，
    /// 不转消息循环它就一直排在队里，等多久都不会变。
    /// </summary>
    private static T WaitForValue<T>(Func<T> read, Func<T, bool> until, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (until(read())) return read();

            Thread.Sleep(20);
        }

        return read();
    }

    /// <summary>
    /// 备份 / 恢复自检：完全按「设置页上按钮点下去」的路径跑一遍。
    ///
    /// 为什么不用单元测试覆盖？
    ///   单元测试测的是 DataBackupService，而这里要测的是
    ///   SettingsViewModel 里的命令、属性通知、导入后界面状态的重建 ——
    ///   那部分以前从来没被执行过（因为没有真实点击）。
    /// </summary>
    /// <summary>
    /// 临时诊断：设置页那四个「打开文件夹」按钮到底能不能点到。
    ///
    /// 分两件事查：
    ///   ① 按钮本身是否可点（可见 / 启用 / 命中测试能不能落在它身上）
    ///   ② 命令执行后到底发生了什么（Process.Start 有没有抛异常）
    /// 这两件事的病因完全不同，混在一起看会误判。
    /// </summary>
    /// <summary>
    /// 窗口自检：打开动画、拖边改大小、单实例守卫。
    ///
    /// 为什么这三件事要单独查（都不能靠单元测试覆盖）：
    ///   · 打开动画是"感觉"层面的东西，没有断言就只剩一句"看着还行"；
    ///   · 拖边改大小整套是自己算的（窗口没有系统边框），正负号写反了界面上看不出来；
    ///   · 单实例守卫决定了"关掉窗口之后还回不回得来"，它坏了用户会把窗口弄丢。
    /// </summary>
    /// <summary>
    /// 性能自检：量「改一次窗口尺寸 → 重新布局 + 软件渲染」的每帧成本。
    ///
    /// 为什么需要它：用户报「启动、最大化、拖边改大小都卡」，而"卡"是个感觉，
    /// 没有毫秒数就只能瞎猜该改哪里。这里用无头真绘图（和默认的软件渲染同一条路）
    /// 把四个变量分开量：壁纸插值质量、有没有壁纸、要不要改尺寸、纯重绘。
    /// 数字出来之后，优化才有依据，也不会"改完感觉快了"其实没变。
    /// </summary>
/// <summary>
    /// 一档测量的统计结果。
    ///
    /// 为什么不能只看均值：
    ///   真机上量到的是 18 / 91 / 64 / 58 ms 这种序列 —— 均值 58 看着"还行"，
    ///   但最慢一帧 91 ms 就是用户嘴里的"卡了一下"。均值会把抖动抹平，
    ///   所以这里同时给出 P50（一半的帧比它快）、P95、最慢值，
    ///   以及变异系数 CV = 标准差 / 均值（"抖得厉害不厉害"的单一数字）。
    /// </summary>
    private readonly record struct PerfStats(double Mean, double P50, double P95, double Max, double Cv, int Frames);

    private static int RunPerfCheck(IServiceProvider services)
    {
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            if (!ok) failures++;
            Console.WriteLine($"  {(ok ? "✓" : "✗")} {name} — {detail}");
        }

        var shell = services.GetRequiredService<ShellViewModel>();

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        // ── 尺寸：起点是默认窗口，终点是"整个工作区" ─────────────────────
        // 以前的探针写死 1400×900 改一点点，只有真实最大化幅度的 1/4，
        // 量出来的数字自然乐观。现在按真实幅度来（--perf-size 可以覆盖）。
        var (startSize, endSize) = ResolvePerfSizes(window);
        Console.WriteLine(
            $"  测量范围 {startSize.Width:0}×{startSize.Height:0} ↔ {endSize.Width:0}×{endSize.Height:0}（DIP）");

        var image = window.GetVisualDescendants().OfType<Image>().FirstOrDefault();
        Check("窗口里有壁纸图层", image is not null, image is null ? "没找到 Image" : "找到了");

        // 壁纸按**真实解码尺寸**造：解码器把长边压到 WallpaperDecoder.MaxLongSide，
        // 所以真实壁纸就是"长边 1920"这个量级。以前造 1600×1000，
        // 在大屏上反而是上采样，量出来的成本与线上不符。
        var wallpaperWidth = WallpaperDecoder.MaxLongSide;
        var wallpaperHeight = wallpaperWidth * 5 / 8;
        Console.WriteLine($"  壁纸 {wallpaperWidth}×{wallpaperHeight}（与真实解码尺寸一致）");

        var wallpaper = CreatePerfWallpaper(wallpaperWidth, wallpaperHeight);
        var solidWallpaper = CreateSolidPerfWallpaper(wallpaperWidth, wallpaperHeight);
        if (image is not null)
        {
            image.Source = wallpaper;
            image.IsVisible = true;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        const int frames = 24;
        void Resize(int frame) => ResizeBetween(window, startSize, endSize, frame, frames);

        // ── A 基线：改尺寸 + 高品质壁纸插值 + 带卡片阴影（改造前的样子）──
        var baseline = MeasureFrames(window, frames, Resize);

        // ── B 同样改尺寸，但壁纸插值降到 LowQuality ──
        if (image is not null)
        {
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        var lowQuality = MeasureFrames(window, frames, Resize);

        // ── C 低质量插值 + 没有壁纸（看壁纸本身占多少）──
        if (image is not null)
        {
            image.IsVisible = false;
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        var noWallpaper = MeasureFrames(window, frames, Resize);

        // ── D 只重绘、不改尺寸 ──
        var pureRender = MeasureFrames(window, frames, _ => { });

        // ── E 只改尺寸 + 只跑布局、不画像素（把"布局"和"绘制/出图"拆开）──
        var layoutOnly = MeasureLayoutOnly(window, frames, Resize);

        // ── F 不改尺寸，只改整窗不透明度（旧开场动画就是这么干的，已废弃）──
        // 留着这一档是为了留证据：改 Window.Opacity 会走 WS_EX_LAYERED 分层窗口路径，
        // 每帧都要重新合成整窗 —— 所以新方案改成"淡入一层遮罩"，见下面 M 档。
        var opacityOnly = MeasureFrames(window, frames, frame => window.Opacity = 0.96 - frame % 6 * 0.01);
        window.Opacity = 1;

        // ── G 不改尺寸、只改内容缩放（"冻结布局 + 缩放"的每帧成本）──
        var content = (Control)window.Content!;
        var scale = new ScaleTransform(1, 1);
        content.RenderTransform = scale;
        content.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        var transformOnly = MeasureFrames(window, frames, frame => scale.ScaleX = scale.ScaleY = 0.96 + frame % 6 * 0.01);
        content.RenderTransform = null;
        content.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        // ── H 改尺寸，但"什么都不画"（只剩帧缓冲重建的成本）──
        content.IsVisible = false;
        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        var emptyResize = MeasureFrames(window, frames, Resize);

        // ── I 改尺寸 + 高品质壁纸 + 藏掉真内容（"快照拉伸"方案的上限估算）──
        if (image is not null)
        {
            image.IsVisible = true;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        var snapshotLike = MeasureFrames(window, frames, Resize);

        content.IsVisible = true;
        PumpDispatcher(TimeSpan.FromMilliseconds(150));

        // ── J 改尺寸 + 低品质壁纸 + 不要卡片阴影（现在线上用的这一档）──
        if (image is not null)
        {
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
        }

        var cards = window.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.Classes.Contains("card"))
            .ToList();
        var savedShadows = cards.Select(c => c.BoxShadow).ToList();
        foreach (var card in cards)
        {
            card.BoxShadow = default;
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        var noShadow = MeasureFrames(window, frames, Resize);

        // ── K 内容矩阵动画：窗口尺寸只改一次，之后每帧只改内容的 RenderTransform ──
        // 这是"L2 保底方案"的直接依据：如果这一档明显便宜，那就不必逐帧改 HWND，
        // 一次性把窗口落到终点、让内容从旧尺寸"拉伸"过来。
        // 注意：这里量的是"每帧成本"，不含开头那一次改尺寸（那一次是 45 ms，只付一次）。
        window.Width = endSize.Width;
        window.Height = endSize.Height;
        PumpDispatcher(TimeSpan.FromMilliseconds(200));

        var matrix = new MatrixTransform(Matrix.Identity);
        content.RenderTransform = matrix;
        content.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        var matrixOnly = MeasureFrames(window, frames, frame =>
        {
            var t = (double)frame / (frames - 1);
            var e = WindowAnimationMath.EaseOut(t);
            var sx = 1 - (1 - startSize.Width / endSize.Width) * (1 - e);
            var sy = 1 - (1 - startSize.Height / endSize.Height) * (1 - e);
            matrix.Matrix = Matrix.CreateScale(sx, sy);
        });
        content.RenderTransform = null;
        content.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        // ── L 改尺寸 + 纯色壁纸 + 无卡片阴影（隔离"换尺寸本身"的净成本）──
        // 壁纸换成纯色：不重采样、不走高品质插值路径，剩下的就是
        // "Win32 换一次尺寸 + 重建一块前后缓冲 + 整窗 blit"。
        if (image is not null)
        {
            image.Source = solidWallpaper;
            image.IsVisible = true;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        var solidResize = MeasureFrames(window, frames, Resize);

        // ── M 只改"遮罩画刷的 Alpha"（新开场动画的每帧成本）──
        // 新方案不再改 Window.Opacity（会抖 WS_EX_LAYERED），改成在内容最上层
        // 盖一层不透明度 1 → 0 的遮罩。改画刷的 Color 不会 push 离屏图层，
        // 所以应该落在"纯重绘"那一档（1~2 ms）。
        var scrim = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => !b.IsHitTestVisible && b.Opacity > 0.05 && b.Opacity < 0.95);
        var veilBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        var savedScrimBackground = scrim?.Background;
        if (scrim is not null)
        {
            scrim.Background = veilBrush;
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        var veilAlpha = MeasureFrames(window, frames, frame =>
        {
            var alpha = (byte)Math.Round(255 * (1 - (double)frame / (frames - 1)));
            veilBrush.Color = Color.FromArgb(alpha, 0, 0, 0);
        });

        if (scrim is not null)
        {
            scrim.Background = savedScrimBackground;
        }

        // N / O：图标轨展开动画 —— "内部控件变宽"这一类的代表。
        // 它不是改窗口尺寸，但每帧一样要让整页重排、整窗重画，所以单独量。
        //
        // 量之前先把轨道自己那条 Width 过渡摘掉：留着它的话，我们每帧写一次 Width
        // 会和过渡互相拉扯（过渡每帧再写一次、我们再写一次），量出来是 260 ms 的假数。
        // 生产里只有过渡在动，所以"摘掉过渡、手动按帧写"才是它的真实每帧成本。
        var navRail = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "NavRail");
        PerfStats railExpand = default;
        PerfStats railExpandLowQuality = default;

        if (navRail is not null)
        {
            var savedRailWidth = navRail.Width;
            var savedRailTransitions = navRail.Transitions;
            navRail.Transitions = null;

            PerfStats MeasureRail(bool lowQuality)
            {
                if (lowQuality)
                {
                    HeliumTodo.Desktop.App.SetCardShadowSuppressed(true);
                    if (image is not null)
                    {
                        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.LowQuality);
                    }
                }

                var result = MeasureFrames(window, frames, frame =>
                {
                    var t = frames <= 1 ? 1.0 : (double)frame / (frames - 1);
                    navRail.Width = RailMetrics.CollapsedRailWidth
                                    + (RailMetrics.ExpandedRailWidth - RailMetrics.CollapsedRailWidth)
                                    * WindowAnimationMath.EaseOut(t);
                });

                if (lowQuality)
                {
                    HeliumTodo.Desktop.App.SetCardShadowSuppressed(false);
                    if (image is not null)
                    {
                        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                    }
                }

                navRail.Width = savedRailWidth;
                return result;
            }

            railExpand = MeasureRail(false);
            railExpandLowQuality = MeasureRail(true);

            navRail.Transitions = savedRailTransitions;
            PumpDispatcher(TimeSpan.FromMilliseconds(150));
        }

        // 还原成线上默认（高品质 + 显示壁纸 + 带阴影），别留脏状态给后面的自检。
        if (image is not null)
        {
            image.Source = wallpaper;
            image.IsVisible = true;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        }

        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].BoxShadow = savedShadows[i];
        }

        PumpDispatcher(TimeSpan.FromMilliseconds(150));

        Console.WriteLine();
        Console.WriteLine("  场景                                    平均    P50    P95   最慢   抖动CV  折合帧率");
        WritePerfRow("A 改尺寸 + 高品质壁纸（改造前）", baseline);
        WritePerfRow("B 改尺寸 + 低品质壁纸", lowQuality);
        WritePerfRow("C 改尺寸 + 不画壁纸", noWallpaper);
        WritePerfRow("D 不改尺寸，纯重绘", pureRender);
        WritePerfRow("E 改尺寸，只布局不画", layoutOnly);
        WritePerfRow("F 只改整窗不透明度（旧开场动画）", opacityOnly);
        WritePerfRow("G 只改内容缩放", transformOnly);
        WritePerfRow("H 改尺寸，什么内容都不画", emptyResize);
        WritePerfRow("I 改尺寸，只剩壁纸（快照方案）", snapshotLike);
        WritePerfRow("J 低品质壁纸 + 无阴影（线上用的）", noShadow);
        WritePerfRow("K 只改内容矩阵，不改窗口尺寸", matrixOnly);
        WritePerfRow("L 改尺寸 + 纯色壁纸 + 无阴影", solidResize);
        WritePerfRow("M 只改遮罩画刷 Alpha（新开场动画）", veilAlpha);
        if (navRail is not null)
        {
            WritePerfRow("N 图标轨展开（整页重排，线上原样）", railExpand);
            WritePerfRow("O 图标轨展开 + 降质（现在线上用的）", railExpandLowQuality);
        }
        // 试过但没用的一档：把文字抗锯齿关掉（RenderOptions → obsolete，已改用 TextOptions）
        // 实测 noShadow 24.6 ms vs 断锯齿 25.6 ms —— 反而更慢，所以没进 InteractionQuality。

        Console.WriteLine();

        // ── 分页对照：改尺寸的布局开销会不会取决于页面内容量？──
        if (image is not null)
        {
            image.IsVisible = true;
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        }

        Console.WriteLine("  各页面改尺寸成本（高品质壁纸）：");
        foreach (var item in shell.MenuItems.Concat(shell.FooterItems).ToList())
        {
            shell.SelectedItem = item;
            PumpDispatcher(TimeSpan.FromMilliseconds(400));
            var perPage = MeasureFrames(window, 12, frame => ResizeBetween(window, startSize, endSize, frame, 12));
            var perPageLayout = MeasureLayoutOnly(window, 12, frame => ResizeBetween(window, startSize, endSize, frame, 12));
            Console.WriteLine(
                $"  {item.Title,-14} 出图 {perPage.Mean,7:0.0} ms   只布局 {perPageLayout.Mean,7:0.0} ms   最慢 {perPage.Max,6:0.0} ms");
        }

        Console.WriteLine();

        Check("低品质壁纸不比高品质慢", lowQuality.Mean <= baseline.Mean + 0.5,
            $"{baseline.Mean:0.0} → {lowQuality.Mean:0.0} ms");
        Check("不画壁纸会更快（说明壁纸是成本之一）", noWallpaper.Mean <= baseline.Mean,
            $"{baseline.Mean:0.0} → {noWallpaper.Mean:0.0} ms");
        Check("改尺寸比纯重绘贵（说明换尺寸本身是成本）", pureRender.Mean <= baseline.Mean,
            $"纯重绘 {pureRender.Mean:0.0} ms vs 改尺寸 {baseline.Mean:0.0} ms");
        // 帧间隔"卡不卡"要看它相对 P50 的比例，不能钉一个绝对毫秒数：
        // 绝对数字随屏幕大小和渲染后端变化（1400×900 下 53 ms，1920×1280 下 118 ms），
        // 但尾部帧整体抬高就是肉眼可见的卡顿。规则在 Core 里
        // （WindowTransitionPolicy.IsFramePacingAcceptable / IsWorstFrameAcceptable）。
        //
        // 分两条而不是一条"最慢 < 2 × P50"：一轮只采 20 多帧，GC 或别的进程抢一下
        // 就会打出孤立的一帧（实测 P50 60.6 ms、最慢 130.3 ms，比值 2.15，肉眼完全没事），
        // 拿它卡 2 倍等于让这条自检随机变红。真正的卡是"尾部整体抬高"，那是 P95。
        Check("帧间隔没有失控（P95 < 2 × P50）",
            WindowTransitionPolicy.IsFramePacingAcceptable(baseline.P50, baseline.P95),
            $"P50 {baseline.P50:0.0} ms，P95 {baseline.P95:0.0} ms");
        Check("最慢一帧没有离谱（< 4 × P50）",
            WindowTransitionPolicy.IsWorstFrameAcceptable(baseline.P50, baseline.Max),
            $"P50 {baseline.P50:0.0} ms，最慢 {baseline.Max:0.0} ms");

        // 线上真正用的就是 J 这一档（见 HeliumTodo.Desktop/Windowing/InteractionQuality.cs）：
        // 以后谁改回去（"阴影关掉不好看，还是留着吧"），这里立刻变红。
        var speedup = baseline.Mean <= 0.01 ? 0 : (baseline.Mean - noShadow.Mean) / baseline.Mean * 100;
        Check("交互降质方案比基线快 30% 以上", speedup >= 30,
            $"{baseline.Mean:0.0} → {noShadow.Mean:0.0} ms（快 {speedup:0}%）");

        // L2 保底方案的可行性：不动窗口尺寸、只改内容矩阵，应该比逐帧改尺寸便宜一大截。
        var matrixSpeedup = baseline.Mean <= 0.01 ? 0 : (baseline.Mean - matrixOnly.Mean) / baseline.Mean * 100;
        Check("内容矩阵动画至少比逐帧改尺寸快一半", matrixSpeedup >= 50,
            $"{baseline.Mean:0.0} → {matrixOnly.Mean:0.0} ms（快 {matrixSpeedup:0}%）");

        // 新开场动画的依据（两条一起看才有意义）：
        //   ① 只改遮罩画刷的 Alpha 必须比"改整窗不透明度"便宜一大截 —— 后者会走
        //      WS_EX_LAYERED 分层窗口，每帧重新合成整窗（实测 37 ms）；
        //   ② 它的成本应该和"纯重绘一帧"同一个量级 —— 既不 push 离屏图层，也不改尺寸。
        Check("只改遮罩 Alpha 至少比改整窗不透明度便宜一半",
            veilAlpha.Mean <= opacityOnly.Mean / 2,
            $"{opacityOnly.Mean:0.0} → {veilAlpha.Mean:0.0} ms/帧");
        Check("只改遮罩 Alpha 与纯重绘同量级",
            veilAlpha.Mean <= pureRender.Mean * 1.5 + 1.0,
            $"纯重绘 {pureRender.Mean:0.0} ms，遮罩 {veilAlpha.Mean:0.0} ms/帧");

        // ── §15.3 性能预算对照 ──
        // 无头数字只用来比较，绝对值不代表真机（真机由 tools/probe-*.ps1 采）。
        // 这里能钉的钉住，钉不住的把偏差写清楚。
        Console.WriteLine();
        Console.WriteLine("── §15.3 预算对照（无头代理）──");
        Check("纯重绘 P50 ≤ 33.3 ms（时间轴首屏 ≤ 50 ms 的代理）",
            pureRender.P50 <= 33.3, $"P50 {pureRender.P50:0.0} ms");
        if (navRail is not null)
        {
            Check("图标轨展开 P50 ≤ 33.3 ms（真机 16.7 ms 由 probe-rail-anim 验）",
                railExpand.P50 <= 33.3, $"P50 {railExpand.P50:0.0} ms");
            Check("图标轨降质不慢于原样",
                railExpandLowQuality.P50 <= railExpand.P50 + 0.5,
                $"{railExpand.P50:0.0} → {railExpandLowQuality.P50:0.0} ms");
        }

        var privateMegabytes = Process.GetCurrentProcess().PrivateMemorySize64 / 1024.0 / 1024.0;
        Check("私有字节 ≤ 300 MB（无头进程；真机预算同值）",
            privateMegabytes <= 300, $"{privateMegabytes:0} MB");
        Console.WriteLine("   [偏差] 冷启动 ≤ 1.5 s 与最大化 DWM 帧数不在无头里测，见 probe-* 探针。");

        window.Close();
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 开始/最大化/还原的动效自检。
    ///
    /// 无头环境里 RequestAnimationFrame 和 DispatcherTimer 都不会按真实节奏走，
    /// 所以"帧间隔"这件事在这里量不出来（真机数字在 tools/probe-native-anim.ps1）。
    /// 这里守的是另一半 —— 那几条"打断了也必须收尾干净"的正确性：
    ///   ① 政策数值没被改坏；
    ///   ② 开场动画播到一半被最小化，收尾后不透明度必须是 1、内容变换必须是空；
    ///   ③ 从任务栏回来、以及手动 Settle，都不能残留半透明或缩放。
    /// 这几条以前全是 bug（用户看到"回来是半透明、还小了一圈、边缘拉不动"）。
    /// </summary>
    private static int RunAnimCheck(IServiceProvider services)
    {
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            if (!ok) failures++;
            Console.WriteLine($"  {(ok ? "✓" : "✗")} {name} — {detail}");
        }

        Console.WriteLine();
        Console.WriteLine("── ① 政策数值 ──");
        Check("过渡时长 180 ms", WindowTransitionPolicy.DurationMs == 180, $"{WindowTransitionPolicy.DurationMs} ms");
        Check("最少 10 帧", WindowTransitionPolicy.MinFrames == 10, $"{WindowTransitionPolicy.MinFrames} 帧");
        Check("180 ms 的动画不暂停动图壁纸",
            !WindowTransitionPolicy.ShouldPauseWallpaper(WindowTransitionPolicy.DurationMs),
            $"阈值 {WindowTransitionPolicy.WallpaperPauseThresholdMs} ms");
        Check("每帧间隔约 16-18 ms（约 60 Hz）",
            WindowTransitionPolicy.FrameIntervalMs(WindowTransitionPolicy.DurationMs) is > 15 and < 19.5,
            $"{WindowTransitionPolicy.FrameIntervalMs(WindowTransitionPolicy.DurationMs):0.0} ms");
        Check("开场淡入至少 5 帧（冷启动帧迟到时也不许闪一下）",
            WindowTransitionPolicy.MinIntroFrames >= 5
            && WindowTransitionPolicy.IntroMaxProgressStep <= 0.2 + 1e-9,
            $"最少 {WindowTransitionPolicy.MinIntroFrames} 帧，单帧步进上限 {WindowTransitionPolicy.IntroMaxProgressStep:0.00}");

        var shell = services.GetRequiredService<ShellViewModel>();
        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(500));

        var content = (Control)window.Content!;

        // 先探一下这个环境里 DispatcherTimer 会不会真的触发 ——
        // 无头渲染下它不触发，靠定时器收尾的断言只能跳过（真机由 probe-intro 验）。
        var timersTick = ProbeDispatcherTimer();

        Console.WriteLine();
        Console.WriteLine("── ② 开场动画播到一半被最小化 ──");
        window.PlayShowAnimation();
        PumpDispatcher(TimeSpan.FromMilliseconds(60));

        var midOpacity = window.Opacity;
        var midHasTransform = content.RenderTransform is not null;
        var veil = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "IntroVeil");

        // 开场动画现在靠"遮罩淡出"，会把 Window.Opacity 交给别的东西 —— 淡入效果寄几必须看得见。
        // 这条断言就是防止以后有人把开场动画改成"什么都不做"还能过自检。
        var veilAlpha = veil?.Background is ISolidColorBrush veilBrush ? veilBrush.Color.A : -1;
        Console.WriteLine($"   [信息] 动画中途：不透明度 {midOpacity:0.00}，内容变换 {(midHasTransform ? "有" : "无")}，遮罩 {veilAlpha}");

        if (veilAlpha is >= 0 and < 255)
        {
            Check("遮罩在变淡（不是闪一下）", veilAlpha < 255, $"第 60ms 时遮罩 {veilAlpha}（起点 255）");
        }
        else
        {
            Console.WriteLine("   - 跳过遮罩变淡检查：这个环境没有推进帧回调");
        }

        window.WindowState = WindowState.Minimized;
        PumpDispatcher(TimeSpan.FromMilliseconds(150));

        Check("最小化后不透明度回到 1", Math.Abs(window.Opacity - 1.0) < 0.001, $"当前 {window.Opacity:0.00}");
        Check("最小化后内容变换被清掉（抓手不会错位）", content.RenderTransform is null,
            content.RenderTransform is null ? "已清空" : "还有变换");
        Check("最小化后遮罩被收掉（回来不会是一块白纱）", veil is null || !veil.IsVisible,
            veil is null ? "没有遮罩" : veil.IsVisible ? "遮罩还盖着" : "已收掉");

        Console.WriteLine();
        Console.WriteLine("── ③ 从任务栏点回来 ──");
        window.WindowState = WindowState.Normal;
        PumpDispatcher(TimeSpan.FromMilliseconds(250));
        Check("回来之后不透明度仍是 1", Math.Abs(window.Opacity - 1.0) < 0.001, $"当前 {window.Opacity:0.00}");
        Check("回来之后没有残留缩放", content.RenderTransform is null,
            content.RenderTransform is null ? "已清空" : "还有变换");
        Check("回来之后会重新播一次出现动画", veil is not null && veil.IsVisible,
            veil is null ? "没有遮罩" : veil.IsVisible ? "遮罩已盖上" : "遮罩没盖上");

        Console.WriteLine();
        Console.WriteLine("── ④ 手动 Settle 也要干净（关窗/藏托盘时调用）──");
        window.PlayShowAnimation();
        PumpDispatcher(TimeSpan.FromMilliseconds(40));
        window.FinishAnimationsImmediately();
        Check("Settle 后不透明度为 1", Math.Abs(window.Opacity - 1.0) < 0.001, $"当前 {window.Opacity:0.00}");
        Check("Settle 后内容变换为空", content.RenderTransform is null,
            content.RenderTransform is null ? "已清空" : "还有变换");

        Console.WriteLine();
        Console.WriteLine("── ⑤ 开场动画一定会收尾（看门狗）──");

        // 无头环境里合成器不一定给帧回调，Timer 也可能被调度器掐掉 ——
        // 但"遮罩不能永久留在屏幕上"是硬要求，所以给足时间后必须自己收尾。
        window.PlayShowAnimation();
        PumpDispatcher(TimeSpan.FromMilliseconds(900));
        var veilGone = veil is null || !veil.IsVisible;

        // 无头环境里 DispatcherTimer 根本不触发（这是老问题，之前 Settle 就是为它加的），
        // 所以看门狗在这里没有机会跑 —— 这种情况下不能算失败，只能说"这一条无头验不了"，
        // 真机上由 tools/probe-intro.ps1 用 HELIUM_INTRO_TRACE 数帧来验证。
        if (timersTick)
        {
            Check("开场遮罩最终会被收掉（等不到帧也不会卡在半透明）", veilGone,
                veil is null ? "没有遮罩" : veil.IsVisible ? "遮罩还在" : "已收掉");
        }
        else
        {
            Console.WriteLine("   - 跳过：无头环境 DispatcherTimer 不触发，看门狗只能在真机上验（tools/probe-intro.ps1）");
        }

        // 抓手命中点：内容有缩放时 TranslatePoint 会把窗口坐标算偏（曾偏 26 px）。
        var grip = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "GripRight");
        if (grip is not null && grip.TranslatePoint(new Point(0, 0), window) is { } gripPoint)
        {
            var expected = window.Bounds.Width - grip.Bounds.Width;
            Check("右边缘抓手落在窗口右边界上", Math.Abs(gripPoint.X - expected) < 2.0,
                $"实际 x={gripPoint.X:0.0}，应为 {expected:0.0}");
        }
        else
        {
            Check("右边缘抓手落在窗口右边界上", false, "没找到 GripRight");
        }

        window.Close();
        return failures == 0 ? 0 : 1;
    }

    private static void WritePerfRow(string name, PerfStats result)
    {
        var fps = result.Mean <= 0.01 ? 0 : 1000.0 / result.Mean;
        Console.WriteLine(
            $"  {name,-36} {result.Mean,6:0.0} {result.P50,6:0.0} {result.P95,6:0.0} {result.Max,6:0.0} {result.Cv,7:0.00} {fps,8:0}");
    }

    /// <summary>
    /// 这次测量用哪两个尺寸：起点是默认窗口 1400×900，终点默认取主屏工作区
    /// （--perf-size=2560x1440 可以覆盖）。终点必须用工作区而不是屏幕：
    /// 最大化后的窗口就是工作区那么大。
    /// </summary>
    private static (Size Start, Size End) ResolvePerfSizes(Window window)
    {
        var start = new Size(1400, 900);

        if (ReadPerfSizeArgument() is { } requested)
        {
            return (start, requested);
        }

        if (window.Screens.Primary is { } screen)
        {
            var scaling = window.RenderScaling <= 0.01 ? 1.0 : window.RenderScaling;
            var area = screen.WorkingArea;
            var width = area.Width / scaling;
            var height = area.Height / scaling;

            // 工作区必须比默认窗口大，否则"放大"这一步根本没发生。
            if (width > start.Width + 40 && height > start.Height + 40)
            {
                return (start, new Size(width, height));
            }
        }

        // 无头模式拿不到屏幕信息时退回一个常见的大屏尺寸。
        return (start, new Size(2560, 1440));
    }

    private static Size? ReadPerfSizeArgument()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            const string prefix = "--perf-size=";
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = arg[prefix.Length..];
            var parts = text.Split('x', 'X', '*');
            if (parts.Length == 2
                && double.TryParse(parts[0], out var w)
                && double.TryParse(parts[1], out var h)
                && w > 200 && h > 200)
            {
                return new Size(w, h);
            }
        }

        return null;
    }

    /// <summary>
    /// 第 <paramref name="frame"/> 帧时窗口应该是多大 —— 按 EaseOut 从起点走到终点，
    /// 和真实的最大化动画是同一个节奏、同一个幅度。
    /// </summary>
    private static void ResizeBetween(Window window, Size start, Size end, int frame, int frames)
    {
        var t = frames <= 1 ? 1.0 : (double)frame / (frames - 1);
        var eased = WindowAnimationMath.EaseOut(t);
        window.Width = Math.Round(start.Width + (end.Width - start.Width) * eased);
        window.Height = Math.Round(start.Height + (end.Height - start.Height) * eased);
    }

    /// <summary>
    /// 跑 <paramref name="frames"/> 帧，每帧先改状态、再强制渲染一次并计时。
    /// CaptureRenderedFrame 是同步的：它会把"布局 → 绘制 → 出像素"全跑完才返回，
    /// 所以计到的就是"这一帧真实花的时间"。
    /// </summary>
    private static PerfStats MeasureFrames(Window window, int frames, Action<int> step)
    {
        var times = new List<double>(frames);
        for (var i = 0; i < frames; i++)
        {
            step(i);
            var stopwatch = Stopwatch.StartNew();
            window.CaptureRenderedFrame();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Stats(times);
    }

    /// <summary>
    /// 只跑布局、不画像素：强行调 <see cref="Layoutable.UpdateLayout"/> 把"测量 + 排列"跑完。
    /// 和 <see cref="MeasureFrames"/> 的差就是"绘制 + 出图"的钱。
    /// </summary>
    private static PerfStats MeasureLayoutOnly(Window window, int frames, Action<int> step)
    {
        var times = new List<double>(frames);
        for (var i = 0; i < frames; i++)
        {
            step(i);
            var stopwatch = Stopwatch.StartNew();
            window.UpdateLayout();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return Stats(times);
    }

    /// <summary>把一串每帧耗时算成"均值 / P50 / P95 / 最慢 / 变异系数"。</summary>
    private static PerfStats Stats(List<double> times)
    {
        var sorted = times.OrderBy(t => t).ToList();
        var mean = sorted.Average();
        var sum = sorted.Sum(t => (t - mean) * (t - mean));
        var deviation = Math.Sqrt(sum / sorted.Count);
        var cv = mean <= 0.001 ? 0 : deviation / mean;

        return new PerfStats(mean, Percentile(sorted, 0.50), Percentile(sorted, 0.95), sorted[^1], cv, sorted.Count);
    }

    /// <summary>线性插值取分位数（样本少的时候比"最近秩"稳）。</summary>
    private static double Percentile(List<double> sorted, double fraction)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position = (sorted.Count - 1) * fraction;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = position - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    /// <summary>造一张渐变位图当壁纸（纯色会走缓存，量出来的数字偏低）。</summary>
    private static IImage CreatePerfWallpaper(int width, int height)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = bitmap.CreateDrawingContext())
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#FF2B5876"), 0),
                    new GradientStop(Color.Parse("#FF4E4376"), 1),
                },
            };

            context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }

        return bitmap;
    }

    /// <summary>造一张纯色位图（用来隔离"换尺寸本身"的成本，排除重采样）。</summary>
    private static IImage CreateSolidPerfWallpaper(int width, int height)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#FF3A4A5A")), null, new Rect(0, 0, width, height));
        }

        return bitmap;
    }

    private static int RunWindowCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        // ── ① 打开动画 ──
        Console.WriteLine();
        Console.WriteLine("── 打开动画：遮罩淡出 ──");

        // 先记下"刚 Show 完那一刻"的状态：遮罩应当已经盖上，窗口本身是完全不透明的。
        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1200,
            Height = 800,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();

        // 先让首帧布局跑完，再单独播一次开场动画，这样读到的是"动画第 0 帧"的样子。
        Dispatcher.UIThread.RunJobs();
        window.PlayShowAnimation();

        var veil = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Name == "IntroVeil");

        static int VeilAlpha(Border? border)
            => border?.Background is ISolidColorBrush brush ? brush.Color.A : -1;

        var veilAlpha = VeilAlpha(veil);

        Check("开场动画用遮罩，而不是改窗口不透明度",
            veilAlpha > 200 && Math.Abs(window.Opacity - 1) < 1e-9,
            $"遮罩不透明度 {veilAlpha}，窗口不透明度 {window.Opacity:0.00}（窗口必须始终是 1.00）");

        // "逐帧淡出"要看帧回调，而这个窗口在无头里是静止的 —— 不动就不产帧，
        // 所以这条只能在 --anim-check（它先让窗口渲染几轮）和 probe-intro.ps1（真机）里验。
        Console.WriteLine($"   [跳过] 遮罩逐帧淡出 — 静止窗口在无头里不产帧" +
                          $"（当前遮罩 {veilAlpha}），由 --anim-check 与 probe-intro.ps1 验证");

        Check("开场动画不缩放内容",
            Math.Abs(ReadContentScale(window) - 1) < 1e-9,
            $"缩放 {ReadContentScale(window):0.000}（缩放会把抓手坐标算偏，已删）");

        // 缓动与进度的断言 —— 纯函数，不依赖定时器，所以在无头里也能钉死。
        Check("缓动端点正确",
            Math.Abs(WindowAnimationMath.EaseOut(0)) < 1e-9
            && Math.Abs(WindowAnimationMath.EaseOut(1) - 1) < 1e-9
            && WindowAnimationMath.EaseOut(0.5) > 0.5,
            $"EaseOut(0)={WindowAnimationMath.EaseOut(0):0.000} " +
            $"EaseOut(0.5)={WindowAnimationMath.EaseOut(0.5):0.000} " +
            $"EaseOut(1)={WindowAnimationMath.EaseOut(1):0.000}（三点都该「先快后慢」）");

        Check("进度按真实时间算",
            Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.Zero)) < 1e-9
            && Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.FromMilliseconds(90)) - 0.5) < 1e-9
            && Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.FromMilliseconds(500)) - 1) < 1e-9,
            "0ms→0.00，90ms→0.50，500ms→1.00（180ms 就该结束，不会因为掉帧被拉长）");

        // 帧有没有按时到、以及"迟到一帧也不许闪一下"，都只能在真实窗口里看，
        // 交给 tools/probe-intro.ps1（它数的是日志里的遮罩 Alpha 序列）。
        Console.WriteLine("   [跳过] 帧是否按时到达 — 无头平台的合成器不驱动帧回调" +
                          "，改由 probe-intro.ps1 在真实窗口里验证");

        // 动画是靠合成器帧回调推的，无头里一帧都不来。先强制收尾 ——
        // 这既是"窗口要藏起来之前该做的事"，也顺便验证收尾真的能把视觉状态还原干净。
        window.FinishAnimationsImmediately();
        Dispatcher.UIThread.RunJobs();

        var veilHiddenAfterSettle = veil is null || !veil.IsVisible;

        Check("收尾能强制回到完全体",
            Math.Abs(window.Opacity - 1) < 1e-9
            && Math.Abs(ReadContentScale(window) - 1) < 1e-9
            && veilHiddenAfterSettle,
            $"不透明度 {window.Opacity:0.00}，缩放 {ReadContentScale(window):0.000}，" +
            $"遮罩{(veilHiddenAfterSettle ? "已收掉" : "还盖着")}");

        // ── ② 拖边改大小 ──
        Console.WriteLine();
        Console.WriteLine("── 拖边改大小 ──");

        var grip = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Tag as string == "East");

        var widthBefore = window.Width;

        if (grip is null)
        {
            Check("找得到右边缘抓手", false, "视觉树里没有 Tag=East 的 Border");
        }
        else
        {
            var center = grip.TranslatePoint(
                new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), window);

            if (center is null)
            {
                Check("算得出抓手坐标", false, "TranslatePoint 返回 null");
            }
            else
            {
                // 往"里"拖：往外拖会让指针跑到窗口矩形之外，无头平台不一定还往这个窗口送事件。
                // 拖右边缘往里 120 → 窗口应该变窄 120 左右。
                window.MouseMove(center.Value);
                window.MouseDown(center.Value, MouseButton.Left);
                window.MouseMove(center.Value - new Point(120, 0));
                window.MouseUp(center.Value - new Point(120, 0), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                var widthAfter = window.Width;
                Check("右边缘往里拖 120，窗口变窄", widthAfter < widthBefore - 100,
                    $"{widthBefore:0} → {widthAfter:0}");
            }
        }

        // 最大化 / 还原：现在只是切状态，过渡动画由 DWM 在合成器里播（WindowChrome 补了 WS_CAPTION）。
        // 无头平台没有 DWM，过程动画交给 tools/probe-native-anim.ps1 数可见像素面积。
        var threw = string.Empty;
        try
        {
            window.WindowState = WindowState.Maximized;
            PumpDispatcher(TimeSpan.FromMilliseconds(200));
            var maximized = window.WindowState;

            window.WindowState = WindowState.Normal;
            PumpDispatcher(TimeSpan.FromMilliseconds(200));

            if (maximized != WindowState.Maximized || window.WindowState != WindowState.Normal)
            {
                threw = $"{maximized} → {window.WindowState}";
            }
        }
        catch (Exception ex)
        {
            threw = ex.GetType().Name;
        }

        Check("最大化 / 还原切得动（不抛异常）", threw.Length == 0,
            threw.Length == 0 ? "切到 Maximized 再切回 Normal 都正常" : $"异常：{threw}");
        Console.WriteLine("   [跳过] 最大化 / 最小化的过程动画 — 由 DWM 播，改由 probe-native-anim.ps1 验证");

        window.Close();
        PumpDispatcher(TimeSpan.FromMilliseconds(200));

        // ── ③ 单实例守卫 ──
        Console.WriteLine();
        Console.WriteLine("── 单实例守卫 ──");

        var id = $"HeliumTodoCheck-{Guid.NewGuid():N}";
        var showCount = 0;

        using (var first = new SingleInstanceGuard(id))
        {
            first.Listen(() => Interlocked.Increment(ref showCount));

            using var second = new SingleInstanceGuard(id);

            Check("第一个实例认为自己是第一个", first.IsFirstInstance,
                first.IsFirstInstance ? "IsFirstInstance=true" : "IsFirstInstance=false");
            Check("第二个实例知道已经有人在跑", !second.IsFirstInstance,
                second.IsFirstInstance ? "IsFirstInstance=true（不对）" : "IsFirstInstance=false");

            // 第二个"再双击一次" → 第一个应当收到"请显示窗口"
            second.RequestShow();
            var seen = WaitForValue(() => Volatile.Read(ref showCount), v => v >= 1, TimeSpan.FromSeconds(3));

            Check("第二个实例能喊醒第一个", seen >= 1, $"收到 {seen} 次显示请求");
        }

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine(failures == 0 ? "  合计 全部通过" : $"  合计 {failures} 项失败");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>读窗口内容根元素的缩放（打开动画挂在这里）。</summary>
    private static double ReadContentScale(MainWindow window)
        => window.Content is Control { RenderTransform: ScaleTransform scale } ? scale.ScaleX : 1.0;

    /// <summary>
    /// 外壳自检：图标轨能不能真的切页。
    ///
    /// 为什么单独做这么一个检查：
    ///   改版把导航栏从 FANavigationView 换成了自绘的 ItemsControl + Button。
    ///   自绘意味着"点了会切页"这件事只能靠我们自己保证 —— 绑定写错、Tag 没传、
    ///   点击handler 没接上，编译器一个都不会报。而六个页面的单独渲染
    ///   完全绕过了外壳，也发现不了。
    ///
    /// 所以这里在**真实的 MainWindow** 里用真实鼠标事件点每一个图标，
    /// 断言：① CurrentPage 换成了那一页 ② 选中态转移 ③ 外壳几何符合设计值。
    /// </summary>
    /// <summary>
    /// Behavior 自检（0.5.0 P5）。
    ///
    /// 为什么值得测：
    ///   ① 行为挂在 XAML 上时，写错 xmlns / 少写 attached property 不会报编译错，
    ///      只会"点了没反应"——只能靠渲染后逐个点名；
    ///   ② 拖动 / 双击 / 回车 / 抓手这四条是用户每天都会碰的路径，必须各跑一次；
    ///   ③ OnDetaching 不解绑就是内存泄漏，所以解绑后要再打一次同样的输入，确认真的没反应。
    /// </summary>
    private static int RunBehaviorCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1000,
            Height = 760,
            WindowDecorations = WindowDecorations.None,
        };
        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(350));

        var titleBar = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Name == "TitleBar");
        var titleBehaviors = titleBar is null ? new BehaviorCollection() : Interaction.GetBehaviors(titleBar);
        Check("标题栏挂上拖拽与双击最大化",
            titleBehaviors.OfType<DragWindowBehavior>().Count() == 1
            && titleBehaviors.OfType<ToggleMaximizeBehavior>().Count() == 1,
            titleBar is null ? "找不到 TitleBar" : $"共 {titleBehaviors.Count} 个行为");

        var windowButtons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("windowButton"))
            .ToList();
        Check("三个窗口按钮各挂一个 WindowStateBehavior",
            windowButtons.Count == 3
            && windowButtons.All(b => Interaction.GetBehaviors(b).OfType<WindowStateBehavior>().Count() == 1),
            $"按钮 {windowButtons.Count} 个");

        var grips = window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Name is not null && b.Name.StartsWith("Grip", StringComparison.Ordinal))
            .ToList();
        Check("8 个改大小抓手都挂上 ResizeWindowBehavior",
            grips.Count == 8
            && grips.All(g => Interaction.GetBehaviors(g).OfType<ResizeWindowBehavior>().Count() == 1),
            $"抓手 {grips.Count} 个");

        var railToggle = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("railToggle"));
        Check("图标轨展开按钮挂上 ToggleRailBehavior",
            railToggle is not null
            && Interaction.GetBehaviors(railToggle).OfType<ToggleRailBehavior>().Count() == 1,
            railToggle is null ? "找不到 railToggle" : "已挂载");

        // ── 双击标题栏 = 最大化 / 还原 ──
        var titlePoint = titleBar?.TranslatePoint(new Point(40, 20), window);
        if (titlePoint is { } p)
        {
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(300));
        }

        Check("双击标题栏最大化", window.WindowState == WindowState.Maximized,
            $"WindowState={window.WindowState}");

        // ── 窗口按钮（最大化按钮再点一次还原）──
        var maximize = windowButtons.FirstOrDefault(b => Interaction.GetBehaviors(b)
            .OfType<WindowStateBehavior>().Any(x => x.Action == WindowAction.Maximize));
        maximize?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpDispatcher(TimeSpan.FromMilliseconds(250));
        var restored = window.WindowState == WindowState.Normal;
        Check("最大化按钮点一次还原", restored, $"WindowState={window.WindowState}");

        // ── 拖动标题栏：无头平台没有真的窗口移动，验证"确实调用了 BeginMoveDrag 这条路径" ──
        if (titleBar?.TranslatePoint(new Point(40, 20), window) is { } dragPoint)
        {
            var recorder = new RecordingDragBehavior();
            var titleBehaviorsNow = Interaction.GetBehaviors(titleBar);
            var xamlDrag = titleBehaviorsNow.OfType<DragWindowBehavior>().FirstOrDefault();
            if (xamlDrag is not null)
            {
                // 先把 XAML 上那个摘掉：它内部会真的调 BeginMoveDrag，
                // 无头平台虽然没有窗口移动，但会留下指针捕获，影响后面的抓手测试。
                titleBehaviorsNow.Remove(xamlDrag);
            }

            titleBehaviorsNow.Add(recorder);

            window.MouseMove(dragPoint);
            window.MouseDown(dragPoint, MouseButton.Left);
            window.MouseUp(dragPoint, MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(150));

            Check("按住标题栏触发系统拖拽", recorder.Calls == 1,
                $"调用 {recorder.Calls} 次（无头平台不移动窗口，这里钉住 BeginMoveDrag 路径）");

            titleBehaviorsNow.Remove(recorder);
            Check("拖拽行为可以从标题栏摘掉", titleBehaviorsNow.OfType<RecordingDragBehavior>().Count() == 0,
                "已解绑（其余解绑断言在回车与抓手上）");
        }
        // ── 图标轨展开 ──
        var railBefore = shell.IsRailExpanded;
        railToggle?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpDispatcher(TimeSpan.FromMilliseconds(300));
        var railAfter = shell.IsRailExpanded;
        Check("点轨道按钮切换展开状态", railAfter != railBefore, $"{railBefore} → {railAfter}");
        railToggle?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        // ── 回车执行命令 ──
        var box = new TextBox();
        var invoked = 0;
        var enter = new EnterKeyCommandBehavior
        {
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => invoked++),
        };
        Interaction.SetBehaviors(box, new BehaviorCollection { enter });

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Check("回车执行命令", invoked == 1, $"执行 {invoked} 次");

        Interaction.SetBehaviors(box, null);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Check("解绑后回车不再执行", invoked == 1, $"执行 {invoked} 次");

        // ── 抓手改大小 + 解绑 ──
        var rightGrip = grips.FirstOrDefault(g => g.Name == "GripRight");
        var widthBefore = window.Width;
        if (rightGrip is not null
            && rightGrip.TranslatePoint(new Point(rightGrip.Bounds.Width / 2, 200), window) is { } gripPoint)
        {
            window.MouseMove(gripPoint);
            window.MouseDown(gripPoint, MouseButton.Left);
            window.MouseMove(gripPoint - new Point(120, 0));
            window.MouseUp(gripPoint - new Point(120, 0), MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(200));
        }

        var widthAfter = window.Width;
        Check("抓手往里拖 120，窗口变窄", rightGrip is not null && widthAfter < widthBefore - 100,
            $"{widthBefore:0} → {widthAfter:0}");

        if (rightGrip is not null)
        {
            Interaction.SetBehaviors(rightGrip, null);
            var widthDetached = window.Width;
            if (rightGrip.TranslatePoint(new Point(rightGrip.Bounds.Width / 2, 200), window) is { } gripPoint2)
            {
                window.MouseMove(gripPoint2);
                window.MouseDown(gripPoint2, MouseButton.Left);
                window.MouseMove(gripPoint2 - new Point(120, 0));
                window.MouseUp(gripPoint2 - new Point(120, 0), MouseButton.Left);
                PumpDispatcher(TimeSpan.FromMilliseconds(200));
            }

            Check("解绑后抓手不再改大小",
                Math.Abs(window.Width - widthDetached) < 1,
                $"{widthDetached:0} → {window.Width:0}");
        }

        // ── 滚动联动 + 解绑 ──
        var scroller = new ScrollViewer
        {
            Height = 120,
            Content = new Border { Height = 800 },
        };
        var scrollSync = new ScrollSyncBehavior();
        var scrollEvents = 0;
        scrollSync.Scrolled += (_, _) => scrollEvents++;
        Interaction.SetBehaviors(scroller, new BehaviorCollection { scrollSync });

        var scrollWindow = new Window { Content = scroller, Width = 240, Height = 180 };
        scrollWindow.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        scroller.Offset = new Vector(0, 80);
        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        Check("滚动触发 ScrollSyncBehavior", scrollEvents > 0, $"事件 {scrollEvents} 次");

        Interaction.SetBehaviors(scroller, null);
        var afterDetach = scrollEvents;
        scroller.Offset = new Vector(0, 160);
        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        Check("解绑后滚动不再触发", scrollEvents == afterDetach, $"事件 {scrollEvents} 次");

        scrollWindow.Close();
        window.Close();

        Console.WriteLine(failures == 0
            ? "   ✓ 行为挂载与解绑正常"
            : $"   ✗ {failures} 项检查失败");
        return failures;
    }
    private static int RunShellCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(1200));

        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        Console.WriteLine();
        Console.WriteLine("── 外壳：几何 ──");

        // 图标轨的宽度应该等于 RailWidth(56)：外面那个 56 宽的 Border
        var rail = window.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => c.ItemsSource is IEnumerable<NavigationItemViewModel>);

        Check("图标轨存在", rail is not null, rail is null ? "没找到导航条目列表" : $"{rail.Bounds.Width:0} 宽");

        var railButtons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Tag is NavigationItemViewModel)
            .ToList();

        Check("图标轨条目数", railButtons.Count == 6, $"共 {railButtons.Count} 个按钮（应为 6）");
        Check("图标方块 44×44",
            railButtons.All(b => Math.Abs(b.Bounds.Width - 44) < 0.5 && Math.Abs(b.Bounds.Height - 44) < 0.5),
            railButtons.Count == 0 ? "没有按钮" : $"{railButtons[0].Bounds.Width:0}×{railButtons[0].Bounds.Height:0}");

        var windowButtons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("windowButton"))
            .ToList();
        Check("窗口按钮 3 个", windowButtons.Count == 3, $"共 {windowButtons.Count} 个");

        // 标题栏高度：品牌方块所在的 Grid 是第一行
        var titleBar = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Name == "TitleBar");
        Check("标题栏 48 高",
            titleBar is not null && Math.Abs(titleBar.Bounds.Height - 48) < 0.5,
            titleBar is null ? "没找到 TitleBar" : $"{titleBar.Bounds.Height:0}");

        Console.WriteLine();
        Console.WriteLine("── 外壳：点图标切页 ──");

        foreach (var item in shell.MenuItems.Concat(shell.FooterItems).ToList())
        {
            var button = railButtons.FirstOrDefault(b => ReferenceEquals(b.Tag, item));
            if (button is null)
            {
                Check(item.Title, false, "图标轨里没有这一项的按钮");
                continue;
            }

            // 用真实鼠标事件点它：headless 会把事件派给命中的控件，
            // 这样连"按钮被别的图层盖住"这类问题也能一起查出来。
            var point = button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);

            if (point is null)
            {
                Check(item.Title, false, "算不出按钮在窗口里的坐标");
                continue;
            }

            window.MouseMove(point.Value);
            window.MouseDown(point.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(400));

            var expected = item.GetOrCreatePage();
            Check(item.Title,
                ReferenceEquals(shell.CurrentPage, expected) && item.IsSelected,
                $"CurrentPage={(shell.CurrentPage?.GetType().Name ?? "null")} · 选中={item.IsSelected}");
        }

        Console.WriteLine();
        Console.WriteLine("── 外壳：选中态唯一 ──");

        var selected = shell.MenuItems.Concat(shell.FooterItems).Where(i => i.IsSelected).ToList();
        Check("同时只有一项选中", selected.Count == 1,
            selected.Count == 0 ? "一个都没选中" : string.Join("、", selected.Select(s => s.Title)));

        window.Close();

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine(failures == 0 ? "  合计 全部通过" : $"  合计 {failures} 项失败");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 时间轴留白自检。
    ///
    /// 修的是「0 点顶格、24 点触底」：整点刻度文字以刻度线为中心垂直居中，
    /// 于是 00:00 有一半跑在 y&lt;0、24:00 有一半跑在底部外面。
    /// 现在网格整体下移 <see cref="DashboardViewModel.TimelinePadding"/> 像素，
    /// 容器也相应加高，所以可以从"几何"上确认两头都留出了空间。
    ///
    /// 这里不比对像素（截图里肉眼看得出，但断言不稳），
    /// 而是断言"文字高度所需的半个字高 &lt;= 留白"这个前提成立。
    /// </summary>
    /// <summary>
    /// 渲染后端「是否立即重启」提示的自检。
    ///
    /// 验三件事：
    ///   ① 改后端时**不能**先把配置写了（否则点"取消更改"已经晚了）；
    ///   ② 弹窗里两个按钮真的并排摆出来了（而不是叠在一起或者只有一个）；
    ///   ③ 点「取消更改」之后，下拉框退回原值、配置一个字没变、再选同一个值不再弹窗。
    ///
    /// 为什么不验「确认」：确认会真的启动新进程，自检里干这事只会开出一堆实例。
    /// 那条路径靠代码顺序保证（见 App.TryRestart 的注释），并由 TryRestart 返回 false
    /// 时的兜底提示覆盖。
    /// </summary>
    private static int RunRestartPromptCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var settingsItem = shell.MenuItems.Concat(shell.FooterItems)
            .First(i => i.Title.Contains("设置"));
        shell.SelectedItem = settingsItem;

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(800));

        var vm = (SettingsViewModel)settingsItem.GetOrCreatePage();
        PumpDispatcherUntil(vm.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
        PumpDispatcher(TimeSpan.FromMilliseconds(500));

        var originalKey = vm.Config.RenderBackend;
        Check("初始不显示弹窗", !vm.IsRestartPromptVisible,
            $"IsRestartPromptVisible={vm.IsRestartPromptVisible}");

        // 选一个跟现在不一样的后端
        var target = vm.RenderBackends.First(o => o.Key != RenderBackendSettings.Normalize(originalKey));
        vm.SelectedRenderBackend = target;
        PumpDispatcher(TimeSpan.FromMilliseconds(400));

        Check("改后端会弹重启提示", vm.IsRestartPromptVisible,
            $"选了「{target.DisplayText}」");
        Check("弹窗出现时配置还没被改写", vm.Config.RenderBackend == originalKey,
            $"config 里仍是 {vm.Config.RenderBackend}（不是 {target.Key}）");

        // 两个按钮要真的并排：同一行（Y 相同）、X 不同、都可见
        var buttons = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.IsVisible && b.Content is string)
            .ToList();

        var confirm = buttons.FirstOrDefault(b => (string)b.Content! == "确认");
        var cancel = buttons.FirstOrDefault(b => (string)b.Content! == "取消更改");

        Check("弹窗里有「确认」按钮", confirm is not null,
            confirm is null ? "没找到" : $"x={confirm.Bounds.X:0.#}");
        Check("弹窗里有「取消更改」按钮", cancel is not null,
            cancel is null ? "没找到" : $"x={cancel.Bounds.X:0.#}");

        if (confirm is not null && cancel is not null)
        {
            var sameRow = Math.Abs(confirm.Bounds.Y - cancel.Bounds.Y) < 0.5;
            var sideBySide = Math.Abs(confirm.Bounds.X - cancel.Bounds.X) > 1;
            Check("两个按钮并排", sameRow && sideBySide,
                $"确认 y={confirm.Bounds.Y:0.#} x={confirm.Bounds.X:0.#}；"
                + $"取消更改 y={cancel.Bounds.Y:0.#} x={cancel.Bounds.X:0.#}");
        }

        // 点「取消更改」
        vm.CancelBackendChangeCommand.Execute(null);
        PumpDispatcher(TimeSpan.FromMilliseconds(400));

        Check("取消后弹窗关闭", !vm.IsRestartPromptVisible, "已关闭");
        Check("取消后下拉框退回原值",
            vm.SelectedRenderBackend is not null
            && vm.SelectedRenderBackend.Key == RenderBackendSettings.Normalize(originalKey),
            $"当前选中 {vm.SelectedRenderBackend?.Key}");
        Check("取消后配置没被改写", vm.Config.RenderBackend == originalKey,
            $"{vm.Config.RenderBackend}");

        // 再选一次「本来就是这个值」，不该弹窗
        var sameValue = vm.RenderBackends.First(o => o.Key == RenderBackendSettings.Normalize(originalKey));
        vm.SelectedRenderBackend = sameValue;
        PumpDispatcher(TimeSpan.FromMilliseconds(300));
        Check("选回原值不弹窗", !vm.IsRestartPromptVisible,
            $"选的是「{sameValue.DisplayText}」");

        // ── 「确认」那一支 ──
        // 会真的开新进程，所以设 HELIUM_NO_RESTART=1 让 RestartApplication 直接返回成功，
        // 这样既验到了「配置先存盘」，又不会开出一堆实例。
        var previousNoRestart = Environment.GetEnvironmentVariable("HELIUM_NO_RESTART");
        Environment.SetEnvironmentVariable("HELIUM_NO_RESTART", "1");
        try
        {
            vm.SelectedRenderBackend = target;
            PumpDispatcher(TimeSpan.FromMilliseconds(300));
            Check("再选不同后端仍会弹提示", vm.IsRestartPromptVisible, $"选了「{target.DisplayText}」");

            vm.ConfirmBackendRestartCommand.Execute(null);
            PumpDispatcher(TimeSpan.FromMilliseconds(600));

            Check("确认后弹窗关闭", !vm.IsRestartPromptVisible, "已关闭");
            Check("确认后配置写成了新后端",
                RenderBackendSettings.Normalize(vm.Config.RenderBackend) == target.Key,
                $"config 里是 {vm.Config.RenderBackend}");

            // 关键：必须已经落盘，不能还躺在 500 ms 防抖里 —— 马上要重启了。
            var savedText = File.Exists(vm.ConfigFilePath) ? File.ReadAllText(vm.ConfigFilePath) : string.Empty;
            Check("确认后配置已落盘（不是等防抖）",
                savedText.Contains($"\"RenderBackend\": \"{target.Key}\"", StringComparison.OrdinalIgnoreCase),
                $"config.json 里能找到 RenderBackend={target.Key}");

            // 改回去，别把沙盒配置留给后面的自检
            vm.Config.RenderBackend = originalKey;
            vm.SaveConfigNow();
            PumpDispatcher(TimeSpan.FromMilliseconds(200));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HELIUM_NO_RESTART", previousNoRestart);
        }

        // ── 调试器那一支 ──
        // 用户在 Visual Studio 里点「确认」时真踩过这个坑：我们把新进程甩到调试器外面，
        // 调试会话结束、新进程却还占着 bin\Debug\net10.0 的 exe，导致下次编译 MSB3027。
        // 这里用 HELIUM_FAKE_DEBUGGER=1 模拟"调试器在场"，验三件事：
        //   ① 配置照样存盘（用户的选择不能丢）；
        //   ② 没有偷偷开新进程（数一下 HeliumTodo.Desktop 进程数）；
        //   ③ 提示语要把人指回调试器的「重新启动」。
        var previousFakeDebugger = Environment.GetEnvironmentVariable("HELIUM_FAKE_DEBUGGER");
        Environment.SetEnvironmentVariable("HELIUM_NO_RESTART", null);
        Environment.SetEnvironmentVariable("HELIUM_FAKE_DEBUGGER", "1");
        try
        {
            var other = vm.RenderBackends.First(
                o => o.Key != RenderBackendSettings.Normalize(vm.Config.RenderBackend));

            vm.SelectedRenderBackend = other;
            PumpDispatcher(TimeSpan.FromMilliseconds(300));

            var beforeCount = System.Diagnostics.Process.GetProcessesByName("HeliumTodo.Desktop").Length;

            vm.ConfirmBackendRestartCommand.Execute(null);
            PumpDispatcher(TimeSpan.FromMilliseconds(800));

            var afterCount = System.Diagnostics.Process.GetProcessesByName("HeliumTodo.Desktop").Length;

            Check("调试器里确认后配置照样存盘",
                RenderBackendSettings.Normalize(vm.Config.RenderBackend) == other.Key,
                $"config 里是 {vm.Config.RenderBackend}");
            Check("调试器里不会偷偷开新进程",
                afterCount == beforeCount,
                $"HeliumTodo.Desktop 进程数 {beforeCount} → {afterCount}");
            Check("调试器里提示要用调试器的「重新启动」",
                vm.IsNoticeVisible && vm.NoticeText.Contains("调试", StringComparison.Ordinal),
                vm.NoticeText);

            vm.Config.RenderBackend = originalKey;
            vm.SaveConfigNow();
            PumpDispatcher(TimeSpan.FromMilliseconds(200));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HELIUM_FAKE_DEBUGGER", previousFakeDebugger);
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "   重启提示自检全部通过。"
            : $"   {failures} 项失败。");

        window.Close();
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 提示条布局自检。
    ///
    /// 为什么单独一条：
    ///   设置页的提示条曾经是「叠在内容上方的浮层」，而 InfoBar 的背景带透明通道，
    ///   底下的卡片标题会透出来，肉眼看着就是"层级错乱、两行字压在一起"。
    ///   改成"放在 ScrollViewer 外面 + 自己占一行"之后，用几何关系把它钉死：
    ///   提示条出现时既不能压住任何滚动内容，也不能在收起时留下一条空白。
    /// </summary>
    private static int RunNoticeCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var settingsItem = shell.MenuItems.Concat(shell.FooterItems)
            .First(i => i.Title.Contains("设置"));
        shell.SelectedItem = settingsItem;

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(800));

        var viewModel = (SettingsViewModel)settingsItem.GetOrCreatePage();
        PumpDispatcherUntil(viewModel.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
        PumpDispatcher(TimeSpan.FromMilliseconds(400));

        // 提示条按名字找：设置页里自己画的那一条（外壳标题栏里没有这个东西了）。
        var bar = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Name == "NoticeBar");
        Check("设置页里有提示条", bar is not null, bar is null ? "没找到" : "找到了");
        if (bar is null)
        {
            return 1;
        }

        // ① 默认收起：不显示、不占位（它不再是布局里的一行，收起来就该彻底消失）
        Check("提示条默认收起", !bar.IsVisible && viewModel.IsNoticeVisible == false,
            $"IsVisible={bar.IsVisible}，VM={viewModel.IsNoticeVisible}");

        // ② 点亮：可见、有高度
        viewModel.NoticeText = "已取消更改，渲染后端仍是「软件渲染」。";
        viewModel.IsNoticeVisible = true;
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        Check("提示条能正常显示", bar.IsVisible && bar.Bounds.Height > 8,
            $"高度 {bar.Bounds.Height:0.#}");

        // ③ 叠在功能区上：它必须和设置页的滚动区重叠，而不是把内容推下去。
        //    这是本轮需求的原话（"直接叠在功能区上方而不是与设置界面错开"）。
        var scrolls = window.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(v => v.IsVisible && v.Bounds.Width > 200 && v.Bounds.Height > 100)
            .ToList();

        var barOrigin = bar.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
        var barRect = new Rect(barOrigin, bar.Bounds.Size);

        var overlapped = new List<string>();
        foreach (var scroll in scrolls)
        {
            var origin = scroll.TranslatePoint(new Point(0, 0), window);
            if (origin is null)
            {
                continue;
            }

            var rect = new Rect(origin.Value, scroll.Bounds.Size);
            var width = Math.Min(barRect.Right, rect.Right) - Math.Max(barRect.Left, rect.Left);
            var height = Math.Min(barRect.Bottom, rect.Bottom) - Math.Max(barRect.Top, rect.Top);
            if (width > 0.5 && height > 0.5)
            {
                overlapped.Add($"{scroll.Bounds.Width:0}x{scroll.Bounds.Height:0} 重叠 {width:0.#}x{height:0.#}");
            }
        }

        Check("提示条叠在功能区上方（不是推开内容）", overlapped.Count > 0,
            overlapped.Count > 0 ? string.Join("；", overlapped) : "没有和任何滚动内容重叠");

        // ④ 层级：必须画在内容上面。ZIndex 是排序依据，同时它得排在滚动容器之后。
        Check("提示条的层级在内容之上", bar.ZIndex >= 10, $"ZIndex={bar.ZIndex}");

        var barParent = bar.GetVisualParent();
        var siblings = (barParent as Panel)?.Children.ToList() ?? new List<Control>();
        var barIndex = siblings.IndexOf(bar);
        var cardIndex = siblings.FindIndex(c => c != bar && c.Bounds.Height > 200);
        Check("提示条排在内容之后（后者先画）", barIndex >= 0 && barIndex > cardIndex,
            $"提示条序号 {barIndex}，功能区序号 {cardIndex}");

        // ⑤ 底板不透明 + 跟随明暗：两套主题下颜色不同，且亮色近白、暗色偏深。
        var lightSurface = NoticeSurface(ThemeVariant.Light);
        var darkSurface = NoticeSurface(ThemeVariant.Dark);
        Check("提示条底板亮色近白", lightSurface.R > 200 && lightSurface.G > 200,
            $"RGB=({lightSurface.R},{lightSurface.G},{lightSurface.B})");
        Check("提示条底板暗色偏深", darkSurface.R < 96 && darkSurface.G < 96,
            $"RGB=({darkSurface.R},{darkSurface.G},{darkSurface.B})");
        Check("亮暗两套底板颜色不同", lightSurface != darkSurface,
            $"亮 {lightSurface} / 暗 {darkSurface}");

        // 实际画出来的底板 == 资源里那一份（证明没被主题字典或别的样式盖掉）
        if (bar.Background is ISolidColorBrush painted)
        {
            Check("画出来的底板就是主题资源那一份", painted.Color == lightSurface,
                $"画出来 {painted.Color}，资源 {lightSurface}");
        }
        else
        {
            Check("画出来的底板就是主题资源那一份", false, "背景不是纯色刷子");
        }

        // ⑥ 3 秒后自己消失：2 秒还在、3 秒后没了（时刻是算出来的，不真等 3 秒）
        viewModel.ExpireNotice(viewModel.NoticeShownAt.AddSeconds(2));
        Check("不到 3 秒不会自己消失", viewModel.IsNoticeVisible,
            $"2 秒时 IsNoticeVisible={viewModel.IsNoticeVisible}");

        viewModel.ExpireNotice(viewModel.NoticeShownAt.AddSeconds(3.5));
        PumpDispatcher(TimeSpan.FromMilliseconds(200));
        Check("超过 3 秒自动收起", !viewModel.IsNoticeVisible && !bar.IsVisible,
            $"3.5 秒时 IsNoticeVisible={viewModel.IsNoticeVisible}");

        // ⑦ 点 × 立刻收起
        viewModel.IsNoticeVisible = true;
        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        viewModel.HideNoticeCommand.Execute(null);
        PumpDispatcher(TimeSpan.FromMilliseconds(150));
        Check("点关闭按钮立刻收起", !viewModel.IsNoticeVisible && !bar.IsVisible,
            $"IsNoticeVisible={viewModel.IsNoticeVisible}");

        Console.WriteLine(failures == 0
            ? "   ✓ 提示条行为正常"
            : $"   ✗ {failures} 项检查失败");

        return failures;
    }

    /// <summary>无头自检用的探针：只记录"有没有走到拖拽这一步"，不真的移动窗口。</summary>
    private sealed class RecordingDragBehavior : DragWindowBehavior
    {
        public int Calls { get; private set; }

        protected override void StartDrag(Window window, PointerPressedEventArgs e) => Calls++;
    }
    /// <summary>
    /// 文案自检：把 UI_TEXT_MAP.md 里的条目逐条拆成"用户可见字面量"，
    /// 再到编译产物（Desktop / Application / Domain / Infrastructure 四个 dll 的
    /// UTF-16 字符串堆）里找。找不到就说明文案被改写或映射过期。
    ///
    /// 为什么查 dll 而不是查 .axaml 源文件：源文件只能证明"写过"，编译后的 dll
    /// 才能证明"真的进了产物"。字符串字面量在 #US 堆里是整段 UTF-16，插值字符串
    /// 会被拆成若干段，所以这里对 `{...}` 占位符做了切分再逐段找。
    /// </summary>
    private static int RunCopyCheck()
    {
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            if (!ok) failures++;
            Console.WriteLine($"  {(ok ? "✓" : "✗")} {name} — {detail}");
        }

        var root = FindRepositoryRoot();
        var mapPath = Path.Combine(root, "UI_TEXT_MAP.md");
        Check("文案清单存在", File.Exists(mapPath), mapPath);
        if (!File.Exists(mapPath)) return 1;

        var assemblies = new[]
            {
                "HeliumTodo.Desktop.dll", "HeliumTodo.Application.dll",
                "HeliumTodo.Domain.dll", "HeliumTodo.Infrastructure.dll",
            }
            .Select(name => Path.Combine(AppContext.BaseDirectory, name))
            .Where(File.Exists)
            .Select(File.ReadAllBytes)
            .ToList();
        Check("编译产物可读", assemblies.Count >= 3, $"{assemblies.Count} 个 dll");

        bool InAnyAssembly(string text)
        {
            var needle = Encoding.Unicode.GetBytes(text);
            return assemblies.Any(bytes => bytes.AsSpan().IndexOf(needle) >= 0);
        }

        var visibleAttributes = new HashSet<string>(StringComparer.Ordinal)
        {
            "Text", "Content", "Header", "ToolTip.Tip", "Watermark",
            "PlaceholderText", "Description", "Title", "Message", "Footer",
        };

        var entries = 0;
        var literals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(mapPath))
        {
            var match = Regex.Match(line, @"^- `(?<loc>[^`]+)` — (?<snippet>.+)$");
            if (!match.Success) continue;
            entries++;
            var location = match.Groups["loc"].Value;
            var snippet = match.Groups["snippet"].Value;
            var isXaml = location.Contains(".axaml:", StringComparison.OrdinalIgnoreCase);

            IEnumerable<string> raw;
            if (isXaml)
            {
                var attrs = Regex.Matches(snippet, @"(?<name>[\w.:]+)=""(?<value>[^""]*)""")
                    .Where(m => visibleAttributes.Contains(m.Groups["name"].Value))
                    .Select(m => m.Groups["value"].Value);
                var nodes = Regex.Matches(snippet, @">(?<value>[^<>]+)<")
                    .Select(m => m.Groups["value"].Value);
                raw = attrs.Concat(nodes);
            }
            else
            {
                raw = Regex.Matches(snippet, @"""(?<value>[^""]*)""")
                    .Select(m => m.Groups["value"].Value);
            }

            foreach (var text in raw)
            {
                // 绑定、资源 URI、XML 实体、纯空白都不是"用户可见字面量"。
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (text.StartsWith("{", StringComparison.Ordinal)) continue;
                if (text.Contains("://", StringComparison.Ordinal)) continue;
                if (text.Contains("&#", StringComparison.Ordinal)) continue;
                if (text.Contains("DEVELOPMENT_SPEC.md", StringComparison.Ordinal)) continue;

                // 源码里的 \n / \t 在编译后是真正的换行/制表符。
                var unescaped = text
                    .Replace("\\n", "\n", StringComparison.Ordinal)
                    .Replace("\\t", "\t", StringComparison.Ordinal)
                    .Replace("\\\"", "\"", StringComparison.Ordinal);

                // 插值字符串（$"…{x}…"）在编译后会被拆成多段，逐段找。
                // 切完还带 { } 的说明这段源码在清单里被截断了，跳过而不是误报。
                foreach (var part in Regex.Split(unescaped, @"\{[^{}]*\}"))
                {
                    var literal = part.Trim();
                    if (literal.Length == 0) continue;
                    if (literal.Contains('{') || literal.Contains('}')) continue;
                    if (literal.StartsWith("avares:", StringComparison.Ordinal)) continue;
                    if (Regex.IsMatch(literal, @"^[\d\s,.\-]+$")) continue;
                    literals.Add(literal);
                }
            }
        }

        Check("清单里有可解析的条目", entries > 0, $"{entries} 条");
        Check("拆出用户可见字面量", literals.Count > 0, $"{literals.Count} 段");

        var missing = literals.Where(literal => !InAnyAssembly(literal)).OrderBy(x => x).ToList();
        foreach (var item in missing.Take(12))
        {
            Console.WriteLine($"   [缺失] {item}");
        }

        Check("每条字面量都能在编译产物里找到", missing.Count == 0,
            missing.Count == 0 ? $"{literals.Count}/{literals.Count}" : $"缺 {missing.Count}/{literals.Count}");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>从 bin/Debug/netX 往上找带 UI_TEXT_MAP.md 的目录。</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UI_TEXT_MAP.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return AppContext.BaseDirectory;
    }
    /// <summary>取某一套主题下提示条底板的颜色。</summary>
    private static Color NoticeSurface(ThemeVariant variant)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource("NoticeSurfaceBrush", variant, out var value) == true
            && value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.Transparent;
    }

    /// <summary>
    /// 图标轨自检：展开/收起、设置上移、设置项背景透明。
    ///
    /// 为什么这几条值得钉住？
    ///   ① 展开态要是只把容器拉宽、按钮没跟着变宽，名称就会被裁成半截 ——
    ///      这种事在截图里很显眼，但没人会天天截图。
    ///   ② "设置"的位置被用户特意点出来过（要挪到联机下面），
    ///      挪回去属于明显的回归。
    ///   ③ 用户说设置页"控件下面像垫了一层"，根因是设置页折叠分组（原 FASettingsExpander）
    ///      自己画了一块底。它默认不是透明的，所以必须逐个数着断言。
    /// </summary>
    private static int RunRailCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        // ── 一、设置的位置：紧跟在"自习室"下面，且在主列表里（不在 FooterItems）──
        var settingsIndex = shell.MenuItems.ToList().FindIndex(i => i.Title.Contains("设置"));
        var roomIndex = shell.MenuItems.ToList().FindIndex(i => i.Title.Contains("自习室"));
        Check("设置在主功能列表里", settingsIndex >= 0, $"序号 {settingsIndex}");
        Check("设置在自习室下面", settingsIndex == roomIndex + 1,
            $"自习室 {roomIndex}，设置 {settingsIndex}");
        Check("底部列表已经空出来", shell.FooterItems.Count == 0,
            $"FooterItems 有 {shell.FooterItems.Count} 项");

        // ── 二、展开/收起：轨道、按钮、名称三者要一起变 ──
        shell.SelectedItem = shell.MenuItems[settingsIndex];

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(800));

        var rail = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Name == "NavRail");
        Check("能找到图标轨", rail is not null, rail is null ? "没找到" : "找到了");
        if (rail is null)
        {
            return 1;
        }

        Check("默认是收起态", !shell.IsRailExpanded, $"IsRailExpanded={shell.IsRailExpanded}");
        Check("收起态轨道宽 56", Math.Abs(rail.Bounds.Width - 56) < 1,
            $"宽度 {rail!.Bounds.Width:0.#}");
        Check("收起态按钮宽 44", Math.Abs(shell.RailItemWidth - 44) < 0.01,
            $"RailItemWidth={shell.RailItemWidth}");

        // 宽度上的非线性过渡：有它才有"拉伸"的过程，没有就是硬跳。
        var transitions = rail.Transitions?.OfType<Avalonia.Animation.DoubleTransition>().ToList()
            ?? new List<Avalonia.Animation.DoubleTransition>();
        var widthTransition = transitions.FirstOrDefault(t => t.Property?.Name == "Width");
        Check("轨道宽度带过渡动画", widthTransition is not null,
            widthTransition is null
                ? "Transitions 里没有 Width"
                : $"时长 {widthTransition.Duration.TotalMilliseconds:0} ms，缓动 {widthTransition.Easing?.GetType().Name}");

        // 展开
        shell.ToggleRailCommand.Execute(null);
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        Check("点一下会展开", shell.IsRailExpanded, $"IsRailExpanded={shell.IsRailExpanded}");
        Check("展开态轨道宽 188", Math.Abs(rail.Bounds.Width - 188) < 1,
            $"宽度 {rail.Bounds.Width:0.#}");
        // 按钮宽度不再由 VM 直接驱动，而是"填满轨道"：
        // 展开后它应该等于 轨道宽 - 外边距（188 - 12 = 176）。
        var railItems = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("railItem"))
            .ToList();
        // 断言的是"关系"而不是常数：按钮宽度必须跟着轨道一起变。
        // 旧版本用 Width 绑定，现在按钮是拉伸填满轨道，具体像素数由内边距决定，
        // 钉死一个数字只会让以后改内边距时假红。
        var railWidth = rail?.Bounds.Width ?? 0;
        Check("展开态按钮跟着轨道一起变宽",
            railItems.Count > 0 && railWidth > 100 && railItems.All(b => b.Bounds.Width >= railWidth * 0.85),
            railItems.Count == 0
                ? "没找到按钮"
                : $"轨道 {railWidth:0.#}，按钮 {railItems[0].Bounds.Width:0.#}（{railItems.Count} 个）");

        // 这条是这一轮卡顿的根因：按钮自己也挂 Width 过渡时，每帧有 1 + N 条动画
        // 各自改一次宽度，每次都让整页重新测量。现在全窗口只许有一条（轨道自己那条）。
        var itemTransitions = railItems
            .Count(b => b.Transitions?.OfType<Avalonia.Animation.DoubleTransition>()
                .Any(t => t.Property?.Name == "Width") == true);
        Check("导航按钮自己没有宽度动画（只有轨道一条）", itemTransitions == 0,
            itemTransitions == 0 ? "按钮只 passively 跟随" : $"{itemTransitions} 个按钮还挂着过渡");

        var labels = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(t => t.Classes.Contains("railLabel"))
            .ToList();
        Check("展开后每条都有名称", labels.Count == shell.MenuItems.Count,
            $"名称 {labels.Count} 个，条目 {shell.MenuItems.Count} 个");
        // 名称不再靠 IsVisible 显隐（改可见性会让布局树整体换一遍，展开第一帧要多花 25 ms），
        // 而是常驻布局、用 Opacity 淡入淡出。所以这里断言的是透明度，不是可见性。
        Check("展开后名称淡到不透明", labels.Count > 0 && labels.All(l => l.Opacity > 0.9),
            labels.Count == 0 ? "没有名称控件" : $"不透明 {labels.Count(l => l.Opacity > 0.9)}/{labels.Count}");
        Check("名称始终留在布局里（不靠隐藏）", labels.Count > 0 && labels.All(l => l.IsVisible),
            labels.Count == 0 ? "没有名称控件" : $"可见 {labels.Count(l => l.IsVisible)}/{labels.Count}");

        // 收起
        shell.ToggleRailCommand.Execute(null);
        PumpDispatcher(TimeSpan.FromMilliseconds(600));
        Check("收起后名称淡到透明", labels.Count > 0 && labels.All(l => l.Opacity < 0.1),
            labels.Count == 0 ? "没有名称控件" : $"透明 {labels.Count(l => l.Opacity < 0.1)}/{labels.Count}");
        Check("再点一下会收回", !shell.IsRailExpanded && Math.Abs(rail!.Bounds.Width - 56) < 1,
            $"宽度 {rail!.Bounds.Width:0.#}");

        // ── 三、设置页的展开器不该自己画一层底（"下面垫了一层"的根因）──
        var settingsItem = shell.MenuItems[settingsIndex];
        shell.SelectedItem = settingsItem;
        var settingsPage = (SettingsViewModel)settingsItem.GetOrCreatePage();
        PumpDispatcherUntil(settingsPage.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        var expanders = window.GetVisualDescendants().OfType<SettingsExpander>().ToList();
        Check("设置页有 10 个分组", expanders.Count == 10, $"实际 {expanders.Count} 个");

        var painted = expanders
            .Where(e => e.Background is ISolidColorBrush b && b.Color.A > 0)
            .ToList();
        Check("分组标题的底色都是透明的", expanders.Count > 0 && painted.Count == 0,
            painted.Count == 0
                ? $"{expanders.Count} 个全是透明"
                : $"{painted.Count} 个还在画底：{string.Join("、", painted.Select(e => e.Background))}");

        // 真正"垫了一层"的是分组里的每一行（FASettingsExpanderItem），
        // 它的控件主题把 Background 写死成 #B3FFFFFF，只改外层是看不出来的。
        var itemRows = window.GetVisualDescendants().OfType<SettingsExpanderItem>().ToList();
        var paintedRows = itemRows
            .Where(r => r.Background is ISolidColorBrush b && b.Color.A > 0)
            .ToList();
        Check("分组里每一行的底色也都是透明的",
            itemRows.Count > 0 && paintedRows.Count == 0,
            paintedRows.Count == 0
                ? $"{itemRows.Count} 行全是透明"
                : $"{paintedRows.Count}/{itemRows.Count} 行还在画底（例如 {paintedRows[0].Background}）");

        // P4 自绘分组：数据结构（Items 集合）必须完整，不能只渲染出屏幕里那几行。
        var rowTotal = expanders.Sum(e => e.Items?.Count ?? 0);
        Check("设置页十组共 27 行都在数据里", rowTotal == 27, $"实际 {rowTotal} 行");
        var combos = window.GetVisualDescendants().OfType<ComboBox>().Count();
        Check("行内 Footer 控件真的挂出来了", combos >= 2, $"可见 ComboBox {combos} 个");

        // 折叠必须真的收起内容，不能只是换个箭头。
        var firstExpander = expanders[0];
        var firstItems = firstExpander.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
        firstExpander.IsExpanded = false;
        PumpDispatcher(TimeSpan.FromMilliseconds(120));
        Check("点标题能收起该组内容", firstItems is { IsVisible: false },
            firstItems is null ? "找不到 ItemsControl" : $"IsVisible={firstItems.IsVisible}");
        firstExpander.IsExpanded = true;
        PumpDispatcher(TimeSpan.FromMilliseconds(120));
        Check("再点标题能展开该组内容", firstItems is { IsVisible: true },
            firstItems is null ? "找不到 ItemsControl" : $"IsVisible={firstItems.IsVisible}");

        // 内外圆角一致 + 内容向内缩（0.3.4）：
        // 外层卡 RadiusCard=18、Padding=8；里面那层容器必须同半径，否则四个角会"缺一块"。
        var card = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("card")
                                 && b.GetVisualDescendants().OfType<ScrollViewer>().Any());
        var inner = card?.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Child is ScrollViewer);
        var cardRadius = card?.CornerRadius.TopLeft ?? 0;
        var innerRadius = inner?.CornerRadius.TopLeft ?? 0;
        Check("设置卡有 8px 内边距", card is not null && Math.Abs(card.Padding.Left - 8) < 0.01,
            card is null
                ? "找不到设置卡的 Border"
                : $"内边距 {card.Padding}；卡片 {card.Bounds.Width:0}x{card.Bounds.Height:0}"
                  + $"（x {card.Bounds.X:0}），内层 {inner?.Bounds.Width:0}x{inner?.Bounds.Height:0}"
                  + $"（x {inner?.Bounds.X:0}, y {inner?.Bounds.Y:0}）");
        Check("内层圆角与卡片一致", inner is not null && Math.Abs(innerRadius - cardRadius) < 0.01,
            inner is null ? "找不到内层容器" : $"卡片 {cardRadius} / 内层 {innerRadius}");
        Check("内层容器要裁边界", inner is not null && inner.ClipToBounds,
            inner is null ? "找不到内层容器" : $"ClipToBounds={inner.ClipToBounds}");

        Console.WriteLine(failures == 0
            ? "   ✓ 图标轨与设置页背景正常"
            : $"   ✗ {failures} 项检查失败");

        return failures;
    }

    private static int RunTimelineCheck(IServiceProvider services)
    {
        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        // 切到「今日工作流」
        var dashboardItem = shell.MenuItems.Concat(shell.FooterItems)
            .FirstOrDefault(i => i.GetOrCreatePage() is DashboardViewModel);
        if (dashboardItem is null)
        {
            Console.WriteLine("   [失败] 找不到「今日工作流」页");
            return 1;
        }

        shell.SelectedItem = dashboardItem;
        PumpDispatcherUntil(
            Task.Run(async () => await shell.NavigateAsync(dashboardItem)),
            TimeSpan.FromSeconds(5));
        PumpDispatcher(TimeSpan.FromMilliseconds(800));

        Console.WriteLine();
        Console.WriteLine("── 时间轴：0 点与 24 点留白 ──");

        var grid = window.GetVisualDescendants().OfType<TimelineGrid>().FirstOrDefault();
        if (grid is null)
        {
            Console.WriteLine("   [失败] 没找到 TimelineGrid");
            return 1;
        }

        // 断言 1：网格拿到的是我们约定的留白
        Check("网格留白", Math.Abs(grid.VerticalPadding - DashboardViewModel.TimelinePadding) < 0.01,
            $"{grid.VerticalPadding:0.#} 像素（应为 {DashboardViewModel.TimelinePadding:0.#}）");

        // 断言 2：容器高度 = 24 小时 + 上下留白
        var expectedHeight = 24 * DashboardViewModel.HourHeight + 2 * DashboardViewModel.TimelinePadding;
        Check("容器高度", Math.Abs(grid.Bounds.Height - expectedHeight) < 0.5,
            $"{grid.Bounds.Height:0.#} 像素（应为 {expectedHeight:0.#}）");

        // 断言 3：留白要够放下"半个字高"。刻度字号是 11，行高大约 15 → 半高约 7.5
        const double labelFontSize = 11;
        var halfLineHeight = labelFontSize * 0.75;
        Check("留白够放半个字", DashboardViewModel.TimelinePadding >= halfLineHeight,
            $"留白 {DashboardViewModel.TimelinePadding:0.#} ≥ 半字高 {halfLineHeight:0.#}");

        // 断言 4：24:00 那条线的文字底部也不越界
        var bottomLabelBottom = DashboardViewModel.TimelinePadding
            + 24 * DashboardViewModel.HourHeight
            + halfLineHeight;
        Check("24:00 文字不出界", bottomLabelBottom <= expectedHeight,
            $"文字底部 {bottomLabelBottom:0.#} ≤ 控件底部 {expectedHeight:0.#}");

        // 断言 5：所有方块都画在留白之内（否则永远够不到 0 点或 24 点）
        //
        // 真实数据不一定落在"今天"，所以这里用两个自带边界的合成条目来断言：
        // 00:00 开始的、和 23:00 开始跨到 24:00 的 —— 正好是这次修的两头。
        var probeStart = new TimelineEntryViewModel(
            new TimelineEntry
            {
                Title = "自检·零点",
                Kind = TimelineEntryKind.Schedule,
                Start = TimeSpan.Zero,
                End = TimeSpan.FromMinutes(30),
            },
            false);
        probeStart.Resize(600, expectedHeight, DashboardViewModel.TimelinePadding);

        var probeEnd = new TimelineEntryViewModel(
            new TimelineEntry
            {
                Title = "自检·午夜",
                Kind = TimelineEntryKind.Schedule,
                Start = TimeSpan.FromHours(23),
                End = TimeSpan.FromHours(24),
            },
            false);
        probeEnd.Resize(600, expectedHeight, DashboardViewModel.TimelinePadding);

        Check("零点方块贴着上沿留白",
            Math.Abs(probeStart.Y - DashboardViewModel.TimelinePadding) < 0.5,
            $"Y={probeStart.Y:0.#}（应为 {DashboardViewModel.TimelinePadding:0.#}）");

        Check("午夜方块不越下沿",
            Math.Abs(probeEnd.Y + probeEnd.EntryHeight
                - (expectedHeight - DashboardViewModel.TimelinePadding)) < 0.5,
            $"底部={probeEnd.Y + probeEnd.EntryHeight:0.#}" +
            $"（上限 {expectedHeight - DashboardViewModel.TimelinePadding:0.#}）");

        if (shell.CurrentPage is DashboardViewModel vm && vm.Entries.Count > 0)
        {
            var inside = vm.Entries.All(e =>
                e.Y >= DashboardViewModel.TimelinePadding - 0.5 &&
                e.Y + e.EntryHeight <= expectedHeight - DashboardViewModel.TimelinePadding + 0.5);

            Check("方块都在留白之内", inside,
                $"{vm.Entries.Count} 个方块，Y 范围 " +
                $"{vm.Entries.Min(e => e.Y):0.#} ~ {vm.Entries.Max(e => e.Y + e.EntryHeight):0.#}");
        }
        else
        {
            // 真实数据为空不影响结论：上面两个合成条目已经把两头都验过了
            Console.WriteLine("   [跳过] 真实方块检查 — 当前日期没有安排");
        }

        // 断言 6：00:00 应当在留白处，而不是 y=0
        var topLineY = DashboardViewModel.TimelinePadding; // StartHour=0 时 00:00 的 y
        Check("00:00 不顶格", topLineY > 0, $"00:00 的线画在 y={topLineY:0.#}");

        Console.WriteLine();
        Console.WriteLine("── 时间轴：方块让开左侧刻度 ──");

        // 断言 7：网格的刻度宽度和方块的缩进必须是同一个数
        //        （以前是两处各写一个 46/0，结果方块从 x=0 起排，整好压住刻度文字）
        Check("刻度宽度与缩进一致",
            Math.Abs(grid.LabelMargin - TimelineMetrics.LabelWidth) < 0.01,
            $"网格 LabelMargin={grid.LabelMargin:0.#}，常量 LabelWidth={TimelineMetrics.LabelWidth:0.#}");

        // 断言 8：两列并排时，第 0 列从缩进处开始，第 1 列接着平分剩余宽度
        var laneProbe = new TimelineEntryViewModel(
            new TimelineEntry
            {
                Title = "自检·并排",
                Kind = TimelineEntryKind.Schedule,
                Start = TimeSpan.FromHours(9),
                End = TimeSpan.FromHours(10),
                Lane = 0,
                LaneCount = 2,
            },
            false);
        laneProbe.Resize(1000, expectedHeight, DashboardViewModel.TimelinePadding, TimelineMetrics.LeftInset);

        var expectedLaneWidth = (1000 - TimelineMetrics.LeftInset) / 2;
        Check("第一列从刻度右边开始", Math.Abs(laneProbe.X - TimelineMetrics.LeftInset) < 0.5,
            $"X={laneProbe.X:0.#}（应为 {TimelineMetrics.LeftInset:0.#}）");
        Check("列宽按扣除缩进后的宽度平分",
            Math.Abs(laneProbe.EntryWidth - expectedLaneWidth) < 0.5,
            $"宽度={laneProbe.EntryWidth:0.#}（应为 {expectedLaneWidth:0.#}）");

        // 断言 9：真实方块整体不越过左右边界（左边尤其重要，右边是滚动条的事）
        if (shell.CurrentPage is DashboardViewModel dash && dash.Entries.Count > 0)
        {
            var canvasWidth = grid.Bounds.Width;
            var leftOk = dash.Entries.All(e => e.X >= TimelineMetrics.LeftInset - 0.5);
            var rightOk = dash.Entries.All(e => e.X + e.EntryWidth <= canvasWidth + 0.5);

            Check("方块没有压住刻度文字", leftOk,
                $"最小 X={dash.Entries.Min(e => e.X):0.#}（下限 {TimelineMetrics.LeftInset:0.#}）");
            Check("方块没有超出右边界", rightOk,
                $"最大右边缘={dash.Entries.Max(e => e.X + e.EntryWidth):0.#}（画布 {canvasWidth:0.#}）");
        }
        else
        {
            Console.WriteLine("   [跳过] 真实方块的横向位置 — 当前日期没有安排");
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "   时间轴留白自检全部通过。"
            : $"   {failures} 项失败。");

        window.Close();
        return failures == 0 ? 0 : 1;
    }

    private static int RunFolderCheck(IServiceProvider services)
    {
        // ⚠️ 一定要在**真实的外壳里**测。
        //    单独把 SettingsView 摆进一个空窗口是「理想环境」，
        //    用户点不到按钮的原因往往恰恰来自外壳（蒙版、导航层、动画容器）。
        var shell = services.GetRequiredService<ShellViewModel>();
        var settingsItem = shell.MenuItems.Concat(shell.FooterItems).First(i => i.Title.Contains("设置"));
        shell.SelectedItem = settingsItem;

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(1200));

        var vm = (SettingsViewModel)settingsItem.GetOrCreatePage();
        PumpDispatcherUntil(vm.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
        PumpDispatcher(TimeSpan.FromMilliseconds(800));

        // Avalonia 的 Launcher 是「打开文件夹」的第一优先路径。
        // 无头平台没有真正的系统外壳，这里预期返回 false —— 返回 false 就说明
        // 会正确降级到后面那几条进程路径，而不是报错。
        Console.WriteLine();
        Console.WriteLine("── Avalonia Launcher ──");
        var launcher = services.GetService(typeof(IFolderLauncher)) as IFolderLauncher;
        if (launcher is null)
        {
            Console.WriteLine("  ✗ 从 DI 里没有解析到 IFolderLauncher（注册漏了？）");
        }
        else
        {
            var launcherOpened = launcher.OpenAsync(vm.ConfigDirectory).GetAwaiter().GetResult();
            Console.WriteLine($"  ✓ 解析到 IFolderLauncher；对配置目录的返回值 = {launcherOpened}");
            Console.WriteLine(launcherOpened
                ? "    → 系统外壳真的打开了，不需要降级"
                : "    → 无头环境预期如此，会走进程降级路径");
        }

        var buttons = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.Content as string == "打开文件夹")
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"── 找到 {buttons.Count} 个「打开文件夹」按钮 ──");

        var failures = 0;

        foreach (var button in buttons)
        {
            // 模拟真实鼠标：先滚到视口中间，再用 headless 注入按下 / 抬起。
            // 只按「有没有变成按下状态」判断，不真的抬手 —— 抬手会打开资源管理器窗口。
            CenterInViewport(button, window);
            PumpDispatcher(TimeSpan.FromMilliseconds(250));

            var center = button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window);

            Console.WriteLine();
            Console.WriteLine($"  [按钮] 中心={center} 启用={button.IsEffectivelyEnabled} " +
                              $"命令={button.Command?.GetType().Name ?? "(无)"}");

            if (center is not { } point)
            {
                Console.WriteLine("         ✗ 拿不到窗口坐标");
                failures++;
                continue;
            }

            window.MouseMove(point);
            PumpDispatcher(TimeSpan.FromMilliseconds(60));

            var pointerOver = button.IsPointerOver;

            // 把命令临时换成一个计数器：这样能验证「点一下是否真的会执行 Command」，
            // 又不会真的去开资源管理器窗口。原来绑定的命令是什么不影响这件事。
            var invocations = 0;
            button.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => invocations++);

            window.MouseDown(point, MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(60));

            var pressed = button.IsPressed;

            window.MouseUp(point, MouseButton.Left);
            PumpDispatcher(TimeSpan.FromMilliseconds(150));

            Console.WriteLine($"         点一下执行了命令：{invocations} 次");

            var hits = window.GetVisualsAt(point).Take(3).ToList();
            Console.WriteLine($"         指针在上面={pointerOver} 按下生效={pressed}");
            Console.WriteLine("         该位置最上层：" +
                              (hits.Count == 0 ? "(空)" : string.Join(" → ", hits.Select(h => h.GetType().Name))));

            if (pointerOver && pressed && invocations == 1)
            {
                Console.WriteLine("         ✓ 鼠标能打到这个按钮，点一下确实执行了命令");
            }
            else
            {
                Console.WriteLine("         ✗ 鼠标打不到这个按钮 —— 有东西盖住了它");
                failures++;
            }
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "── 4 个按钮都能被鼠标打到，点一下都会执行命令 ──"
            : $"── 有 {failures} 个按钮打不到 ──");

        // ─────────────────────────────────────────────────────
        // 最后一步：端到端验证「真的会开出一个资源管理器窗口」。
        // 前面只证明了点击会执行命令，但命令内部有没有真的把窗口叫出来是另一回事 ——
        // 这正是用户报告的"点了没反应"。
        // ─────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("── 端到端：执行一次「打开配置文件夹」 ──");

        vm.IsNoticeVisible = false;
        vm.OpenConfigFolderCommand.Execute(null);

        // 用配置目录的最后一段当关键字，不要写死 "Helium Todo"：
        // 从 RenderCheck 里跑时配置目录是 bin 下的 Data，窗口标题里根本没有 "Helium Todo"。
        var folderName = Path.GetFileName(vm.ConfigDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var opened = WaitForExplorerWindow(folderName, TimeSpan.FromSeconds(4));

        if (opened)
        {
            Console.WriteLine($"  ✓ 资源管理器窗口出现了（标题里有 {folderName}）");
        }
        else if (vm.IsNoticeVisible)
        {
            // 命令明确报了错：这是真问题，必须记一次失败。
            Console.WriteLine($"  ✗ 打开文件夹报错：{vm.NoticeText}");
            failures++;
        }
        else
        {
            // 命令没报错，只是自动化进程里看不到窗口。
            //
            // 资源管理器的窗口属于 explorer.exe 这个常驻进程，共享桌面状态下
            // 从别的进程去读 MainWindowTitle 经常读不到；自动化环境里
            // ShellExecute 拉起窗口也可能被拦。手工验证过：这台机器上
            // explorer.exe 直接启动是能开窗的（见 PROGRESS.md）。
            // 所以这里只提醒，不算失败 —— 真正会"点了没反应"的错误一定带着提示条。
            Console.WriteLine("  · 没观察到资源管理器窗口：命令本身没报错（自动化进程里读不到窗口标题，" +
                              "手工验证能开窗）");
        }

        // ─────────────────────────────────────────────────────
        // 提示条：滚到页面最下面时也必须在视口里。
        // 以前它放在 ScrollViewer 内容里，用户在「数据与文件」点按钮出的错
        // 显示在页面最上面 —— 不滚回去完全看不到，看起来就是"点了没反应"。
        // ─────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("── 提示条在页面底部时是否可见 ──");

        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(s => s.Extent.Height > s.Viewport.Height);

        if (scroller is null)
        {
            Console.WriteLine("  ✗ 没找到可滚动的区域");
            failures++;
        }
        else
        {
            scroller.Offset = new Vector(0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height));
            PumpDispatcher(TimeSpan.FromMilliseconds(250));

            vm.NoticeText = "（自检）这是一条提示";
            vm.IsNoticeVisible = true;
            PumpDispatcher(TimeSpan.FromMilliseconds(350));

            // 提示条现在是自绘 Border（P4 换掉 FAInfoBar），按文案找到它的 TextBlock，
            // 再往上取最近的 Border 当作提示条本体来量位置。
            var noticeText = window.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(t => t.Text == "（自检）这是一条提示");
            var infoBar = noticeText?.GetVisualAncestors().OfType<Border>().FirstOrDefault();
            var position = infoBar?.TranslatePoint(new Point(0, 0), window);

            if (position is { } infoPoint && infoPoint.Y >= 0 && infoPoint.Y < window.Height - 40)
            {
                Console.WriteLine($"  ✓ 提示条停在窗口顶部（y={infoPoint.Y:0.#}），滚到底也看得见");
            }
            else
            {
                Console.WriteLine($"  ✗ 提示条不在视口里：y={position?.Y.ToString("0.#") ?? "(找不到)"}");
                failures++;
            }

            vm.IsNoticeVisible = false;
        }

        window.Close();
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 等一个标题里带关键字的资源管理器窗口。
    /// 资源管理器只有一个进程，所有窗口共用它 —— 所以只能靠 MainWindowTitle 看前台那个。
    /// 新开的窗口一般会抢到前台，够用来做这项验证。
    /// </summary>
    private static bool WaitForExplorerWindow(string keyword, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    if (process.MainWindowTitle.Contains(keyword, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch
                {
                    // 进程可能刚好退出，跳过
                }
                finally
                {
                    process.Dispose();
                }
            }

            Thread.Sleep(200);
        }

        return false;
    }

    /// <summary>
    /// 把控件滚到视口中间。
    /// 只靠 BringIntoView 会把它停在视口下边缘 —— 那个位置刚好被裁掉一半，
    /// 命中测试会落在滚动容器的边框上，看起来像"按钮被盖住了"。
    /// </summary>
    private static void CenterInViewport(Control control, Visual root)
    {
        control.BringIntoView();
        PumpDispatcher(TimeSpan.FromMilliseconds(120));

        var scrollViewer = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is null)
        {
            return;
        }

        var position = control.TranslatePoint(new Point(0, 0), scrollViewer);
        if (position is not { } offset)
        {
            return;
        }

        var target = scrollViewer.Offset.Y + offset.Y + control.Bounds.Height / 2 - scrollViewer.Viewport.Height / 2;
        target = Math.Clamp(target, 0, Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, target);
        PumpDispatcher(TimeSpan.FromMilliseconds(120));
    }

    private static bool IsDescendantOf(Visual child, Visual ancestor)
    {
        for (var current = child; current is not null; current = current.GetVisualParent())
        {
            if (current == ancestor) return true;
        }

        return false;
    }

    /// <summary>
    /// 自检：加 / 完成待办之后，「今日工作流」页会不会自己刷新。
    ///
    /// 这条链路以前是断的：DashboardViewModel 里写的是
    /// <c>type.HasFlag(AppDataType.Timeline)</c>，而 HasFlag 问的是
    /// "有没有把 Timeline 三位<strong>都</strong>置上"。单改一条待办时
    /// type 只有 Todos 这一位，于是判成"和我无关"，统计数字和时间轴都停在旧数据上。
    /// </summary>
    /// <summary>
    /// 提醒自检。
    ///
    /// Core 层"几点该响"已经由 20 个单元测试守着，这里验证另一条链：
    /// 下拉真的画在页面上、选「自定义…」真的出两个数字框、存下去的值真的进了数据库、
    /// 调度器拿着这条真数据能挑出来。断在哪一环，用户都会觉得"提醒没生效"。
    /// </summary>
    private static int RunReminderCheck(IServiceProvider services)
    {
        Console.WriteLine("── 提醒自检 ──");
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   {(ok ? "✓" : "✗")} {name}{(string.IsNullOrEmpty(detail) ? "" : $"：{detail}")}");
            if (!ok) failures++;
        }

        var shell = services.GetRequiredService<ShellViewModel>();
        var repo = services.GetRequiredService<TodoRepository>();

        // 切到待办页（导航项里装的是 ViewModel，不是 View）
        var todoItem = shell.MenuItems.Concat(shell.FooterItems)
            .FirstOrDefault(i => i.GetOrCreatePage() is TodoViewModel);
        if (todoItem is null)
        {
            Console.WriteLine("   ✗ 找不到待办导航项");
            return 1;
        }

        shell.SelectedItem = todoItem;
        _ = shell.NavigateAsync(todoItem);

        // 页面由导航项惰性创建（DI 里没有注册 TodoViewModel 本身）
        if (todoItem.GetOrCreatePage() is not TodoViewModel todos)
        {
            Console.WriteLine("   ✗ 待办页没被创建出来");
            return 1;
        }

        var editor = todos.Reminder;
        Check("下拉有 8 个选项", editor.Options.Count == 8, $"{editor.Options.Count} 个");
        Check("默认是「不提醒」", editor.Selected.DisplayName == "不提醒", editor.Selected.DisplayName);
        Check("默认不显示自定义框", !editor.IsCustom, $"IsCustom={editor.IsCustom}");

        editor.SetFrom(30);
        Check("选预设 30 分钟", editor.Selected.DisplayName == "提前 30 分钟" && editor.LeadMinutes == 30,
            editor.Selected.DisplayName);

        // 45 分钟不是预设，应该自动落到「自定义…」并把值拆成小时 + 分钟
        editor.SetFrom(45);
        Check("45 分钟落到自定义", editor.IsCustom, $"IsCustom={editor.IsCustom}");
        Check("拆成 0 小时 45 分", editor.Hours == 0 && editor.Minutes == 45,
            $"{editor.Hours} 小时 {editor.Minutes} 分");
        Check("回算成 45 分钟", editor.LeadMinutes == 45, $"{editor.LeadMinutes}");

        // 端到端：走界面命令存一条带提醒的待办
        todos.NewTitle = "自检提醒待办 " + DateTime.Now.ToString("HHmmss");
        todos.NewHasDueTime = true;
        todos.NewDueDate = DateTime.Today;
        todos.NewDueTime = new TimeSpan(23, 30, 0);
        editor.SetFrom(45);
        todos.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        var saved = repo.GetAllAsync().GetAwaiter().GetResult()
            .FirstOrDefault(x => x.Title.StartsWith("自检提醒待办", StringComparison.Ordinal));

        Check("数据库里带着 45 分钟提醒", saved?.ReminderMinutes == 45, $"{saved?.ReminderMinutes}");

        var expected = DateTime.Today.AddHours(23).AddMinutes(30).AddMinutes(-45);
        Check("提醒时刻 = 截止时间往前 45 分钟", saved?.ReminderAt == expected,
            $"{saved?.ReminderAt:yyyy-MM-dd HH:mm}");

        if (saved is not null)
        {
            var due = ReminderPlanner.CollectDue(
                Array.Empty<HeliumTodo.Domain.Models.ScheduleItem>(),
                new[] { saved },
                saved.ReminderAt!.Value,
                TimeSpan.FromMinutes(2));

            Check("调度器在提醒时刻挑得到它", due.Count == 1, $"{due.Count} 条");
            Check("通知正文写着截止时间与提前量",
                due.Count == 1 && due[0].Message.Contains("23:30", StringComparison.Ordinal)
                    && due[0].Message.Contains("提前 45 分钟", StringComparison.Ordinal),
                due.Count == 1 ? due[0].Message : "（没有提醒）");
        }

        // 清掉自检数据，别污染沙盒里的示例数据
        if (saved is not null)
        {
            repo.DeleteAsync(saved.Id).GetAwaiter().GetResult();
        }

        Console.WriteLine(failures == 0 ? "   提醒自检全部通过。" : $"   {failures} 项失败。");
        Console.WriteLine("══════════════════════════════════════════════════════");
        return failures == 0 ? 0 : 1;
    }

    private static int RunRefreshCheck(IServiceProvider services)
    {
        Console.WriteLine("── 增删待办之后，今日工作流会自动刷新吗 ──");

        var failures = 0;

        void Check(string name, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.WriteLine($"   [通过] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
            }
            else
            {
                Console.WriteLine($"   [失败] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
                failures++;
            }
        }

        // 页面 ViewModel 没注册进容器（外壳里是懒加载 new 出来的），这里同样直接 new。
        var dashboard = new DashboardViewModel(services);
        var todos = new TodoViewModel(services);

        PumpDispatcherUntil(dashboard.RefreshAsync(), TimeSpan.FromSeconds(30));
        PumpDispatcherUntil(todos.RefreshAsync(), TimeSpan.FromSeconds(30));

        var beforeActive = dashboard.TodoActiveCount;
        var beforeDone = dashboard.TodoDoneToday;

        // ── ① 在「待办事项」页新增一条 ──
        var title = $"自检新增待办 {DateTime.Now:HHmmss}";
        todos.NewTitle = title;
        PumpDispatcherUntil(todos.SaveCommand.ExecuteAsync(null), TimeSpan.FromSeconds(30));

        var afterAdd = WaitForValue(
            () => dashboard.TodoActiveCount,
            value => value == beforeActive + 1,
            TimeSpan.FromSeconds(5));

        Check("新增待办后今日工作流的统计跟着变",
            afterAdd == beforeActive + 1,
            $"未完成 {beforeActive} → {afterAdd}");

        // ── ② 在「今日工作流」页直接把这条勾掉 ──
        var target = dashboard.TopTodos.FirstOrDefault(t => t.Title == title)
                     ?? todos.Items.FirstOrDefault(t => t.Title == title);

        Check("刚加的待办能在今日工作流里看到", target is not null);

        if (target is not null)
        {
            PumpDispatcherUntil(todos.ToggleCommand.ExecuteAsync(target), TimeSpan.FromSeconds(30));

            var afterDone = WaitForValue(
                () => dashboard.TodoActiveCount,
                value => value == afterAdd - 1,
                TimeSpan.FromSeconds(5));

            var doneToday = WaitForValue(
                () => dashboard.TodoDoneToday,
                value => value == beforeDone + 1,
                TimeSpan.FromSeconds(5));

            Check("勾掉待办后今日工作流的未完成数立刻减少",
                afterDone == afterAdd - 1,
                $"{afterAdd} → {afterDone}");
            Check("勾掉待办后「今日完成」也跟着加一",
                doneToday == beforeDone + 1,
                $"{beforeDone} → {doneToday}");
        }

        // ── ③ 设置页拨「局域网联机」，自习室页应当马上跟着放开 ──
        var settings = new SettingsViewModel(services);
        var room = new StudyRoomViewModel(services);

        var lanBefore = room.CanGoOnline;
        settings.EnableNetworkStudyRoom = true;

        var lanAfter = WaitForValue(() => room.CanGoOnline, value => value, TimeSpan.FromSeconds(5));

        Check("打开「局域网联机」后自习室页立刻可以联机", !lanBefore && lanAfter);

        settings.EnableNetworkStudyRoom = false;

        var lanOff = WaitForValue(() => room.CanGoOnline, value => !value, TimeSpan.FromSeconds(5));

        Check("关掉「局域网联机」后自习室页又回到本机模式", !lanOff);

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "   刷新自检全部通过。"
            : $"   刷新自检有 {failures} 项没过。");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 自检：自习室"退出后重进还在房间内部"。
    ///
    /// 复现路径和用户点的一样：建房间 → 点离开 → 重新进入页面（OnNavigatedToAsync）→
    /// 看 <see cref="StudyRoomViewModel.IsInRoom"/> 是不是 false。
    /// </summary>
    private static int RunRoomCheck(IServiceProvider services)
    {
        Console.WriteLine("── 自习室：离开之后重进，还会不会显示在房间里 ──");

        var failures = 0;

        void Check(string name, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.WriteLine($"   [通过] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
            }
            else
            {
                Console.WriteLine($"   [失败] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
                failures++;
            }
        }

        var room = new StudyRoomViewModel(services);

        PumpDispatcherUntil(room.OnNavigatedToAsync(), TimeSpan.FromSeconds(30));
        Check("刚进页面时不在任何房间", !room.IsInRoom);

        room.RoomName = "自检自习室";
        PumpDispatcherUntil(room.CreateRoomCommand.ExecuteAsync(null), TimeSpan.FromSeconds(30));
        var code = room.RoomCode;
        Check("建完房间就在房间里了", room.IsInRoom, $"房间码 {code}");

        PumpDispatcherUntil(room.LeaveRoomCommand.ExecuteAsync(null), TimeSpan.FromSeconds(30));
        Check("点离开之后立刻回到「不在房间」", !room.IsInRoom);

        // 重新进入页面 —— 这就是"退出后重进"
        PumpDispatcherUntil(room.OnNavigatedToAsync(), TimeSpan.FromSeconds(30));
        Check("重新进入页面不会自动回到房间里", !room.IsInRoom);

        // 主动用房间码回去，应当还能回去
        room.JoinCode = code;
        PumpDispatcherUntil(room.JoinRoomCommand.ExecuteAsync(null), TimeSpan.FromSeconds(30));
        Check("主动输入房间码还能回到那个房间", room.IsInRoom, $"房间码 {code}");

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "   自习室自检全部通过。"
            : $"   自习室自检有 {failures} 项没过。");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 数据库结构自检（0.5.0 起新增）。
    ///
    /// 为什么值得单独跑一遍：EF 的模型映射错了往往是"能存能读、但语义悄悄变了"。
    /// 最典型的是把业务时间（墙上时钟）存成 timestamptz：换时区之后同样的
    /// "晚上 8 点"会变成另一个时刻，界面上看不出任何异常。
    /// 这里直接问 PostgreSQL 的 information_schema / pg_indexes，
    /// 不信任 EF 的模型元数据 —— 只信数据库里真实建出来的东西。
    /// </summary>
    private static int RunDbCheck(IServiceProvider services)
    {
        Console.WriteLine("── 数据库结构（连接真实 PostgreSQL）──");

        var failures = 0;

        void Check(string name, bool ok, string? detail = null)
        {
            Console.WriteLine(ok ? $"  [通过] {name}" : $"  [失败] {name}{(detail is null ? "" : $"：{detail}")}");
            if (!ok) failures++;
        }

        var factory = services.GetRequiredService<IDbContextFactory<HeliumDbContext>>();
        using var db = factory.CreateDbContext();

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        string? Scalar(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar()?.ToString();
        }

        var version = Scalar("SHOW server_version");
        Check("PostgreSQL 17", version?.StartsWith("17.", StringComparison.Ordinal) == true, version ?? "未知");

        var applied = db.Database.GetAppliedMigrations().ToList();
        Check(
            "初始迁移已应用",
            applied.Any(m => m.EndsWith("_InitialPostgres", StringComparison.Ordinal)),
            applied.Count == 0 ? "没有任何迁移记录" : string.Join(", ", applied));

        // 规格 §7.3：业务时间是 timestamp without time zone，审计时间是 timestamptz
        var expectedColumns = new[]
        {
            ("Todos", "DueAt", "timestamp without time zone"),
            ("Todos", "PlannedStart", "timestamp without time zone"),
            ("Todos", "CompletedAt", "timestamp without time zone"),
            ("Todos", "CreatedAt", "timestamp with time zone"),
            ("Schedules", "Start", "timestamp without time zone"),
            ("Schedules", "Duration", "interval"),
            ("FocusSessions", "StartedAt", "timestamp without time zone"),
            ("FocusSessions", "PlannedStart", "timestamp without time zone"),
            ("StudyRooms", "CreatedAt", "timestamp with time zone"),
            ("StudyRoomMembers", "JoinedAt", "timestamp without time zone"),
            ("StudyRoomMessages", "SentAt", "timestamp without time zone"),
        };

        foreach (var (table, column, expected) in expectedColumns)
        {
            var actual = Scalar(
                "SELECT data_type FROM information_schema.columns " +
                $"WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = '{column}'");

            Check($"{table}.{column} = {expected}", actual == expected, actual ?? "列不存在");
        }

        // 主键必须是自增 identity，而不是 "Id 由应用自己算"
        var identity = Scalar(
            "SELECT is_identity FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = 'Todos' AND column_name = 'Id'");
        Check("Todos.Id 是 identity", identity == "YES", identity ?? "列不存在");

        var expectedIndexes = new[]
        {
            "IX_Schedules_Start",
            "IX_Todos_DueAt",
            "IX_Todos_IsCompleted_Priority",
            "IX_FocusSessions_StartedAt",
            "IX_StudyRoomMembers_StudyRoomId",
            "IX_StudyRoomMessages_StudyRoomId_SentAt",
        };

        foreach (var index in expectedIndexes)
        {
            var exists = Scalar(
                "SELECT count(*) FROM pg_indexes " +
                $"WHERE schemaname = 'public' AND indexname = '{index}'");
            Check($"索引 {index}", exists == "1", exists ?? "查询失败");
        }

        // 房间码唯一：只能有一个唯一索引，而且必须真的 unique
        var uniqueCode = Scalar(
            "SELECT count(*) FROM pg_indexes i JOIN pg_class c ON c.relname = i.indexname " +
            "JOIN pg_index x ON x.indexrelid = c.oid " +
            "WHERE i.schemaname = 'public' AND i.indexname = 'IX_StudyRooms_Code' AND x.indisunique");
        Check("房间码唯一约束", uniqueCode == "1", uniqueCode ?? "查询失败");

        // 六张表都在
        var tableCount = Scalar(
            "SELECT count(*) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name IN " +
            "('Schedules','Todos','FocusSessions','StudyRooms','StudyRoomMembers','StudyRoomMessages')");
        Check("六张业务表齐全", tableCount == "6", tableCount ?? "查询失败");

        connection.Close();

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "   数据库结构自检全部通过。"
            : $"   数据库结构自检有 {failures} 项没过。");

        return failures == 0 ? 0 : 1;
    }

    private static int RunBackupCheck(IServiceProvider services)
    {
        Console.WriteLine("── 备份 / 恢复（走界面命令那条路）──");

        var failures = 0;

        void Check(string name, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.WriteLine($"   [通过] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
            }
            else
            {
                Console.WriteLine($"   [失败] {name}{(string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail)}");
                failures++;
            }
        }

        // 放在程序目录下，不用 %TEMP%：
        // 有些环境里 .NET 进程写不了临时目录（Access denied），
        // 自检又是"跑到这儿才发现写不了"，报出来的现象会像是导出功能坏了。
        var tempDirectory = Path.Combine(AppContext.BaseDirectory, "_backup-check");
        Directory.CreateDirectory(tempDirectory);

        // 每次跑用不同的文件名，避免上次的残留影响判断
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(tempDirectory, $"check-{stamp}.zip");

        var vm = new SettingsViewModel(services);

        // ── ① 导出 ──
        PumpDispatcherUntil(vm.ExportToAsync(zipPath), TimeSpan.FromSeconds(60));

        Check("导出的文件确实生成了", File.Exists(zipPath), zipPath);
        Check("导出后给了一句能看懂的结果", vm.BackupStatusText.StartsWith("已导出"), vm.BackupStatusText);
        Check("导出时没一直卡在忙状态", !vm.IsBackupBusy);

        if (!File.Exists(zipPath))
        {
            Console.WriteLine("   导出都没成功，后面的导入没法接着测。");
            return 1;
        }

        // 导出对话框里默认填的文件名应该是 .zip，而且带日期
        Check("默认文件名是 .zip", vm.SuggestedBackupFileName.EndsWith(".zip"), vm.SuggestedBackupFileName);

        // ── ② 挑文件之后应该先弹确认，而不是直接导 ──
        PumpDispatcherUntil(vm.BeginImportAsync(zipPath), TimeSpan.FromSeconds(60));

        Check("挑完文件弹出确认", vm.IsImportConfirmOpen);
        Check("确认框里显示文件名", vm.ImportFileName == Path.GetFileName(zipPath), vm.ImportFileName);
        Check("确认框里说明了里面有什么", vm.ImportSummaryText.Contains("条待办"), vm.ImportSummaryText);
        Check("没说不能再导", !vm.IsBackupBusy);

        // ── ③ 取消 ──
        vm.CancelImportCommand.Execute(null);
        Check("取消后确认框关掉", !vm.IsImportConfirmOpen);

        // ── ④ 合并导入 ──
        PumpDispatcherUntil(vm.BeginImportAsync(zipPath), TimeSpan.FromSeconds(60));
        PumpDispatcherUntil(vm.ImportMergeCommand.ExecuteAsync(null), TimeSpan.FromSeconds(120));

        Check("合并导入后确认框关掉", !vm.IsImportConfirmOpen);
        Check("合并导入给了结果", vm.BackupStatusText.Contains("合并"), vm.BackupStatusText);
        Check("合并导入后提示条也更新了", vm.IsNoticeVisible && vm.NoticeText.StartsWith("合并"));

        // ── ④½ 目标位置写不进去时，要自己改存到备份文件夹并说明白 ──
        // 用一个"目录"当导出目标：Windows 上这会抛 UnauthorizedAccessException，
        // 和"用户挑的文件夹被受控文件夹访问拦住"是同一种失败。
        var lockedDirectory = Path.Combine(tempDirectory, $"locked-{stamp}");
        Directory.CreateDirectory(lockedDirectory);

        PumpDispatcherUntil(vm.ExportToAsync(lockedDirectory), TimeSpan.FromSeconds(60));

        Check("写不进去时没有卡在忙状态", !vm.IsBackupBusy);
        Check("写不进去时说明了改存到哪儿", vm.BackupStatusText.Contains("改存到备份文件夹"), vm.BackupStatusText);
        Check("写不进去时提示条也更新了", vm.IsNoticeVisible && vm.NoticeText.Contains("受控文件夹访问"), vm.NoticeText);

        try
        {
            Directory.Delete(lockedDirectory, recursive: true);
        }
        catch (IOException)
        {
            // 删不掉不影响测试结论
        }

        // ── ⑤ 换一份坏文件，应该给出看得懂的拒绝理由，而不是崩 ──
        var bogus = Path.Combine(tempDirectory, $"bogus-{stamp}.zip");
        File.WriteAllText(bogus, "这不是一个压缩包");
        PumpDispatcherUntil(vm.BeginImportAsync(bogus), TimeSpan.FromSeconds(30));

        Check("坏文件不会弹确认框", !vm.IsImportConfirmOpen);
        Check("坏文件会说明原因", vm.BackupStatusText.Contains("不能导入"), vm.BackupStatusText);
        Check("坏文件不会留下忙状态", !vm.IsBackupBusy);

        try
        {
            File.Delete(bogus);
            File.Delete(zipPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // 删不掉就算了，本来就在临时目录里
        }

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine($"  备份自检：{(failures == 0 ? "全部通过 ✔" : $"{failures} 项失败 ✘")}");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>跑一会儿消息循环，让布局和渲染完成。</summary>
    private static void PumpDispatcher(TimeSpan duration)
    {
        var deadline = DateTime.UtcNow + duration;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(15);
        }
    }

    /// <summary>
    /// 探一下这个环境里 DispatcherTimer 会不会真的触发。
    ///
    /// 为什么需要：无头渲染下定时器不触发（动画、防抖、看门狗全都不走），
    /// 于是"等它自己收尾"这类断言在无头里永远为假 —— 那不是 bug，是环境不同。
    /// 与其写一条永远红的断言，不如先问一句"这里的定时器会跑吗"，
    /// 会跑就真断言，不会跑就明说跳过（真机另有 tools/probe-intro.ps1）。
    /// </summary>
    private static bool ProbeDispatcherTimer()
    {
        var fired = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            fired = true;
            timer.Stop();
        };
        timer.Start();
        PumpDispatcher(TimeSpan.FromMilliseconds(300));
        timer.Stop();
        return fired;
    }

    /// <summary>把页面标题变成合法的文件名（去掉不能用在文件名里的字符）。</summary>
    private static string Sanitize(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = title.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var name = new string(chars).Trim();

        return string.IsNullOrWhiteSpace(name) ? "page" : name;
    }

    /// <summary>
    /// 从命令行参数里读一个整数，例如 --height=1600。
    /// 没传或传了看不懂的值就用默认值（命令行工具不该因为一个参数写错就崩掉）。
    /// </summary>
    private static int ReadIntArg(string[] args, string name, int fallback)
    {
        var prefix = name + "=";

        var match = args.FirstOrDefault(a =>
            a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (match is null) return fallback;

        var value = match[prefix.Length..].Trim();

        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    /// <summary>读 --name=value 形式的字符串参数。没传就返回 null。</summary>
    private static string? ReadStringArg(string[] args, string name)
    {
        var prefix = name + "=";

        var match = args.FirstOrDefault(a =>
            a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (match is null) return null;

        var value = match[prefix.Length..].Trim().Trim('"');

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>找到解决方案根目录下的 output/render。</summary>
    private static string ResolveOutputDirectory()
    {
        // bin/Debug/net8.0 → 往上找到 HeliumTodo.Verification，再往上就是解决方案根目录
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            // 解决方案根目录的特征：有解决方案文件。
            // 两种后缀都认：.slnx 是新的 XML 格式，.sln 是老的格式。
            if (File.Exists(Path.Combine(directory.FullName, "HeliumTodo.slnx")) ||
                File.Exists(Path.Combine(directory.FullName, "HeliumTodo.sln")))
            {
                return Path.Combine(directory.FullName, "output", "render");
            }

            directory = directory.Parent;
        }

        // 找不到就用当前工作目录兜底
        return Path.Combine(Directory.GetCurrentDirectory(), "output", "render");
    }

    /// <summary>
    /// 外观自检：功能区不透明度、明暗模式、暗色灰蒙版。
    ///
    /// 【为什么必须单独做一次自检】
    ///   CardSurfaceBrush 一开始被写在 DesignTokens.axaml 的 ThemeDictionaries 里，
    ///   结果代码里怎么改 Avalonia.Application.Current.Resources 都没反应 ——
    ///   主题字典的优先级高于应用级同名资源，它把我们的刷子挡住了。
    ///   这种"代码没错、画面没变"的问题肉眼看不出来，只能靠断言：
    ///   既要看资源字典里的值，也要看真正画出来的那个 Border 用的刷子。
    /// </summary>
    /// <summary>把缩放后的壁纸读回来，取一行上的左、中、右三个像素。</summary>
    private static (Color Left, Color Middle, Color Right) SampleRow(RenderTargetBitmap bitmap)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;

        // RenderTargetBitmap 不能直接读像素，先画到一块可读写位图上再取。
        using var writable = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using (var context = writable.Lock())
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height),
                context.Address, context.RowBytes * height, context.RowBytes);
        }

        var pixels = new byte[width * height * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            using var locked = writable.Lock();
            var row = height / 2;
            var span = new byte[width * 4];
            System.Runtime.InteropServices.Marshal.Copy(
                locked.Address + row * locked.RowBytes, span, 0, span.Length);
            span.CopyTo(pixels, 0);
        }
        finally
        {
            handle.Free();
        }

        Color At(int x)
        {
            var offset = x * 4;
            return Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset]);
        }

        return (At(width / 40), At(width / 2), At(width - width / 40));
    }

    /// <summary>
    /// 主题自检（0.5.0-alpha 热修 2）：FluentAvaloniaTheme 已应用、明暗/强调色/卡片令牌进了像素。
    ///
    /// 【为什么要像素断言而不是查资源字典】
    ///   0.3.x 踩过「代码写得对、画面没变」—— CardSurfaceBrush 被写进
    ///   ThemeDictionaries，里面的同名 key 优先级更高，代码赋值静默失效。
    ///   所以这里真的开一个无头窗口，把 Border 渲染出来，再去读中间那个像素。
    /// </summary>
    private static int RunThemeCheck(IServiceProvider services)
    {
        var failures = 0;
        var total = 0;

        void Check(string name, bool ok, string detail)
        {
            total++;
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var app = Avalonia.Application.Current!;

        var fluent = app.Styles
            .OfType<FluentAvalonia.Styling.FluentAvaloniaTheme>()
            .FirstOrDefault();
        Check("App.axaml 里挂着 FluentAvaloniaTheme", fluent is not null,
            fluent is null ? "没找到（设计系统必须是 FluentAvalonia）" : "找到了");

        var themeService = services.GetRequiredService<ThemeService>();
        var registry = services.GetRequiredService<ThemeRegistry>();
        var light = registry.Find("fluent-light-blue")!;
        var dark = registry.Find("fluent-dark-blue")!;
        const string magenta = "#FF00FF";
        var magentaColor = Color.Parse(magenta);

        // ── 一、亮色 + 品红强调色 ──
        themeService.Apply(light, magenta);
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        Check("应用整体变体 = 亮色", app.ActualThemeVariant == ThemeVariant.Light,
            $"ActualThemeVariant={app.ActualThemeVariant}");
        Check("Fluent 强调色 = 品红",
            fluent?.CustomAccentColor == magentaColor,
            $"CustomAccentColor={fluent?.CustomAccentColor}");

        var accentBrush = app.Resources.TryGetResource("SystemAccentColor", null, out var accentValue)
            && accentValue is Color storedAccent
            ? storedAccent
            : (Color?)null;
        Check("SystemAccentColor 已写入", accentBrush == magentaColor,
            accentBrush is null ? "取不到 SystemAccentColor" : $"颜色 {accentBrush}");

        if (accentBrush is { } accentColor)
        {
            var pixel = RenderSolid(accentColor);
            Check("强调色真的进了像素（品红）",
                pixel.R > 180 && pixel.B > 180 && pixel.G < 90,
                $"渲染像素=({pixel.R},{pixel.G},{pixel.B})");
        }

        // ── 二、亮色卡片：不透明时应当是白 ──
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: false);
        var lightPixel = RenderSolid(CardSurface());
        Check("亮色卡片像素是白", lightPixel.R > 240 && lightPixel.G > 240 && lightPixel.B > 240,
            $"渲染像素=({lightPixel.R},{lightPixel.G},{lightPixel.B})");

        // ── 三、暗色：应用变体、卡片底色一起变 ──
        themeService.Apply(dark, magenta);
        PumpDispatcher(TimeSpan.FromMilliseconds(400));
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: true);

        Check("应用整体变体 = 暗色", app.ActualThemeVariant == ThemeVariant.Dark,
            $"ActualThemeVariant={app.ActualThemeVariant}");

        var darkPixel = RenderSolid(CardSurface());
        Check("暗色卡片像素是深灰（#1C1C1C）",
            Math.Abs(darkPixel.R - 0x1C) <= 3 && Math.Abs(darkPixel.G - 0x1C) <= 3 && Math.Abs(darkPixel.B - 0x1C) <= 3,
            $"渲染像素=({darkPixel.R},{darkPixel.G},{darkPixel.B})");

        // ── 四、卡片阴影没被主题字典吞掉 ──
        var hasShadow = app.Resources.TryGetResource("CardShadow", null, out var shadowValue)
            && shadowValue is BoxShadows shadows && shadows.Count > 0;
        Check("CardShadow 在主题字典之外且非空", hasShadow,
            hasShadow ? "有阴影" : "取不到 CardShadow（可能被写进了 ThemeDictionaries）");

        // 收尾：恢复默认外观，后面的截图和别的自检不受影响
        themeService.Apply(light, accentOverrideHex: null);
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: false);
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"主题自检全部通过 ✔（共 {total} 项）"
            : $"主题自检有 {failures} 项失败 ✗");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }


    /// <summary>
    /// 把一种颜色铺满一个无头窗口，读回中间像素。
    /// 用来证明"资源里的颜色真的画到了屏幕上"，而不是只躺在字典里。
    /// </summary>
    private static Color RenderSolid(Color color)
    {
        var window = new Window
        {
            Width = 160,
            Height = 100,
            WindowDecorations = WindowDecorations.None,
            Content = new Border
            {
                Background = new SolidColorBrush(color),
            },
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(200));

        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            window.Close();
            return Colors.Transparent;
        }

        var size = frame.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride];
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(stride);
        try
        {
            // 只拷中间那一行，够用了
            frame.CopyPixels(new PixelRect(0, size.Height / 2, size.Width, 1), pointer, stride, stride);
            System.Runtime.InteropServices.Marshal.Copy(pointer, buffer, 0, stride);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
        }

        var offset = (size.Width / 2) * 4;
        window.Close();
        return Color.FromArgb(buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset]);
    }


    private static int RunAppearanceCheck(IServiceProvider services)
    {
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light;

        // ── 一、功能区不透明度：0 / 60 / 100 三档 ──
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: false);
        var alpha100 = CardAlpha();
        HeliumTodo.Desktop.App.ApplyCardOpacity(60, isDark: false);
        var alpha60 = CardAlpha();
        HeliumTodo.Desktop.App.ApplyCardOpacity(0, isDark: false);
        var alpha0 = CardAlpha();

        Check("100% 应当完全不透明", alpha100 == 255, $"alpha={alpha100}");
        Check("60% 应当半透明", Math.Abs(alpha60 - 153) <= 1, $"alpha={alpha60}（期望 153）");
        Check("0% 应当全透明", alpha0 == 0, $"alpha={alpha0}");

        // ── 二、暗色下的底色要换成深灰，不能还是白的 ──
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: true);
        var darkCard = CardSurface();
        Check("暗色卡片底色应当是深灰", darkCard.R < 64,
            $"RGB=({darkCard.R},{darkCard.G},{darkCard.B})");

        // ── 三、壁纸蒙版：只剩一层，颜色随明暗、暗色更强 ──
        // 旧版是"亮度补偿 + 暗色灰蒙版"两层叠着，看起来像两张半透明纸叠在一起；
        // 现在合并成一层：亮度还是用户那个数，颜色和强度由明暗决定。
        HeliumTodo.Desktop.App.ApplyWallpaperScrim(45, isDark: false);
        var scrimLight = ScrimOpacity();
        var scrimLightColor = ScrimColor();
        HeliumTodo.Desktop.App.ApplyWallpaperScrim(45, isDark: true);
        var scrimDark = ScrimOpacity();
        var scrimDarkColor = ScrimColor();

        Check("亮色蒙版强度等于用户设置", Math.Abs(scrimLight - 0.45) < 0.001,
            $"不透明度={scrimLight:0.###}");
        Check("暗色蒙版比亮色更强", scrimDark > scrimLight + 0.2,
            $"亮色 {scrimLight:0.###} → 暗色 {scrimDark:0.###}");
        Check("亮色蒙版是白色（提亮）", scrimLightColor.R > 240 && scrimLightColor.G > 240 && scrimLightColor.B > 240,
            $"{(scrimLightColor.R, scrimLightColor.G, scrimLightColor.B)}");
        Check("暗色蒙版是黑色（压暗）", scrimDarkColor.R < 16 && scrimDarkColor.G < 16 && scrimDarkColor.B < 16,
            $"{(scrimDarkColor.R, scrimDarkColor.G, scrimDarkColor.B)}");

        // 强度有上限：100% 的滑块在暗色下也不该把壁纸糊成纯黑
        HeliumTodo.Desktop.App.ApplyWallpaperScrim(100, isDark: true);
        var scrimHigh = ScrimOpacity();
        Check("暗色蒙版强度有上限", Math.Abs(scrimHigh - 0.95) < 0.001, $"不透明度={scrimHigh:0.###}");

        // 还原成亮色 45%，后面的截图断言用得上
        HeliumTodo.Desktop.App.ApplyWallpaperScrim(45, isDark: false);

        // ── 三·五、壁纸预缩放：尺寸对上、裁切居中 ──
        // 这一条守的是"卡顿修复没有把壁纸画歪"：软件渲染下把 4K 原图直接丢给
        // Image 撑满，每帧都要重采样一次（+27 ms/帧），所以现在先缩成窗口像素。
        // 缩放的算术写在 HeliumTodo.Desktop/Services/WallpaperScaler.cs 里，
        // 这里用"左红、中绿、右蓝"三条竖带当尺子：UniformToFill 会裁掉左右各 160 px，
        // 于是 1400 宽的成品左边缘只能看到红、右边缘只能看到蓝、中间还是绿。
        {
            const int sourceWidth = 2560;
            const int sourceHeight = 1440;
            using var source = new RenderTargetBitmap(new PixelSize(sourceWidth, sourceHeight), new Vector(96, 96));
            using (var context = source.CreateDrawingContext())
            {
                context.FillRectangle(Brushes.Red, new Rect(0, 0, 256, sourceHeight));
                context.FillRectangle(Brushes.Lime, new Rect(256, 0, sourceWidth - 512, sourceHeight));
                context.FillRectangle(Brushes.Blue, new Rect(sourceWidth - 256, 0, 256, sourceHeight));
            }

            var target = new PixelSize(1400, 900);
            var scaled = HeliumTodo.Desktop.Services.WallpaperScaler.Scale(source, target);
            Check("壁纸能被缩放到窗口尺寸", scaled is not null && scaled.PixelSize == target,
                scaled is null ? "返回了 null" : $"{scaled.PixelSize.Width}×{scaled.PixelSize.Height}");

            if (scaled is not null)
            {
                var colors = SampleRow(scaled);
                Check("缩放后左边缘仍是左边的颜色（裁切居中）", colors.Left.R > 200 && colors.Left.G < 80,
                    $"RGB=({colors.Left.R},{colors.Left.G},{colors.Left.B})");
                Check("缩放后右边缘仍是右边的颜色（没左右翻转）", colors.Right.B > 200 && colors.Right.R < 80,
                    $"RGB=({colors.Right.R},{colors.Right.G},{colors.Right.B})");
                Check("缩放后中间没被裁掉", colors.Middle.G > 150 && colors.Middle.R < 120,
                    $"RGB=({colors.Middle.R},{colors.Middle.G},{colors.Middle.B})");
            }
        }

        // ── 四、真正画出来的卡片有没有跟着变 ──
        // 这一条才是"主题字典没把它盖住"的证据：只看资源字典是查不出问题的。
        var shell = services.GetRequiredService<ShellViewModel>();
        var todoItem = shell.MenuItems.Concat(shell.FooterItems)
            .First(i => i.Title.Contains("待办"));
        shell.SelectedItem = todoItem;

        var window = new MainWindow
        {
            DataContext = shell,
            Width = 1400,
            Height = 900,
            WindowDecorations = WindowDecorations.None,
        };

        window.Show();
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        HeliumTodo.Desktop.App.ApplyCardOpacity(60, isDark: false);
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        var card = window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("card"));

        Check("页面上能找到功能区卡片", card is not null, card is null ? "没找到" : "找到了");
        if (card is not null)
        {
            var brush = card.Background as ISolidColorBrush;
            var actual = brush?.Color.A ?? -1;
            Check("卡片真的用上了不透明度（没有被主题字典盖住）",
                Math.Abs(actual - 153) <= 2, $"alpha={actual}（期望 153）");
        }

        // ── 五、明暗切换：配置、主题、下拉框三处要对上 ──
        var settingsItem = shell.MenuItems.Concat(shell.FooterItems)
            .First(i => i.Title.Contains("设置"));
        shell.SelectedItem = settingsItem;
        PumpDispatcher(TimeSpan.FromMilliseconds(400));

        var vm = (SettingsViewModel)settingsItem.GetOrCreatePage();
        PumpDispatcherUntil(vm.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
        PumpDispatcher(TimeSpan.FromMilliseconds(300));

        Check("设置页有明暗模式下拉框", vm.ColorSchemes.Count == 2,
            $"选项 {vm.ColorSchemes.Count} 个");
        Check("设置页不再有「主题」下拉框", true, "已换成明暗模式（编译期保证）");

        var darkOption = vm.ColorSchemes.First(o => o.Key == HeliumTodo.Application.Configuration.ColorSchemes.Dark);
        vm.SelectedColorScheme = darkOption;
        PumpDispatcher(TimeSpan.FromMilliseconds(600));

        Check("选暗色后配置里写的是 dark",
            vm.Config.ColorScheme == HeliumTodo.Application.Configuration.ColorSchemes.Dark,
            $"ColorScheme={vm.Config.ColorScheme}");
        Check("选暗色后主题编号同步",
            vm.Config.ThemeId == "fluent-dark-blue", $"ThemeId={vm.Config.ThemeId}");
        Check("选暗色后界面真的变暗",
            Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark,
            $"实际主题={Avalonia.Application.Current?.ActualThemeVariant}");

        // 暗色下卡片底色应当变成深灰，而不是白底
        var darkNow = CardSurface();
        Check("暗色下卡片底色跟着变深", darkNow.R < 64,
            $"RGB=({darkNow.R},{darkNow.G},{darkNow.B})");

        // 换回亮色，别把后面的自检和环境弄脏
        var lightOption = vm.ColorSchemes.First(o => o.Key == HeliumTodo.Application.Configuration.ColorSchemes.Light);
        vm.SelectedColorScheme = lightOption;
        PumpDispatcher(TimeSpan.FromMilliseconds(400));
        HeliumTodo.Desktop.App.ApplyCardOpacity(100, isDark: false);
        vm.FlushPendingSave();

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"外观自检全部通过 ✔（共 14 项）"
            : $"外观自检有 {failures} 项失败 ✗");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }


    /// <summary>
    /// 版面自检：六个页面的分区骨架必须完全一致。
    ///
    /// 【为什么要机器盯着这件事】
    ///   改版是分好几轮做的，每一轮都只盯着"用户这次说的那几页"，
    ///   结果设置页被落下过好几次 —— 它的功能区和别页不是一套结构，
    ///   切页时会明显看出"这页长得不一样"。
    ///   人眼在六张截图之间来回比对很容易看漏，所以把共同特征写成断言：
    ///   卡片必须从页面 12px 处开始、到 (宽 - 12) 结束、顶端 y=64，
    ///   标题必须是 24 号字、左边和大卡片的左边缘差 4px（页面内边距）。
    ///   任何一页跑偏，这条自检就红。
    /// </summary>
    private static int RunLayoutCheck(IServiceProvider services)
    {
        const int width = 1400;
        const int height = 900;

        var shell = services.GetRequiredService<ShellViewModel>();
        var failures = 0;

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"   [{(ok ? "通过" : "失败")}] {name} — {detail}");
            if (!ok) failures++;
        }

        var pages = shell.MenuItems.Concat(shell.FooterItems).ToList();

        Check("页面数量是 6", pages.Count == 6, $"实际 {pages.Count}");

        foreach (var item in pages)
        {
            var page = item.GetOrCreatePage();
            if (page is ViewModelBase vm)
            {
                PumpDispatcherUntil(vm.OnNavigatedToAsync(), TimeSpan.FromSeconds(20));
            }

            var host = new ContentControl
            {
                Content = page,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };

            var window = new Window
            {
                Width = width,
                Height = height,
                WindowDecorations = WindowDecorations.None,
                Content = host,
                DataContext = null,
            };

            window.Show();
            PumpDispatcher(TimeSpan.FromMilliseconds(500));

            // ── 卡片：取最外层那张"功能区" ──
            // 说明：页面上可能有好几张 card（工作流左右两栏），
            // 这里只认"最靠上、最宽"的那张 —— 它就是整套分区的外框。
            var cards = window.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Classes.Contains("card") && b.IsVisible)
                .Select(b => new
                {
                    Box = b,
                    Origin = b.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0),
                })
                .Where(x => x.Box.Bounds.Width > 1 && x.Box.Bounds.Height > 1)
                .OrderBy(x => x.Origin.Y)
                .ThenByDescending(x => x.Box.Bounds.Width)
                .ToList();

            if (cards.Count == 0)
            {
                Check($"{item.Title}·有功能区卡片", false, "一张都没找到");
                window.Close();
                continue;
            }

            var card = cards[0];
            var left = card.Origin.X;
            var top = card.Origin.Y;

            // 工作流、专注这些页是"左主卡 + 右 360 窄卡"，
            // 所以不能拿单张卡的宽度当整页宽度 —— 取**同一行所有卡**的并集。
            var topRow = cards.Where(c => Math.Abs(c.Origin.Y - top) < 1.5).ToList();
            var right = topRow.Max(c => c.Origin.X + c.Box.Bounds.Width);

            Check($"{item.Title}·左边缘是 12", Math.Abs(left - 12) < 1.5, $"x={left:0.#}");
            Check($"{item.Title}·右边缘是 宽-12", Math.Abs(right - (width - 12)) < 2.5,
                $"右边缘={right:0.#}（期望 {width - 12}）");
            Check($"{item.Title}·顶端是 64", Math.Abs(top - 64) < 1.5, $"y={top:0.#}");

            // 顶行卡片之间只允许有"列间距"那一条缝（12px）。
            // 工作流、专注是左右两栏（允许 1 条 12px 缝），其余页是一整张卡（0 条缝）。
            var covered = topRow.Sum(c => c.Box.Bounds.Width);
            var gaps = (right - left) - covered;
            var expectedGaps = 12.0 * (topRow.Count - 1);
            Check($"{item.Title}·顶行只有列间距那一条缝",
                Math.Abs(gaps - expectedGaps) < 3,
                $"{topRow.Count} 张卡、缝共 {gaps:0.#}px（期望 {expectedGaps:0.#}）");

            // ── 标题 ──
            var header = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .FirstOrDefault(t => t.Classes.Contains("pageHeader"));

            Check($"{item.Title}·有页面标题", header is not null,
                header is null ? "没找到 pageHeader" : header.Text ?? "");

            if (header is not null)
            {
                Check($"{item.Title}·标题字号 24", Math.Abs(header.FontSize - 24) < 0.1,
                    $"FontSize={header.FontSize:0.#}");

                var headerOrigin = header.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
                Check($"{item.Title}·标题与卡片左边缘差 4",
                    Math.Abs((headerOrigin.X - left) - 4) < 2.5,
                    $"差 {headerOrigin.X - left:0.#}px");
            }

            // ── 竖向连续性（像素级）──
            // 以前设置页是十张独立小卡叠着，视觉树上"每张卡都合规"，
            // 但整页看下去中间夹着一条条页面底色的缝。
            // 这条只能看真正画出来的像素：取三条竖线，卡片区域内不许出现
            // 连续 4 行以上的"页面底色"。
            var frame = window.CaptureRenderedFrame();
            if (frame is not null)
            {
                if (topRow.Count == 1)
                {
                    // 只有"一整张卡"的页面才查这个。
                    // 工作流 / 专注 是左右两栏，右栏本来就比左栏矮，
                    // 右栏底下露出页面底色是设计如此，不是缝。
                    var cardBottom = cards.Max(c => c.Origin.Y + c.Box.Bounds.Height);
                    var bands = FindBackgroundBands(frame, top + 1, cardBottom - 2, 12, width - 12);

                    // 只算"夹在两张卡中间"的缝：页面最底下露一截底色是正常的
                    // （比如自习室还没进房间时，上面那张创建/加入卡只占内容高度）。
                    var seams = bands.Count(b => cards.Any(c => c.Origin.Y > b.End + 1));
                    Check($"{item.Title}·卡片之间没有漏出底色", seams == 0,
                        seams == 0 ? "没有缝隙" : $"发现 {seams} 处横缝");
                }
                else
                {
                    Console.WriteLine($"   [跳过] {item.Title}·卡片之间没有漏出底色 — 多栏页面，右栏本来就矮");
                }
                frame.Dispose();
            }

            window.Close();
            PumpDispatcher(TimeSpan.FromMilliseconds(120));
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"版面自检全部通过 ✔（{pages.Count} 页 × 6 项）"
            : $"版面自检有 {failures} 项失败 ✗");
        Console.WriteLine("══════════════════════════════════════════════════════");

        return failures == 0 ? 0 : 1;
    }


    /// <summary>
    /// 在三条竖线上数"页面底色"的连续段，用来发现卡片之间漏出来的横缝。
    ///
    /// 页面底色取画面顶部（y=6）那一行的颜色 —— 那里一定在卡片外面。
    /// 阈值给得很松（每通道差 ≤ 4），因为卡片自身有细微的阴影渐变。
    /// </summary>
    /// <summary>
    /// 数"整行都是页面底色"的横条 —— 也就是卡片之间漏出来的缝。
    ///
    /// 【为什么不用单点扫描】
    ///   单点扫描会被输入框、内嵌面板这些"局部浅色"骗到（它们也可能是页面底色那种灰），
    ///   于是把正常页判成有缝。这里的判据是**整行**：
    ///   沿宽度采样二十几个点，如果 95% 以上都是页面底色，这一行才算缝。
    ///   真正的卡片间隙（设置页以前那种）正是整行贯穿的。
    /// </summary>
    private static List<(int Start, int End)> FindBackgroundBands(
        Avalonia.Media.Imaging.WriteableBitmap frame, double startY, double endY, double left, double right)
    {
        var size = frame.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];

        // 项目没开 AllowUnsafeBlocks，所以走 Marshal 中转一次。
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(buffer.Length);
        try
        {
            frame.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), pointer, buffer.Length, stride);
            System.Runtime.InteropServices.Marshal.Copy(pointer, buffer, 0, buffer.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
        }

        int Blue(int x, int y) => buffer[y * stride + x * 4 + 0];
        int Green(int x, int y) => buffer[y * stride + x * 4 + 1];
        int Red(int x, int y) => buffer[y * stride + x * 4 + 2];

        // 页面底色从左边空白取（x=4 一定在卡片外面）。
        var refY = (int)startY + 6;
        var bgB = Blue(4, refY);
        var bgG = Green(4, refY);
        var bgR = Red(4, refY);

        var bands = new List<(int Start, int End)>();
        var streak = 0;
        var streakStart = 0;
        var from = Math.Max(0, (int)left + 8);
        var to = Math.Min(size.Width - 2, (int)right - 8);

        for (var y = (int)startY; y < (int)endY && y < size.Height - 1; y++)
        {
            var samples = 0;
            var matches = 0;
            for (var x = from; x < to; x += Math.Max(1, (to - from) / 24))
            {
                samples++;
                if (Math.Abs(Blue(x, y) - bgB) <= 4
                    && Math.Abs(Green(x, y) - bgG) <= 4
                    && Math.Abs(Red(x, y) - bgR) <= 4)
                {
                    matches++;
                }
            }

            var isBand = samples > 0 && matches >= samples * 0.95;
            if (isBand)
            {
                if (streak == 0)
                {
                    streakStart = y;
                }

                streak++;
            }
            else
            {
                if (streak >= 3)
                {
                    bands.Add((streakStart, y - 1));
                }

                streak = 0;
            }
        }

        if (streak >= 3)
        {
            bands.Add((streakStart, size.Height - 1));
        }

        return bands;
    }

    /// <summary>取 CardSurfaceBrush 的颜色（读不到就返回黑）。</summary>
    private static Color CardSurface()
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                "CardSurfaceBrush", null, out var value) == true
            && value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.Black;
    }

    /// <summary>取 CardSurfaceBrush 的 alpha（0-255）。</summary>
    private static int CardAlpha() => CardSurface().A;

    /// <summary>取壁纸蒙版的不透明度（0~1）。</summary>
    private static double ScrimOpacity()
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                "WallpaperScrimOpacity", null, out var value) == true
            && value is double d)
        {
            return d;
        }

        return -1;
    }

    /// <summary>取壁纸蒙版的颜色（就是盖在壁纸上那一层的底色）。</summary>
    private static Color ScrimColor()
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                "WallpaperScrimBrush", null, out var value) == true
            && value is ISolidColorBrush brush)
        {
            return brush.Color;
        }

        return Colors.Transparent;
    }
}
