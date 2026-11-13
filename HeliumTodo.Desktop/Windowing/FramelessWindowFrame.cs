// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace HeliumTodo.Desktop.Windowing;

/// <summary>
/// 这个窗口在系统眼里的"外框"厚度（含标题栏位），单位 DIP。
///
/// 为什么需要它：
///   WindowChrome 给窗口补了 WS_CAPTION / WS_THICKFRAME（为了 DWM 的最小化/最大化过渡），
///   同时用 WM_NCCALCSIZE 把非客户区抹成 0，于是"客户区 = 窗口矩形"，视觉上没有边框。
///   但 Avalonia 设置 Width/Height 时按"客户区 + 外框 = 窗口矩形"来算：
///   它把请求值加上外框，再交给 SetWindowPos。由于我们把外框抹掉了，
///   加上的那圈外框就变成客户区的一部分 —— 第一次改大小时窗口会凭空长宽各多一圈，
///   表现为按下抓手瞬间"窗口整体放大"。调用方在赋值前减掉这一圈即可抵消。
///
/// 数值直接问 AdjustWindowRectExForDpi，和 Avalonia 内部用的是同一套系统度量：
///   96 dpi  → 16 × 39 px
///   144 dpi → 22 × 56 px
/// 实测窗口在 150% 下第一次拖右边缘时正好宽 +22、高 +56 物理像素。
/// </summary>
internal readonly record struct WindowFrame(double WidthDip, double HeightDip)
{
    public static readonly WindowFrame None = new(0, 0);

    /// <summary>把尺寸限制在正数；外框可能比请求的尺寸还大（最小化附近）。</summary>
    public double SubtractWidth(double widthDip) => Math.Max(1, widthDip - WidthDip);

    public double SubtractHeight(double heightDip) => Math.Max(1, heightDip - HeightDip);
}

/// <summary>读取当前窗口外框厚度；非 Windows 或没有平台句柄时返回 0。</summary>
internal static class FramelessWindowFrame
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const uint DefaultDpi = 96;

    public static WindowFrame Measure(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return WindowFrame.None;
        }

        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
        {
            return WindowFrame.None;
        }

        var style = (uint)GetWindowLongPtr(handle.Handle, GWL_STYLE).ToInt64();
        var exStyle = (uint)GetWindowLongPtr(handle.Handle, GWL_EXSTYLE).ToInt64();
        var dpi = GetDpiForWindow(handle.Handle);
        if (dpi == 0)
        {
            dpi = DefaultDpi;
        }

        var rect = new NativeRect { Left = 0, Top = 0, Right = 100, Bottom = 100 };
        if (!AdjustWindowRectExForDpi(ref rect, style, false, exStyle, dpi))
        {
            return WindowFrame.None;
        }

        // 从 100×100 的客户区反推外框：窗口宽 − 客户宽。
        var frameWidthPx = (rect.Right - rect.Left) - 100;
        var frameHeightPx = (rect.Bottom - rect.Top) - 100;
        if (frameWidthPx < 0 || frameHeightPx < 0)
        {
            return WindowFrame.None;
        }

        var scaling = window.RenderScaling > 0 ? window.RenderScaling : 1.0;
        return new WindowFrame(frameWidthPx / scaling, frameHeightPx / scaling);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustWindowRectExForDpi(
        ref NativeRect rect, uint style, [MarshalAs(UnmanagedType.Bool)] bool menu, uint exStyle, uint dpi);
}
