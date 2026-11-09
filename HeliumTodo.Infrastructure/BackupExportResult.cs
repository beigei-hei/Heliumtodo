// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using HeliumTodo.Domain.Backup;

namespace HeliumTodo.Infrastructure;

/// <summary>一次导出的结果。</summary>
public sealed class BackupExportResult
{
    public BackupExportResult(string path, BackupManifest manifest)
    {
        Path = path;
        Manifest = manifest;
    }

    /// <summary>生成的 .zip 路径。</summary>
    public string Path { get; }

    /// <summary>写进包里的清单（大小、条数都在里面）。</summary>
    public BackupManifest Manifest { get; }

    /// <summary>
    /// 用户本来挑的路径。只有"原位置写不进去、改存到备份文件夹"时才和 <see cref="Path"/> 不同，
    /// 界面靠这个告诉用户"我改了地方"。
    /// </summary>
    public string? RequestedPath { get; init; }

    /// <summary>是不是原位置写不进去、改存到应用自己的备份文件夹了。</summary>
    public bool FellBack => RequestedPath is not null;

    /// <summary>文件多大（字节）。</summary>
    public long SizeBytes => new FileInfo(Path).Exists ? new FileInfo(Path).Length : 0;

    /// <summary>给界面提示的一句话。</summary>
    public string DescribeText
    {
        get
        {
            var size = SizeBytes;
            var sizeText = size < 1024 * 1024
                ? $"{size / 1024.0:0.#} KB"
                : $"{size / (1024.0 * 1024.0):0.#} MB";

            return $"已导出 {Path}（{sizeText}，{Manifest.Counts.DescribeText}）";
        }
    }
}