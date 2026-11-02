using CommunityToolkit.Mvvm.ComponentModel;
using HeliumTodo.Core.Configuration;
using HeliumTodo.Core.Services;
using HeliumTodo.Core.Theming;

namespace HeliumTodo.App.ViewModels;

/// <summary>
/// 全局共享的状态。
///
/// 为什么需要它？
///   三大核心功能是互相联系的 —— 在「待办」页勾掉一个待办，
///   「今日工作流」和「专注」页也应该跟着变。
///   最省事的做法就是让所有页面共用同一份状态，谁改了数据就发个通知。
///
/// 它就像一块公告板：谁改了数据，就在上面贴一张通知，
/// 关心这件事的页面自己来读最新版本。
/// </summary>
public sealed class AppState : ObservableObject
{
    /// <summary>当前的配置。</summary>
    public AppConfig Config { get; }

    /// <summary>配置服务（用来存盘）。</summary>
    public ConfigService ConfigService { get; }

    /// <summary>主题注册表。</summary>
    public ThemeRegistry ThemeRegistry { get; }

    /// <summary>专注计时器（全局唯一的一份，切页面也不会中断计时）。</summary>
    public FocusTimerService FocusTimer { get; }

    /// <summary>今天（改了日期就触发刷新）。</summary>
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly Today
    {
        get => _today;
        private set => SetProperty(ref _today, value);
    }

    /// <summary>「今日工作流」当前正在看哪一天（可以往前/往后翻）。</summary>
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                DataChanged?.Invoke(this, AppDataType.Timeline);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 数据变化通知
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 数据变了。
    /// 参数说明是哪一类数据变了，页面据此决定要不要刷新自己。
    /// </summary>
    public event EventHandler<AppDataType>? DataChanged;

    /// <summary>
    /// 设置变了（比如用户开关了局域网联机）。
    ///
    /// 和 <see cref="DataChanged"/> 分开：这类变化不用重读数据库，
    /// 订阅者只要刷新自己那几个文案和可用性就行。
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>通知「这类数据变了」。</summary>
    public void NotifyChanged(AppDataType dataType) => DataChanged?.Invoke(this, dataType);

    /// <summary>通知「设置变了」。</summary>
    public void NotifySettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>通知「所有数据都可能变了」（切换日期、批量操作后用）。</summary>
    public void NotifyAllChanged() => DataChanged?.Invoke(this, AppDataType.All);

    public AppState(
        ConfigService configService,
        ThemeRegistry themeRegistry,
        FocusTimerService focusTimer)
    {
        ConfigService = configService;
        ThemeRegistry = themeRegistry;
        FocusTimer = focusTimer;
        Config = configService.Current;
    }

    /// <summary>把配置存盘。</summary>
    public void SaveConfig() => ConfigService.Save();

    /// <summary>
    /// 检查是否跨天了（程序开着过了一夜）。
    /// 如果跨天就把「今天」更新掉，让界面回到新的一天。
    /// </summary>
    public void RefreshTodayIfNeeded()
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        if (now != Today)
        {
            Today = now;
            SelectedDate = now;
        }
    }
}

/// <summary>哪一类数据发生了变化。</summary>
[Flags]
public enum AppDataType
{
    /// <summary>什么都没变（不会用到）。</summary>
    None = 0,

    /// <summary>日程变了。</summary>
    Schedules = 1,

    /// <summary>待办变了。</summary>
    Todos = 2,

    /// <summary>专注记录变了。</summary>
    Focus = 4,

    /// <summary>自习室变了。</summary>
    StudyRoom = 8,

    /// <summary>时间轴需要重算（日程/待办/专注任意一项变了都要）。</summary>
    Timeline = Schedules | Todos | Focus,

    /// <summary>全能选手：所有数据都可能变了。</summary>
    All = Schedules | Todos | Focus | StudyRoom,
}

/// <summary>判断"变的是不是和我有关"的小工具。</summary>
public static class AppDataTypeExtensions
{
    /// <summary>
    /// 变化类型里有没有包含 <paramref name="watch"/> 关心的那一位。
    ///
    /// 为什么不用 <c>type.HasFlag(watch)</c>：
    /// HasFlag 问的是"type 里有没有把 watch 的<b>所有</b>位都置上"，
    /// 所以 <c>Todos.HasFlag(Timeline)</c> 是 false —— 单改一条待办，
    /// 今日工作流就以为和自己无关，界面停在旧数据上不刷新。
    /// </summary>
    public static bool Includes(this AppDataType changed, AppDataType watch)
        => (changed & watch) != 0;
}
