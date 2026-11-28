using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>Los contadores por hora de los topes de mensajes en memoria, con los locks que se pidieron, en orden.</summary>
internal sealed class FakeMessageQuotaRepository : IMessageQuotaRepository
{
    public Dictionary<(MessageQuotaScope Scope, string Key, DateTime Hour), int> Counts { get; } = [];

    public List<IReadOnlyCollection<MessageQuotaKey>> Locks { get; } = [];

    public int Total(MessageQuotaScope scope) => Counts.Where(pair => pair.Key.Scope == scope).Sum(pair => pair.Value);

    public Task LockAsync(IReadOnlyCollection<MessageQuotaKey> keys, CancellationToken cancellationToken)
    {
        Locks.Add(keys);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageQuotaHour>> ListAsync(MessageQuotaKey key, DateTime fromHourUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MessageQuotaHour>>([.. Counts
            .Where(pair => pair.Key.Scope == key.Scope && pair.Key.Key == key.KeyHash && pair.Key.Hour >= fromHourUtc)
            .Select(pair => new MessageQuotaHour(pair.Key.Hour, pair.Value))]);

    public Task IncrementAsync(MessageQuotaKey key, DateTime hourUtc, int amount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Counts[(key.Scope, key.KeyHash, hourUtc)] = Counts.GetValueOrDefault((key.Scope, key.KeyHash, hourUtc)) + amount;
        return Task.CompletedTask;
    }
}

/// <summary>Una clave legible por alcance y valor: los tests ven qué contador sumó.</summary>
internal sealed class FakeMessageQuotaKeyHasher : IMessageQuotaKeyHasher
{
    public string ForAddress(string? ipAddress) => "ip:" + (ipAddress ?? "unknown");

    public string ForSubject(MessageQuotaScope scope, Guid id) => scope + ":" + id;
}
