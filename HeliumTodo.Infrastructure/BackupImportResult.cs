// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Backup;

namespace HeliumTodo.Infrastructure;

/// <summary>一次导入的结果。</summary>
public sealed class BackupImportResult
{
    /// <summary>是否成功（目前失败都靠抛异常，这里恒为 true，留给以后做"部分成功"）。</summary>
    public bool Success { get; init; }

    /// <summary>用的是替换还是合并。</summary>
    public BackupImportMode Mode { get; init; }

    /// <summary>导入的备份文件。</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>这次实际写进去的行数。</summary>
    public BackupCounts Counts { get; init; } = new();

    /// <summary>导入前的安全备份路径（合并模式下为 null，因为它不改现有数据）。</summary>
    public string? SafetyBackupPath { get; init; }

    /// <summary>是否应用了备份里的配置。</summary>
    public bool ConfigApplied { get; init; }

    /// <summary>是否恢复了壁纸。</summary>
    public bool WallpaperRestored { get; init; }

    /// <summary>给界面提示的一句话。</summary>
    public string DescribeText
    {
        get
        {
            var text = $"{BackupImportModeInfo.DisplayName(Mode)}：已导入 {Counts.DescribeText}";

            if (SafetyBackupPath is not null)
            {
                text += $"；导入前的数据已备份到 {SafetyBackupPath}";
            }

            if (ConfigApplied)
            {
                text += "；配置已同步";
            }

            if (WallpaperRestored)
            {
                text += "；壁纸已恢复";
            }

            return text;
        }
    }
}