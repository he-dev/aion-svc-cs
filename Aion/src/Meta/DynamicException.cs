using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Meta;

public abstract class DynamicException(Exception? inner, [StructuredMessageTemplate] string message, params object?[] args)
    : Exception(RenderMessage(message, args), inner)
{
    internal void Log(ILogger logger) => logger.LogError(this, message, args);

    /// <summary>
    /// Starts building a dynamic exception, appending 'Exception' to the name when missing.
    /// </summary>
    public static Builder Create(string name) => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private Exception? _inner;

        internal Builder(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _name = name.EndsWith(nameof(Exception), StringComparison.OrdinalIgnoreCase)
                ? name
                : $"{name}{nameof(Exception)}";
        }

        public Builder Inner(Exception? exception)
        {
            _inner = exception;
            return this;
        }

        /// <summary>
        /// Finishes building the exception with its message template and arguments.
        /// </summary>
        public DynamicException Message([StructuredMessageTemplate] string template, params object?[] args)
        {
            return Factory.Create(_name, _inner, template, args);
        }
    }

    private static string RenderMessage([StructuredMessageTemplate] string template, object?[] values)
    {
        if (!global::Serilog.Log.BindMessageTemplate(template, values, out var parsed, out var properties))
        {
            throw new ArgumentException($"Cannot bind message template: '{template}'");
        }

        return parsed.Render(properties.ToDictionary(p => p.Name, p => p.Value));
    }

    private static class Factory
    {
        private static readonly Type[] ConstructorParameterTypes = [typeof(Exception), typeof(string), typeof(object[])];

        private static readonly ConstructorInfo BaseConstructor =
            typeof(DynamicException)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, ConstructorParameterTypes, null)
            ?? throw new InvalidOperationException("The dynamic exception base constructor was not found.");

        private static readonly ConcurrentDictionary<string, Lazy<Type>> Cache = new(StringComparer.Ordinal);
        private static readonly Lock EmitLock = new();

        private static readonly Lazy<ModuleBuilder> Module = new
        (() =>
            AssemblyBuilder
                .DefineDynamicAssembly(new AssemblyName($"DynamicAssembly_{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("DynamicModule")
        );

        public static DynamicException Create(string name, Exception? inner, string message, object?[] args)
        {
            var dynamicExceptionType = GetExceptionType(name);
            return (DynamicException)Activator.CreateInstance(dynamicExceptionType, inner, message, args)!;
        }

        private static Type GetExceptionType(string name)
        {
            // GetOrAdd may create multiple Lazy instances, but only the cached instance emits a type.
            return Cache.GetOrAdd(name, static key => new Lazy<Type>(() => CreateExceptionType(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        private static Type CreateExceptionType(string name)
        {
            // Different names can reach this method concurrently; they share one module.
            lock (EmitLock)
            {
                var typeBuilder = Module.Value.DefineType(name, TypeAttributes.Public, typeof(DynamicException));
                var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, ConstructorParameterTypes);

                var ilGenerator = constructor.GetILGenerator();
                ilGenerator.Emit(OpCodes.Ldarg_0); // this
                ilGenerator.Emit(OpCodes.Ldarg_1); // inner
                ilGenerator.Emit(OpCodes.Ldarg_2); // message
                ilGenerator.Emit(OpCodes.Ldarg_3); // args
                ilGenerator.Emit(OpCodes.Call, BaseConstructor);
                ilGenerator.Emit(OpCodes.Ret);

                return typeBuilder.CreateType()!;
            }
        }

    }
}

public static class DynamicExceptionExtensions
{
    public static void LogError(this ILogger logger, DynamicException exception) => exception.Log(logger);
}
