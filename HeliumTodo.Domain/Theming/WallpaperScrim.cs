// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Theming;

/// <summary>
/// 壁纸蒙版：一层盖在壁纸上的单色，用来把照片压淡，让上面的卡片和文字看得清。
///
/// 【为什么只有一个蒙版？】
///   0.3.3 之前有两层：一层白色的「蒙版浓度」（用户拖）、一层灰色的「暗色灰蒙版」
///   （只在暗色下出现）。两层叠在一起的效果很难预判 —— 用户在设置里拖上面那条
///   觉得太淡，拖下面那条又觉得发灰，最后干脆两个都不敢动。
///   现在合并成一条强度，颜色交给明暗决定：亮色用白、暗色用黑。
///   白的是"提亮"，黑的是"压暗"——暗色模式下要的正是后者：只让壁纸变暗，
///   不要把颜色洗掉。
///
/// 【为什么暗色要更强？】
///   同一张彩色照片，在白色卡片旁边只是有点花；在深灰卡片旁边就成了唯一的光源，
///   眼睛全被它吸走。所以暗色下同样的滑块值要额外压一档（×1.6），
///   并留 0.95 的上限 —— 再高壁纸就完全看不见了，那还不如关掉。
/// </summary>
public static class WallpaperScrim
{
    /// <summary>暗色模式下强度的倍率。</summary>
    public const double DarkStrengthBoost = 1.6;

    /// <summary>强度上限，留一点让壁纸始终看得见。</summary>
    public const double MaxStrength = 0.95;

    /// <summary>
    /// 把用户拖的百分比（0–100）换算成真正画到界面上的不透明度（0–1）。
    /// </summary>
    /// <param name="percent">设置里的值。</param>
    /// <param name="isDark">当前是不是暗色模式。</param>
    public static double ComputeStrength(int percent, bool isDark)
    {
        var baseValue = Math.Clamp(percent, 0, 100) / 100.0;

        if (!isDark) return baseValue;

        return Math.Min(MaxStrength, baseValue * DarkStrengthBoost);
    }

    /// <summary>
    /// 蒙版该用什么颜色：亮色白（提亮）、暗色黑（压暗）。
    /// 返回十六进制字符串，方便界面层直接转成画刷，也方便单测断言。
    /// </summary>
    public static string ColorHex(bool isDark) => isDark ? "#000000" : "#FFFFFF";
}
