// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;

namespace HeliumTodo.Tests;

/// <summary>
/// 测试用的临时目录。
///
/// 为什么不用 Path.GetTempPath()？
///   开发机上测过：这套沙箱环境里 .NET 进程**建不了** %TEMP% 下的目录，
///   一律 UnauthorizedAccessException（同一个目录用 python 或 bash 就能建）。
///   测试要是把目录建在 %TEMP%，在这台机器上就全是"环境问题"的红灯，
///   真正要测的东西反而看不见。
///
/// 所以统一建在测试程序自己的输出目录（bin/Debug/net10.0/_test-tmp/）下：
///   · 一定可写（程序能跑起来就说明这个目录可写）
///   · 每个测试一个 guid 子目录，互不干扰
///   · 用完即删，不留在仓库里
/// </summary>
public static class TestWorkspace
{
    /// <summary>建一个全新的、唯一的空目录，返回绝对路径。</summary>
    public static string Create(string prefix)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "_test-tmp", prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>删掉整个目录树。删不掉也不算错 —— 留着不影响下次，因为每次都是新 guid。</summary>
    public static void Delete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // 数据库文件还被某个连接占着时删不掉，忽略即可。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上。
        }
    }
}
