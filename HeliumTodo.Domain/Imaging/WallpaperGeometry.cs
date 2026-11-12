// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Imaging;

/// <summary>
/// 壁纸预缩放的几何计算（纯算术，不依赖 Avalonia）。
///
/// 为什么值得单独测：
///   拖拽改窗口时壁纸"突然放大/缩小"，根因是预缩放位图被裁成了"旧窗口的长宽比"，
///   贴到新窗口时 UniformToFill 要重新 cover 一次，松手重做位图后又跳回 1:1。
///   只要位图始终保留**原图长宽比**，UniformToFill 在任何位图尺寸下裁出的画面都完全一致，
///   替换位图只改变清晰度、不改变构图 —— 拖拽过程中就不会再跳。
/// </summary>
public static class WallpaperGeometry
{
    /// <summary>覆盖目标窗口所需的最小位图尺寸（保留原图长宽比）。</summary>
    public readonly record struct CoverSize(int Width, int Height);

    /// <param name="sourceWidth">原图像素宽。</param>
    /// <param name="sourceHeight">原图像素高。</param>
    /// <param name="targetWidth">窗口像素宽。</param>
    /// <param name="targetHeight">窗口像素高。</param>
    public static CoverSize Compute(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new CoverSize(Math.Max(0, targetWidth), Math.Max(0, targetHeight));
        }

        if (targetWidth <= 0 || targetHeight <= 0)
        {
            return new CoverSize(sourceWidth, sourceHeight);
        }

        // 取较大的缩放比：位图两边都不小于窗口，UniformToFill 只需要轻微缩小/裁边。
        var scale = Math.Max(
            (double)targetWidth / sourceWidth,
            (double)targetHeight / sourceHeight);

        // 向上取整，避免算出 1919.6 → 1919 之后窗口右边露一条缝。
        var width = Math.Max(targetWidth, (int)Math.Ceiling(sourceWidth * scale));
        var height = Math.Max(targetHeight, (int)Math.Ceiling(sourceHeight * scale));
        return new CoverSize(width, height);
    }
}
