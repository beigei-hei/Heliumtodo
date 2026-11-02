using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace HeliumTodo.App.Services;

/// <summary>
/// 打开一个文件夹。
///
/// 单独抽成接口有两个原因：
///   ① Avalonia 的 <see cref="ILauncher"/> 挂在 <see cref="TopLevel"/> 上，
///      而 ViewModel 里拿不到 TopLevel —— 让它来要，比每个 ViewModel 都往上爬一遍干净；
///   ② 它能返回"到底成没成"，而 <c>Process.Start</c> 在很多情况下是"开了但看不出"。
/// </summary>
public interface IFolderLauncher
{
    /// <summary>用系统的默认方式打开一个文件夹。返回 false 表示这条路走不通，调用方该换下一条。</summary>
    Task<bool> OpenAsync(string directoryPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 用 Avalonia 自带的 <see cref="ILauncher"/> 打开文件夹。
///
/// 这是 12.x 才有的能力：`LauncherExtensions.LaunchDirectoryInfoAsync` 会把目录交给系统外壳，
/// 由它决定是资源管理器还是用户自己设的第三方文件管理器 —— 比硬编码 `explorer.exe` 更"守规矩"。
/// </summary>
public sealed class AvaloniaFolderLauncher : IFolderLauncher
{
    public async Task<bool> OpenAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var top = ResolveTopLevel();
            if (top is null || !Directory.Exists(directoryPath))
            {
                return false;
            }

            return await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directoryPath));
        }
        catch (Exception ex)
        {
            // 无头平台、或者系统不认这个动作时会抛 —— 当作"走不通"，让调用方继续降级
            Debug.WriteLine($"[FolderLauncher] 用 Launcher 打开失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 找到能用的 <see cref="TopLevel"/>。主窗口被藏到托盘时它依然存在，
    /// 所以"隐藏到后台"不影响打开文件夹。
    /// </summary>
    private static TopLevel? ResolveTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
