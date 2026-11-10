using System;
using System.Threading.Tasks;

namespace Aion.Util.Dotnet;

public static class ExtensionChaining
{
    public static async Task<TResult> Let<T, TResult>(this Task<T> task, Func<T, TResult> block)
    {
        var value = await task;
        return block(value);
    }

    public static async Task<TResult> Let<T, TResult>(this T value, Func<T, Task<TResult>> block)
    {
        return await block(value);
    }

    public static async Task<TResult> Let<T, TResult>(this Task<T> task, Func<T, Task<TResult>> block)
    {
        var value = await task;
        return await block(value);
    }

    public static async Task<T> Also<T>(this Task<T> task, Action<T> block)
    {
        var value = await task;
        block(value);
        return value;
    }

    public static async Task<T> Also<T>(this Task<T> task, Func<T, Task> block)
    {
        var value = await task;
        await block(value);
        return value;
    }

    public static T Also<T>(this T value, Action<T> block)
    {
        block(value);
        return value;
    }
}