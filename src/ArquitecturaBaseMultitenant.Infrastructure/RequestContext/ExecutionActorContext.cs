using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.RequestContext;

/// <summary>Actor ambiental por flujo async. La entrada técnica sólo admite callers de background confiables.</summary>
internal sealed class ExecutionActorContext(Func<bool>? hasHttpRequest = null,
    Func<bool>? hasBoundTenant = null, Func<bool>? hasActiveTransaction = null)
    : IExecutionActorContext, IExecutionActorScope, IDisposable
{
    private readonly AsyncLocal<Scope?> _current = new();
    private volatile bool _disposed;

    public ExecutionActor? Actor => !_disposed && hasHttpRequest?.Invoke() != true
        && _current.Value is { IsActive: true } scope ? scope.Actor : null;

    internal void EnsureCanEnter()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (hasHttpRequest?.Invoke() == true)
            throw new InvalidOperationException("An HTTP request cannot replace its signed identity with an execution actor.");
        if (hasBoundTenant?.Invoke() == true)
            throw new InvalidOperationException("An authenticated or technical tenant scope is already active.");
        if (hasActiveTransaction?.Invoke() == true)
            throw new InvalidOperationException("The execution actor must be bound before opening a transaction.");
    }

    public IDisposable Enter(ExecutionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureCanEnter();
        if (actor.OwnerUserId == Guid.Empty || !Enum.IsDefined(actor.Access)
            || string.IsNullOrWhiteSpace(actor.Culture) || string.IsNullOrWhiteSpace(actor.TimeZoneId)
            || actor.SessionStartedAtUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("A complete server execution actor is required.", nameof(actor));
        var validBinding = actor.Access switch
        {
            Access.Consumer => actor.TenantId is { } personal && personal != Guid.Empty && actor.TenantKind == TenantKind.Personal,
            Access.Business => actor.TenantId is { } business && business != Guid.Empty && actor.TenantKind == TenantKind.Business,
            Access.Platform => actor.TenantId is null && actor.TenantKind is null,
            _ => false,
        };
        if (!validBinding) throw new ArgumentException("The actor has an incompatible access binding.", nameof(actor));
        var scope = new Scope(this, actor, _current.Value);
        _current.Value = scope;
        return scope;
    }

    public void Dispose()
    {
        _disposed = true;
        _current.Value = null;
    }

    private sealed class Scope(ExecutionActorContext context, ExecutionActor actor, Scope? parent) : IDisposable
    {
        private volatile bool _disposed;
        internal ExecutionActor Actor => actor;
        internal bool IsActive => !_disposed && (parent?.IsActive ?? true);

        public void Dispose()
        {
            if (_disposed) return;
            if (context._disposed) { _disposed = true; return; }
            if (!ReferenceEquals(context._current.Value, this))
                throw new InvalidOperationException("Execution actor scopes must be disposed in reverse order.");
            _disposed = true;
            context._current.Value = parent;
        }
    }
}
