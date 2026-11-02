using System.IO.Compression;
using System.Security.Cryptography;

namespace HeliumTodo.Core.Backup;

/// <summary>备份包有问题时抛这个异常。消息是写给人看的，可以直接弹到界面上。</summary>
public sealed class BackupPackageException : IOException
{
    public BackupPackageException(string message) : base(message)
    {
    }
}

/// <summary>
/// 打开并读取一个 .zip 备份包。
///
/// 设计原则：**先验后动**。
///   打开 → 读清单 → 确认是本程序的备份、版本能认、每个文件都对得上 SHA256，
///   全部通过之后，调用方才允许去碰真正的数据库。
///   任何一步不对都抛 <see cref="BackupPackageException"/>，消息直接能展示给用户。
/// </summary>
public sealed class BackupPackageReader : IDisposable
{
    private readonly ZipArchive _archive;

    private BackupPackageReader(string filePath, ZipArchive archive, BackupManifest manifest)
    {
        FilePath = filePath;
        _archive = archive;
        Manifest = manifest;
    }

    /// <summary>备份文件路径。</summary>
    public string FilePath { get; }

    /// <summary>已经解析并校验过版本兼容性的清单。</summary>
    public BackupManifest Manifest { get; }

    /// <summary>
    /// 打开备份包并做基础校验（是不是 zip、有没有清单、格式能不能认）。
    /// </summary>
    /// <param name="filePath">.zip 路径。</param>
    /// <exception cref="BackupPackageException">任何一项不合格。</exception>
    public static BackupPackageReader Open(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new BackupPackageException($"找不到备份文件：{filePath}");
        }

        ZipArchive archive;
        try
        {
            // FileShare.Read：允许用户同时在资源管理器里复制这个文件
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            archive = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new BackupPackageException("这个文件不是有效的 .zip，可能下载不完整或已经损坏。");
        }
        catch (IOException ex)
        {
            throw new BackupPackageException($"打不开这个文件：{ex.Message}");
        }

        try
        {
            var manifestEntry = FindEntry(archive, BackupFormat.ManifestEntry)
                ?? throw new BackupPackageException(
                    "这个 zip 里没有 manifest.json，不是 Helium Todo 导出的备份。");

            var json = ReadAllBytes(manifestEntry);
            var manifest = BackupManifest.TryParse(json, out var error)
                ?? throw new BackupPackageException(error ?? "清单解析失败。");

            ValidateManifest(manifest);

            return new BackupPackageReader(filePath, archive, manifest);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 逐个核对清单里声明的 SHA256。
    /// 返回 false 时 <paramref name="error"/> 是「哪个文件坏了」。
    /// </summary>
    public bool TryVerifyChecksums(out string? error)
    {
        foreach (var entry in DeclaredEntries())
        {
            var zipEntry = FindEntry(_archive, entry.Entry);
            if (zipEntry is null)
            {
                error = $"备份里缺少文件：{entry.Entry}";
                return false;
            }

            var actual = Convert.ToHexString(SHA256.HashData(ReadAllBytes(zipEntry))).ToLowerInvariant();
            if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"文件校验失败：{entry.Entry}（可能传输过程中损坏了）";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary>把某个条目读成字节数组。</summary>
    /// <exception cref="BackupPackageException">条目不存在。</exception>
    public byte[] ReadEntry(string entryName)
    {
        var entry = FindEntry(_archive, entryName)
            ?? throw new BackupPackageException($"备份里缺少文件：{entryName}");
        return ReadAllBytes(entry);
    }

    /// <summary>
    /// 把某个条目解压到指定路径（流式，适合数据库这种稍大的文件）。
    /// </summary>
    public void ExtractEntryTo(string entryName, string targetPath)
    {
        var entry = FindEntry(_archive, entryName)
            ?? throw new BackupPackageException($"备份里缺少文件：{entryName}");

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var source = entry.Open();
        using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }

    /// <summary>包里所有条目的名字（调试/展示用）。</summary>
    public IReadOnlyList<string> EntryNames => _archive.Entries.Select(e => e.FullName).ToList();

    public void Dispose() => _archive.Dispose();

    // ────────────── 内部工具 ──────────────

    private IEnumerable<BackupFileEntry> DeclaredEntries()
    {
        if (Manifest.Database is not null) yield return Manifest.Database;
        if (Manifest.Config is not null) yield return Manifest.Config;
        if (Manifest.Wallpaper is not null) yield return Manifest.Wallpaper;
    }

    /// <summary>
    /// 版本兼容性判断。规则：只接受「格式版本不超过自己」的备份。
    /// 更高的版本可能带了我们不认识的字段/语义，硬导进去会悄悄丢数据，不如直接拒绝并说清楚。
    /// </summary>
    private static void ValidateManifest(BackupManifest manifest)
    {
        if (!string.Equals(manifest.Format, BackupFormat.FormatId, StringComparison.Ordinal))
        {
            throw new BackupPackageException(
                $"这个 zip 不是 Helium Todo 的备份（清单里的格式标识是「{manifest.Format}」）。");
        }

        if (manifest.FormatVersion > BackupFormat.CurrentVersion)
        {
            throw new BackupPackageException(
                $"这份备份来自更新版本的程序（备份格式 v{manifest.FormatVersion}），" +
                $"当前程序最多支持 v{BackupFormat.CurrentVersion}。请先升级程序再导入。");
        }

        if (manifest.FormatVersion < 1)
        {
            throw new BackupPackageException($"备份格式版本无效（v{manifest.FormatVersion}）。");
        }

        if (manifest.Database is null)
        {
            throw new BackupPackageException("清单里没有数据库条目，这个备份是空的。");
        }
    }

    /// <summary>
    /// 在 zip 里找条目。zip 规范用 `/` 分隔，但有的工具会写成 `\`，
    /// 所以两边都规范一下再比，且忽略大小写。
    /// </summary>
    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryName)
    {
        var wanted = Normalize(entryName);
        return archive.Entries.FirstOrDefault(e => Normalize(e.FullName) == wanted);
    }

    private static string Normalize(string name) => name.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    private static byte[] ReadAllBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream((int)Math.Max(entry.Length, 1024));
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}