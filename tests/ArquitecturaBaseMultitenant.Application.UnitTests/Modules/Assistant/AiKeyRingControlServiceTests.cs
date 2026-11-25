using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

/// <summary>0064 §11 (B15): la primera copia escribe un control por clave y las demás lo recalculan; la que no coincide no escribe.</summary>
public sealed class AiKeyRingControlServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid First = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000009");

    [Fact]
    public async Task The_first_copy_writes_a_control_per_key_and_a_copy_with_the_same_ring_matches_without_writing()
    {
        var repository = new Repository();
        Assert.Empty(await CheckAsync(repository, new Ring("a", [First, Second])));
        Assert.Equal([First, Second], repository.Rows.Select(row => row.KeyId).Order());
        Assert.Empty(await CheckAsync(repository, new Ring("a", [First, Second])));
        Assert.Equal(2, repository.Rows.Count);
    }

    [Fact]
    public async Task Another_ring_under_the_same_key_id_does_not_match_and_writes_nothing()
    {
        var repository = new Repository();
        await CheckAsync(repository, new Ring("a", [First]));
        var before = repository.Rows.Single().Control;
        Assert.Equal([First], await CheckAsync(repository, new Ring("b", [First, Second])));
        Assert.Equal(before, Assert.Single(repository.Rows).Control);
    }

    [Fact]
    public async Task A_ring_of_other_keys_does_not_match_once_there_are_controls()
    {
        var repository = new Repository();
        await CheckAsync(repository, new Ring("a", [First]));
        Assert.Equal([Other], await CheckAsync(repository, new Ring("a", [Other])));
        Assert.Single(repository.Rows);
    }

    [Fact]
    public async Task A_key_with_a_control_that_this_copy_cannot_open_does_not_match()
    {
        var repository = new Repository();
        await CheckAsync(repository, new Ring("a", [First]));
        Assert.Equal([First, Other], await CheckAsync(repository, new Ring("a", [Other], unreadable: [First])));
        Assert.Single(repository.Rows);
    }

    [Fact]
    public async Task A_rotated_key_gets_its_control_and_an_unreadable_key_without_one_is_not_this_ring()
    {
        var repository = new Repository();
        await CheckAsync(repository, new Ring("a", [First]));
        Assert.Empty(await CheckAsync(repository, new Ring("a", [First, Second], unreadable: [Other])));
        Assert.Equal([First, Second], repository.Rows.Select(row => row.KeyId).Order());
    }

    private static async Task<IReadOnlyList<Guid>> CheckAsync(Repository repository, Ring ring)
    {
        var service = new AiKeyRingControlService(repository, ring, new FakeUnitOfWork(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-11-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            NullLogger<AiKeyRingControlService>.Instance);
        var result = await service.CheckAsync(Ct);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class Repository : IAiKeyRingControlRepository
    {
        public List<AiKeyRingControl> Rows { get; } = [];
        public int Locks { get; private set; }
        public Task LockAsync(CancellationToken ct) { Locks++; return Task.CompletedTask; }
        public Task<IReadOnlyList<AiKeyRingControl>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AiKeyRingControl>>([.. Rows]);
        public void Add(AiKeyRingControl control) => Rows.Add(control);
    }

    /// <summary>Un anillo de prueba: el control de una clave depende de su id y de la instalación.</summary>
    private sealed class Ring(string installation, Guid[] readable, Guid[]? unreadable = null) : IAiKeyRingControl
    {
        public AiKeyRingKeys ReadKeys() => new(readable, unreadable ?? []);
        public byte[] CreateControl(Guid keyId) => System.Text.Encoding.UTF8.GetBytes(installation + ":" + keyId.ToString("N"));
        public bool Matches(Guid keyId, byte[] control) => CreateControl(keyId).AsSpan().SequenceEqual(control);
    }
}
