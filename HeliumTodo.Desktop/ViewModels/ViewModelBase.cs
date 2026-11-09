// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using CommunityToolkit.Mvvm.ComponentModel;

namespace HeliumTodo.Desktop.ViewModels;

/// <summary>
/// 所有 ViewModel 的基类。
///
/// 它继承自 <see cref="ObservableObject"/>（来自 CommunityToolkit.Mvvm），
/// 这个基类实现了 INotifyPropertyChanged —— 也就是「属性变了要通知界面刷新」的机制。
///
/// 用法：字段上加 [ObservableProperty]，源生成器会自动生成同名的大写属性。
/// <code>
/// [ObservableProperty]
/// private string _title = "";     // 会自动生成 public string Title { get; set; }
/// </code>
/// 这样就不用自己写一大堆 get/set + OnPropertyChanged 了。
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;

    /// <summary>是否正在忙（界面上可以显示进度环）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// 页面每次被显示时调用（用来刷新数据）。
    /// 默认什么也不做，需要的页面自己重写。
    /// </summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}
