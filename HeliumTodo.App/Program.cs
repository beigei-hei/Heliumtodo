using System;
using Avalonia;
using HeliumTodo.App.Services;

namespace HeliumTodo.App;

/// <summary>
/// 程序入口。
///
/// 启动顺序：
///   1. 配置 Avalonia（用哪个渲染后端、加载哪些字体）
///   2. 启动 Avalonia
///   3. Avalonia 内部会调用 <see cref="App"/>，由它去建窗口
/// </summary>
internal static class Program
{
    /// <summary>
    /// 这个方法在 Avalonia 初始化之前运行，所以不能在这里用任何 Avalonia 的类型
    /// （比如不能用 Window、不能读 App.axaml 里的资源）。
    /// </summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// 配置 Avalonia。
    /// 这个方法名和签名要保持原样 —— Avalonia 的设计器要靠它来预览界面。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()          // 自动检测平台（Windows / macOS / Linux）
            .WithInterFont()              // 内置 Inter 字体，避免换台电脑字体就变了
            .LogToTrace();                // 日志输出到调试窗口

        // 默认用软件渲染，理由见 CreateWin32Options 的注释。
        return builder.With(CreateWin32Options());
    }

    /// <summary>
    /// 决定用哪个渲染后端：默认软件渲染，<c>HELIUM_RENDER</c> 环境变量可以覆盖。
    ///
    /// 【为什么默认软件渲染？】
    ///   Windows 上 Avalonia 默认走 ANGLE → D3D11，也就是把画面交给显卡驱动合成。
    ///   代价是会把这套驱动模块全部映射进进程：实测这台机器上一次启动就多出
    ///   nvgpucomp64 (94 MB) + nvwgf2umx (86 MB) + nvppex (7 MB)，线程从 19 条涨到 84 条，
    ///   私有工作集 48 MB → 69 MB。
    ///   而这个界面是纯 2D 的卡片 + 少量 180ms 动画，软件渲染（Skia CPU）完全够用，
    ///   所以把默认换成软件渲染，用一点 CPU 换回二十多兆内存和六十多条线程。
    ///
    /// 为什么要留环境变量口子？
    ///   4K 屏 + 大量动画的用户可能更想要 GPU；渲染后端必须在 Avalonia 初始化
    ///   「之前」定下来（那时配置文件还没读），所以只能走环境变量而不是设置项。
    ///   想换回显卡渲染：<c>set HELIUM_RENDER=angle</c>，另外还有 <c>wgl</c> / <c>vulkan</c>。
    /// </summary>
    /// <summary>
    /// 决定用哪个渲染后端。优先级：<c>HELIUM_RENDER</c> 环境变量 &gt; 设置页里的「底层设置」&gt; 默认软件渲染。
    ///
    /// 【为什么默认软件渲染？】
    ///   Windows 上 Avalonia 默认走 ANGLE → D3D11，也就是把画面交给显卡驱动合成。
    ///   代价是会把这套驱动模块全部映射进进程：实测这台机器上一次启动就多出
    ///   nvgpucomp64 (94 MB) + nvwgf2umx (86 MB) + nvppex (7 MB)，线程从 19 条涨到 84 条，
    ///   私有工作集 48 MB → 69 MB。
    ///   而这个界面是纯 2D 的卡片 + 少量 180ms 动画，软件渲染（Skia CPU）完全够用，
    ///   所以把默认换成软件渲染，用一点 CPU 换回二十多兆内存和六十多条线程。
    ///
    /// 【现在可以在设置里改了】
    ///   设置 → 底层设置 → 渲染后端，可选 Avalonia 自带渲染器 / OpenGL / 软件 / Vulkan。
    ///   因为渲染后端必须在 Avalonia 初始化「之前」定下来（那时配置文件都还没读），
    ///   这个设置只在启动时生效一次，改完要重启程序。
    ///   环境变量仍然优先，用于"界面看不清了"时兜底：
    ///   <c>set HELIUM_RENDER=angle</c>，另外还有 <c>wgl</c> / <c>vulkan</c> / <c>software</c>。
    /// </summary>
    private static Win32PlatformOptions CreateWin32Options()
    {
        // 顺手把"这次用的是什么"记进静态字段，设置页要显示给用户看
        var activeKey = RenderBackendChoice.Resolve();

        return new Win32PlatformOptions { RenderingMode = RenderBackendChoice.ToModes(activeKey) };
    }
}