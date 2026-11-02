using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace HeliumTodo.SelfTest;

/// <summary>
/// 极简测试运行器：用反射找出带 [Fact] 的方法，逐个调用。
///
/// 它不依赖 VSTest，所以不会遇到「测试宿主进程附加失败」的问题。
/// 退出码：0 = 全部通过，1 = 有失败。
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 默认扫描 HeliumTodo.Tests 这个程序集
        var assemblyName = args.Length > 0 ? args[0] : "HeliumTodo.Tests";

        Assembly assembly;
        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 加载程序集 {assemblyName} 失败：{ex.Message}");
            return 1;
        }

        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine($"  Helium Todo 自检 · 程序集 {assembly.FullName}");
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine();

        var testMethods = DiscoverTests(assembly);
        if (testMethods.Count == 0)
        {
            Console.WriteLine("[警告] 没有找到任何测试方法。");
            return 1;
        }

        var passed = 0;
        var failed = new List<(string Name, Exception Error)>();
        var stopwatch = Stopwatch.StartNew();

        // 按类名分组输出，看起来更清楚
        foreach (var group in testMethods.GroupBy(t => t.DeclaringType?.Name ?? "(未知类)"))
        {
            Console.WriteLine($"── {group.Key} ──");

            foreach (var method in group)
            {
                var displayName = method.Name.Replace('_', ' ');

                try
                {
                    InvokeTest(method);
                    passed++;
                    Console.WriteLine($"  [通过] {displayName}");
                }
                catch (Exception ex)
                {
                    // 反射调用会把真实异常包在 TargetInvocationException 里，要拆开
                    var real = ex is TargetInvocationException { InnerException: not null }
                        ? ex.InnerException
                        : ex;

                    failed.Add(($"{group.Key}.{method.Name}", real));
                    Console.WriteLine($"  [失败] {displayName}");
                    Console.WriteLine($"         {real.GetType().Name}: {real.Message}");
                }
            }

            Console.WriteLine();
        }

        stopwatch.Stop();

        // ── 汇总 ──
        Console.WriteLine("══════════════════════════════════════════════════════");
        Console.WriteLine($"  合计 {testMethods.Count} 个测试 · 通过 {passed} · 失败 {failed.Count}" +
                          $" · 用时 {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine("══════════════════════════════════════════════════════");

        if (failed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("失败详情：");
            foreach (var (name, error) in failed)
            {
                Console.WriteLine();
                Console.WriteLine($"▸ {name}");
                Console.WriteLine($"  {error.GetType().Name}: {error.Message}");
                if (!string.IsNullOrWhiteSpace(error.StackTrace))
                {
                    var firstLine = error.StackTrace!
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => l.Contains("HeliumTodo.Tests"));
                    if (firstLine is not null)
                    {
                        Console.WriteLine($"  位置：{firstLine.Trim()}");
                    }
                }
            }
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("全部通过 ✔");
        return 0;
    }

    /// <summary>找出手上所有带 [Fact] 的公开无参方法。</summary>
    private static List<MethodInfo> DiscoverTests(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<FactAttribute>() is not null)
            .Where(m => m.GetParameters().Length == 0)
            .OrderBy(m => m.DeclaringType?.Name)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>调用一个测试方法（每个测试都用新的实例，和 xunit 的行为一致）。</summary>
    private static void InvokeTest(MethodInfo method)
    {
        var instance = Activator.CreateInstance(method.DeclaringType!);
        try
        {
            var result = method.Invoke(instance, null);

            // ⚠️ 必须等异步测试跑完。
            //
            // 这里原来只是 method.Invoke(...) 就完事 —— 对 void 测试没问题，
            // 但对 `async Task` 的测试等于"发射后不管"：
            //   · 方法里 await 之后的断言根本没执行到，就已经算通过了
            //   · 异步过程里抛的异常被 Task 吞掉，永远不会变成失败
            // 联机那几个测试全是 async 的，等第一次 await 一到这里就直接返回，
            // 于是"全绿"其实是假的。抓这个问题的契机是加了个要等 4 秒心跳的
            // 测试：整套跑完只用了 159 ms，明显不可能。
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }
            else if (result is ValueTask valueTask)
            {
                valueTask.GetAwaiter().GetResult();
            }
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }
}
