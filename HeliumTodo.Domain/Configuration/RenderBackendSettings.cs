// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;

namespace HeliumTodo.Domain.Configuration;

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

    /// <summary>
    /// 出厂默认值：<see cref="Auto"/>（Windows 上就是 ANGLE → D3D11）。
    ///
    /// 【这里改过一次，理由也换过一次，别改回去】
    ///   0.3.3 曾经把默认值定成软件渲染，换来的好处是省内存：
    ///   ANGLE 会把整套显卡驱动映射进进程（nvgpucomp64.dll 94 MB + nvwgf2umx.dll 86 MB），
    ///   实测私有字节 78.2 MB → 256.9 MB、线程 19 → 84、句柄 481 → 846。
    ///
    ///   但 0.3.5 补完窗口动效之后，天平反过来了：软件渲染下每次改窗口尺寸要
    ///   45 ms（2560×1440 上整页重排 + 全窗光栅化的固定成本），2560×1440 的展开动画
    ///   只有 25–43 fps；同一台机器换 ANGLE 直接到 74 fps。用户明确要求"流畅优先"，
    ///   所以默认值改回自动，内存那一笔照旧记在 TECH_STACK.md 里当已知代价。
    ///
    ///   想省回内存：设置页「底层设置 → 渲染后端」选回"软件渲染"，或者设
    ///   环境变量 HELIUM_RENDER=software。
    /// </summary>
    public const string Default = Auto;

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