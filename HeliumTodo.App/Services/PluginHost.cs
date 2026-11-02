using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using HeliumTodo.Core.Configuration;
using HeliumTodo.Core.Models;
using HeliumTodo.Core.Plugins;
using HeliumTodo.Core.Services;
using HeliumTodo.Core.Theming;
using Microsoft.Extensions.Logging;

namespace HeliumTodo.App.Services;

/// <summary>
/// 插件宿主：负责加载插件，并把主程序的能力交给它们。
///
/// 【扩展点 2：插件】的工作流程：
///   1. 程序启动 → <see cref="LoadPlugins"/> 扫描 plugins 文件夹
///   2. 每个 dll 里找到实现了 <see cref="IHeliumPlugin"/> 的类
///   3. 创建实例并调用它的 Initialize，把 <see cref="IHeliumHostContext"/> 传进去
///   4. 插件用这个 context 注册主题、注册页面、挂事件钩子
///
/// 注意：单个插件出错不会影响主程序 —— 每个插件都单独 try/catch，
/// 出错只记日志并跳过，程序照常启动。
/// </summary>
public sealed class PluginHost : IHeliumHostContext
{
    private readonly ConfigService _configService;
    private readonly ILogger<PluginHost> _logger;
    private readonly List<IHeliumPlugin> _plugins = new();

    /// <summary>插件注册的页面。</summary>
    public List<RegisteredPage> RegisteredPages { get; } = new();

    /// <summary>已成功加载的插件（设置页会显示这个列表）。</summary>
    public IReadOnlyList<PluginInfo> LoadedPlugins { get; private set; } = Array.Empty<PluginInfo>();

    /// <summary>plugins 文件夹的完整路径。</summary>
    public string PluginDirectory { get; }

    // ── IHeliumHostContext 的实现 ──

    public string AppVersion { get; }

    public ThemeRegistry Themes { get; }

    public event EventHandler<TodoItem>? TodoCreated;
    public event EventHandler<TodoItem>? TodoCompleted;
    public event EventHandler<FocusSession>? FocusCompleted;
    public event EventHandler<DayTimeline>? TimelineRebuilt;

    public PluginHost(
        ThemeRegistry themes,
        ConfigService configService,
        ILogger<PluginHost> logger,
        string appVersion)
    {
        Themes = themes;
        _configService = configService;
        _logger = logger;
        AppVersion = appVersion;

        PluginDirectory = Path.Combine(configService.ConfigDirectory, "plugins");
    }

    // ─────────────────────────────────────────────────────────
    // 加载插件
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 扫描 plugins 文件夹并加载所有插件。
    /// 返回加载结果（成功几个、失败几个）。
    /// </summary>
    public (int Loaded, int Failed) LoadPlugins()
    {
        var loaded = 0;
        var failed = 0;
        var infos = new List<PluginInfo>();

        try
        {
            // 文件夹不存在就建一个空的，并在里面放一份说明，方便用户知道该往哪放插件
            if (!Directory.Exists(PluginDirectory))
            {
                Directory.CreateDirectory(PluginDirectory);
                WriteReadme();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法创建插件目录 {Directory}", PluginDirectory);
            return (0, 0);
        }

        // 插件 dll 按文件名排序加载，保证每次启动顺序一致
        var dllFiles = Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var count = LoadFromAssembly(dllPath, infos, ref failed);
                loaded += count;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "加载插件文件失败：{File}", Path.GetFileName(dllPath));
                infos.Add(new PluginInfo(
                    Id: Path.GetFileNameWithoutExtension(dllPath),
                    Name: Path.GetFileName(dllPath),
                    Version: "-",
                    Author: string.Empty,
                    Description: $"加载失败：{ex.Message}",
                    IsLoaded: false));
            }
        }

        LoadedPlugins = infos;
        return (loaded, failed);
    }

    /// <summary>从一个 dll 里找出所有插件并初始化。</summary>
    private int LoadFromAssembly(string dllPath, List<PluginInfo> infos, ref int failed)
    {
        // 用独立的 AssemblyLoadContext 加载，这样插件之间、插件和主程序之间的依赖不会互相干扰
        var context = new PluginLoadContext(dllPath);
        var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dllPath));

        // 找出所有「实现了 IHeliumPlugin 且不是抽象类」的类型
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IHeliumPlugin).IsAssignableFrom(t)
                        && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        if (pluginTypes.Count == 0)
        {
            // 这个 dll 里没有插件，不算失败（可能是插件的依赖库）
            return 0;
        }

        var count = 0;
        foreach (var type in pluginTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is not IHeliumPlugin plugin)
                {
                    continue;
                }

                // 防止同一个插件被加载两次
                if (_plugins.Any(p => p.Id == plugin.Id))
                {
                    _logger.LogInformation("插件 {Id} 已经加载过，跳过", plugin.Id);
                    continue;
                }

                plugin.Initialize(this);
                _plugins.Add(plugin);
                count++;

                infos.Add(new PluginInfo(
                    plugin.Id, plugin.Name, plugin.Version,
                    plugin.Author, plugin.Description, IsLoaded: true));

                _logger.LogInformation("已加载插件 {Name} ({Id}) v{Version}",
                    plugin.Name, plugin.Id, plugin.Version);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "初始化插件 {Type} 失败", type.FullName);

                infos.Add(new PluginInfo(
                    type.FullName ?? type.Name,
                    type.Name,
                    "-",
                    string.Empty,
                    $"初始化失败：{ex.Message}",
                    IsLoaded: false));
            }
        }

        return count;
    }

    /// <summary>程序退出时通知所有插件。</summary>
    public void ShutdownAll()
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                plugin.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭插件 {Id} 时出错", plugin.Id);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 提供给插件的能力
    // ─────────────────────────────────────────────────────────

    public T GetConfig<T>(string key, T defaultValue)
    {
        var sectionKey = $"plugin:{key}";
        var json = _configService.Current.PluginSettings.GetValueOrDefault(sectionKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取插件配置 {Key} 失败，使用默认值", key);
            return defaultValue;
        }
    }

    public void SetConfig<T>(string key, T value)
    {
        var sectionKey = $"plugin:{key}";

        try
        {
            _configService.Current.PluginSettings[sectionKey] = JsonSerializer.Serialize(value);
            _configService.Save();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存插件配置 {Key} 失败", key);
        }
    }

    public void Log(string message) => _logger.LogInformation("[插件] {Message}", message);

    public void RegisterPage(string icon, string title, Func<object> pageFactory)
    {
        if (string.IsNullOrWhiteSpace(title) || pageFactory is null) return;

        RegisteredPages.Add(new RegisteredPage(icon, title, _ => pageFactory()));
    }
    // ── 给业务代码调用的事件触发方法 ──

    /// <summary>通知插件「待办被创建了」。</summary>
    public void RaiseTodoCreated(TodoItem item)
    {
        SafeRaise(() => TodoCreated?.Invoke(this, item), nameof(TodoCreated));
    }

    /// <summary>通知插件「待办完成了」。</summary>
    public void RaiseTodoCompleted(TodoItem item)
    {
        SafeRaise(() => TodoCompleted?.Invoke(this, item), nameof(TodoCompleted));
    }

    /// <summary>通知插件「一次专注结束了」。</summary>
    public void RaiseFocusCompleted(FocusSession session)
    {
        SafeRaise(() => FocusCompleted?.Invoke(this, session), nameof(FocusCompleted));
    }

    /// <summary>通知插件「今天的时间轴重算完了」。</summary>
    public void RaiseTimelineRebuilt(DayTimeline timeline)
    {
        SafeRaise(() => TimelineRebuilt?.Invoke(this, timeline), nameof(TimelineRebuilt));
    }

    /// <summary>触发插件事件时兜底，避免插件抛异常影响主流程。</summary>
    private void SafeRaise(Action action, string eventName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "插件事件 {Event} 处理时出错", eventName);
        }
    }

    /// <summary>在插件目录里放一份说明文件，告诉用户怎么用。</summary>
    private void WriteReadme()
    {
        const string readme = """
            Helium Todo · 插件目录
            ═══════════════════════════════════════

            怎么用？
              把编译好的插件 dll 直接放进这个文件夹，重启程序就会自动加载。

            怎么写一个插件？
              1. 新建一个「类库」项目，目标框架选 net8.0
              2. 引用 HeliumTodo.Core.dll
              3. 写一个类实现 IHeliumPlugin 接口，例如：

                 public sealed class MyPlugin : IHeliumPlugin
                 {
                     public string Id => "com.example.myplugin";
                     public string Name => "我的插件";
                     public string Version => "1.0.0";

                     public void Initialize(IHeliumHostContext host)
                     {
                         host.Log("我的插件启动了！");

                         // 加一套自定义主题
                         // host.Themes.Register(new MyTheme());

                         // 在左边导航栏加一个页面
                         // host.RegisterPage("★", "我的页面", () => new MyPageViewModel());
                     }
                 }

              4. 编译后把 dll 复制到这里

            提示：
              · 插件出错不会影响主程序，只会在「设置 → 插件」里显示为加载失败
              · 同一个 Id 的插件只会加载一次
            """;

        try
        {
            File.WriteAllText(Path.Combine(PluginDirectory, "README.txt"), readme);
        }
        catch
        {
            // 写说明失败无所谓，不影响功能
        }
    }
}

/// <summary>一个已注册的页面。</summary>
public sealed record RegisteredPage(string Icon, string Title, Func<IServiceProvider, object> CreatePage);

/// <summary>插件信息（设置页展示用）。</summary>
public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    bool IsLoaded);

/// <summary>
/// 插件的程序集加载上下文。
///
/// 它的作用：让插件用自己文件夹里的依赖库，而不是被迫用主程序的那一份。
/// 如果插件依赖的某个库主程序里也有（版本可能不同），
/// 这里会优先用主程序的那一份，避免类型冲突。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// 解析插件需要的程序集。
    /// 这是 AssemblyLoadContext 提供的扩展点：返回 null 表示「我这里没有，交给默认上下文去找」。
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 主程序已经加载过的程序集，直接复用（避免同一个类型出现两份）
        var shared = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (shared is not null)
        {
            return shared;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
