// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Windowing;

/// <summary>
/// 启动时窗口的尺寸与位置（纯算术，不依赖 Avalonia，便于无头测试）。
/// 尺寸按“物理像素”给出目标 1920×1080，再按当前缩放换算成 DIP；
/// 屏幕装不下时按工作区 94% 收缩，最后在物理坐标里居中。
/// </summary>
public static class StartupWindowPlacement
{
    public const int PreferredPixelWidth = 1920;
    public const int PreferredPixelHeight = 1080;

    /// <summary>窗口最终应取的 DIP 尺寸与物理左上角坐标。</summary>
    public readonly record struct Placement(double WidthDip, double HeightDip, int LeftPx, int TopPx);

    /// <param name="workArea">工作区（物理像素，任务栏已排除）。</param>
    /// <param name="scaling">渲染缩放（1.0 / 1.5 / 2.0 …）。</param>
    public static Placement Compute(
        int workX,
        int workY,
        int workAreaWidth,
        int workAreaHeight,
        double scaling,
        double minWidthDip,
        double minHeightDip)
    {
        if (scaling <= 0 || double.IsNaN(scaling) || double.IsInfinity(scaling))
        {
            scaling = 1.0;
        }

        // 工作区不可用时退回 1920×1080 DIP，至少别把窗口摆到 (0,0) 之外。
        if (workAreaWidth <= 0 || workAreaHeight <= 0)
        {
            return new Placement(
                Math.Max(minWidthDip, PreferredPixelWidth),
                Math.Max(minHeightDip, PreferredPixelHeight),
                workX,
                workY);
        }

        var widthDip = PreferredPixelWidth / scaling;
        var heightDip = PreferredPixelHeight / scaling;
        var workWidthDip = workAreaWidth / scaling;
        var workHeightDip = workAreaHeight / scaling;

        // 只收缩装不下的那一维：1920×1080 在 150% 的 2560×1600 上宽度放得下、高度也放得下，
        // 不该因为另一维溢出就把两维一起压小。
        if (widthDip > workWidthDip)
        {
            widthDip = Math.Max(minWidthDip, workWidthDip * 0.94);
        }

        if (heightDip > workHeightDip)
        {
            heightDip = Math.Max(minHeightDip, workHeightDip * 0.94);
        }

        widthDip = Math.Max(widthDip, minWidthDip);
        heightDip = Math.Max(heightDip, minHeightDip);

        var widthPx = (int)Math.Round(widthDip * scaling);
        var heightPx = (int)Math.Round(heightDip * scaling);

        return new Placement(
            widthDip,
            heightDip,
            workX + Math.Max(0, (workAreaWidth - widthPx) / 2),
            workY + Math.Max(0, (workAreaHeight - heightPx) / 2));
    }
}
