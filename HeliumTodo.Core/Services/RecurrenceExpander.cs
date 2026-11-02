using System.Text.RegularExpressions;

namespace HeliumTodo.Core.Services;

/// <summary>
/// 重复规则展开器。
///
/// 支持两种写法（故意做得简单，够用又好懂）：
///   ""                只发生一次
///   "daily"           每天重复
///   "weekly:1,3,5"    每周一、三、五重复（0=周日，1=周一 … 6=周六）
/// </summary>
public static partial class RecurrenceExpander
{
    /// <summary>把规则文字变成「星期几」的集合。返回空集合表示「只发生一次」。</summary>
    public static IReadOnlyCollection<DayOfWeek> Parse(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return Array.Empty<DayOfWeek>();
        }

        var text = rule.Trim().ToLowerInvariant();

        // "daily" → 一周七天全选
        if (text == "daily")
        {
            return new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
            };
        }

        // "weekly:1,3,5" → 解析出星期几
        var match = WeeklyRegex().Match(text);
        if (match.Success)
        {
            var result = new List<DayOfWeek>();
            foreach (var part in match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var day) && day is >= 0 and <= 6)
                {
                    var dow = (DayOfWeek)day;
                    if (!result.Contains(dow)) result.Add(dow);
                }
            }
            return result;
        }

        // 认不出来的规则就当「不重复」，避免因为一条脏数据让整个界面崩掉
        return Array.Empty<DayOfWeek>();
    }

    /// <summary>
    /// 判断一条日程是否发生在指定日期。
    ///
    /// 规则：
    ///   · 不重复的日程：开始日期 == 目标日期 就算。
    ///   · 重复的日程：目标日期的星期几在规则里就算，并且不能早于首次开始日期。
    /// </summary>
    public static bool OccursOn(DateTime start, string? rule, DateOnly date)
    {
        var days = Parse(rule);

        // 不重复：只有开始那天算
        if (days.Count == 0)
        {
            return DateOnly.FromDateTime(start) == date;
        }

        // 重复：不能早于首次开始的那一天
        if (date < DateOnly.FromDateTime(start))
        {
            return false;
        }

        return days.Contains(date.DayOfWeek);
    }

    /// <summary>给界面显示的可读文字，例如 "每周一、三、五"。</summary>
    public static string Describe(string? rule)
    {
        var days = Parse(rule);
        if (days.Count == 0) return "单次";
        if (days.Count == 7) return "每天";

        static string Name(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "一",
            DayOfWeek.Tuesday => "二",
            DayOfWeek.Wednesday => "三",
            DayOfWeek.Thursday => "四",
            DayOfWeek.Friday => "五",
            DayOfWeek.Saturday => "六",
            _ => "日",
        };

        // 按周一→周日的顺序排列，看起来更自然
        var ordered = days.OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d).Select(Name);
        return "每周" + string.Join("、", ordered);
    }

    [GeneratedRegex(@"^weekly\s*:\s*([0-6][0-6,\s]*)$")]
    private static partial Regex WeeklyRegex();
}
