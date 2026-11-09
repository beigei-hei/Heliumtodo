// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace HeliumTodo.Desktop.Behaviors;

/// <summary>几个 Behavior 共用的小判断，避免每个文件各写一份。</summary>
internal static class BehaviorTargets
{
    /// <summary>从事件源往上找，看它是不是落在某个 Button 里面。</summary>
    internal static bool IsInsideButton(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button) return true;
        }

        return false;
    }

    /// <summary>取控件所在的窗口，没有就返回 null。</summary>
    internal static Window? GetWindow(Visual? visual) => TopLevel.GetTopLevel(visual) as Window;
}