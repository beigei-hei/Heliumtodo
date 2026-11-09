// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace HeliumTodo.Desktop.Windowing;

/// <summary>
/// 窗口的「外壳」部分：把系统边框的样式位加回来，再把非客户区抹掉。
///
/// 为什么需要这么做（记一笔，免得以后又改回去）：
///   窗口为了铺满壁纸用了 WindowDecorations="None"，Avalonia 对应地把窗口变成了
///   不带 WS_THICKFRAME 的样子。铺满是铺满了，但 Windows 就认为"这不是一个能最大化的
///   普通窗口"，于是窗口没有了系统阴影，行为也和普通窗口不一样。
///   实测两种样式位：
///       BorderOnly（普通窗口） 0x16870000  —— 含 WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
///       None（我们的窗口）     0x16030000  —— 只剩 MINIMIZEBOX | MAXIMIZEBOX
///   所以这里做两件事：
///     ① 把 WS_THICKFRAME / WS_SYSMENU 加回去（样式位而已，它自己不会画出标题栏）；
///     ② 挂钩 WM_NCCALCSIZE，告诉系统"这个窗口的非客户区是 0"，
///        这样加了 THICKFRAME 也不会重新长出那圈 7px 的灰色边框。
///   结果是：样式上是个普通可调整窗口（有阴影、有圆角），
///   视觉上客户区等于整个窗口（壁纸照样铺满）。
///
/// 0.3.5 补的一条（L1）：WS_CAPTION。
///   之前缺的不是"边框"而是"标题栏位"：DWM 判断"这个窗口配不配拥有最小化/最大化过渡动画"
///   看的是 WS_CAPTION（= WS_BORDER | WS_DLGFRAME）。少了它，点最小化/最大化就是硬切；
///   补上之后过渡交给 DWM 在合成器里播 —— 哪怕窗口内容是软件渲染的，动画照样 60 fps，
///   因为代价不在我们的进程里。加了 WS_CAPTION 也不会真的长出标题栏：
///   WM_NCCALCSIZE 已经把非客户区抹成 0。
///
/// 为什么单独放一个类，而不是继续写在 MainWindow 的 partial 里：
///   这些都是"窗口外壳"的通用做法，跟这个应用有什么页面没关系。
///   抽出来之后，MainWindow 只需要一行 <c>WindowChrome.Attach(this);</c>，
///   读代码的人不必在四个 partial 文件之间跳来跳去。
/// </summary>
internal static class WindowChrome
{
    // ── WinUser.h 里的常量，值都抄的原值，方便对着文档看 ──
    private const uint WS_THICKFRAME = 0x00040000u;
    private const uint WS_MINIMIZEBOX = 0x00020000u;
    private const uint WS_MAXIMIZEBOX = 0x00010000u;
    private const uint WS_SYSMENU = 0x00080000u;
    private const uint WS_BORDER = 0x00800000u;
    private const uint WS_DLGFRAME = 0x00400000u;

    /// <summary>WS_CAPTION = WS_BORDER | WS_DLGFRAME。只影响 DWM 的判定，不会画出标题栏。</summary>
    private const uint WS_CAPTION = WS_BORDER | WS_DLGFRAME;
    private const uint WM_NCCALCSIZE = 0x0083;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary>
    /// 给窗口装上"系统边框样式位 + 非客户区归零"。
    /// 非 Windows 平台直接什么都不做 —— 那边本来就没有"系统边框"这回事。
    /// </summary>
    public static void Attach(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;

        // ① 样式位：Avalonia 每次重设样式都会回调这里，所以每次都把它们或上。
        //    这里的 WS_CAPTION 只给 DWM 看（决定要不要播最小化/最大化过渡），
        //    真正画不画标题栏由下面的 WM_NCCALCSIZE 说了算。
        Win32Properties.AddWindowStylesCallback(
            window,
            static (style, exStyle) =>
                (style | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX, exStyle));

        // ② 非客户区归零
        Win32Properties.AddWndProcHookCallback(window, OnNativeMessage);

        // ③ 显式允许 DWM 过渡：万一系统的"关闭动画"策略是开着的（辅助功能里的降低动画、
        //    或者用户手动关过），这里明确要回来 —— 我们宁可跟着系统设置走，也不要自己装死。
        window.Opened += (_, _) => AllowDwmTransitions(window);
    }

    /// <summary>
    /// 告诉 DWM「这个窗口的过渡动画允许播」。
    ///
    /// DWMWA_TRANSITIONS_FORCEDISABLED（=3）传 FALSE 表示"不要强制禁用"。
    /// 注意这个开关是"别拦着我"，不是"必须有动画" —— 用户在系统里关掉动画仍然有效，
    /// 我们不跟用户的辅助功能设置对着干。
    /// </summary>
    private static void AllowDwmTransitions(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null)
        {
            return;
        }

        var allow = 0; // FALSE：不要强制禁用过渡
        var hr = DwmSetWindowAttribute(
            handle.Handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref allow, sizeof(int));

        if (hr != 0)
        {
            Trace($"[窗口] 没能显式开启 DWM 过渡（hr=0x{hr:X8}），沿用系统默认");
        }
    }

    private static void Trace(string message)
    {
        try
        {
            HeliumTodo.Desktop.App.LogBackground(message);
        }
        catch
        {
            // 记日志失败不值得影响窗口。
        }
    }

    /// <summary>
    /// WM_NCCALCSIZE 的处理。
    ///
    /// 参数规则（Win32 里少见的怪脾气）：
    ///   wParam == TRUE  表示"系统要计算客户区大小"，lParam 是 NCCALCSIZE_PARAMS*
    ///   wParam == FALSE 表示"系统要计算客户区矩形"，lParam 是 RECT*
    /// 我们只处理前一种；返回 0 表示"接受调整后的客户区"。
    /// 把 rgrc[0]（窗口矩形）原样留着，就等于"客户区 = 整个窗口"。
    /// </summary>
    private static IntPtr OnNativeMessage(
        IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WM_NCCALCSIZE || wParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var parameters = Marshal.PtrToStructure<NativeCalCSizeParams>(lParam);

        // 最大化的时候，"窗口矩形"是整个屏幕（比工作区高出一条任务栏）。
        // 如果原样接受，客户区会把任务栏盖住 —— 所以这时候换成显示器的工作区。
        if (IsZoomed(hWnd))
        {
            var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                parameters.WindowRect = info.Work;
            }
        }

        Marshal.StructureToPtr(parameters, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>DwmSetWindowAttribute 的属性号：WCA 里的 TRANSITIONS_FORCEDISABLED。</summary>
    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    // ── 下面三个结构体和 WinUser.h 里的字段顺序严格一致，不能调换 ──

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCalCSizeParams
    {
        /// <summary>窗口矩形（调整后就是客户区矩形）。</summary>
        public NativeRect WindowRect;

        /// <summary>调整前的客户区。</summary>
        public NativeRect ClientRectBefore;

        /// <summary>调整后的客户区。</summary>
        public NativeRect ClientRectAfter;

        /// <summary>WINDOWPOS*，这里用不到，但要占位。</summary>
        public IntPtr WindowPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }
}