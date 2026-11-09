// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeliumTodo.Application.Ipc;

/// <summary>
/// 进程间通信的请求。
/// </summary>
public sealed record IpcRequest
{
    /// <summary>要做什么。取值见 <see cref="IpcActions"/>，也可以用自定义字符串。</summary>
    public required string Action { get; init; }

    /// <summary>参数。自由结构的 JSON，由具体 Action 自己解释。</summary>
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// 进程间通信的应答。
/// </summary>
public sealed record IpcResponse
{
    /// <summary>成功与否。</summary>
    public required bool Ok { get; init; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? Error { get; init; }

    /// <summary>返回的数据。</summary>
    public JsonElement? Data { get; init; }

    public static IpcResponse Success(JsonElement? data = null) => new() { Ok = true, Data = data };

    public static IpcResponse Failure(string error) => new() { Ok = false, Error = error };
}

/// <summary>
/// 已经定下来的动作名。
///
/// 用常量而不是枚举：将来加动作不用改协议版本、也不用担心值改了别人解析错。
/// 命名用「对象.动词」，一眼能看出是干什么的。
/// </summary>
public static class IpcActions
{
    /// <summary>握手 / 探活。</summary>
    public const string Ping = "system.ping";

    /// <summary>取应用版本与数据规模。</summary>
    public const string Describe = "system.describe";

    /// <summary>按日期范围读日程。Payload: <c>{ "date": "2026-10-31" }</c></summary>
    public const string GetSchedules = "schedule.query";

    /// <summary>按状态读待办。Payload: <c>{ "includeCompleted": true }</c></summary>
    public const string GetTodos = "todo.query";

    /// <summary>新建一条待办。Payload: <c>{ "title": "...", "dueAt": "..." }</c></summary>
    public const string AddTodo = "todo.create";

    /// <summary>读专注记录。Payload: <c>{ "days": 7 }</c></summary>
    public const string GetFocusSessions = "focus.query";
}

/// <summary>
/// 协议里 JSON 的读法：统一一套选项，避免两边字段大小写对不上。
/// </summary>
public static class IpcJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}