// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeliumTodo.Domain.Configuration;

/// <summary>
/// 程序配置。
///
/// 【需求 9：可分离的配置文件】
/// 所有设置都存在 %AppData%\Helium Todo\config.json 里，
/// 和程序本体分开 —— 换台电脑直接把这个文件拷过去就行。
/// 删掉它程序也能跑（会自动用默认值重新生成）。
/// </summary>
public sealed class AppConfig
{
    /// <summary>配置文件的当前版本，以后升级配置结构时用来做兼容处理。</summary>
    public int ConfigVersion { get; set; } = 1;

    // ── 外观 ──────────────────────────────────────────

    /// <summary>
    /// 主题 Id，对应 <c>ThemeRegistry</c> 里的主题。
    ///
    /// 从 0.3.3 起这个字段不再由用户在"九个主题"里挑，而是由
    /// <see cref="ColorScheme"/> 换算出来（见 <see cref="ColorSchemes.ToThemeId"/>）。
    /// 还留着它是为了兼容老配置文件：里面存的是 "fluent-dark-purple" 这种，
    /// 读进来之后会反推成暗色。
    /// </summary>
    public string ThemeId { get; set; } = "fluent-light-blue";

    /// <summary>
    /// 亮色 / 暗色，取值见 <see cref="ColorSchemes"/>。
    ///
    /// 【为什么把"主题"拆成"明暗 + 强调色"？】
    ///   原来是九套主题，每套 = 一个明暗 + 一个固定强调色，
    ///   想换个蓝色就得换整套主题，而且强调色和主题色是两套东西，
    ///   界面上看着像"两个都能调颜色"，实际互相打架。
    ///   现在合成一套：明暗是底，强调色是面，强调色优先。
    /// </summary>
    public string ColorScheme { get; set; } = ColorSchemes.Default;

    /// <summary>
    /// 功能区（卡片）的不透明度，0-100，100 = 完全不透明。
    ///
    /// 【为什么要让用户调？】
    ///   卡片默认是半透明的"玻璃"，好让壁纸透上来。
    ///   但玻璃也有代价：壁纸花的时候，卡片里的字会跟着花。
    ///   给个滑块让用户自己找平衡点，比我们替他决定强。
    ///
    /// 【默认值为什么是 100？】
    ///   没有壁纸时，卡片后面就是窗口底色，半透明只会让它看起来"发灰"，
    ///   一点好处都没有。所以默认不透明；
    ///   打开壁纸时会自动降到 <see cref="WallpaperCardOpacity"/> 附近（见设置页）。
    /// </summary>
    public int CardOpacity { get; set; } = 100;

    /// <summary>
    /// 打开壁纸时，如果卡片不透明度还是默认的 100，就自动降到这个值 ——
    /// 相当于"一开壁纸就给你玻璃效果"，但用户自己调过的值不会被覆盖。
    /// </summary>
    public const int WallpaperCardOpacity = 82;

    /// <summary>
    /// 窗口宽度（首次启动 / 恢复默认时的尺寸）。
    ///
    /// 为什么是 1920×1080：三大核心功能里「每日工作流」和「专注时长」都是横向排布的，
    /// 1400×900 那张布局在 1080p 屏上左右两侧的字会挤在一起；1920 是 1080p 屏的整数倍，
    /// 也是"开箱即最大化"的常见做法。屏幕比它小时不用我们处理 —— Avalonia 建窗口时
    /// 会把窗口收进屏幕工作区（实测 2560×1600 @150% 上得到 ≈1724×1080 DIP），
    /// 所以这里写 1920×1080 不会在小屏上顶出屏幕。
    /// </summary>
    public double WindowWidth { get; set; } = 1920;

    /// <summary>窗口高度。</summary>
    public double WindowHeight { get; set; } = 1080;

    /// <summary>启动时窗口是否最大化。</summary>
    public bool StartMaximized { get; set; }

    // ── 底层设置 ───────────────────────────────────────

    /// <summary>
    /// 用哪个渲染后端。取值见 <see cref="RenderBackendSettings"/>：
    ///   · "software" —— 软件渲染（默认，Skia 在 CPU 上画，省内存）
    ///   · "auto"     —— Avalonia 自带渲染器（Windows 上是 ANGLE → D3D11，吃显卡）
    ///   · "wgl"      —— OpenGL
    ///   · "vulkan"   —— Vulkan
    ///
    /// 【为什么需要这个设置项？】
    ///   软件渲染是"为了省内存"做的取舍：正常情况最划算，
    ///   但遇到 4K 屏 + 大量动画、或者某个驱动跟 Skia 的 CPU 路径不对付时，
    ///   换回显卡后端才是解法。设置页把它放在「底层设置」里，
    ///   并配一句"如非遇到渲染问题，请勿调整"。
    ///
    /// 【为什么改了要重启？】
    ///   渲染后端必须在 Avalonia 初始化之前定下来，而那时窗口还没建、设置页更不存在。
    ///   所以这个值只在启动时读一次：改完写进 config.json，下次启动生效。
    ///   （环境变量 HELIUM_RENDER 优先级更高，留给开发和排障用。）
    /// </summary>
    public string RenderBackend { get; set; } = RenderBackendSettings.Default;

    // ── 强调色（主题色）──────────────────────────────────

    /// <summary>
    /// 强调色从哪儿来。取值：
    ///   · "theme"     —— 跟着内置主题走（0.3.3 起界面上已不再提供，只做老配置兼容）
    ///   · "wallpaper" —— 从壁纸里自动提取主色调
    ///   · "custom"    —— 用户自己调的颜色，存在 <see cref="CustomAccentHex"/>（默认）
    ///
    /// 为什么用一个字符串而不是枚举？
    ///   配置文件是人也会手改的（需求 9 要求配置可分离、可手改），
    ///   字符串写在 JSON 里一眼就懂，写成枚举数字反而要查表。
    ///   读的时候认不出来的值一律当 "theme" 处理，不会崩。
    ///
    /// 【0.3.3 起默认值从 "theme" 改成 "custom"】
    ///   主题不再自带颜色，主题色就是强调色本身；
    ///   再默认"跟随主题"等于让用户调了颜色却不生效。
    /// </summary>
    public string AccentSource { get; set; } = AccentSources.Custom;

    /// <summary>用户自定义的强调色（#RRGGBB）。只在 AccentSource 为 "custom" 时生效。</summary>
    public string CustomAccentHex { get; set; } = "#0F6CBD";

    // ── 背景壁纸 ───────────────────────────────────────

    /// <summary>是否显示背景壁纸。</summary>
    public bool WallpaperEnabled { get; set; }

    /// <summary>
    /// 壁纸文件路径。可以是 APNG / PNG / JPG / GIF，
    /// 动图（APNG、GIF）会自动播放。
    /// </summary>
    public string? WallpaperPath { get; set; }

    /// <summary>
    /// 壁纸上面盖一层"蒙版"的浓度，0-95。
    ///
    /// 为什么需要它？
    ///   壁纸再好看，也不能让界面上的字看不清。
    ///   蒙版就是把壁纸整体压暗/提亮一点，把对比度让给文字和卡片。
    ///   0 = 完全不加蒙版（壁纸原样），95 = 几乎全盖住（只隐隐透出一点）。
    /// </summary>
    public int WallpaperScrimOpacity { get; set; } = 45;

    /// <summary>
    /// 壁纸的模糊半径（0-60 像素）。0 表示不模糊。
    ///
    /// 加一点模糊的好处是：壁纸里细碎的纹理不会和界面上的文字"打架"，
    /// 视觉上更容易把注意力放在内容上。代价是加载时多花一点时间。
    ///
    /// 改版后默认值从 8 提到 24：卡片改成半透明之后，壁纸必须糊得够狠，
    /// 卡片背后的颜色才"成片"；只糊 8 的时候仍然看得清图片内容，
    /// 文字压在花里胡哨的图案上，反而比不贴壁纸更乱。
    /// </summary>
    public int WallpaperBlurRadius { get; set; } = 24;

    // ── 专注 ──────────────────────────────────────────

    /// <summary>默认专注时长（分钟）。</summary>
    public int DefaultFocusMinutes { get; set; } = 25;

    /// <summary>休息时长（分钟）。</summary>
    public int BreakMinutes { get; set; } = 5;

    /// <summary>一组长专注之后的长休息（分钟）。</summary>
    public int LongBreakMinutes { get; set; } = 15;

    /// <summary>专注计时结束时是否提示（响铃 / 弹窗）。</summary>
    public bool FocusCompletionAlert { get; set; } = true;

    // ── 自控模块 ──────────────────────────────────────

    /// <summary>专注期间是否开启「离开窗口就提醒」。</summary>
    public bool EnableFocusGuard { get; set; } = true;

    /// <summary>专注期间允许离开窗口几次（超过就记一次打扰）。</summary>
    public int FocusGuardTolerance { get; set; } = 0;

    // ── 每日工作流 ────────────────────────────────────

    /// <summary>时间轴一天的起始小时（默认 0 点，也就是完整 24 小时）。</summary>
    public int TimelineStartHour { get; set; } = 0;

    /// <summary>超过多少小时算「安排太满」。</summary>
    public int OverloadHours { get; set; } = 12;

    /// <summary>是否在时间轴上显示已完成的项目。</summary>
    public bool ShowCompletedOnTimeline { get; set; } = true;

    // ── 数据 ──────────────────────────────────────────

    /// <summary>数据库文件放在哪。留空 = 用默认位置（%AppData%\Helium Todo\helium.db）。</summary>
    public string? DatabasePath { get; set; }

    /// <summary>是否在启动时自动备份数据库（保留最近 7 份）。</summary>
    public bool AutoBackup { get; set; } = true;

    // ── 插件自己存东西的地方 ──────────────────────────

    /// <summary>
    /// 插件配置区。键是 "plugin:插件的key"，值是序列化后的 JSON 字符串。
    ///
    /// 为什么要这么设计？
    ///   插件是后来才装上的，主程序事先不知道它要存什么设置。
    ///   所以留一个「通用口袋」：插件想存什么就往里塞一个 key，
    ///   主程序只负责把它一起写进 config.json，不用为每个插件改代码。
    /// </summary>
    public Dictionary<string, string> PluginSettings { get; set; } = new();

    // ── 自习室 ────────────────────────────────────────

    /// <summary>自习室默认专注时长（分钟）。</summary>
    public int StudyRoomFocusMinutes { get; set; } = 45;

    /// <summary>昵称（联机时显示给别人看）。</summary>
    public string DisplayName { get; set; } = Environment.UserName;

    /// <summary>
    /// 局域网联机的开关，默认关闭。
    ///
    /// 关着的时候自习室只在本机用：不监听端口、不广播、也不去碰防火墙，
    /// 想联机就到「设置 → 自习室」打开。
    /// </summary>
    public bool EnableNetworkStudyRoom { get; set; } = false;
}

/// <summary>
/// 配置服务：负责把 <see cref="AppConfig"/> 读出来、写回去。
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,                                    // 格式化输出，方便手动改
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 让中文正常显示
    };

    /// <summary>配置文件所在文件夹，例如 C:\Users\你\AppData\Roaming\Helium Todo。</summary>
    public string ConfigDirectory { get; }

    /// <summary>配置文件的完整路径。</summary>
    public string ConfigFilePath { get; }

    /// <summary>当前配置。</summary>
    public AppConfig Current { get; private set; } = new();

    /// <summary>
    /// 数据目录是不是「便携模式」——也就是没能用 %AppData%，
    /// 退而求其次放在了程序自己旁边。
    /// </summary>
    public bool IsPortableMode { get; }

    /// <summary>为什么用了便携模式（给界面提示用，正常时为 null）。</summary>
    public string? FallbackReason { get; }

    /// <summary>构造时传入数据目录；不传就用 %AppData%\Helium Todo。</summary>
    public ConfigService(string? configDirectory = null)
    {
        // 没指定目录时用默认位置；但如果那个位置写不了（比如没权限），
        // 就退到「程序自己旁边的 Data 文件夹」，保证程序永远能启动。
        if (!string.IsNullOrWhiteSpace(configDirectory))
        {
            ConfigDirectory = configDirectory!;
            IsPortableMode = false;
        }
        else
        {
            var preferred = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Helium Todo");

            if (TryPrepareDirectory(preferred, out var reason))
            {
                ConfigDirectory = preferred;
                IsPortableMode = false;
            }
            else
            {
                ConfigDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
                IsPortableMode = true;
                FallbackReason = reason;

                // 便携目录也建不出来就真没办法了，把原因记下来让界面显示
                if (!TryPrepareDirectory(ConfigDirectory, out var secondReason))
                {
                    FallbackReason = $"{reason}；备用目录也不可用：{secondReason}";
                }
            }
        }

        ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");
    }

    /// <summary>
    /// 试着把目录准备好并确认「真的能写进去」。
    ///
    /// 为什么要真的写一个文件试试？
    ///   光看目录存不存在是不够的 —— 目录可能因为没有权限而建不出来，
    ///   或者建出来了但写不进去。写一个临时文件再删掉，才能确定真的可用。
    /// </summary>
    private static bool TryPrepareDirectory(string directory, out string? reason)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);

            reason = null;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"{directory} 不可写（{ex.GetType().Name}: {ex.Message}）";
            return false;
        }
    }

    /// <summary>
    /// 读取配置。文件不存在或内容坏了都不会崩，直接用默认值。
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            // 目录建不出来也不能让程序起不来 —— 下面读配置失败会走默认值分支
            TryCreateConfigDirectory();

            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded is not null)
                {
                    // 老配置（0.3.2 及以前）里没有 ColorScheme，只有 ThemeId。
                    // 直接默认成亮色的话，原来用深色主题的用户一升级就"闪白"一下，
                    // 所以这里反推一次：JSON 里没这个键，就从 ThemeId 换算。
                    if (!json.Contains("\"ColorScheme\"", StringComparison.Ordinal))
                    {
                        loaded.ColorScheme = ColorSchemes.FromThemeId(loaded.ThemeId);
                    }

                    Current = loaded;
                    return Current;
                }
            }
        }
        catch (Exception ex)
        {
            // 配置读坏了不算致命错误：用默认值继续跑，并把坏文件改名留档
            System.Diagnostics.Debug.WriteLine($"[ConfigService] 读取配置失败，使用默认值：{ex.Message}");
            TryPreserveBrokenFile();
        }

        Current = new AppConfig();
        Save();   // 首次运行就生成一份配置文件，用户能看到并手动改
        return Current;
    }

    /// <summary>保存配置。</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] 保存配置失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 当前配置序列化成 UTF-8 JSON 字节（导出备份时用）。
    ///
    /// 和 <see cref="Save"/> 的区别：这个不碰磁盘，
    /// 所以导出流程里配置内容直接进 zip，不会在导出中途把本机配置改坏。
    /// </summary>
    public byte[] ExportJsonBytes() => JsonSerializer.SerializeToUtf8Bytes(Current, JsonOptions);

    /// <summary>
    /// 把一份备份里的配置合并进来（导入备份时用）。
    ///
    /// 为什么要"合并"而不是直接覆盖？
    ///   备份可能来自别人的电脑：里面的数据库路径、窗口尺寸都是他那台机器的，
    ///   照搬会让程序去找一个不存在的文件，甚至开出一个空库。
    ///   所以这几项一律保留本机的，只接收真正的偏好设置
    ///   （主题、强调色、专注时长、蒙版浓度……）。
    /// </summary>
    /// <param name="json">备份里 config.json 的内容。</param>
    /// <returns>合并后的配置；解不出来时原样返回当前配置。</returns>
    public AppConfig ApplyImportedConfig(string json)
    {
        AppConfig? imported;
        try
        {
            imported = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // 备份里的配置坏了不算致命：数据照样能导入，配置沿用本机的
            return Current;
        }

        if (imported is null)
        {
            return Current;
        }

        // ── 和「本机」绑定的项，不跟着备份走 ──
        imported.DatabasePath = Current.DatabasePath;
        imported.WindowWidth = Current.WindowWidth;
        imported.WindowHeight = Current.WindowHeight;
        imported.StartMaximized = Current.StartMaximized;

        // 配置版本取两边的高者：备份来自更新版本时，别把版本号倒退回去
        imported.ConfigVersion = Math.Max(imported.ConfigVersion, Current.ConfigVersion);

        Current = imported;
        Save();
        return Current;
    }

    /// <summary>把配置恢复成默认值并保存。</summary>
    public AppConfig ResetToDefault()
    {
        Current = new AppConfig();
        Save();
        return Current;
    }

    /// <summary>
    /// 算出数据库文件应该放哪：
    /// 配置里指定了就用指定的，否则用数据目录下的 helium.db。
    /// </summary>
    public string ResolveDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(Current.DatabasePath))
        {
            return Current.DatabasePath!;
        }

        TryCreateConfigDirectory();
        return Path.Combine(ConfigDirectory, "helium.db");
    }

    /// <summary>建数据目录；建不出来就记一笔，不抛异常（程序要能继续启动）。</summary>
    private void TryCreateConfigDirectory()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] 创建数据目录失败：{ex.Message}");
        }
    }

    /// <summary>把读坏的配置文件改名保存，方便排查（不会覆盖已有的备份）。</summary>
    private void TryPreserveBrokenFile()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return;

            var backup = Path.Combine(
                ConfigDirectory,
                $"config.broken.{DateTime.Now:yyyyMMddHHmmss}.json");

            File.Move(ConfigFilePath, backup);
        }
        catch
        {
            // 备份失败也无所谓，不要因为备份再抛一次异常
        }
    }
}
