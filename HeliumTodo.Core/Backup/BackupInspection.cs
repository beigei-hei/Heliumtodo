namespace HeliumTodo.Core.Backup;

/// <summary>导入方式。</summary>
public enum BackupImportMode
{
    /// <summary>清空现有数据，原样换成备份里的（"恢复"）。</summary>
    Replace = 0,

    /// <summary>保留现有数据，把备份里的内容作为新记录追加进来。</summary>
    Merge = 1,
}

/// <summary>导入方式的显示名与说明（放在 Core 里，界面和日志共用一套措辞）。</summary>
public static class BackupImportModeInfo
{
    public static string DisplayName(BackupImportMode mode) => mode switch
    {
        BackupImportMode.Merge => "合并（保留现有数据）",
        _ => "替换（清空现有数据）",
    };

    public static string Description(BackupImportMode mode) => mode switch
    {
        BackupImportMode.Merge => "现有数据全部保留，备份里的记录以新编号追加进来。",
        _ => "现有数据会被清空，完全变成备份里的内容。导入前会自动存一份当前数据的备份。",
    };
}

/// <summary>
/// 「看一眼这个备份里有什么」的结果 —— 只读清单，不打开数据库、不动任何文件。
/// 界面在用户确认导入之前先展示它，避免"手一抖点了导入，数据全变了"。
/// </summary>
public sealed class BackupInspection
{
    /// <summary>能不能导入（清单合法、校验通过）。</summary>
    public bool IsUsable { get; init; }

    /// <summary>不能导入的原因；能用时为 null。</summary>
    public string? Error { get; init; }

    /// <summary>能导入但值得提醒一句（例如没带配置、没带壁纸）。</summary>
    public string? Warning { get; init; }

    /// <summary>备份文件路径。</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>格式版本。</summary>
    public int FormatVersion { get; init; }

    /// <summary>导出它的程序版本。</summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>导出时间。</summary>
    public DateTime ExportedAt { get; init; }

    /// <summary>各表行数。</summary>
    public BackupCounts Counts { get; init; } = new();

    /// <summary>数据库文件大小。</summary>
    public long DatabaseBytes { get; init; }

    /// <summary>是否带配置。</summary>
    public bool HasConfig { get; init; }

    /// <summary>是否带壁纸。</summary>
    public bool HasWallpaper { get; init; }

    /// <summary>壁纸原始文件名。</summary>
    public string? WallpaperFileName { get; init; }

    /// <summary>给界面看的一行摘要。</summary>
    public string DescribeText
    {
        get
        {
            if (!IsUsable)
            {
                return Error ?? "这个文件不能导入。";
            }

            var parts = new List<string>
            {
                ExportedAt.ToString("yyyy-MM-dd HH:mm") + " 导出",
                string.IsNullOrWhiteSpace(AppVersion) ? "版本未知" : "v" + AppVersion.TrimStart('v', 'V'),
                Counts.DescribeText,
                FormatSize(DatabaseBytes),
            };

            parts.Add(HasConfig ? "含配置" : "无配置");
            parts.Add(HasWallpaper ? $"含壁纸（{WallpaperFileName}）" : "无壁纸");

            return string.Join(" · ", parts);
        }
    }

    /// <summary>构造一个「不能用」的结果。</summary>
    public static BackupInspection Failed(string filePath, string error) => new()
    {
        IsUsable = false,
        Error = error,
        FilePath = filePath,
    };

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}