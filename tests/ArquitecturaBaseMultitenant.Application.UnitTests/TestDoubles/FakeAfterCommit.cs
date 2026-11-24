using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>Lo que se deja para después del commit, en memoria: corre con <see cref="Commit"/> y se descarta con <see cref="Rollback"/>.</summary>
internal sealed class FakeAfterCommit : IAfterCommit
{
    private readonly List<Action> _pending = [];

    public int PendingCount => _pending.Count;

    public void Add(Action action) => _pending.Add(action);

    public void Commit()
    {
        Action[] actions = [.. _pending];
        _pending.Clear();
        foreach (var action in actions) action();
    }

    public void Rollback() => _pending.Clear();
}
