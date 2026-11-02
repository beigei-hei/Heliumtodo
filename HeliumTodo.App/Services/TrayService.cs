using System.Runtime.InteropServices;

namespace HeliumTodo.App.Services;

/// <summary>
/// 托盘图标 + 右键菜单 + 气泡通知（Windows 上会进「通知中心」）。
///
/// 为什么不用 WinForms 的 <c>NotifyIcon</c>：
///   那条路要求把目标框架改成 <c>net10.0-windows</c>（WinForms 只支持 -windows TFM），
///   而本项目是 <c>net10.0</c> 且被无头渲染工具引用着，改 TFM 会波及一圈。
///   于是直接用 <c>Shell_NotifyIcon</c> —— 反正 WinForms 底层也是它。
///
/// 为什么自己开一个线程跑消息循环：
///   托盘图标要一个窗口来收回调消息（左键、右键）。
///   Avalonia 的窗口有自己的消息循环，往里挂回调要改主窗口的 WndProc；
///   独立线程 + 独立隐藏窗口更干净，也不会跟界面线程抢消息。
/// </summary>
public sealed class TrayService : IDisposable
{
    // ── Win32 常量 ──
    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 1;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0x0000;
    private const int NIM_MODIFY = 0x0001;
    private const int NIM_DELETE = 0x0002;
    private const int NIF_MESSAGE = 0x0001;
    private const int NIF_ICON = 0x0002;
    private const int NIF_TIP = 0x0004;
    private const int NIF_INFO = 0x0010;
    private const int NIIF_INFO = 0x0001;
    private const int NIIF_NOSOUND = 0x0010;

    private const uint MF_STRING = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const int WM_NULL = 0x0000;

    private const int ID_SHOW = 1;
    private const int ID_BACKUP = 2;
    private const int ID_EXIT = 3;

    private const int ICON_SIZE = 32;

    private readonly string _tooltip;
    private readonly Action _onShow;
    private readonly Action _onBackup;
    private readonly Action _onExit;

    /// <summary>诊断日志（托盘起不来时唯一的线索来源 —— WinExe 没有控制台）。</summary>
    public Action<string>? Trace;

    private Thread? _thread;
    private IntPtr _window;
    private IntPtr _icon;
    private WndProcDelegate? _wndProc;   // 必须留住引用，否则被 GC 回收后回调直接崩
    private uint _taskbarCreatedMessage;

    /// <summary>
    /// 托盘线程"已经尝试过添加图标"的信号。
    /// Start() 靠它等待结果，避免出现"线程还没跑、主线程已经下结论"的竞态。
    /// </summary>
    private readonly ManualResetEventSlim _ready = new(false);

    public TrayService(string tooltip, Action onShow, Action onBackup, Action onExit)
    {
        _tooltip = tooltip;
        _onShow = onShow;
        _onBackup = onBackup;
        _onExit = onExit;
    }

    /// <summary>托盘图标真的加上了。</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// 启动，并等待"图标到底加上没有"这个结果。
    ///
    /// 为什么要等：<see cref="IsRunning"/> 是在托盘线程里 NIM_ADD 成功之后才置 true 的。
    /// 早先这里启动线程后立刻 return IsRunning —— 主线程几乎总是先跑到，
    /// 拿到的永远是 false，于是调用方（App）以为托盘不可用、不装"关窗口=藏起来"的拦截，
    /// 结果就是用户点 × 时进程真的退出了。这正是"后台进程留不住"的直接原因。
    ///
    /// 等待上限 5 秒：正常情况几毫秒就返回；真机上资源管理器刚起来时会慢一些，
    /// 里面还有重试。失败也不抛异常 —— 托盘起不来不该拦住主界面。
    /// </summary>
    public bool Start()
    {
        if (_thread is not null) return IsRunning;

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "helium-tray",
        };
        if (OperatingSystem.IsWindows())
        {
            _thread.SetApartmentState(ApartmentState.STA);
        }
        _thread.Start();

        // 等托盘线程报告"加好了"或"加不上"，别抢在它前面下结论
        _ready.Wait(TimeSpan.FromSeconds(5));
        return IsRunning;
    }

    /// <summary>弹一条气泡通知（Windows 10/11 上会进通知中心）。</summary>
    public void ShowBalloon(string title, string message)
    {
        if (_window == IntPtr.Zero || _icon == IntPtr.Zero) return;

        var data = CreateData(showInfo: true);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(message, 255);
        data.dwInfoFlags = NIIF_INFO | NIIF_NOSOUND;
        data.uTimeoutOrVersion = 10000;
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_window != IntPtr.Zero)
        {
            var data = CreateData(showInfo: false);
            Shell_NotifyIconW(NIM_DELETE, ref data);
            PostMessageW(_window, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            _window = IntPtr.Zero;
        }

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        IsRunning = false;
    }

    // ───────────────────────── 内部 ─────────────────────────

    private void RunMessageLoop()
    {
        _wndProc = WindowProc;

        var windowClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = "HeliumTodoTrayWindow",
        };
        RegisterClassExW(ref windowClass);

        // 隐藏窗口，只用来收托盘回调
        _window = CreateWindowExW(0, "HeliumTodoTrayWindow", "HeliumTodo", 0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_window == IntPtr.Zero)
        {
            Trace?.Invoke($"托盘窗口创建失败，错误码 {Marshal.GetLastWin32Error()}");
            _ready.Set();
            return;
        }

        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        _icon = CreateBrandIcon();
        if (_icon == IntPtr.Zero) _icon = LoadIconW(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION 兜底

        // 加图标。加不上会重试几次 —— 最常见的失败原因是"程序开机自启，
        // 资源管理器还没把通知区域准备好"，此时 NIM_ADD 返回错误码 5（拒绝访问）。
        // 等一两秒再试通常就好了；等太久会让 Start() 的调用方干等，所以总时长控制在 3 秒内。
        var added = false;
        var lastError = 0;
        var attempts = 0;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            attempts = attempt;
            var data = CreateData(showInfo: false);
            added = Shell_NotifyIconW(NIM_ADD, ref data);
            lastError = Marshal.GetLastWin32Error();
            if (added) break;

            // 第 n 次失败等 n*300ms：300 / 600 / 900 / 1200
            if (attempt < 5) Thread.Sleep(300 * attempt);
        }

        IsRunning = added;

        // 这几行是"托盘看不见"时唯一的线索：图标句柄是不是 0、加没加上、系统错误码是多少。
        Trace?.Invoke($"托盘图标：window=0x{_window.ToInt64():X} icon=0x{_icon.ToInt64():X} "
                      + $"add={added} err={lastError} attempts={attempts} "
                      + $"cbSize={Marshal.SizeOf<NOTIFYICONDATA>()}");

        if (!added)
        {
            Trace?.Invoke($"托盘图标加不上（错误码 {lastError}）：{DescribeShellError(lastError)}");
        }

        // 无论成败都要放行 Start()，否则它得干等到超时
        _ready.Set();

        while (GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private NOTIFYICONDATA CreateData(bool showInfo) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | (showInfo ? NIF_INFO : 0),
        uCallbackMessage = WM_TRAYICON,
        hIcon = _icon,
        szTip = Truncate(_tooltip, 127),
    };

    private IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        // 资源管理器重启之后，托盘图标会消失，系统会广播 TaskbarCreated 让我们自己加回来
        if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            var readd = CreateData(showInfo: false);
            Shell_NotifyIconW(NIM_ADD, ref readd);
            return IntPtr.Zero;
        }

        if (message == WM_TRAYICON)
        {
            switch ((int)(long)lParam)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    _onShow();
                    return IntPtr.Zero;

                case WM_RBUTTONUP:
                    ShowContextMenu(hWnd);
                    return IntPtr.Zero;
            }
        }

        return DefWindowProcW(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd)
    {
        if (!GetCursorPos(out var point)) return;

        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            AppendMenuW(menu, MF_STRING, ID_SHOW, "显示主界面");
            AppendMenuW(menu, MF_STRING, ID_BACKUP, "立即备份一次");
            AppendMenuW(menu, 0x0800 /* MF_SEPARATOR */, 0, string.Empty);
            AppendMenuW(menu, MF_STRING, ID_EXIT, "退出 Helium Todo");

            // 经典坑：不先 SetForegroundWindow，菜单点空白处不会消失
            SetForegroundWindow(hWnd);

            var command = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD,
                point.X, point.Y, 0, hWnd, IntPtr.Zero);

            PostMessageW(hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case ID_SHOW: _onShow(); break;
                case ID_BACKUP: _onBackup(); break;
                case ID_EXIT: _onExit(); break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    /// <summary>
    /// 画一个 32×32 的牌子当图标：强调色圆角方块 + 白色 "H"。
    ///
    /// 为什么不带 .ico 资源文件：整个项目还没有图标资源，
    /// 为了一个托盘图标加一个二进制资源不划算 —— 反正形状很简单，直接填像素。
    /// </summary>
    private static IntPtr CreateBrandIcon()
    {
        const int size = ICON_SIZE;
        var pixels = new uint[size * size];

        // 强调色 #0F6CBD（ABGR 顺序：内存里是 BGRA）
        const uint accent = 0xFFBD6C0F;
        const uint white = 0xFFFFFFFF;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // 圆角：四角 6 像素半径的地方留空（alpha = 0）
                var inside = IsInsideRoundedSquare(x, y, size, 6);
                pixels[y * size + x] = inside ? accent : 0u;
            }
        }

        // 白色 "H"：两根竖笔 + 一根横笔
        for (var y = 9; y < 23; y++)
        {
            SetPixel(pixels, 11, y, white);
            SetPixel(pixels, 12, y, white);
            SetPixel(pixels, 19, y, white);
            SetPixel(pixels, 20, y, white);
        }
        for (var x = 13; x < 19; x++)
        {
            SetPixel(pixels, x, 15, white);
            SetPixel(pixels, x, 16, white);
        }

        var bitmap = CreateDib(pixels, size, out var bits);
        if (bitmap == IntPtr.Zero) return IntPtr.Zero;

        // 剪裁掩码全 0（= 全部不透明），32bpp 图标靠自己的 alpha
        var mask = CreateBitmap(size, size, 1, 1, IntPtr.Zero);

        var info = new ICONINFO
        {
            fIcon = true,
            hbmMask = mask,
            hbmColor = bitmap,
        };

        var icon = CreateIconIndirect(ref info);
        DeleteObject(bitmap);
        DeleteObject(mask);
        _ = bits;
        return icon;
    }

    private static void SetPixel(uint[] pixels, int x, int y, uint color)
    {
        if (x < 0 || y < 0 || x >= ICON_SIZE || y >= ICON_SIZE) return;
        pixels[y * ICON_SIZE + x] = color;
    }

    private static bool IsInsideRoundedSquare(int x, int y, int size, int radius)
    {
        var cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
        var cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static IntPtr CreateDib(uint[] pixels, int size, out IntPtr bits)
    {
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = size,
                biHeight = -size,           // 负数 = 自上而下，和我们的数组顺序一致
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,          // BI_RGB
            },
        };

        var bitmap = NativeCreateDibSection(IntPtr.Zero, ref header, 0, out bits, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero) return IntPtr.Zero;

        Marshal.Copy(pixels.Select(p => (int)p).ToArray(), 0, bits, pixels.Length);
        return bitmap;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max];

    // ───────────────────────── P/Invoke ─────────────────────────

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    /// <summary>
    /// 把 `Shell_NotifyIconW` 的错误码翻译成能照着处理的话。
    /// 这个函数只在失败路径上调用，不追求覆盖所有错误码。
    /// </summary>
    private static string DescribeShellError(int error) => error switch
    {
        0 => "系统没给出错误码（多半是这次调用根本没到 shell）。",
        5 => "拒绝访问：当前会话没有可用的通知区域（远程桌面 / 无交互会话常见），"
             + "或者程序以管理员身份运行而资源管理器不是。",
        87 => "参数不对：结构体大小和系统期望的不一致。",
        1400 => "窗口句柄无效。",
        _ => "请把这一行连同错误码一起反馈。",
    };

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(int message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEX windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName,
        int style, int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG message, IntPtr hWnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, int id, string text);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y,
        int reserved, IntPtr hWnd, IntPtr rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO info);

    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
    private static extern IntPtr NativeCreateDibSection(IntPtr device, ref BITMAPINFO info, uint usage,
        out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, IntPtr bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}