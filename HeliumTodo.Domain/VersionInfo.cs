// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;

namespace HeliumTodo.Domain;

/// <summary>
/// 版本号的统一出口。
///
/// 为什么需要这个类？
///   想知道「当前是什么版本」其实有好几个来源，而且它们给的东西不一样：
///     · Assembly.GetName().Version      → 只有 0.5.0.0，**看不到 -dev 标记**
///     · AssemblyInformationalVersion    → "0.5.0-alpha+3f2a1b..."，带标记但也带一长串编译哈希
///   界面和插件想要的是「0.5.0-alpha」这种人看的版本号，
///   所以在这里统一处理一次，别处直接调 <see cref="Current"/> 就行。
///
/// 版本号从哪来？
///   根目录 Directory.Build.props 里的 &lt;Version&gt; 标签，改那一处就是全局生效。
/// </summary>
public static class VersionInfo
{
    /// <summary>当前版本号，形如 "0.5.0-alpha"。取不到时返回 "0.0.0"。</summary>
    public static string Current { get; } = Resolve();

    /// <summary>带 v 前缀的显示用版本号，形如 "v0.5.0-alpha"。</summary>
    public static string Display => "v" + Current;

    private static string Resolve()
    {
        var assembly = typeof(VersionInfo).Assembly;

        // 首选「信息版本」：它保留了 -dev 这类预发布标记
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // 形如 "0.5.0-alpha+3f2a1b9c..."：加号后面是源码版本哈希，
            // 对用户没意义，砍掉
            var plusIndex = informational.IndexOf('+');
            var cleaned = (plusIndex >= 0 ? informational[..plusIndex] : informational).Trim();

            if (cleaned.Length > 0)
            {
                return cleaned;
            }
        }

        // 退路：程序集版本（会丢掉 -dev 标记，但总比没有好）
        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
