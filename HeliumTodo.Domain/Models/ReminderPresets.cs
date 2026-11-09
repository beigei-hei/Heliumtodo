// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Models;

/// <summary>
/// 提醒提前量：一个可以「不提醒」的值，单位是分钟。
///
/// 为什么用 int? 而不是枚举：
///   界面要支持"自定义"，用户可以填 7 小时 25 分钟这种任意值，
///   枚举装不下。null 表示不提醒，0 表示"开始时提醒" —— 这两个语义必须能区分开，
///   所以不能用 0 兼作"关闭"。
/// </summary>
public static class ReminderPresets
{
    /// <summary>不提醒。（可空类型不能声明成 const，所以这里用 static readonly）</summary>
    public static readonly int? None = null;

    /// <summary>日程/待办开始时提醒。</summary>
    public const int AtStart = 0;

    /// <summary>上限：提前 7 天。再长就没意义了 —— 数据库里存的是分钟数，
    /// 太大了界面上也不好显示（"提前 10080 分钟"），这里给个线。</summary>
    public const int MaxMinutes = 7 * 24 * 60;

    /// <summary>
    /// 下拉框里的固定选项（"自定义"由界面单独处理）。
    /// 顺序从"不提醒"到"提前最久"，符合用户从上往下找的习惯。
    /// </summary>
    public static IReadOnlyList<int?> Presets { get; } = new int?[]
    {
        None,
        AtStart,
        5,
        10,
        30,
        60,
        120,
    };

    /// <summary>某个值是不是固定选项之一。</summary>
    public static bool IsPreset(int? minutes) => Presets.Contains(minutes);

    /// <summary>
    /// 把分钟数说成人话：「不提醒」「开始时」「提前 10 分钟」「提前 1 小时」
    /// 「提前 1 小时 30 分钟」。
    /// </summary>
    public static string Describe(int? minutes)
    {
        if (minutes is not { } value) return "不提醒";
        if (value <= 0) return "开始时";

        var hours = value / 60;
        var rest = value % 60;

        if (hours == 0) return $"提前 {rest} 分钟";
        if (rest == 0) return $"提前 {hours} 小时";
        return $"提前 {hours} 小时 {rest} 分钟";
    }

    /// <summary>把用户填的小时/分钟合成分数，并夹到合法区间。</summary>
    public static int Compose(int hours, int minutes)
    {
        var total = Math.Max(0, hours) * 60 + Math.Max(0, minutes);
        return Math.Clamp(total, 0, MaxMinutes);
    }

    /// <summary>把一个分钟数拆成小时 + 分钟，给"自定义"那两个数字框填初值。</summary>
    public static (int Hours, int Minutes) Split(int? minutes)
    {
        if (minutes is not { } value || value <= 0) return (0, 0);
        return (value / 60, value % 60);
    }
}