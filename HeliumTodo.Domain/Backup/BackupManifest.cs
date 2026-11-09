// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeliumTodo.Domain.Backup;

/// <summary>清单里记录的一个文件：位置、大小、SHA256。</summary>
public sealed class BackupFileEntry
{
    /// <summary>在 zip 里的路径，例如 data/Todos.jsonl。</summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>解压后的大小（字节）。</summary>
    public long SizeBytes { get; set; }

    /// <summary>内容的 SHA256（小写十六进制），用于导入前校验没坏。</summary>
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>备份里各个表各有多少行 —— 让用户在导入前能看出"这份备份是不是空的"。</summary>
public sealed class BackupCounts
{
    public int Schedules { get; set; }

    public int Todos { get; set; }

    public int FocusSessions { get; set; }

    public int StudyRooms { get; set; }

    public int StudyRoomMembers { get; set; }

    public int StudyRoomMessages { get; set; }

    /// <summary>主要数据的总条数（日程 + 待办 + 专注），用来一眼判断空不空。</summary>
    [JsonIgnore]
    public int Total => Schedules + Todos + FocusSessions;

    /// <summary>给界面看的一行说明。</summary>
    [JsonIgnore]
    public string DescribeText =>
        $"{Schedules} 条日程 · {Todos} 条待办 · {FocusSessions} 条专注记录" +
        (StudyRooms > 0 ? $" · {StudyRooms} 个自习室" : string.Empty);
}

/// <summary>
/// 备份清单（manifest.json）。
///
/// 它存在的意义：让导入在做任何破坏性动作之前，就能回答三个问题 ——
///   ① 这是不是本程序的备份？② 是哪一版导出的？③ 文件有没有损坏？
/// </summary>
public sealed class BackupManifest
{
    /// <summary>固定值 <see cref="BackupFormat.FormatId"/>，用来识别"这是一个 Helium Todo 备份"。</summary>
    public string Format { get; set; } = BackupFormat.FormatId;

    /// <summary>格式版本，见 <see cref="BackupFormat.CurrentVersion"/>。</summary>
    public int FormatVersion { get; set; } = BackupFormat.CurrentVersion;

    /// <summary>导出这份备份的程序版本（例如 0.5.0-alpha）。</summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>导出时的存储提供程序（0.5.0 起固定为 postgres）。</summary>
    public string Provider { get; set; } = "postgres";

    /// <summary>导出时间（本地时间，界面展示用）。</summary>
    public DateTime ExportedAt { get; set; } = DateTime.Now;

    /// <summary>导出时间（UTC，规格 §7.5 的 createdAtUtc）。</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>每张表的 JSONL 文件（v2）。</summary>
    public List<BackupFileEntry> Tables { get; set; } = new();

    /// <summary>旧版（v1）的 SQLite 数据库文件；v2 备份里为 null。</summary>
    public BackupFileEntry? Database { get; set; }

    /// <summary>配置文件（可能没有）。</summary>
    public BackupFileEntry? Config { get; set; }

    /// <summary>壁纸文件（可能没有）。</summary>
    public BackupFileEntry? Wallpaper { get; set; }

    /// <summary>壁纸的原始文件名，导入时用来恢复文件名。</summary>
    public string? WallpaperFileName { get; set; }

    /// <summary>壁纸被跳过的原因（太大之类）。有值就说明清单里不会有壁纸条目。</summary>
    public string? WallpaperSkippedReason { get; set; }

    /// <summary>各表行数。</summary>
    public BackupCounts Counts { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 让中文在清单里原样显示（否则会变成 \uXXXX，用记事本打开看不懂）
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>序列化成 UTF-8 JSON 字节。</summary>
    public byte[] ToJsonBytes() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    /// <summary>
    /// 解析清单。解析不出来不抛异常，把原因写进 <paramref name="error"/> 返回 null。
    /// 导入流程要能"友好地拒绝"，不能崩。
    /// </summary>
    public static BackupManifest? TryParse(byte[] json, out string? error)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);
            if (manifest is null)
            {
                error = "清单内容为空。";
                return null;
            }

            error = null;
            return manifest;
        }
        catch (JsonException ex)
        {
            error = $"清单不是合法的 JSON：{ex.Message}";
            return null;
        }
    }
}
