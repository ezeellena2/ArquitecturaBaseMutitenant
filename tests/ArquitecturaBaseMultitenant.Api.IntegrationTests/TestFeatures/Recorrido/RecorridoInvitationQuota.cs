using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;

/// <summary>
/// Orden privada <c>invitation-quota</c> del recorrido (B-3 de la puerta de la base 2): «invitar hasta el tope, con la espera
/// en horas» sin mandar un millón de correos. El entorno corre con los topes altos (<c>TestMessageQuotas</c>) y la app no
/// cambia: <c>fill</c> suma en el contador de quien invita (ADR 0063 y 256) lo que le falta para quedar a <c>remaining</c>
/// invitaciones por correo de su tope, en la hora anterior a la actual, que sale de la ventana en unas 23 horas; <c>release</c>
/// resta exactamente lo que sumó, así las invitaciones que siguen en el recorrido no chocan con el tope. Los demás contadores
/// (la organización, el global) no se tocan.
/// </summary>
public sealed class RecorridoInvitationQuota(IServiceProvider services)
{
    private const int MaxRemaining = 10;
    private readonly Dictionary<string, Filled> _filled = new(StringComparer.Ordinal);

    /// <summary><c>{ kind: "fill", email, remaining }</c> o <c>{ kind: "release", email }</c>; <c>email</c> es quien invita.</summary>
    public async Task<object> ExecuteAsync(JsonElement command, CancellationToken ct)
    {
        RecorridoCommandArguments.Allow(command, "kind", "email", "remaining");
        var kind = RecorridoCommandArguments.Text(command, "kind");
        var email = RecorridoCommandArguments.SyntheticEmail(RecorridoCommandArguments.Text(command, "email"));
        int? remaining = kind switch
        {
            "fill" => command.TryGetProperty("remaining", out var value) && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var parsed) && parsed is >= 0 and <= MaxRemaining
                ? parsed : throw new InvalidOperationException("The isolated quota leaves 0 to 10 invitations."),
            "release" when !command.TryGetProperty("remaining", out _) => null,
            _ => throw new InvalidOperationException("Unsupported isolated quota command."),
        };
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        RecorridoCommandArguments.RequireTesting(provider);
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var inviterId = await RecorridoCommandArguments.UserIdAsync(db, email, ct);
        var key = new MessageQuotaKey(MessageQuotaScope.Inviter,
            provider.GetRequiredService<IMessageQuotaKeyHasher>().ForSubject(MessageQuotaScope.Inviter, inviterId));
        if (remaining is { } left)
        {
            if (_filled.ContainsKey(key.KeyHash)) throw new InvalidOperationException("The isolated inviter quota is already filled.");
            var limit = provider.GetRequiredService<IOptions<EmailQuotaOptions>>().Value.DailyInvitationLimitPerInviter;
            _filled[key.KeyHash] = await WriteAsync(provider, async token =>
            {
                var buckets = provider.GetRequiredService<IMessageQuotaRepository>();
                await buckets.LockAsync([key], token);
                var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var count = MessageQuotaWindow.Count(await buckets.ListAsync(key, MessageQuotaWindow.FirstHour(nowUtc), token), nowUtc);
                var added = Math.Max(0, limit - left - count);
                var hourUtc = MessageQuotaWindow.HourOf(nowUtc).AddHours(-1);
                if (added > 0) await AddAsync(db, key, hourUtc, added, token);
                return new Filled(hourUtc, added);
            }, ct);
            return new { remaining = left };
        }
        if (!_filled.Remove(key.KeyHash, out var previous)) return new { released = false };
        await WriteAsync(provider, async token =>
        {
            await provider.GetRequiredService<IMessageQuotaRepository>().LockAsync([key], token);
            await AddAsync(db, key, previous.HourUtc, -previous.Added, token);
            return previous;
        }, ct);
        return new { released = true };
    }

    // Como MessageQuotaRepository.IncrementAsync, con la cantidad: una suma negativa devuelve lo que se sumó, y la hora que
    // queda en cero se borra, como la purga.
    private static async Task AddAsync(ApplicationDbContext db, MessageQuotaKey key, DateTime hourUtc, int amount, CancellationToken ct)
    {
        var scope = key.Scope.ToString();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO identity."MessageQuotaBuckets" ("Scope", "KeyHash", "HourUtc", "Count")
            VALUES ({scope}, {key.KeyHash}, {hourUtc}, {Math.Max(0, amount)})
            ON CONFLICT ("Scope", "KeyHash", "HourUtc") DO UPDATE SET "Count" = identity."MessageQuotaBuckets"."Count" + {amount}
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM identity."MessageQuotaBuckets"
            WHERE "Scope" = {scope} AND "KeyHash" = {key.KeyHash} AND "HourUtc" = {hourUtc} AND "Count" <= 0
            """, ct);
    }

    private static async Task<Filled> WriteAsync(IServiceProvider provider, Func<CancellationToken, Task<Filled>> work, CancellationToken ct)
    {
        var result = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            async token => Result.Success(await work(token)), CommitPolicy.OnSuccess, ct);
        return result.IsSuccess ? result.Value : throw new InvalidOperationException("The isolated inviter quota could not change.");
    }

    private sealed record Filled(DateTime HourUtc, int Added);
}
