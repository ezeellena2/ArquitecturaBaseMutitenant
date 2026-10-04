using ArquitecturaBaseMultitenant.Application.Configuration.Invitations;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Crea, dentro de la UoW del llamador, la identidad exclusiva de un operador invitado: cuenta marcada como operadora,
/// su correo verificado como método principal y sus permisos, sin espacio Personal ni organizaciones. El correo queda
/// verificado porque lo da un operador con <c>platform.operators.manage</c>; el primer ingreso con código lo prueba y
/// acepta la invitación, que vence como la de una organización (ADR 0042). Cultura y zona horaria salen de los datos
/// de referencia, como en el seed del primer operador. También renueva y revoca la invitación sin aceptar.
/// </summary>
internal sealed class PlatformOperatorProvisioner(IUserRepository users, ILoginMethodRepository methods,
    IPlatformPermissionAssignmentRepository assignments, UserCultureResolver cultures, ICountryCatalog countries,
    IOptions<InvitationOptions> invitations)
{
    public async Task<Result<PlatformOperatorInvitee>> CreateAsync(Email email, IReadOnlyCollection<string> permissions,
        DateTime nowUtc, CancellationToken ct)
    {
        var culture = await cultures.ResolveAsync(null, null, ct);
        var timeZone = (await countries.FindAsync(culture.Entry.CountryCode, ct))?.DefaultTimeZoneId
            ?? throw new InvalidOperationException("The default culture country has no time zone in reference data.");
        var account = await users.CreateAsync(null, culture.Entry.Code, timeZone, ct);
        var assignment = PlatformPermissionAssignment.Create(account.Id, permissions);
        if (assignment.IsFailure) return assignment.Error;
        var expiresAtUtc = ExpiresAt(nowUtc);
        await users.InvitePlatformOperatorAsync(account.Id, expiresAtUtc, ct);
        var method = LoginMethod.CreateEmail(account.Id, email);
        method.Verify(nowUtc);
        if (method.MakePrimary().IsFailure)
            throw new InvalidOperationException("A verified operator email could not become primary.");
        methods.Add(method);
        await users.SetPrimaryEmailAsync(account.Id, email, ct);
        assignments.Add(assignment.Value);
        return new PlatformOperatorInvitee(account.Id, email, [.. assignment.Value.Permissions], expiresAtUtc, null,
            account.Culture, account.TimeZoneId);
    }

    /// <summary>La cuenta es una invitación de operador sin aceptar, vigente o vencida.</summary>
    public async Task<bool> IsPendingAsync(Guid accountId, CancellationToken ct) =>
        (await users.GetByIdAsync(accountId, ct))?.HasPendingOperatorInvitation == true;

    /// <summary>Reenviar: bajo el lock de la cuenta, la invitación vigente o vencida vence de nuevo en 7 días.</summary>
    public async Task<Result<PlatformOperatorInvitee>> RenewAsync(Guid accountId, DateTime nowUtc, CancellationToken ct)
    {
        var pending = await LoadPendingAsync(accountId, ct);
        if (pending.IsFailure) return pending.Error;
        var (account, assignment) = pending.Value;
        var expiresAtUtc = ExpiresAt(nowUtc);
        var renewed = await users.RenewOperatorInvitationAsync(accountId, expiresAtUtc, ct);
        if (renewed.IsFailure) return renewed.Error;
        return new PlatformOperatorInvitee(accountId,
            account.PrimaryEmail ?? throw new InvalidOperationException("A pending operator invitation has no email."),
            [.. assignment.Permissions], expiresAtUtc, account.OperatorInvitationExpiresAtUtc, account.Culture,
            account.TimeZoneId);
    }

    /// <summary>
    /// Revocar: bajo el lock de la cuenta, borra el correo (queda libre) y los permisos; la identidad queda eliminada,
    /// fuera de Cuentas. Devuelve los permisos que tenía la invitación, para el diff del evento.
    /// </summary>
    public async Task<Result<IReadOnlyList<string>>> DiscardAsync(Guid accountId, DateTime nowUtc, CancellationToken ct)
    {
        var pending = await LoadPendingAsync(accountId, ct);
        if (pending.IsFailure) return pending.Error;
        var (_, assignment) = pending.Value;
        foreach (var method in await methods.ListByUserIdAsync(accountId, ct)) methods.Remove(method);
        assignments.Remove(assignment);
        var discarded = await users.DiscardOperatorInvitationAsync(accountId, nowUtc,
            ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"), ct);
        if (discarded.IsFailure) return discarded.Error;
        return Result.Success<IReadOnlyList<string>>([.. assignment.Permissions]);
    }

    // El mismo lock de cuenta que el ingreso con código: aceptar, reenviar y revocar no se cruzan.
    private async Task<Result<(UserAccountRow Account, PlatformPermissionAssignment Assignment)>> LoadPendingAsync(
        Guid accountId, CancellationToken ct)
    {
        await methods.LockUserAsync(accountId, ct);
        var account = await users.GetByIdAsync(accountId, ct);
        if (account is null || account.Status == UserStatus.Deleted) return UserErrors.NotFound;
        if (!account.HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        var assignment = await assignments.GetByUserIdAsync(accountId, ct)
            ?? throw new InvalidOperationException("A pending operator invitation has no permissions.");
        return (account, assignment);
    }

    // En microsegundos, la precisión de la base: la respuesta muestra el mismo vencimiento que después Cuentas.
    private DateTime ExpiresAt(DateTime nowUtc)
    {
        var expiresAtUtc = nowUtc.AddDays(invitations.Value.LifetimeDays);
        return expiresAtUtc.AddTicks(-(expiresAtUtc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}

/// <summary>Lo que necesita el aviso de una invitación de operador, después de crearla o renovarla.</summary>
internal sealed record PlatformOperatorInvitee(Guid Id, Email Email, IReadOnlyList<string> Permissions,
    DateTime ExpiresAtUtc, DateTime? PreviousExpiresAtUtc, string Culture, string TimeZoneId);
