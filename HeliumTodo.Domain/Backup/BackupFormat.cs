// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Domain.Backup;

/// <summary>
/// 备份包（.zip）的格式约定。
///
/// 为什么把「导出的长什么样」写成一份常量表？
///   因为导出和导入必须是同一套规则 —— 分开写两遍迟早会不一致。
///   这里集中定义，导出时按它打包，导入时按它校验，改格式只改这一处。
///
/// 包内结构（v1）：
///   manifest.json          清单：版本、时间、每个文件的 SHA256、各表行数
///   data/helium.db         SQLite 数据库快照
///   data/config.json       配置
///   assets/wallpaper.&lt;ext&gt;  可选：当前壁纸原图
/// </summary>
public static class BackupFormat
{
    /// <summary>包的"身份证"，用来判断一个 zip 到底是不是本程序的备份。</summary>
    public const string FormatId = "helium-todo-backup";

    /// <summary>
    /// 当前格式版本。
    /// 规则：只加字段 → 升 minor（这里用小整数递增，不做 major.minor）；
    /// 删字段或改语义 → 必须 +1，并让旧版本拒绝导入。
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>备份文件扩展名（统一用 .zip）。</summary>
    public const string FileExtension = ".zip";

    /// <summary>备份文件名前缀。</summary>
    public const string FilePrefix = "HeliumTodo-backup";

    /// <summary>清单文件名。</summary>
    public const string ManifestEntry = "manifest.json";

    /// <summary>数据库在包内的路径。</summary>
    public const string DatabaseEntry = "data/helium.db";

    /// <summary>配置在包内的路径。</summary>
    public const string ConfigEntry = "data/config.json";

    /// <summary>壁纸在包内的路径前缀（后面接扩展名）。</summary>
    public const string WallpaperEntryPrefix = "assets/wallpaper";

    /// <summary>导入时把壁纸落到哪个子目录（相对配置目录）。</summary>
    public const string WallpaperDirectoryName = "wallpapers";

    /// <summary>备份存放的子目录（相对数据库所在目录）。</summary>
    public const string BackupDirectoryName = "backups";

    /// <summary>导入前的安全备份文件名前缀。</summary>
    public const string PreImportPrefix = "pre-import";

    /// <summary>
    /// 壁纸最多打包多大。
    /// 备份是要发给别人/传到网盘的，塞一张 50MB 的照片不合适 —— 超过就跳过并记在清单里。
    /// </summary>
    public const int MaxWallpaperBytes = 32 * 1024 * 1024;

    /// <summary>按时间生成一个备份文件名，例如 HeliumTodo-backup-20261027-180500.zip。</summary>
    public static string BuildFileName(DateTime time) =>
        $"{FilePrefix}-{time:yyyyMMdd}-{time:HHmmss}{FileExtension}";

    /// <summary>生成导入前安全备份的文件名。</summary>
    public static string BuildPreImportFileName(DateTime time) =>
        $"{PreImportPrefix}-{time:yyyyMMdd}-{time:HHmmss}{FileExtension}";

    /// <summary>壁纸在包内的路径（带扩展名）。</summary>
    public static string WallpaperEntry(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return WallpaperEntryPrefix + ext.ToLowerInvariant();
    }
}