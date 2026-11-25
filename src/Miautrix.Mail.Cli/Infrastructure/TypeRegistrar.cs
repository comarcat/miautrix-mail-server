using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System;

namespace Miautrix.Mail.Cli.Infrastructure;

public sealed class TypeRegistrar : ITypeRegistrar, IDisposable
{
    private readonly IServiceCollection _builder;
    private readonly List<TypeResolver> _resolvers = [];

    public TypeRegistrar(IServiceCollection builder)
    {
        _builder = builder;
    }

    public ITypeResolver Build()
    {
        var resolver = new TypeResolver(_builder.BuildServiceProvider());
        _resolvers.Add(resolver);
        return resolver;
    }

    public void Register(Type service, Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _builder.AddSingleton(service, (provider) => factory());
    }

    public void Dispose()
    {
        foreach (var resolver in _resolvers)
        {
            resolver.Dispose();
        }
        _resolvers.Clear();
    }
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;
    private bool _disposed;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
