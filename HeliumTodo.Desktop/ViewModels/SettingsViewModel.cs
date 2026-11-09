// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliumTodo.Desktop.Services;
using HeliumTodo.Domain;
using HeliumTodo.Domain.Backup;
using HeliumTodo.Application.Configuration;
using HeliumTodo.Domain.Lifecycle;
using HeliumTodo.Domain.Theming;
using HeliumTodo.Infrastructure;
using AccentSourceDefs = HeliumTodo.Application.Configuration.AccentSources;
using ColorSchemeDefs = HeliumTodo.Application.Configuration.ColorSchemes;
using HeliumTodo.Application.Abstractions;
using HeliumTodo.Domain.Policies;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 「设置」页面。
///
/// 这里能看出「可分离的配置文件」是怎么落地的：
///   所有改动都会写进 %AppData%\Helium Todo\config.json，
///   这个文件是纯文本 JSON，可以用记事本直接改，也可以拷到别的电脑上。
///   页面上还提供了「打开配置文件夹」按钮，方便找到它。
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly ThemeRegistry _themeRegistry;
    private readonly ThemeService _themeService;
    private readonly AppState _state;
    private readonly WallpaperService _wallpaper;
    private readonly DataBackupService _backup;
    private readonly IFolderLauncher? _folderLauncher;

    /// <summary>
    /// 保存配置的防抖定时器。
    ///
    /// 为什么需要它？
    ///   调色板上的方块和滑块是「拖动即生效」的，拖一下会发出几十上百次变化。
    ///   如果每次都立刻写盘，就是在用几十次/秒的频率写文件 —— 没必要，还费硬盘。
    ///   所以改成"最后一次改动之后 400 毫秒再写"，中间的变化直接吞掉。
    /// </summary>
    private readonly Avalonia.Threading.DispatcherTimer _saveDebounce;

    /// <summary>
    /// 提示条的"到点自动收起来"心跳。
    ///
    /// 为什么不用 Task.Delay？—— 提示条可能被反复重开（用户连点几下），
    /// 每次都起一个 Task 会留下若干"到点就关"的僵尸，后到的那个会把
    /// 用户刚看到的新提示一起关掉。用一个常驻定时器 + 一个"显示时刻"，
    /// 重开时只刷新时刻，永远只有一条在生效。
    ///
    /// 200ms 是取舍：再密没意义（肉眼分不出 3.0 和 3.2 秒），
    /// 再疏（比如 1s）会让"3 秒"变成最坏 3.9 秒。
    /// </summary>
    private readonly Avalonia.Threading.DispatcherTimer _noticeTimer;

    /// <summary>提示条是什么时候弹出来的（自动消失从这里算）。</summary>
    private DateTime _noticeShownAt;

    /// <summary>
    /// 界面是不是已经建好了。
    ///
    /// 为什么需要这个？
    ///   构造函数里会初始化 SelectedTheme，这会触发下面的 OnSelectedThemeChanged。
    ///   如果不加这道闸，程序一打开设置页就会弹出「已切换到 xxx 主题」——
    ///   可用户根本什么都没点。加上闸门后，只有「用户真的改了」才弹提示。
    /// </summary>
    private bool _isInitialized;

    public SettingsViewModel(IServiceProvider services)
    {
        _configService = (ConfigService)services.GetService(typeof(ConfigService))!;
        _themeRegistry = (ThemeRegistry)services.GetService(typeof(ThemeRegistry))!;
        _themeService = (ThemeService)services.GetService(typeof(ThemeService))!;
        _state = (AppState)services.GetService(typeof(AppState))!;
        _wallpaper = (WallpaperService)services.GetService(typeof(WallpaperService))!;
        _backup = (DataBackupService)services.GetService(typeof(DataBackupService))!;
        // 可选依赖：注册了就优先用 Avalonia 的 Launcher，没注册就退回到自己起 explorer.exe
        _folderLauncher = services.GetService(typeof(IFolderLauncher)) as IFolderLauncher;

        Config = _configService.Current;

        // 直接写字段：这时候 _isInitialized 还是 false，不该触发一次"保存 + 提示"
        _enableNetworkStudyRoom = Config.EnableNetworkStudyRoom;

        // 防抖保存：150 毫秒内没有新的改动才真正写盘。
        // 原来是 400ms —— 用户拖完滑块松手、马上关窗口，那一下就丢了。
        // 150ms 已经足够把拖动的连续触发合成一次写盘，肉眼上也接近"改了就存"。
        _saveDebounce = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _saveDebounce.Tick += (_, _) =>
        {
            _saveDebounce.Stop();
            _configService.Save();
        };

        _noticeTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _noticeTimer.Tick += (_, _) => ExpireNotice(DateTime.Now);

        // ── 明暗 ──
        // 从 0.3.3 起界面上不再列"九套主题"，只留亮色 / 暗色两档。
        // 主题作用退化成"决定控件模板的中性色"，强调色由下面那块统一决定。
        foreach (var scheme in ColorSchemeDefs.All)
        {
            ColorSchemes.Add(new ColorSchemeOption(scheme));
        }

        SelectedColorScheme =
            ColorSchemes.FirstOrDefault(o => o.Key == ColorSchemeDefs.Normalize(Config.ColorScheme))
            ?? ColorSchemes[0];

        // ── 强调色来源 ──
        // 只留两项：自定义 / 跟随壁纸。
        // "跟随主题"被去掉的原因见 AppConfig.ColorScheme 的注释 ——
        // 现在主题色就是强调色，再留一个"跟随主题"等于让两个开关抢同一个值。
        foreach (var source in AccentSourceDefs.All)
        {
            if (source == AccentSourceDefs.Theme) continue;
            AccentSourceOptions.Add(new AccentSourceOption(source));
        }

        // 老配置（0.3.2 及以前）里 AccentSource 多半是 "theme"，
        // 那个值现在已经没有对应项了，迁移成"自定义"，颜色沿用原来主题的蓝。
        if (AccentSourceDefs.Normalize(Config.AccentSource) == AccentSourceDefs.Theme)
        {
            Config.AccentSource = AccentSourceDefs.Custom;
        }

        SelectedAccentSource = AccentSourceOptions
            .FirstOrDefault(s => s.Key == AccentSourceDefs.Normalize(Config.AccentSource))
            ?? AccentSourceOptions[0];

        // ── 自定义调色板 ──
        // 先让它停在上次保存的颜色上，再挂事件（否则构造期就会触发一次"改颜色"）
        if (ColorPaletteViewModel.TryParseHex(Config.CustomAccentHex, out var savedColor))
        {
            CustomAccent.SetColor(savedColor);
        }

        CustomAccent.ColorChanged += OnCustomAccentChanged;
        IsCustomAccent = AccentSourceDefs.Normalize(Config.AccentSource) == AccentSourceDefs.Custom;

        // ── 壁纸 ──
        _wallpaperEnabled = Config.WallpaperEnabled;
        _wallpaperPath = Config.WallpaperPath ?? string.Empty;
        _wallpaperScrimOpacity = Config.WallpaperScrimOpacity;
        _wallpaperBlurRadius = Config.WallpaperBlurRadius;

        // ── 卡片玻璃浓度 ──
        _cardOpacity = Config.CardOpacity;

        // ── 底层设置：渲染后端 ──
        // 同样直接写字段：这时候 _isInitialized 还是 false，
        // 不该因为"构造函数摆好了下拉框"就弹一句"已改为 xx，请重启"
        _selectedRenderBackend =
            RenderBackends.FirstOrDefault(o => o.Key == RenderBackendSettings.Normalize(Config.RenderBackend))
            ?? RenderBackends[0];
        RefreshRenderBackendStatus();

        RefreshAccentPreview();
        RefreshWallpaperStatus();

        // 初始化完成，之后 SelectedTheme 再变就是用户真的在操作了
        _isInitialized = true;

        ConfigFilePath = _configService.ConfigFilePath;
        ConfigDirectory = _configService.ConfigDirectory;
        DatabaseFilePath = _configService.ResolveDatabasePath();

        // 用 VersionInfo 统一取版本号，这样能显示成 "0.5.0-alpha"，
        // 而不是 Assembly.GetName().Version 那种丢掉 -dev 标记的 "0.5.0.0"
        AppVersion = VersionInfo.Current;
    }

    // ─────────────────────────────────────────────────────────
    // 当前配置（界面直接双向绑定到它）
    // ─────────────────────────────────────────────────────────

    /// <summary>配置对象。改了它再点「保存」就存盘。</summary>
    public AppConfig Config { get; }

    // ─────────────────────────────────────────────────────────
    // 主题
    // ─────────────────────────────────────────────────────────

    /// <summary>亮色 / 暗色两档。</summary>
    public ObservableCollection<ColorSchemeOption> ColorSchemes { get; } = new();

    /// <summary>当前选中的明暗。</summary>
    [ObservableProperty] private ColorSchemeOption? _selectedColorScheme;

    partial void OnSelectedColorSchemeChanged(ColorSchemeOption? value)
    {
        if (value is null) return;

        // 程序刚打开时不要弹提示（见 _isInitialized 的说明）
        if (!_isInitialized) return;

        ApplyColorScheme(value.Key, announce: true);
    }

    /// <summary>
    /// 真正换肤的那一步：明暗 + 强调色 + 卡片底色，三样一起。
    ///
    /// 【为什么合成一个方法？】
    ///   换明暗的时候卡片底色也得跟着从"白玻璃"变成"深灰玻璃"，
    ///   只换 ThemeVariant 的话，暗色界面里会冒出一块发白的卡片。
    ///   三个地方都要改，散着写迟早漏一个。
    /// </summary>
    private void ApplyColorScheme(string schemeKey, bool announce)
    {
        var normalized = ColorSchemeDefs.Normalize(schemeKey);

        Config.ColorScheme = normalized;
        Config.ThemeId = ColorSchemeDefs.ToThemeId(normalized);

        _themeRegistry.Apply(Config.ThemeId);
        _themeService.ApplyFor(Config, _wallpaper);

        var isDark = normalized == ColorSchemeDefs.Dark;
        App.ApplyCardOpacity(Config.CardOpacity, isDark);
        App.ApplyCardShadow(isDark);

        // 蒙版颜色跟着明暗走（亮白 / 暗黑），强度也要按新明暗重算一次 ——
        // 不加这一句，切到暗色后蒙版还停在亮色那个较弱的强度上，壁纸会突然扎眼。
        App.ApplyWallpaperScrim(Config.WallpaperScrimOpacity, isDark);

        ScheduleSave();
        RefreshAccentPreview();

        if (!announce) return;

        NoticeText = isDark
            ? "已切换到暗色模式：壁纸会自动压暗一档，浓度可以在「背景壁纸」里调。"
            : "已切换到亮色模式。";
        IsNoticeVisible = true;
    }

    // ─────────────────────────────────────────────────────────
    // 卡片玻璃浓度
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 功能区的不透明度（0-100）。100 = 完全不透明。
    /// 没有壁纸时默认就是这个值 —— 卡片后面空空的，半透明只会显得发灰。
    /// </summary>
    [ObservableProperty] private int _cardOpacity = 100;

    partial void OnCardOpacityChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 100);
        Config.CardOpacity = clamped;

        var isDark = ColorSchemeDefs.Normalize(Config.ColorScheme) == ColorSchemeDefs.Dark;
        App.ApplyCardOpacity(clamped, isDark);

        if (!_isInitialized) return;
        ScheduleSave();
    }

    // ─────────────────────────────────────────────────────────
    // 强调色
    // ─────────────────────────────────────────────────────────

    /// <summary>强调色的三种来源。</summary>
    public ObservableCollection<AccentSourceOption> AccentSourceOptions { get; } = new();

    [ObservableProperty] private AccentSourceOption? _selectedAccentSource;

    /// <summary>自定义调色板（选「自定义颜色」时才用得上）。</summary>
    public ColorPaletteViewModel CustomAccent { get; } = new();

    /// <summary>是不是选了「自定义颜色」—— 决定调色板显不显示。</summary>
    [ObservableProperty] private bool _isCustomAccent;

    /// <summary>现在实际生效的强调色（#RRGGBB）。</summary>
    [ObservableProperty] private string _effectiveAccentHex = "#0F6CBD";

    /// <summary>给界面上那个小色块用的画刷。</summary>
    [ObservableProperty] private Avalonia.Media.IBrush _effectiveAccentBrush =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.SteelBlue);

    /// <summary>调色板下方的预设色块。</summary>
    public IReadOnlyList<string> PresetColors { get; } = new[]
    {
        "#0F6CBD", "#C42B1C", "#CA5010", "#986F0B", "#498205",
        "#038387", "#0078D4", "#5B2D8E", "#C239B3", "#616161",
    };

    partial void OnSelectedAccentSourceChanged(AccentSourceOption? value)
    {
        if (value is null) return;

        IsCustomAccent = value.Key == AccentSourceDefs.Custom;

        if (!_isInitialized) return;

        Config.AccentSource = value.Key;
        _themeService.ApplyFor(Config, _wallpaper);
        RefreshAccentPreview();
        ScheduleSave();

        NoticeText = value.Key switch
        {
            AccentSourceDefs.Wallpaper => "强调色改为跟随壁纸：壁纸换一帧，强调色也跟着重新算。",
            AccentSourceDefs.Custom => "强调色改为自定义：下面那块调色板现在可以拖了。",
            _ => "强调色改回跟随主题。",
        };
        IsNoticeVisible = true;
    }

    /// <summary>调色板上任何一个地方动了都会走到这儿。</summary>
    private void OnCustomAccentChanged(object? sender, Avalonia.Media.Color color)
    {
        Config.CustomAccentHex = ColorPaletteViewModel.ToHex(color);

        // 用户开始拖调色板，说明他就是想要这个颜色。
        // 如果当前来源不是"自定义"，顺手替他切过去 —— 不然他拖了半天界面没反应会很困惑。
        if (!IsCustomAccent)
        {
            SelectedAccentSource = AccentSourceOptions
                .FirstOrDefault(o => o.Key == AccentSourceDefs.Custom);
            return; // 上面那行会触发 Apply + 保存，这里不用重复
        }

        if (!_isInitialized) return;

        _themeService.ApplyFor(Config, _wallpaper);
        RefreshAccentPreview();
        ScheduleSave();
    }

    /// <summary>点预设色块。</summary>
    [RelayCommand]
    private void PickPresetColor(string? hex)
    {
        if (ColorPaletteViewModel.TryParseHex(hex, out var color))
        {
            CustomAccent.SetColor(color);
        }
    }

    /// <summary>把"现在到底是什么颜色"重新算一遍，更新色块和说明。</summary>
    private void RefreshAccentPreview()
    {
        var hex = ThemeService.ResolveAccentOverride(Config, _wallpaper)
                  ?? _themeRegistry.Current.AccentColorHex;

        EffectiveAccentHex = hex;

        if (ColorPaletteViewModel.TryParseHex(hex, out var color))
        {
            EffectiveAccentBrush = new Avalonia.Media.SolidColorBrush(color);
        }
    }

    // ─────────────────────────────────────────────────────────
    // 背景壁纸
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private bool _wallpaperEnabled;

    [ObservableProperty] private string _wallpaperPath = string.Empty;

    [ObservableProperty] private int _wallpaperScrimOpacity;

    [ObservableProperty] private int _wallpaperBlurRadius;

    /// <summary>当前有没有选壁纸文件。</summary>
    public bool HasWallpaper => !string.IsNullOrWhiteSpace(WallpaperPath);

    /// <summary>壁纸状态那一行字。</summary>
    [ObservableProperty] private string _wallpaperStatusText = "还没有选壁纸";

    partial void OnWallpaperEnabledChanged(bool value)
    {
        Config.WallpaperEnabled = value;
        if (!_isInitialized) return;

        // 一打开壁纸就把卡片调成"玻璃"（82%），关掉时还原成不透明。
        // 只在用户从没自己动过滑块（还是 100）时才自动改 ——
        // 他要是特意设成了 100% 或者 60%，我们不该替他改。
        if (value && CardOpacity == 100)
        {
            CardOpacity = AppConfig.WallpaperCardOpacity;
        }
        else if (!value && CardOpacity == AppConfig.WallpaperCardOpacity)
        {
            CardOpacity = 100;
        }

        if (!value)
        {
            _wallpaper.Clear();
            RefreshAccentPreview();
            WallpaperStatusText = "壁纸已关闭";
        }
        else if (HasWallpaper)
        {
            _ = ReloadWallpaperAsync();
        }
        else
        {
            WallpaperStatusText = "请先选一张图片";
        }

        ScheduleSave();
    }

    partial void OnWallpaperPathChanged(string value)
    {
        Config.WallpaperPath = string.IsNullOrWhiteSpace(value) ? null : value;
        OnPropertyChanged(nameof(HasWallpaper));
        if (!_isInitialized) return;

        if (HasWallpaper && WallpaperEnabled)
        {
            _ = ReloadWallpaperAsync();
        }
        else if (!HasWallpaper)
        {
            _wallpaper.Clear();
            WallpaperStatusText = "还没有选壁纸";
            RefreshAccentPreview();
        }

        ScheduleSave();
    }

    partial void OnWallpaperScrimOpacityChanged(int value)
    {
        Config.WallpaperScrimOpacity = value;

        // 蒙版浓度只影响那一层半透明遮罩，不用重新解码壁纸
        var isDark = ColorSchemeDefs.Normalize(Config.ColorScheme) == ColorSchemeDefs.Dark;
        App.ApplyWallpaperScrim(value, isDark);

        if (!_isInitialized) return;
        ScheduleSave();
    }

    partial void OnWallpaperBlurRadiusChanged(int value)
    {
        Config.WallpaperBlurRadius = value;
        if (!_isInitialized) return;

        // 模糊是在解码阶段做的，改了得重新解一遍
        if (HasWallpaper && WallpaperEnabled) _ = ReloadWallpaperAsync();

        ScheduleSave();
    }

    /// <summary>由界面上的「浏览…」按钮调用（选文件要用窗口的 StorageProvider，只能从 View 那边发起）。</summary>
    public void SetWallpaperFile(string path)
    {
        WallpaperPath = path;

        // 选了图就默认把壁纸打开 —— 不然用户选了图却什么都没发生，很像坏了
        if (!WallpaperEnabled) WallpaperEnabled = true;
    }

    [RelayCommand]
    private void ClearWallpaper()
    {
        WallpaperPath = string.Empty;
        WallpaperEnabled = false;
    }

    /// <summary>重新解码壁纸，好了以后刷新强调色预览。</summary>
    private async Task ReloadWallpaperAsync()
    {
        if (!HasWallpaper) return;

        WallpaperStatusText = "正在加载…";

        var ok = await _wallpaper.LoadAsync(WallpaperPath, WallpaperBlurRadius, _themeRegistry.Current.Kind);

        // LoadAsync 内部已经 await 回了界面线程，这里可以直接改绑定属性
        WallpaperStatusText = ok
            ? DescribeWallpaper()
            : $"打不开这张图：{_wallpaper.Error}";

        RefreshAccentPreview();
    }

    /// <summary>按当前 WallpaperService 的状态生成那一行说明文字。</summary>
    private void RefreshWallpaperStatus()
    {
        if (!HasWallpaper)
        {
            WallpaperStatusText = "还没有选壁纸";
        }
        else if (!WallpaperEnabled)
        {
            WallpaperStatusText = "壁纸已关闭";
        }
        else if (_wallpaper.IsLoaded)
        {
            WallpaperStatusText = DescribeWallpaper();
        }
        else
        {
            WallpaperStatusText = "还没加载";
        }
    }

    private string DescribeWallpaper()
    {
        var kind = _wallpaper.IsAnimated
            ? $"动图 {_wallpaper.FrameCount}/{_wallpaper.SourceFrameCount} 帧"
            : "静态图";

        return $"{_wallpaper.PixelWidth}×{_wallpaper.PixelHeight} · {kind} · 主色调 {_wallpaper.Accent.ToHex()}";
    }

    // ─────────────────────────────────────────────────────────
    // 局域网联机
    // ─────────────────────────────────────────────────────────

    /// <summary>是否打开局域网联机（默认关闭，关着时不监听端口）。</summary>
    [ObservableProperty] private bool _enableNetworkStudyRoom;

    partial void OnEnableNetworkStudyRoomChanged(bool value)
    {
        if (!_isInitialized) return;

        Config.EnableNetworkStudyRoom = value;
        ScheduleSave();

        // 自习室页不用等切页面，马上就按新开关换文案、显示/隐藏联机输入框
        _state.NotifySettingsChanged();

        NoticeText = value
            ? "已打开局域网联机：可以去自习室建房，或者填同学的 IP 连过去。"
            : "已关闭局域网联机：自习室只在本机用，不再监听端口。";
        IsNoticeVisible = true;
    }

    /// <summary>攒一下再保存（拖动调色板时会连着触发很多次）。</summary>
    /// <summary>
    /// 提示条现在的显示时刻（自检用它算出"该到点了"）。
    /// </summary>
    public DateTime NoticeShownAt => _noticeShownAt;

    /// <summary>提示条显示或收起时开始/停止计时。</summary>
    partial void OnIsNoticeVisibleChanged(bool value)
    {
        if (!value)
        {
            _noticeTimer.Stop();
            return;
        }

        _noticeShownAt = DateTime.Now;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    /// <summary>
    /// 到了 <paramref name="now"/> 这一刻，提示条该不该自己收起来。
    ///
    /// 之所以把"当前时刻"当参数而不是内部直接取 Now：
    ///   自检要在几十毫秒内验证"2 秒还在、3 秒就没了"，
    ///   真等 3 秒既慢又会被 CI 的调度抖动影响。
    /// </summary>
    public void ExpireNotice(DateTime now)
    {
        if (!IsNoticeVisible) return;

        if (!NoticePolicy.ShouldExpire((now - _noticeShownAt).TotalMilliseconds)) return;

        _noticeTimer.Stop();
        IsNoticeVisible = false;
    }

    /// <summary>点提示条右边的 × 立刻收起。</summary>
    [RelayCommand]
    private void HideNotice()
    {
        _noticeTimer.Stop();
        IsNoticeVisible = false;
    }

    private void ScheduleSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    /// <summary>
    /// 立刻把攒着的改动写盘。
    ///
    /// 【什么时候必须调它？】
    ///   防抖是个定时器，页面切走、窗口关掉、程序退出时它可能还没到点。
    ///   SettingsView 在离开视觉树时会调一次，保证"改完就走"也不丢设置 ——
    ///   这就是"数据动态保存"的另一半：不用点保存，也不能靠"多等一会儿"。
    /// </summary>
    public void FlushPendingSave()
    {
        if (!_saveDebounce.IsEnabled) return;

        _saveDebounce.Stop();
        _configService.Save();
    }

    // ─────────────────────────────────────────────────────────
    // 底层设置（渲染后端）
    // ─────────────────────────────────────────────────────────
    //
    // 这一组放在「底层设置」里、默认收起，还要配一句"如非遇到渲染问题请勿调整" ——
    // 因为它不是功能开关，而是"界面画不出来时的救命绳"：
    //   · 默认用显卡渲染（Avalonia 自带渲染器 → ANGLE → D3D11），窗口动效才跟得上 60 Hz
    //   · 软件渲染省内存，但 2560×1440 上展开动画只有 25–43 fps（见 TECH_STACK.md）
    //   · 某个显卡驱动不对劲、或者就是想在无显卡机器上跑，再选回软件渲染
    // 调错了不会弄坏数据，最坏就是画面更费资源或者起不来 —— 起不来时把
    // config.json 里的 RenderBackend 改成 "software"（或者设环境变量）就能救回来。

    /// <summary>可选的渲染后端。顺序、文案都按产品要求写死在这里，界面只负责摆。</summary>
    public ObservableCollection<RenderBackendOption> RenderBackends { get; } =
    [
        new(RenderBackendSettings.Auto, "Avalonia 自带渲染器（默认，最流畅）"),
        new(RenderBackendSettings.Software, "软件渲染（省内存，动画会掉帧）"),
        new(RenderBackendSettings.OpenGl, "OpenGL"),
        new(RenderBackendSettings.Vulkan, "Vulkan"),
    ];

    /// <summary>用户选中的渲染后端（改它会弹"是否立即重启"）。</summary>
    [ObservableProperty] private RenderBackendOption? _selectedRenderBackend;

    /// <summary>
    /// 正在改、但还没确认的后端值。
    ///
    /// 为什么要"挂起"而不是立刻写配置：
    ///   用户在下拉框里点一下就会触发变更事件，可这时代码还没问过"要不要重启"。
    ///   如果先写配置再问，用户点"取消更改"就变成了"配置已经改了，只是没重启" ——
    ///   下次开机会莫名其妙变后端。所以确认之前一个字都不写。
    /// </summary>
    private string _pendingRenderBackendKey = RenderBackendSettings.Default;

    /// <summary>
    /// 程序内部回填下拉框时置 true，避免把"回填"当成"用户改了设置"。
    ///
    /// 取消更改时会用代码把 SelectedRenderBackend 设回旧值，
    /// 如果没有这个开关，那次赋值又会触发一遍 OnSelectedRenderBackendChanged
    /// —— 于是再次弹窗，无限循环。
    /// </summary>
    private bool _suppressRenderBackendChange;

    /// <summary>是否显示"更改此项需要重启，是否立即重启"。</summary>
    [ObservableProperty] private bool _isRestartPromptVisible;

    partial void OnSelectedRenderBackendChanged(RenderBackendOption? value)
    {
        if (!_isInitialized || value is null || _suppressRenderBackendChange) return;

        // 选回原来的值 = 什么都没变，不打扰用户
        if (!RenderBackendSettings.NeedsRestart(Config.RenderBackend, value.Key))
        {
            RefreshRenderBackendStatus();
            return;
        }

        // 先记下来，等用户点"确认"再写配置（见 _pendingRenderBackendKey 的注释）
        _pendingRenderBackendKey = value.Key;
        RefreshRenderBackendStatus();
        IsRestartPromptVisible = true;
    }

    /// <summary>弹窗里点「确认」：写配置 + 立刻重启程序。</summary>
    [RelayCommand]
    private void ConfirmBackendRestart()
    {
        Config.RenderBackend = _pendingRenderBackendKey;

        // 不走 ScheduleSave：那是攒批次的，重启会把这个进程直接送走，
        // 攒着的这次保存就丢了 —— 新进程读到的还是旧值，等于白重启。
        SaveConfigNow();

        IsRestartPromptVisible = false;
        RefreshRenderBackendStatus();

        var backendName = RenderBackendSettings.DisplayName(Config.RenderBackend);
        switch (RestartApplication())
        {
            case RestartOutcome.Started:
                // 新进程已经起来了，本进程马上退出，什么都不用再说
                break;

            case RestartOutcome.BlockedByDebugger:
                // 调试器在场的正确做法：让用户用调试器自己的「重新启动」。
                // 我们在这里偷偷起一个不受调试器管的进程，结果就是
                // "调试已结束、软件还开着、exe 被占着、下次编译 MSB3027"。
                NoticeText = $"渲染后端已保存为「{backendName}」。检测到正在调试器里运行，"
                             + "自动重启会把调试器甩掉、还会占住 exe 让下次编译失败；"
                             + "请用调试器的「重新启动」(Ctrl+Shift+F5)，或停止调试后重新运行。";
                IsNoticeVisible = true;
                break;

            default:
                // 重启失败也要让用户知道"配置已经存了、下次开机会生效"
                NoticeText = $"渲染后端已保存为「{backendName}」，"
                             + "但自动重启没成功，请手动关闭后重新打开 Helium Todo。";
                IsNoticeVisible = true;
                break;
        }
    }

    /// <summary>
    /// 立刻把配置写盘，跳过 500 ms 的防抖。
    /// 「确认」重启时用它（进程马上要退，等不到防抖到点）；
    /// 自检里也用它复原沙盒配置。
    /// </summary>
    public void SaveConfigNow()
    {
        _saveDebounce.Stop();
        _configService.Save();
    }

    /// <summary>弹窗里点「取消更改」：不写配置，把下拉框退回原来的值。</summary>
    [RelayCommand]
    private void CancelBackendChange()
    {
        IsRestartPromptVisible = false;

        var currentKey = RenderBackendSettings.Normalize(Config.RenderBackend);
        var backTo = RenderBackends.FirstOrDefault(o => o.Key == currentKey) ?? RenderBackends[0];

        _suppressRenderBackendChange = true;
        try
        {
            SelectedRenderBackend = backTo;
        }
        finally
        {
            _suppressRenderBackendChange = false;
        }

        NoticeText = $"已取消更改，渲染后端仍是「{backTo.DisplayText}」。";
        IsNoticeVisible = true;
        RefreshRenderBackendStatus();
    }

    /// <summary>
    /// 真的重启。返回值见 <see cref="RestartOutcome"/>。
    ///
    /// 为什么不让 VM 直接碰 Avalonia / Process：
    ///   这段逻辑要跟单实例锁、窗口关闭拦截、调试器判断打交道，属于"程序生命周期"，
    ///   放在 App 里（<see cref="App.TryRestart"/>）比放在设置页更合适；
    ///   这里只在拿不到 Application 时返回 <see cref="RestartOutcome.Failed"/>，不会抛异常。
    /// </summary>
    private static RestartOutcome RestartApplication()
    {
        // 自检里不能真的开新进程（一跑就多一个实例，还会互相抢单实例锁），
        // 用 HELIUM_NO_RESTART=1 把它当成"重启成功"，只验配置有没有先存好。
        if (Environment.GetEnvironmentVariable("HELIUM_NO_RESTART") == "1")
        {
            return RestartOutcome.Started;
        }

        try
        {
            return Avalonia.Application.Current is App app ? app.TryRestart() : RestartOutcome.Failed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[重启] 失败：{ex.Message}");
            return RestartOutcome.Failed;
        }
    }

    /// <summary>
    /// 下拉框下面那行小字：现在**实际**在用的是哪个后端、改完要不要重启。
    ///
    /// 为什么要专门显示"当前生效"？
    ///   这个设置是"下次启动才生效"的，光看下拉框会以为已经切过去了。
    ///   而且环境变量 HELIUM_RENDER 优先级更高 —— 那时候设置页这一项其实不起作用，
    ///   必须让用户看得见，否则他会以为设置坏了。
    /// </summary>
    [ObservableProperty] private string _renderBackendStatusText = string.Empty;

    private void RefreshRenderBackendStatus()
    {
        var activeText = RenderBackendSettings.DisplayName(RenderBackendChoice.ActiveKey);

        if (RenderBackendChoice.IsOverriddenByEnvironment)
        {
            RenderBackendStatusText =
                $"当前生效：{activeText}（由环境变量 HELIUM_RENDER 强制指定，这一项暂时不起作用）";
            return;
        }

        var selectedKey = RenderBackendSettings.Normalize(SelectedRenderBackend?.Key ?? Config.RenderBackend);
        RenderBackendStatusText = selectedKey == RenderBackendChoice.ActiveKey
            ? $"当前生效：{activeText}"
            : $"当前生效：{activeText}；重启程序后改为 {RenderBackendSettings.DisplayName(selectedKey)}";
    }

    // ─────────────────────────────────────────────────────────
    // 路径信息
    // ─────────────────────────────────────────────────────────

    /// <summary>配置文件完整路径。</summary>
    public string ConfigFilePath { get; }

    /// <summary>配置文件夹。</summary>
    public string ConfigDirectory { get; }

    /// <summary>数据库文件路径。</summary>
    public string DatabaseFilePath { get; }

    /// <summary>程序版本。</summary>
    public string AppVersion { get; }

    // ─────────────────────────────────────────────────────────
    // 提示
    // ─────────────────────────────────────────────────────────

    [ObservableProperty] private string _noticeText = string.Empty;
    [ObservableProperty] private bool _isNoticeVisible;

    [RelayCommand]
    private void DismissNotice() => IsNoticeVisible = false;

    // ─────────────────────────────────────────────────────────
    // 命令
    // ─────────────────────────────────────────────────────────

    /// <summary>保存配置到磁盘。</summary>
    [RelayCommand]
    private void Save()
    {
        _configService.Save();
        NoticeText = $"配置已保存到：{_configService.ConfigFilePath}";
        IsNoticeVisible = true;
    }

    /// <summary>恢复默认设置。</summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        var fresh = _configService.ResetToDefault();

        // 把默认值搬到当前界面上（Config 对象是引用，不能直接换掉，否则绑定会断）
        Config.ThemeId = fresh.ThemeId;
        Config.WindowWidth = fresh.WindowWidth;
        Config.WindowHeight = fresh.WindowHeight;
        Config.StartMaximized = fresh.StartMaximized;
        Config.DefaultFocusMinutes = fresh.DefaultFocusMinutes;
        Config.BreakMinutes = fresh.BreakMinutes;
        Config.LongBreakMinutes = fresh.LongBreakMinutes;
        Config.FocusCompletionAlert = fresh.FocusCompletionAlert;
        Config.EnableFocusGuard = fresh.EnableFocusGuard;
        Config.FocusGuardTolerance = fresh.FocusGuardTolerance;
        Config.TimelineStartHour = fresh.TimelineStartHour;
        Config.OverloadHours = fresh.OverloadHours;
        Config.ShowCompletedOnTimeline = fresh.ShowCompletedOnTimeline;
        Config.DatabasePath = fresh.DatabasePath;
        Config.AutoBackup = fresh.AutoBackup;
        Config.StudyRoomFocusMinutes = fresh.StudyRoomFocusMinutes;
        Config.DisplayName = fresh.DisplayName;
        Config.EnableNetworkStudyRoom = fresh.EnableNetworkStudyRoom;
        EnableNetworkStudyRoom = fresh.EnableNetworkStudyRoom;

        // 渲染后端也回默认（注意：这条同样要重启才生效，界面会按"已改"提示）
        Config.RenderBackend = fresh.RenderBackend;
        SelectedRenderBackend =
            RenderBackends.FirstOrDefault(o => o.Key == RenderBackendSettings.Normalize(fresh.RenderBackend))
            ?? RenderBackends[0];

        _configService.Save();

        // 外观也回到默认（明暗 + 强调色 + 卡片浓度）
        Config.ColorScheme = fresh.ColorScheme;
        Config.CardOpacity = fresh.CardOpacity;
        Config.AccentSource = fresh.AccentSource;
        Config.CustomAccentHex = fresh.CustomAccentHex;

        _themeRegistry.Apply(fresh.ThemeId);
        _themeService.ApplyFor(Config, _wallpaper);

        SelectedColorScheme =
            ColorSchemes.FirstOrDefault(o => o.Key == ColorSchemeDefs.Normalize(fresh.ColorScheme))
            ?? ColorSchemes[0];

        CardOpacity = fresh.CardOpacity;

        SelectedAccentSource =
            AccentSourceOptions.FirstOrDefault(o => o.Key == AccentSourceDefs.Normalize(Config.AccentSource))
            ?? AccentSourceOptions[0];

        NoticeText = "已恢复默认设置。";
        IsNoticeVisible = true;
    }

    /// <summary>在文件资源管理器里打开配置文件夹。</summary>
    [RelayCommand]
    private void OpenConfigFolder() => OpenInExplorer(ConfigDirectory);

    /// <summary>打开数据库所在文件夹（备份就在旁边的 backups 子文件夹里）。</summary>
    [RelayCommand]
    private void OpenDatabaseFolder()
    {
        var directory = Path.GetDirectoryName(DatabaseFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            OpenInExplorer(directory);
        }
    }

    // ─────────────────────────────────────────────────────────
    // 备份与恢复（统一 .zip 格式）
    // ─────────────────────────────────────────────────────────
    //
    // 为什么只有一种格式？
    //   以前「启动自动备份」是直接拷一份 .db 出来，用户拿到手不知道该往哪放。
    //   现在所有导出 —— 手动的、启动自动的、导入前的安全备份 —— 都是同一个 .zip：
    //   里面装着数据库、配置和壁纸，双击解压就能看，换电脑整个搬过去就行。

    /// <summary>备份文件夹（导出的默认落点，自动备份也在这里）。</summary>
    public string BackupDirectory => _backup.BackupDirectory;

    /// <summary>正在导出/导入中（忙的时候按钮要禁用，免得连点两下）。</summary>
    [ObservableProperty] private bool _isBackupBusy;

    /// <summary>最近一次备份操作的结果，显示在列表里。</summary>
    [ObservableProperty] private string _backupStatusText = "还没有导出或导入过";

    /// <summary>导出对话框里默认填的文件名。</summary>
    public string SuggestedBackupFileName => BackupFormat.BuildFileName(DateTime.Now);

    /// <summary>刚才用户挑的那份备份（确认之前先存在这里）。</summary>
    private BackupInspection? _pendingImport;

    /// <summary>是否正在问「替换还是合并」。</summary>
    [ObservableProperty] private bool _isImportConfirmOpen;

    /// <summary>要导入的文件名（只显示名字，路径太长会把界面撑开）。</summary>
    [ObservableProperty] private string _importFileName = string.Empty;

    /// <summary>这份备份里有什么。</summary>
    [ObservableProperty] private string _importSummaryText = string.Empty;

    /// <summary>导入前的提醒（比如"这份备份没带配置"）。</summary>
    [ObservableProperty] private string _importWarningText = string.Empty;

    /// <summary>有没有那句提醒（空字符串时那一行不占位置）。</summary>
    [ObservableProperty] private bool _hasImportWarning;

    /// <summary>打开备份文件夹。</summary>
    [RelayCommand]
    private void OpenBackupFolder()
    {
        Directory.CreateDirectory(BackupDirectory);
        OpenInExplorer(BackupDirectory);
    }

    /// <summary>
    /// 导出到指定路径。
    /// 路径是界面那边用保存对话框选出来的 —— 和选壁纸一样，碰界面的事放 View 里。
    /// </summary>
    public async Task ExportToAsync(string targetPath)
    {
        if (IsBackupBusy) return;

        IsBackupBusy = true;
        BackupStatusText = "正在打包…";

        try
        {
            var result = await _backup.ExportAsync(targetPath);

            if (result.FellBack)
            {
                // 原位置写不进去（受控文件夹访问最常见）。已经改存到备份文件夹，
                // 这里要把"改到哪了"和"为什么"一起说，否则用户会以为放桌面上了。
                BackupStatusText = $"原位置不能写入，已改存到备份文件夹：{Path.GetFileName(result.Path)}"
                                   + $"（{DescribeSize(result.SizeBytes)} · {result.Manifest.Counts.DescribeText}）";
                NoticeText = "那个文件夹系统不让写（多半是「受控文件夹访问」），备份已经放到「备份文件夹」里了。";
                IsNoticeVisible = true;
            }
            else
            {
                BackupStatusText = $"已导出 {Path.GetFileName(result.Path)}"
                                   + $"，{DescribeSize(result.SizeBytes)} · {result.Manifest.Counts.DescribeText}";
            }
        }
        catch (Exception ex)
        {
            // 导出失败多半是磁盘满或目录没权限，直接把原因说出来
            BackupStatusText = $"导出失败：{ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    /// <summary>
    /// 用户挑了一份 .zip：先看清楚里面有什么，再问怎么导入。
    ///
    /// 为什么不直接导？
    ///   替换模式会清空现有数据，这一步必须让用户看清楚"要拿什么换什么"。
    /// </summary>
    public async Task BeginImportAsync(string zipPath)
    {
        if (IsBackupBusy) return;

        IsBackupBusy = true;
        BackupStatusText = "正在检查这份备份…";

        try
        {
            var inspection = await _backup.InspectAsync(zipPath);

            if (!inspection.IsUsable)
            {
                BackupStatusText = $"这份文件不能导入：{inspection.Error}";
                return;
            }

            _pendingImport = inspection;
            ImportFileName = Path.GetFileName(inspection.FilePath);
            ImportSummaryText = inspection.DescribeText;
            ImportWarningText = inspection.Warning ?? string.Empty;
            HasImportWarning = !string.IsNullOrWhiteSpace(inspection.Warning);
            IsImportConfirmOpen = true;
            BackupStatusText = string.Empty;
        }
        catch (Exception ex)
        {
            BackupStatusText = $"读不了这份备份：{ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    /// <summary>替换导入：清空现有数据，换成备份里的。</summary>
    [RelayCommand]
    private Task ImportReplaceAsync() => RunImportAsync(BackupImportMode.Replace);

    /// <summary>合并导入：保留现有数据，把备份里的追加进来。</summary>
    [RelayCommand]
    private Task ImportMergeAsync() => RunImportAsync(BackupImportMode.Merge);

    /// <summary>算了，不导了。</summary>
    [RelayCommand]
    private void CancelImport()
    {
        _pendingImport = null;
        IsImportConfirmOpen = false;
    }

    private async Task RunImportAsync(BackupImportMode mode)
    {
        if (_pendingImport is null) return;

        var source = _pendingImport;
        IsImportConfirmOpen = false;
        IsBackupBusy = true;
        BackupStatusText = mode == BackupImportMode.Replace
            ? "正在替换…（会先备份当前数据）"
            : "正在合并…";

        try
        {
            var result = await _backup.ImportAsync(source.FilePath, mode);
            BackupStatusText = result.DescribeText;

            NoticeText = BackupStatusText;
            IsNoticeVisible = true;

            // 数据和配置都换了，界面得整个跟着换一遍
            ApplyImportedState();
        }
        catch (Exception ex)
        {
            BackupStatusText = $"导入失败：{ex.Message}";
            NoticeText = BackupStatusText;
            IsNoticeVisible = true;
        }
        finally
        {
            _pendingImport = null;
            IsBackupBusy = false;
        }
    }

    /// <summary>
    /// 导入之后，把界面上跟配置有关的部分重新读一遍。
    ///
    /// 注意：这里直接改字段再发通知，不通过属性 setter ——
    /// 那些 setter 里带着"用户改了设置"的副作用（存盘、重新解码壁纸、弹提示），
    /// 而这里只是"数据被别人换掉了"，不该再写一遍文件。
    /// </summary>
    private void ApplyImportedState()
    {
        // 外观（明暗 + 强调色 + 卡片浓度）
        _themeRegistry.Apply(Config.ThemeId);
        _themeService.ApplyFor(Config, _wallpaper);

        // 接下来这几个属性会连着改一串值。
        // 它们的 setter 里带着"用户改了设置"的副作用（存盘、重新解码壁纸、弹提示），
        // 而这里只是"数据被别人换掉了"，不需要再走一遍 —— 所以先关掉闸门。
        var wasInitialized = _isInitialized;
        _isInitialized = false;

        try
        {
            SelectedColorScheme =
                ColorSchemes.FirstOrDefault(o => o.Key == ColorSchemeDefs.Normalize(Config.ColorScheme))
                ?? SelectedColorScheme;
            CardOpacity = Config.CardOpacity;
            WallpaperEnabled = Config.WallpaperEnabled;
            WallpaperPath = Config.WallpaperPath ?? string.Empty;
        }
        finally
        {
            _isInitialized = wasInitialized;
        }

        // 闸门开了之后再手动补上"确实需要做"的两件事：显示壁纸、刷新强调色预览
        if (HasWallpaper && WallpaperEnabled)
        {
            _ = ReloadWallpaperAsync();
        }
        else
        {
            _wallpaper.Clear();
            RefreshWallpaperStatus();
        }

        RefreshAccentPreview();

        // 三大核心功能的数据全换了，六个页面一起刷新
        _state.NotifyAllChanged();
    }

    /// <summary>把字节数说成人话。</summary>
    private static string DescribeSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
    };

    /// <summary>
    /// 在资源管理器里打开一个文件夹。
    ///
    /// 为什么试两种启动方式？
    ///   ① 直接把文件夹路径交给系统（UseShellExecute + Verb="open"）
    ///      —— 这是"用『文件夹』的默认处理程序打开"，而默认处理程序可以被第三方
    ///      文件管理器接管。接管项一旦失效，ShellExecute 会静默什么都不做：
    ///      不抛异常、返回 null，界面上就是"点了没反应"。
    ///   ② 让外壳去启动 explorer.exe
    ///      —— 少一层间接，确定会开出文件夹窗口。
    ///   ③ 自己 CreateProcess 起 explorer.exe 是最下策：
    ///      在部分机器上 explorer.exe 这样启动会直接失败
    ///      （报 0xc0000142「应用程序无法正常启动」），因为它需要由外壳拉起。
    ///
    /// 所以在 ② 之前先试 Avalonia 自带的 Launcher（`LaunchDirectoryInfoAsync`）：
    /// 它把"用哪个文件管理器"交还给系统，成功与否还有返回值可判断。
    /// 只有它返回 false（或无头平台不支持）时，才走下面这条进程链。
    ///
    /// 进程链的顺序是「② → ① → ③」，每种失败都把原因收集起来，
    /// 全失败时一次性显示在界面上 —— 不猜、也不静默。
    /// </summary>
    private async void OpenInExplorer(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception ex)
        {
            NoticeText = $"打开文件夹失败：{ex.Message}（{path}）";
            IsNoticeVisible = true;
            return;
        }

        var reasons = new List<string>();

        // ①' 先交给 Avalonia 的 Launcher：它用系统外壳打开，返回值可信
        if (_folderLauncher is not null)
        {
            try
            {
                if (await _folderLauncher.OpenAsync(path))
                {
                    return;
                }

                reasons.Add("Avalonia Launcher：系统没有接受这次打开请求");
            }
            catch (Exception ex)
            {
                reasons.Add($"Avalonia Launcher：{ex.Message}");
            }
        }

        // 路径里有空格或中文都必须带引号，否则会被拆成两个参数
        var quoted = $"\"{path}\"";

        var attempts = new (string Method, ProcessStartInfo Info)[]
        {
            ("外壳启动资源管理器", new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = quoted,
                UseShellExecute = true,
            }),
            ("系统默认方式", new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open",
            }),
            ("直接启动资源管理器", new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                Arguments = quoted,
                UseShellExecute = false,
                CreateNoWindow = true,
            }),
            // 第四条：让 cmd 的 start 去开。
            // 前三行都失败时通常不是"路径不对"，而是外壳这一层被挡了
            // （提权进程 + 非提权的资源管理器、或者外壳被安全软件接管）。
            // cmd 走的是另一条创建进程的路径，值得再试一次。
            ("经 cmd start 打开", new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = $"/c start \"\" \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }),
        };

        foreach (var (method, info) in attempts)
        {
            try
            {
                Process.Start(info)?.Dispose();
                return;
            }
            catch (Exception ex)
            {
                reasons.Add($"{method}：{ex.Message}");
            }
        }

        NoticeText = $"打开文件夹失败（{string.Join("；", reasons)}）路径：{path}";
        IsNoticeVisible = true;
    }
}

/// <summary>强调色来源下拉框里的一项。</summary>
public sealed class AccentSourceOption
{
    public AccentSourceOption(string key)
    {
        Key = AccentSourceDefs.Normalize(key);
        DisplayName = AccentSourceDefs.ToDisplayName(Key);
    }

    public string Key { get; }
    public string DisplayName { get; }

    /// <summary>下拉框里再补一句说明，免得用户不知道"跟随壁纸"是什么意思。</summary>
    public string Hint => Key switch
    {
        AccentSourceDefs.Wallpaper => "从壁纸里自动提一个主色调",
        AccentSourceDefs.Custom => "用下面的调色板自己调",
        _ => "用主题自带的那套颜色",
    };

    public override string ToString() => DisplayName;
}

/// <summary>明暗下拉框里的一项（亮色 / 暗色）。</summary>
public sealed class ColorSchemeOption
{
    public ColorSchemeOption(string key)
    {
        Key = ColorSchemeDefs.Normalize(key);
        DisplayName = ColorSchemeDefs.DisplayName(Key);
    }

    public string Key { get; }
    public string DisplayName { get; }

    /// <summary>下拉框里的文字：前面带个日/月标记，一眼看出明暗。</summary>
    public string DisplayText => Key == ColorSchemeDefs.Dark ? $"🌙  {DisplayName}" : $"☀  {DisplayName}";

    public override string ToString() => DisplayText;
}

/// <summary>渲染后端下拉框里的一项。</summary>
public sealed class RenderBackendOption
{
    public RenderBackendOption(string key, string displayText)
    {
        Key = RenderBackendSettings.Normalize(key);
        DisplayText = displayText;
    }

    /// <summary>归一化后的 key（写进 config.json 的就是它）。</summary>
    public string Key { get; }

    /// <summary>下拉框里显示的文字。</summary>
    public string DisplayText { get; }

    public override string ToString() => DisplayText;
}