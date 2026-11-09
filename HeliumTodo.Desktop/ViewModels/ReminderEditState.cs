// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HeliumTodo.Domain.Models;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 下拉里的一项"提前多久提醒"。
/// </summary>
/// <param name="Minutes">提前的分钟数；<see cref="IsCustom"/> 为 true 时这个值没意义。</param>
/// <param name="DisplayName">显示的文字。</param>
/// <param name="IsCustom">是不是"自定义…"那一项。</param>
public sealed record ReminderOption(int? Minutes, string DisplayName, bool IsCustom = false);

/// <summary>
/// 日程和待办共用的"提醒"编辑器。
///
/// 为什么抽成一个小类而不是两边各写一遍：
///   ① 两处的选项、换算、文案必须一模一样，复制粘贴迟早会跑偏；
///   ② 「自定义」这一项要额外出两个数字框，这套状态（选了自定义没、小时多少、分钟多少）
///      在两个 ViewModel 里各维护一份，很容易一次只改了一边。
///
/// 界面上的样子：一个下拉（不提醒 / 开始时 / 提前 5 分钟 / … / 自定义…），
/// 选到「自定义…」时下面再出「小时 / 分钟」两个数字框。
/// </summary>
public sealed partial class ReminderEditState : ObservableObject
{
    /// <summary>下拉里的全部选项（顺序就是界面上的顺序）。</summary>
    public ObservableCollection<ReminderOption> Options { get; } = new()
    {
        new ReminderOption(null, "不提醒"),
        new ReminderOption(0, "开始时"),
        new ReminderOption(5, "提前 5 分钟"),
        new ReminderOption(10, "提前 10 分钟"),
        new ReminderOption(30, "提前 30 分钟"),
        new ReminderOption(60, "提前 1 小时"),
        new ReminderOption(120, "提前 2 小时"),
        new ReminderOption(null, "自定义…", IsCustom: true),
    };

    /// <summary>当前选中的那一项。</summary>
    [ObservableProperty]
    private ReminderOption _selected;

    /// <summary>自定义：提前几小时。</summary>
    [ObservableProperty]
    private int _hours;

    /// <summary>自定义：再提前几分钟。</summary>
    [ObservableProperty]
    private int _minutes;

    public ReminderEditState()
    {
        _selected = Options[0];
    }

    /// <summary>是不是选到了「自定义…」——界面据此决定要不要显示两个数字框。</summary>
    public bool IsCustom => Selected.IsCustom;

    /// <summary>最终要写进 <see cref="ScheduleItem.ReminderMinutes"/> 的值。</summary>
    public int? LeadMinutes => Selected.IsCustom
        ? ReminderPresets.Compose(Hours, Minutes)
        : Selected.Minutes;

    /// <summary>切到「自定义…」时，两个数字框要跟着可用。</summary>
    partial void OnSelectedChanged(ReminderOption value) => OnPropertyChanged(nameof(IsCustom));

    /// <summary>
    /// 把数据库里的值读进编辑器。
    /// 是预设就用对应那一项，不是预设（比如用户手填的 45 分钟）就自动落到「自定义…」。
    /// </summary>
    public void SetFrom(int? minutes)
    {
        if (minutes is null)
        {
            Selected = Options[0];
            return;
        }

        foreach (var option in Options)
        {
            if (!option.IsCustom && option.Minutes == minutes)
            {
                Selected = option;
                return;
            }
        }

        // 不是预设：拆成小时 + 分钟，选中「自定义…」
        var (hours, mins) = ReminderPresets.Split(minutes);
        Hours = hours;
        Minutes = mins;
        Selected = Options[^1];
    }
}