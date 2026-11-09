// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.IO.Compression;
using System.Security.Cryptography;

namespace HeliumTodo.Domain.Backup;

/// <summary>
/// 要打进备份里的东西。
/// 数据库是必须的，配置和壁纸可选。
/// </summary>
public sealed class BackupPackageContent
{
    /// <summary>数据库快照的字节内容（必须已做过 WAL checkpoint）。</summary>
    public byte[] Database { get; init; } = Array.Empty<byte>();

    /// <summary>配置文件内容（UTF-8 JSON）。</summary>
    public byte[]? Config { get; init; }

    /// <summary>壁纸原图内容。</summary>
    public byte[]? Wallpaper { get; init; }

    /// <summary>壁纸原始文件名（只用于记录，导入时按它命名）。</summary>
    public string? WallpaperFileName { get; init; }

    /// <summary>壁纸没打进去的原因。</summary>
    public string? WallpaperSkippedReason { get; init; }

    /// <summary>各表行数。</summary>
    public BackupCounts Counts { get; init; } = new();

    /// <summary>程序版本。</summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>导出时间。</summary>
    public DateTime ExportedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 把内容打成 .zip 备份包。
///
/// 两个细节值得说明：
///   ① 先写 .tmp 再改名 —— 导出到一半断电/磁盘满，不会留下一个"看起来能导入、其实是半个"的 zip。
///   ② 清单里给每个文件算 SHA256 —— 导入前能发现"文件在传输中坏了"，
///      而不是等 SQLite 打开时报一句看不懂的 malformed。
/// </summary>
/// <summary>
/// 目标文件夹写不进去。
///
/// 为什么单独定义一个类型：`UnauthorizedAccessException` 不是 `IOException` 的子类，
/// 上层如果只 `catch (IOException)` 会漏掉它 —— 表现就是"程序直接崩了，而不是提示一句"。
/// 换成这个类型之后，上层一律 `catch (BackupPackageException)` 就能兜住。
/// </summary>
public sealed class BackupWriteDeniedException : IOException
{
    public BackupWriteDeniedException(string targetPath, string message, Exception? inner = null)
        : base(message, inner)
    {
        TargetPath = targetPath;
    }

    /// <summary>本来想写到哪儿。</summary>
    public string TargetPath { get; }
}

public static class BackupPackageWriter
{
    /// <summary>
    /// 写出一个备份包，并返回写进包里的清单（调用方可以拿去展示）。
    /// </summary>
    /// <param name="targetPath">目标 .zip 路径。</param>
    /// <param name="content">要打包的内容。</param>
    public static BackupManifest Write(string targetPath, BackupPackageContent content)
    {
        if (content.Database.Length == 0)
        {
            throw new ArgumentException("数据库内容是空的，不能导出。", nameof(content));
        }

        var manifest = new BackupManifest
        {
            Format = BackupFormat.FormatId,
            FormatVersion = BackupFormat.CurrentVersion,
            AppVersion = content.AppVersion,
            ExportedAt = content.ExportedAt,
            Counts = content.Counts,
            Database = BuildEntry(BackupFormat.DatabaseEntry, content.Database),
            WallpaperFileName = content.WallpaperFileName,
            WallpaperSkippedReason = content.WallpaperSkippedReason,
        };

        if (content.Config is { Length: > 0 })
        {
            manifest.Config = BuildEntry(BackupFormat.ConfigEntry, content.Config);
        }

        var wallpaperEntry = BackupFormat.WallpaperEntry(
            Path.GetExtension(content.WallpaperFileName ?? string.Empty));

        if (content.Wallpaper is { Length: > 0 })
        {
            manifest.Wallpaper = BuildEntry(wallpaperEntry, content.Wallpaper);
        }

        // 目标目录可能还不存在（用户第一次导出到某个新文件夹）
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or NotSupportedException)
            {
                throw new BackupWriteDeniedException(targetPath, DescribeDenied(directory, ex), ex);
            }
        }

        var tempPath = targetPath + ".tmp";

        try
        {
            using (var stream = OpenTempStream(tempPath, targetPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                // 清单放第一个：用压缩软件打开时一眼能看到这是什么
                AddEntry(zip, BackupFormat.ManifestEntry, manifest.ToJsonBytes());
                AddEntry(zip, BackupFormat.DatabaseEntry, content.Database);

                if (content.Config is { Length: > 0 } configBytes)
                {
                    AddEntry(zip, BackupFormat.ConfigEntry, configBytes);
                }

                if (content.Wallpaper is { Length: > 0 } wallpaperBytes)
                {
                    AddEntry(zip, wallpaperEntry, wallpaperBytes);
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 临时文件写得进去、改名却不允许：典型情况是"目标同名项是个文件夹"，
            // 或者目标文件被别的程序以只读方式占着。对上层来说这仍然是"写不进去"。
            TryDelete(tempPath);
            throw new BackupWriteDeniedException(
                targetPath, DescribeDenied(Path.GetDirectoryName(targetPath) ?? targetPath, ex), ex);
        }
        catch
        {
            // 失败时把临时文件清掉，别在用户的文件夹里留垃圾
            TryDelete(tempPath);
            throw;
        }

        return manifest;
    }

    /// <summary>
    /// 开临时文件。写不进去就换成 <see cref="BackupWriteDeniedException"/>，
    /// 好让上层能"改存到别处"，而不是把异常直接甩给用户。
    /// </summary>
    private static FileStream OpenTempStream(string tempPath, string targetPath)
    {
        try
        {
            return new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or NotSupportedException)
        {
            throw new BackupWriteDeniedException(targetPath, DescribeDenied(Path.GetDirectoryName(targetPath) ?? targetPath, ex), ex);
        }
    }

    /// <summary>
    /// 把"为什么写不进去"翻译成人话。这句会原样显示在界面上，
    /// 所以要提到用户能动手改的东西（受控文件夹访问 / 权限 / 只读盘）。
    /// </summary>
    private static string DescribeDenied(string directory, Exception ex) => ex switch
    {
        UnauthorizedAccessException =>
            $"系统不允许往「{directory}」里写文件。常见原因：Windows 安全中心的「受控文件夹访问」把这个程序拦住了，"
            + "或者这个文件夹本身只读 / 需要管理员权限。",
        DirectoryNotFoundException => $"目录「{directory}」不存在，也建不出来。",
        _ => $"写不进「{directory}」：{ex.Message}",
    };

    private static BackupFileEntry BuildEntry(string entry, byte[] content) => new()
    {
        Entry = entry,
        SizeBytes = content.LongLength,
        Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
    };

    private static void AddEntry(ZipArchive zip, string entryName, byte[] content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 删不掉就算了，别把真正的错误盖掉
        }
    }
}