// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HeliumTodo.Domain.Imaging;

namespace HeliumTodo.Desktop.Services;

/// <summary>
/// 把壁纸预先缩放成"刚好盖住窗口"的一张位图，并**保留原图长宽比**。
///
/// 为什么需要它：
///   <c>Image</c> 直接绑原图、靠 <c>Stretch="UniformToFill"</c> 去撑满窗口时，
///   **每一帧重绘都要重新采样一遍整张图**。2560×1440 的窗口上实测这一项要
///   ~18–27 ms/帧 —— 页面切换、对话框、窗口动画全都替它交这份钱。
///   先把图缩放成接近窗口的像素尺寸，之后每帧就是一次接近 1:1 的拷贝。
///
/// 为什么不再裁成窗口长宽比：
///   如果位图本身是"旧窗口的形状"，拖拽改窗口后 UniformToFill 会重新 cover 一次
///   （画面被放大或缩小），松手重做位图又跳回 1:1 —— 用户看到的就是"壁纸突然放大缩小"。
///   保留原图长宽比后，UniformToFill 对任何尺寸的同比例位图都裁出同一块画面：
///   拖拽期间只是平滑跟随窗口，重做位图只提升清晰度，构图不跳。
///
/// 0.5.0 起壁纸只支持静态图，所有壁纸都走这条预缩放路径；GIF/MP4 已在解码入口拒绝。
/// </summary>
public static class WallpaperScaler
{
    /// <summary>
    /// 把 <paramref name="source"/> 等比缩放到"覆盖 <paramref name="target"/> 窗口"的尺寸，
    /// 不裁切；调用方用 <c>Stretch="UniformToFill"</c> 显示，由控件负责裁边。
    /// </summary>
    public static RenderTargetBitmap? Scale(IImage source, PixelSize target)
    {
        if (target.Width < 2 || target.Height < 2)
        {
            return null;
        }

        // 源图的真实像素尺寸：Bitmap 才有 PixelSize；其它实现退回 DIP 尺寸。
        var sourceWidth = source is Bitmap pixelBitmap ? pixelBitmap.PixelSize.Width : source.Size.Width;
        var sourceHeight = source is Bitmap pixelBitmap2 ? pixelBitmap2.PixelSize.Height : source.Size.Height;
        if (sourceWidth < 1 || sourceHeight < 1)
        {
            return null;
        }

        var cover = WallpaperGeometry.Compute(
            (int)Math.Round(sourceWidth),
            (int)Math.Round(sourceHeight),
            target.Width,
            target.Height);

        if (cover.Width < 1 || cover.Height < 1)
        {
            return null;
        }

        var bitmap = new RenderTargetBitmap(new PixelSize(cover.Width, cover.Height), new Vector(96, 96));

        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawImage(
                source,
                new Rect(0, 0, sourceWidth, sourceHeight),
                new Rect(0, 0, cover.Width, cover.Height));
        }

        return bitmap;
    }
}
