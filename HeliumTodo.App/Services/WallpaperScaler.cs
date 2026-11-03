using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace HeliumTodo.App.Services;

/// <summary>
/// 把壁纸预先缩放成"和窗口一样大"的一张位图。
///
/// 为什么需要它：
///   <c>Image</c> 直接绑原图、靠 <c>Stretch="UniformToFill"</c> 去撑满窗口时，
///   **每一帧重绘都要重新采样一遍整张图**。2560×1440 的窗口上实测这一项要
///   ~18–27 ms/帧 —— 页面切换、对话框、窗口动画全都替它交这份钱。
///   先把图缩放成窗口像素尺寸，之后每帧就是一次 1:1 拷贝。
///
/// 只对静态壁纸这么做：动图/视频每帧都是一张新图，缩放一次的成本等于每帧多做一次
/// 全窗口绘制，不划算，那边仍然交给 <c>Image</c> 自己撑。
/// </summary>
internal static class WallpaperScaler
{
    /// <summary>
    /// 把 <paramref name="source"/> 按"填满"规则缩放到 <paramref name="target"/>。
    /// 返回的位图和窗口像素 1:1，调用方应该用 <c>Stretch="Fill"</c> 显示它。
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

        var bitmap = new RenderTargetBitmap(target, new Vector(96, 96));

        // UniformToFill：取较大的缩放比，多的部分居中裁掉（和 Image 默认的填满规则一致）。
        var scale = Math.Max(
            (double)target.Width / sourceWidth,
            (double)target.Height / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        var left = (target.Width - width) / 2.0;
        var top = (target.Height - height) / 2.0;

        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawImage(
                source,
                new Rect(0, 0, sourceWidth, sourceHeight),
                new Rect(left, top, width, height));
        }

        return bitmap;
    }
}