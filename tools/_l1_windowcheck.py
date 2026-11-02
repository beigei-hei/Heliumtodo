import io

path = r"D:\代码\dsh project\Helium todo demo\HeliumTodo.RenderCheck\Program.cs"
text = io.open(path, encoding="utf-8-sig").read()

start_marker = "        // ── ① 打开动画 ──"
end_marker = "        Console.WriteLine(\"   [跳过] 最大化过程动画 — 无头定时器不推进，改由 probe-maximize-anim.ps1 验证\");"

start = text.index(start_marker)
end = text.index(end_marker) + len(end_marker)

new = '''        // ── ① 打开动画 ──
        Console.WriteLine();
        Console.WriteLine("── 打开动画：遮罩淡出 ──");

        // 先记下"刚 Show 完那一刻"的状态：遮罩应当已经盖上，窗口本身是完全不透明的。
        window.Show();
        window.PlayShowAnimation();
        Dispatcher.UIThread.RunJobs();

        var veil = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Name == "IntroVeil");

        var veilAlpha = veil is null
            ? -1
            : (int)(((ISolidColorBrush)veil.Background!).Color.A);

        Check("开场动画用遮罩，而不是改窗口不透明度",
            veilAlpha > 200 && Math.Abs(window.Opacity - 1) < 1e-9,
            $"遮罩不透明度 {veilAlpha}，窗口不透明度 {window.Opacity:0.00}（窗口必须始终是 1.00）");

        Check("开场动画不缩放内容",
            Math.Abs(ReadContentScale(window) - 1) < 1e-9,
            $"缩放 {ReadContentScale(window):0.000}（缩放会把抓手坐标算偏，已删）");

        // 缓动与进度的断言 —— 纯函数，不依赖定时器，所以在无头里也能钉死。
        Check("缓动端点正确",
            Math.Abs(WindowAnimationMath.EaseOut(0)) < 1e-9
            && Math.Abs(WindowAnimationMath.EaseOut(1) - 1) < 1e-9
            && WindowAnimationMath.EaseOut(0.5) > 0.5,
            $"EaseOut(0)={WindowAnimationMath.EaseOut(0):0.000} " +
            $"EaseOut(0.5)={WindowAnimationMath.EaseOut(0.5):0.000} " +
            $"EaseOut(1)={WindowAnimationMath.EaseOut(1):0.000}（三点都该「先快后慢」）");

        Check("进度按真实时间算",
            Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.Zero)) < 1e-9
            && Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.FromMilliseconds(90)) - 0.5) < 1e-9
            && Math.Abs(WindowAnimationMath.ProgressOf(TimeSpan.FromMilliseconds(500)) - 1) < 1e-9,
            "0ms→0.00，90ms→0.50，500ms→1.00（180ms 就该结束，不会因为掉帧被拉长）");

        // 帧有没有按时到、以及"迟到一帧也不许闪一下"，都只能在真实窗口里看，
        // 交给 tools/probe-intro.ps1（它数的是日志里的遮罩 Alpha 序列）。
        Console.WriteLine("   [跳过] 帧是否按时到达 — 无头平台的合成器不驱动帧回调" +
                          "，改由 probe-intro.ps1 在真实窗口里验证");

        // 动画是靠合成器帧回调推的，无头里一帧都不来。先强制收尾 ——
        // 这既是"窗口要藏起来之前该做的事"，也顺便验证收尾真的能把视觉状态还原干净。
        window.FinishAnimationsImmediately();
        Dispatcher.UIThread.RunJobs();

        var veilHiddenAfterSettle = veil is null || !veil.IsVisible;

        Check("收尾能强制回到完全体",
            Math.Abs(window.Opacity - 1) < 1e-9
            && Math.Abs(ReadContentScale(window) - 1) < 1e-9
            && veilHiddenAfterSettle,
            $"不透明度 {window.Opacity:0.00}，缩放 {ReadContentScale(window):0.000}，" +
            $"遮罩{(veilHiddenAfterSettle ? "已收掉" : "还盖着")}");

        // ── ② 拖边改大小 ──
        Console.WriteLine();
        Console.WriteLine("── 拖边改大小 ──");

        var grip = window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Tag as string == "East");

        var widthBefore = window.Width;

        if (grip is null)
        {
            Check("找得到右边缘抓手", false, "视觉树里没有 Tag=East 的 Border");
        }
        else
        {
            var center = grip.TranslatePoint(
                new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), window);

            if (center is null)
            {
                Check("算得出抓手坐标", false, "TranslatePoint 返回 null");
            }
            else
            {
                // 往"里"拖：往外拖会让指针跑到窗口矩形之外，无头平台不一定还往这个窗口送事件。
                // 拖右边缘往里 120 → 窗口应该变窄 120 左右。
                window.MouseMove(center.Value);
                window.MouseDown(center.Value, MouseButton.Left);
                window.MouseMove(center.Value - new Point(120, 0));
                window.MouseUp(center.Value - new Point(120, 0), MouseButton.Left);
                Dispatcher.UIThread.RunJobs();

                var widthAfter = window.Width;
                Check("右边缘往里拖 120，窗口变窄", widthAfter < widthBefore - 100,
                    $"{widthBefore:0} → {widthAfter:0}");
            }
        }

        // 最大化 / 还原：现在只是切状态，过渡动画由 DWM 在合成器里播（WindowChrome 补了 WS_CAPTION）。
        // 无头平台没有 DWM，过程动画交给 tools/probe-native-anim.ps1 数可见像素面积。
        var threw = string.Empty;
        try
        {
            window.WindowState = WindowState.Maximized;
            PumpDispatcher(TimeSpan.FromMilliseconds(200));
            var maximized = window.WindowState;

            window.WindowState = WindowState.Normal;
            PumpDispatcher(TimeSpan.FromMilliseconds(200));

            if (maximized != WindowState.Maximized || window.WindowState != WindowState.Normal)
            {
                threw = $"{maximized} → {window.WindowState}";
            }
        }
        catch (Exception ex)
        {
            threw = ex.GetType().Name;
        }

        Check("最大化 / 还原切得动（不抛异常）", threw.Length == 0,
            threw.Length == 0 ? "切到 Maximized 再切回 Normal 都正常" : $"异常：{threw}");
        Console.WriteLine("   [跳过] 最大化 / 最小化的过程动画 — 由 DWM 播，改由 probe-native-anim.ps1 验证");'''

text = text[:start] + new + text[end:]

# 其余两处调用点：旧的自写动画入口已删除
text = text.replace("                window.ToggleMaximizeAnimated();", "                window.WindowState = WindowState.Maximized;")

if "ToggleMaximizeAnimated" in text:
    print("MISS: 仍有 ToggleMaximizeAnimated 残留")
    raise SystemExit(1)

io.open(path, "w", encoding="utf-8", newline="\r\n").write(text)
print("ok: RunWindowCheck rewritten")