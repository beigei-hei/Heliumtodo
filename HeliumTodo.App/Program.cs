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

        // 默认用自动（显卡）渲染，理由见 CreateWin32Options 的注释。
        return builder.With(CreateWin32Options());
    }

    /// <summary>
    /// 决定用哪个渲染后端。优先级：<c>HELIUM_RENDER</c> 环境变量 &gt; 设置页里的「底层设置」&gt;
    /// 出厂默认（自动，也就是 Windows 上的 ANGLE → D3D11）。
    ///
    /// 【为什么默认交给显卡？这笔账算过两次】
    ///   第一次（0.3.3）把默认定成软件渲染，图的是省内存：ANGLE 会把整套显卡驱动模块
    ///   映射进进程 —— 实测这台机器上多出 nvgpucomp64（94 MB）+ nvwgf2umx（86 MB）
    ///   + nvppex（7 MB），线程 19 → 84 条，句柄 481 → 846，私有字节 78.2 → 256.9 MB。
    ///   当时的判断是"这个界面纯 2D，Skia 在 CPU 上完全够用"，所以用 CPU 换内存。
    ///
    ///   第二次（0.3.5）改回来自动。原因是窗口动效补齐之后，软件渲染的固定开销藏不住了：
    ///   每次改窗口尺寸要 45 ms（2560×1440 上是整页重排 + 全窗光栅化），
    ///   图标轨展开动画只有 25–43 fps；同一台机器换 ANGLE 是 74 fps。
    ///   也就是说——省下来的二十多兆私有内存，是用"动效掉一半帧"换的，不划算。
    ///
    ///   想省回内存随时可以改：设置 → 底层设置 → 渲染后端选"软件渲染"，
    ///   或者设环境变量 <c>HELIUM_RENDER=software</c>。两个方向的数字都记在 TECH_STACK.md 里。
    ///
    /// 【为什么还要留环境变量这个口子？】
    ///   渲染后端必须在 Avalonia 初始化「之前」定下来（那时配置文件都还没读），
    ///   所以真正读配置是 <see cref="RenderBackendChoice.Resolve"/> 自己开一个 ConfigService 干的。
    ///   环境变量排在配置前面，是为了"界面已经看不清、点不动设置页"时还能救回来：
    ///   <c>set HELIUM_RENDER=software</c>（或 angle / wgl / vulkan）就能起来。
    /// </summary>
    private static Win32PlatformOptions CreateWin32Options()
    {
        // 顺手把"这次用的是什么"记进静态字段，设置页要显示给用户看
        var activeKey = RenderBackendChoice.Resolve();

        return new Win32PlatformOptions { RenderingMode = RenderBackendChoice.ToModes(activeKey) };
    }
}