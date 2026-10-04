using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>
/// Programa una baja ya autorizada, dentro de la transacción del llamador: consulta los bloqueos de los módulos,
/// fija la fecha según la gracia, revoca las sesiones, avisa a los participantes y encola el aviso. La usan la
/// baja que pide la persona y la que inicia Plataforma.
/// </summary>
internal sealed class AccountDeletionScheduler(IUserRepository users, IEnumerable<IAccountDeletionParticipant> participants,
    ISignInService sessions, IPlatformSettingsReader settings, AccountDeletionNotifier notifier, ILoginMethodRepository methods)
{
    internal async Task<Error?> CheckBlocksAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var context = new AccountDeletionContext(userId, nowUtc);
        var blocks = new List<Error>();
        foreach (var participant in participants) blocks.AddRange(await participant.CheckAsync(context, ct));
        if (blocks.FirstOrDefault(error => error.Code == AccountDeletionErrors.LastAdmin.Code) is { } lastAdmin)
            return lastAdmin;
        return blocks.Count == 0 ? null : AccountDeletionErrors.Blocked with
        {
            Metadata = new Dictionary<string, object?> { ["reasons"] = blocks.Select(error => ErrorTexts.Get(error.Code)).ToArray() },
        };
    }

    /// <summary>Con <paramref name="operatorId"/>, la baja la inicia Plataforma: el aviso lo dice y su evento lo registra el llamador.</summary>
    internal async Task<Result<DateTime>> ScheduleAsync(UserAccountRow account, string reason, DateTime nowUtc,
        Guid? operatorId, CancellationToken ct)
    {
        var platform = await settings.FindAsync(ct)
            ?? throw new InvalidOperationException("The platform settings have not been seeded.");
        var grace = platform.AccountDeletionGraceDays;
        var requested = operatorId is { } byOperator
            ? await users.RequestDeletionByOperatorAsync(account.Id, byOperator, reason, nowUtc, grace, ct)
            : await users.RequestDeletionAsync(account.Id, reason, nowUtc, grace, ct);
        if (requested.IsFailure) return requested.Error;
        await sessions.RevokeSessionsAsync(account.Id, ct);
        var context = new AccountDeletionContext(account.Id, nowUtc);
        foreach (var participant in participants) await participant.OnRequestedAsync(context, ct);
        var scheduledForUtc = nowUtc.AddDays(grace);
        var loginMethods = await methods.ListByUserIdAsync(account.Id, ct);
        if (operatorId is null) await notifier.RequestedAsync(account, loginMethods, nowUtc, scheduledForUtc, ct);
        else await notifier.PlatformRequestedAsync(account, loginMethods, nowUtc, scheduledForUtc,
            canCancel: account.Status == UserStatus.Active, platform.SupportEmail, ct);
        return scheduledForUtc;
    }
}
