using System;

namespace HeliumTodo.Core.Configuration;

/// <summary>
/// 渲染后端的取值表。
///
/// 【为什么这个要放在 Core 里？】
///   Core 不引用 Avalonia，所以这里只存"字符串常量 + 归一化规则"，
///   真正变成 Avalonia 的 <c>Win32RenderingMode</c> 是在 App/Program.cs 里做的。
///   这样做的好处是：配置项的取值可以被单元测试直接覆盖，
///   而不用为了测一个下拉框去启动整个 UI 框架。
///
/// 【为什么文件名不叫 RenderBackend？】
///   App 层还有一个同名的辅助类（要碰 Avalonia 的类型），
///   文件叫 xxxSettings.cs 是为了让"这是纯配置"一眼看得出来。
/// </summary>
public static class RenderBackendSettings
{
    /// <summary>软件渲染（Skia 在 CPU 上光栅化）。</summary>
    public const string Software = "software";

    /// <summary>Avalonia 自带的渲染路径（Windows 上就是 ANGLE → D3D11）。</summary>
    public const string Auto = "auto";

    /// <summary>OpenGL（Windows 上走 WGL，也就是显卡驱动自己的 GL 实现）。</summary>
    public const string OpenGl = "wgl";

    /// <summary>Vulkan。</summary>
    public const string Vulkan = "vulkan";

    /// <summary>出厂默认值：软件渲染（理由见 Program.cs 的注释，简单说就是省内存）。</summary>
    public const string Default = Software;

    /// <summary>
    /// 把配置文件里/环境变量里写的东西归一化成上面四个常量之一。
    ///
    /// 容忍几种写法，是因为用户会手改配置文件：
    ///   "angle" / "default" / "avalonia" → auto
    ///   "opengl" / "gl"                → wgl
    /// 大小写、前后空格也一并容错。
    /// 认不出来的值一律回落到默认值 —— 不能因为配置文件里写错一个词，程序就起不来。
    /// </summary>
    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Software => Software,
        "cpu" or "skia" or "soft" => Software,

        Auto => Auto,
        "angle" or "default" or "avalonia" or "angleegl" => Auto,

        OpenGl => OpenGl,
        "opengl" or "gl" => OpenGl,

        Vulkan => Vulkan,

        _ => Default,
    };

    /// <summary>
    /// 换到 <paramref name="selected"/> 之后，是不是必须要重启才生效。
    ///
    /// 判断放在 Core 里是为了能被单测覆盖：界面问"要不要弹重启提示"、
    /// 状态文字问"要不要显示'重启后改为 X'" —— 这两处必须用同一个判断，
    /// 否则会出现"提示说不用重启，状态文字又说重启后生效"这种自相矛盾的界面。
    ///
    /// 注意：这里只比较"配置值"，不关心当前真正在跑的是哪个后端 ——
    /// 因为渲染后端只在启动时读一次，配置值与运行值本来就可能是新旧的差异。
    /// </summary>
    public static bool NeedsRestart(string? current, string? selected)
        => Normalize(current) != Normalize(selected);

    /// <summary>给界面/日志看的名字（不含"（默认）"这类修饰，修饰由界面自己加）。</summary>
    public static string DisplayName(string? value) => Normalize(value) switch
    {
        Auto => "Avalonia 自带渲染器",
        OpenGl => "OpenGL",
        Vulkan => "Vulkan",
        _ => "软件渲染",
    };
}