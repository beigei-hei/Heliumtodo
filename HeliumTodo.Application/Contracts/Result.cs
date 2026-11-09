// SPDX-FileCopyrightText: 2025 Helium Todo contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace HeliumTodo.Application.Contracts;

/// <summary>
/// 用例层统一返回值：要么成功带回数据，要么失败带回一句能给用户看的原因。
/// 故意不用异常表达"业务上的失败"（例如"这个时段冲突"），异常只留给真正的意外。
/// </summary>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    /// <summary>成功时的值；失败时为 <c>default</c>，调用方必须先看 <see cref="IsSuccess"/>。</summary>
    public T? Value { get; }

    /// <summary>失败原因；成功时为 <c>null</c>。</summary>
    public string? Error { get; }

    public static Result<T> Ok(T value) => new(true, value, null);

    public static Result<T> Fail(string error) => new(false, default, error);
}

/// <summary>没有返回值的用例结果。</summary>
public readonly struct Result
{
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public string? Error { get; }

    public static Result Ok() => new(true, null);

    public static Result Fail(string error) => new(false, error);
}