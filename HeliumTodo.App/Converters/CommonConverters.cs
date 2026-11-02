using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HeliumTodo.App.Converters;

/// <summary>
/// 把 true / false 变成透明度（true → 1，false → 0.35）。
///
/// 用在「已完成待办」这类地方：做完了就把整行变淡，视觉上自然区分开。
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    /// <summary>true 时的透明度。</summary>
    public double TrueOpacity { get; set; } = 1.0;

    /// <summary>false 时的透明度。</summary>
    public double FalseOpacity { get; set; } = 0.35;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? TrueOpacity : FalseOpacity;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("这个转换器只支持单向绑定。");
}

/// <summary>
/// 把布尔值变成「显示 / 隐藏」。
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>设为 true 时反转逻辑（true → 隐藏）。</summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("这个转换器只支持单向绑定。");
}

/// <summary>
/// 根据优先级返回对应的画刷。
///
/// 参数用来区分要哪一种：留空 = 背景，传 "Border" = 边框，传 "Text" = 文字。
/// 例如：{Binding Priority, Converter={StaticResource PriorityBrush}, ConverterParameter=Border}
/// </summary>
public sealed class PriorityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var priority = value switch
        {
            HeliumTodo.Core.Models.Priority p => p,
            int i => (HeliumTodo.Core.Models.Priority)i,
            _ => HeliumTodo.Core.Models.Priority.Normal,
        };

        var suffix = (parameter as string) switch
        {
            "Border" => "BorderBrush",
            "Text" => "TextBrush",
            _ => "Brush",
        };

        var key = priority switch
        {
            HeliumTodo.Core.Models.Priority.Urgent => "PriorityUrgent",
            HeliumTodo.Core.Models.Priority.High => "PriorityHigh",
            HeliumTodo.Core.Models.Priority.Low => "PriorityLow",
            _ => "PriorityNormal",
        } + suffix;

        if (Avalonia.Application.Current?.TryFindResource(key, out var resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("这个转换器只支持单向绑定。");
}

/// <summary>
/// 把优先级的枚举值变成中文文字：Urgent → 紧急。
/// </summary>
public sealed class PriorityTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var priority = value switch
        {
            HeliumTodo.Core.Models.Priority p => p,
            int i => (HeliumTodo.Core.Models.Priority)i,
            _ => HeliumTodo.Core.Models.Priority.Normal,
        };

        return priority switch
        {
            HeliumTodo.Core.Models.Priority.Urgent => "紧急",
            HeliumTodo.Core.Models.Priority.High => "较高",
            HeliumTodo.Core.Models.Priority.Low => "较低",
            _ => "普通",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("这个转换器只支持单向绑定。");
}

/// <summary>
/// 把「时段」枚举变成中文：Morning → 上午。
/// 不这样做的话，界面上会直接显示英文的 "Morning"，很出戏。
/// </summary>
public sealed class TimeBlockTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var block = value switch
        {
            HeliumTodo.Core.Models.TimeBlock b => b,
            int i => (HeliumTodo.Core.Models.TimeBlock)i,
            _ => HeliumTodo.Core.Models.TimeBlock.Any,
        };

        return block switch
        {
            HeliumTodo.Core.Models.TimeBlock.Morning => "上午",
            HeliumTodo.Core.Models.TimeBlock.Afternoon => "下午",
            HeliumTodo.Core.Models.TimeBlock.Evening => "晚上",
            _ => "不限",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("这个转换器只支持单向绑定。");
}
