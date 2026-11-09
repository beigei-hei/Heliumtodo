// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Desktop.Views;
using HeliumTodo.Domain.Theming;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 主窗口的 ViewModel：管理左侧导航栏和当前显示的页面。
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    /// <summary>左侧导航栏里的所有条目。</summary>
    public ObservableCollection<NavigationItemViewModel> MenuItems { get; } = new();

    /// <summary>
    /// 导航栏底部的条目。
    ///
    /// 【为什么现在空着？】
    ///   以前「设置」停在这儿（和原生导航栏的习惯一致：功能入口在上、设置在底）。
    ///   0.3.3 按用户要求把它上提到「自习室（联机）」下面 —— 设置是"配置"，
    ///   不是"退出"，放在最底下离手指太远，而且底部那块本来留给将来的账户/状态。
    ///   集合保留着：插件或以后的页面想钉在底部，仍然可以用。
    /// </summary>
    public ObservableCollection<NavigationItemViewModel> FooterItems { get; } = new();

    /// <summary>
    /// 图标轨是不是展开成"图标 + 名称"的样子。
    ///
    /// 展开状态放在外壳 VM 而不是某个按钮上：它同时决定轨道宽度、
    /// 每个条目的宽度、以及每个条目要不要显示名称，是整条轨的状态。
    /// </summary>
    [ObservableProperty]
    private bool _isRailExpanded;

    /// <summary>当前轨道宽度（收起 56 / 展开 188），界面绑它，Transition 负责动画。</summary>
    public double RailWidth => RailMetrics.RailWidth(IsRailExpanded);

    /// <summary>
    /// 轨道宽度过渡的时长。
    /// 从 <see cref="RailMetrics.AnimationDurationMs"/> 来，XAML 只绑定、不写字面量 ——
    /// 否则改时长要同时改 XAML、改降质时长、改自检三处。
    /// </summary>
    public TimeSpan RailAnimationDuration => TimeSpan.FromMilliseconds(RailMetrics.AnimationDurationMs);

    /// <summary>当前每个导航按钮的宽度，条目模板绑它。</summary>
    public double RailItemWidth => RailMetrics.ItemWidth(IsRailExpanded);

    /// <summary>
    /// 条目名称的透明度（收起 0 / 展开 1）。
    /// 这里刻意不用 IsVisible：改可见性会把整棵布局树换一遍，展开动画的第一帧
    /// 要为 7 个名称重新排版一次（实测那一帧要 25 ms，是整段动画里唯一的尖峰）。
    /// 名称一直待在布局里、只是透明，轨道用 ClipToBounds 裁掉超出的部分即可。
    /// </summary>
    public double RailLabelOpacity => IsRailExpanded ? 1.0 : 0.0;

    partial void OnIsRailExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(RailWidth));
        OnPropertyChanged(nameof(RailItemWidth));
        OnPropertyChanged(nameof(RailLabelOpacity));
        OnPropertyChanged(nameof(RailToggleGlyph));

        // 条目自己也要知道 —— 模板里拿不到外壳 VM（数据上下文是条目本身），
        // 所以状态往下发一份，别让每行去做 $parent[] 的祖先查找。
        foreach (var entry in MenuItems.Concat(FooterItems))
        {
            entry.IsExpanded = value;
        }
    }

    /// <summary>
    /// 轨道顶部那个按钮的字形：收起时是"向右展开"，展开时是"向左收回"。
    /// 用字形而不是两张图片：颜色、大小、对齐都跟着主题走，
    /// 也不会因为 DPI 变化糊掉（同 ShellView 里其它图标）。
    /// </summary>
    public string RailToggleGlyph => IsRailExpanded ? "\uE76B" : "\uE76C";

    /// <summary>点轨道最上面那个按钮：展开 / 收起。</summary>
    [RelayCommand]
    private void ToggleRail() => IsRailExpanded = !IsRailExpanded;

    /// <summary>中间显示的内容（当前页面）。</summary>
    [ObservableProperty]
    private object? _currentPage;

    /// <summary>当前选中的导航条目。</summary>
    [ObservableProperty]
    private NavigationItemViewModel? _selectedItem;

    /// <summary>状态栏文字（可以显示「已保存」「正在加载」等）。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// 左栏底部显示的版本号，例如 "v0.5.0-alpha"。
    ///
    /// 从 <see cref="HeliumTodo.Domain.VersionInfo"/> 取，所以升级版本号只要改
    /// 根目录 Directory.Build.props 里的那一处，界面会自动跟着变，不用手工同步。
    /// </summary>
    public string VersionText => HeliumTodo.Domain.VersionInfo.Display;

    /// <summary>顶部信息条：有严重冲突时显示红色提醒。</summary>
    [ObservableProperty]
    private string _alertMessage = string.Empty;

    [ObservableProperty]
    private bool _isAlertVisible;

    // ── 背景壁纸 ──

    /// <summary>当前该显示的壁纸帧；没开壁纸时是 null。</summary>
    [ObservableProperty]
    private Avalonia.Media.IImage? _wallpaperImage;

    /// <summary>要不要显示壁纸图层（有没有加载成功）。</summary>
    [ObservableProperty]
    private bool _isWallpaperVisible;

    private readonly WallpaperService? _wallpaper;

    /// <summary>
    /// 壁纸是不是会动的（动图 / 视频）。
    /// 窗口按这个决定"要不要把图预缩放成窗口尺寸"：动图每帧都是新图，
    /// 预缩放等于每帧多做一次全窗口绘制，不划算（见 WallpaperScaler 的注释）。
    /// </summary>
    public bool IsWallpaperAnimated => _wallpaper?.IsAnimated == true;

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;

        BuildNavigation();

        // 默认停在「今日工作流」
        SelectedItem = MenuItems.FirstOrDefault();

        // 壁纸：订阅"换帧了"，把当前帧搬到自己身上给界面绑定。
        // 界面直接绑服务上的属性会很别扭（服务不实现属性变更通知），
        // 所以这里做一层转接。
        _wallpaper = services.GetService(typeof(WallpaperService)) as WallpaperService;
        if (_wallpaper is not null)
        {
            _wallpaper.Changed += OnWallpaperChanged;
            RefreshWallpaper();
        }
    }

    private void OnWallpaperChanged(object? sender, EventArgs e) => RefreshWallpaper();

    private void RefreshWallpaper()
    {
        OnPropertyChanged(nameof(IsWallpaperAnimated));
        WallpaperImage = _wallpaper?.CurrentFrame;
        IsWallpaperVisible = _wallpaper is { IsLoaded: true } && WallpaperImage is not null;
    }

    /// <summary>搭出内置的导航结构。</summary>
    private void BuildNavigation()
    {
        MenuItems.Add(new NavigationItemViewModel(
            "\uE80F",      // 日历
            "今日工作流",
            () => new DashboardViewModel(_services)));

        MenuItems.Add(new NavigationItemViewModel(
            "\uE787",      // 时钟
            "日程",
            () => new ScheduleViewModel(_services)));

        MenuItems.Add(new NavigationItemViewModel(
            "\uE73A",      // 清单
            "待办事项",
            () => new TodoViewModel(_services)));

        MenuItems.Add(new NavigationItemViewModel(
            "\uE916",      // 计时器
            "专注",
            () => new FocusViewModel(_services)));

        MenuItems.Add(new NavigationItemViewModel(
            "\uE716",      // 多人
            "自习室",
            () => new StudyRoomViewModel(_services)));

        // 设置紧跟"自习室（联机）"下面 —— 见 FooterItems 的注释
        MenuItems.Add(new NavigationItemViewModel(
            "\uE713",      // 设置
            "设置",
            () => new SettingsViewModel(_services)));
    }

    /// <summary>
    /// 用户点了导航栏。
    /// </summary>
    public async Task NavigateAsync(NavigationItemViewModel? item)
    {
        if (item is null) return;

        SelectedItem = item;
        UpdateSelection(item);

        // 页面只创建一次，之后来回切换都复用（保留滚动位置、输入内容）
        var page = item.GetOrCreatePage();

        if (ReferenceEquals(page, CurrentPage)) return;

        CurrentPage = page;

        StatusText = item.Title;

        // 让新页面自己去刷新数据
        if (page is ViewModelBase vm)
        {
            try
            {
                await vm.OnNavigatedToAsync();
            }
            catch (Exception ex)
            {
                // 单个页面刷新失败不能把整个程序带崩
                System.Diagnostics.Debug.WriteLine($"[Shell] 页面 {item.Title} 刷新失败：{ex}");
                StatusText = $"{item.Title} 加载失败：{ex.Message}";
            }
        }
    }

    /// <summary>把选中态同步给所有导航项（图标轨要据此画高亮块）。</summary>
    private void UpdateSelection(NavigationItemViewModel current)
    {
        foreach (var entry in MenuItems.Concat(FooterItems))
        {
            entry.IsSelected = ReferenceEquals(entry, current);
        }
    }

    /// <summary>显示一条顶部提醒（例如「今天有 2 处时间冲突」）。</summary>
    public void ShowAlert(string message)
    {
        AlertMessage = message;
        IsAlertVisible = !string.IsNullOrWhiteSpace(message);
    }

    /// <summary>收起提醒。</summary>
    public void DismissAlert() => IsAlertVisible = false;
}

/// <summary>
/// 导航栏里的一个条目。
/// </summary>
public sealed partial class NavigationItemViewModel : ObservableObject
{
    private object? _page;

    /// <summary>
    /// 是不是「正在看的那一项」。
    /// 图标轨靠它决定要不要画强调色实心块 —— 所以这里要能被界面观察到，
    /// 不能是个普通属性（普通属性改了界面不知道）。
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 图标轨展开时，条目要不要显示名称。
    ///
    /// 由外壳统一发下来（见 ShellViewModel.OnIsRailExpandedChanged）：
    /// 条目模板的数据上下文是条目自己，拿外壳 VM 得走祖先查找，
    /// 既啰嗦又容易在编译绑定里失败，不如直接推一个布尔下来。
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// 名称的透明度（收起 0 / 展开 1），模板绑它而不是绑 IsVisible。
    ///
    /// 为什么不用可见性：改可见性会把整棵布局树换一遍，展开动画的第一帧要为
    /// 所有名称重新排一次版（实测那一帧 25 ms，是整段动画里唯一的尖峰）。
    /// 名称一直待在布局里、只是透明，轨道用 ClipToBounds 裁掉超出的部分。
    /// </summary>
    public double RailLabelOpacity => IsExpanded ? 1.0 : 0.0;

    /// <summary>名称淡入的时长（和轨道宽度同一个数，都来自 RailMetrics）。</summary>
    public TimeSpan RailAnimationDuration => TimeSpan.FromMilliseconds(RailMetrics.AnimationDurationMs);

    /// <summary>条目宽度：收起 44、展开 176（和轨道同源，见 RailMetrics）。</summary>
    public double ItemWidth => HeliumTodo.Domain.Theming.RailMetrics.ItemWidth(IsExpanded);

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(RailLabelOpacity));
        OnPropertyChanged(nameof(ItemWidth));
    }

    public NavigationItemViewModel(string icon, string title, Func<object> pageFactory)
    {
        Icon = icon;
        Title = title;
        PageFactory = pageFactory;
    }

    /// <summary>
    /// 图标：一个来自系统图标字体的私有区字符（不是 emoji）。
    ///
    /// 为什么不用 emoji：emoji 是彩色位图，在 56px 纯图标轨里大小不齐、
    /// 颜色也不受强调色控制（选中时想让它变白都做不到）。
    /// 换成字形之后，颜色、大小、对齐全都能跟着主题走。
    /// 字形码位来自 Segoe Fluent Icons（Win11）/ Segoe MDL2 Assets（Win10）。
    /// </summary>
    public string Icon { get; }

    /// <summary>显示名称。</summary>
    public string Title { get; }

    /// <summary>页面的创建方法（延迟创建：第一次点开才 new）。</summary>
    private Func<object> PageFactory { get; }

    /// <summary>取页面，没有就创建一个（同一个条目永远返回同一个页面）。</summary>
    public object GetOrCreatePage() => _page ??= PageFactory();
}
