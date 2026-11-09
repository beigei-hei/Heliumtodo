// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using HeliumTodo.Domain.Configuration;

namespace HeliumTodo.Desktop.Services;

/// <summary>
/// 决定"这次启动用哪个渲染后端"，并记住最终生效的是哪一个。
///
/// 【为什么单独一个文件？】
///   这件事有点别扭：必须在 Avalonia 初始化**之前**就定下来，
///   而那时候还没有依赖注入、没有窗口、也没有设置页（App.axaml.cs 还没跑）。
///   所以只能在这里自己开一个 <see cref="ConfigService"/> 去读 config.json，
///   读完把结果存成静态字段，等设置页建好了再回头看"当前生效的是哪个"。
///
/// 【优先级】
///   HELIUM_RENDER 环境变量 &gt; config.json 的 RenderBackend &gt; 出厂默认（自动/显卡）。
///   环境变量排第一是留给排障用的：设置项要重启才生效，
///   而"怀疑渲染出问题"的时候往往连界面都看不清，这时候用环境变量最快。
/// </summary>
internal static class RenderBackendChoice
{
    /// <summary>本次进程实际生效的后端（归一化后的 key）。</summary>
    public static string ActiveKey { get; private set; } = RenderBackendSettings.Default;

    /// <summary>是不是被环境变量覆盖了（设置页可以据此提示"环境变量优先"）。</summary>
    public static bool IsOverriddenByEnvironment { get; private set; }

    /// <summary>
    /// 算出这次该用哪个后端。只该在 <see cref="Program.BuildAvaloniaApp"/> 里调一次。
    /// </summary>
    public static string Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("HELIUM_RENDER");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            IsOverriddenByEnvironment = true;
            ActiveKey = RenderBackendSettings.Normalize(fromEnvironment);
            return ActiveKey;
        }

        IsOverriddenByEnvironment = false;
        ActiveKey = RenderBackendSettings.Normalize(PeekConfigured());
        return ActiveKey;
    }

    /// <summary>
    /// 把 key 变成 Avalonia 要的数组。
    ///
    /// 为什么是数组？Avalonia 的语义是"按顺序尝试"，第一个失败了才试下一个。
    /// 这里只给一个值：用户明确选了哪个就用哪个，悄悄降级反而让人查不出问题
    /// （真起不来时 Avalonia 会抛异常并写日志，比"默默变成软件渲染"好排查）。
    /// </summary>
    public static Win32RenderingMode[] ToModes(string key) => RenderBackendSettings.Normalize(key) switch
    {
        RenderBackendSettings.Auto => [Win32RenderingMode.AngleEgl],
        RenderBackendSettings.OpenGl => [Win32RenderingMode.Wgl],
        RenderBackendSettings.Vulkan => [Win32RenderingMode.Vulkan],
        _ => [Win32RenderingMode.Software],
    };

    /// <summary>
    /// 直接从 config.json 里抠出 RenderBackend 这个字段。
    ///
    /// 为什么不直接 new ConfigService().Load()？
    ///   因为 Load() 在文件不存在时会"顺手写一份默认配置"，
    ///   而这时候连窗口都还没有 —— 万一写失败，报错会发生在最难看懂的地方。
    ///   这里只读不写：读不到、格式坏了、字段没有，统统返回 null 走默认值。
    /// </summary>
    public static string? PeekConfigured()
    {
        try
        {
            // 配置目录的解析逻辑（%AppData% 不可写时退到程序旁的 Data）已经在 ConfigService 里了，
            // 这里复用它的路径，只在内存里读一个字段。
            var paths = new ConfigService(Environment.GetEnvironmentVariable("HELIUM_CONFIG_DIR"));
            if (!File.Exists(paths.ConfigFilePath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(paths.ConfigFilePath));
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(nameof(AppConfig.RenderBackend), out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (Exception ex)
        {
            // 配置读不出来不该影响启动：记一笔，按默认值走
            System.Diagnostics.Debug.WriteLine($"[渲染] 读取配置失败，按默认后端启动：{ex.Message}");
        }

        return null;
    }
}