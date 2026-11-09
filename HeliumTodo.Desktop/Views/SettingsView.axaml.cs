// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HeliumTodo.Desktop.ViewModels;

namespace HeliumTodo.Desktop.Views;

/// <summary>「设置」页面。</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 页面被换走时，把攒着的设置立刻写盘。
    ///
    /// 【为什么需要这一步？】
    ///   ViewModel 的保存是防抖的（150ms 内没有新改动才写），
    ///   用户拖完滑块立刻点侧栏切页，那个定时器还没到点就被换页面了 ——
    ///   设置看起来"改了但没存"。这里补最后一脚，保证"改完就走"也不丢。
    /// </summary>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (DataContext is SettingsViewModel vm)
        {
            vm.FlushPendingSave();
        }
    }

    /// <summary>
    /// 「浏览…」按钮：弹出系统文件选择框挑一张壁纸。
    ///
    /// 为什么这段代码写在 View 里而不是 ViewModel 里？
    ///   因为打开文件对话框需要拿到窗口（TopLevel），而 ViewModel 是故意不认识界面的。
    ///   这类"必须碰界面"的动作放在代码后置里最干净 —— 选完把路径交给 ViewModel 就行。
    /// </summary>
    private async void OnBrowseWallpaper(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选一张背景壁纸",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                // 图片和视频分两组放：用户想找 mp4 时不用在几十种扩展名里翻。
                // 视频能播是系统解码器说了算（Media Foundation），
                // 解不出来的会在设置页下方直接提示，而不是静默失败。
                new FilePickerFileType("图片（PNG / GIF / APNG / JPG / WebP）")
                {
                    Patterns = new[] { "*.png", "*.apng", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" },
                    MimeTypes = new[] { "image/*" },
                },
                new FilePickerFileType("视频（MP4 / MOV / WMV 等）")
                {
                    Patterns = new[] { "*.mp4", "*.m4v", "*.mov", "*.wmv", "*.avi", "*.mkv", "*.webm" },
                    MimeTypes = new[] { "video/*" },
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" },
                },
            },
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.SetWallpaperFile(path);
        }
    }

    /// <summary>「导出…」：先问用户存到哪，再让 ViewModel 去打包。</summary>
    private async void OnExportBackup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { } storage) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出全部数据",
            SuggestedFileName = vm.SuggestedBackupFileName,
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Helium Todo 备份")
                {
                    Patterns = new[] { "*.zip" },
                    MimeTypes = new[] { "application/zip" },
                },
            },
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await vm.ExportToAsync(path);
        }
    }

    /// <summary>「导入…」：挑一个 .zip，先让 ViewModel 检查内容再弹确认。</summary>
    private async void OnImportBackup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择一份 Helium Todo 备份",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Helium Todo 备份")
                {
                    Patterns = new[] { "*.zip" },
                    MimeTypes = new[] { "application/zip" },
                },
            },
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await vm.BeginImportAsync(path);
        }
    }
}
