using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>
/// Identidad global. Los métodos verificados viven en identity.LoginMethods;
/// las propiedades heredadas Email y PhoneNumber son solo copias del método principal.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>, IVersioned
{
    public uint Version { get; private set; }

    /// <summary>
    /// Versión de «Tus datos»: sube al guardar el perfil o completar un nombre vacío desde Google. La fila también
    /// cambia con el contacto principal, la última organización, las sesiones y los intentos fallidos, y su xmin
    /// daría un 409 falso al guardar el perfil (docs/rules/concurrencia.md).
    /// </summary>
    public uint ProfileVersion { get; private set; }

    public string? DisplayName { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string Culture { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public bool IsPlatformOperator { get; private set; }
    public Guid? LastBusinessTenantId { get; private set; }
    public DateTime? DeletionRequestedAtUtc { get; private set; }
    public DateTime? DeletionScheduledForUtc { get; private set; }
    public string? DeletionReason { get; private set; }
    public Guid? DeletionRequestedByOperatorId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime? CreatedAtUtc { get; private set; }
    public Guid? DeletionLeaseId { get; private set; }
    public DateTime? DeletionLeaseExpiresAtUtc { get; private set; }

    /// <summary>
    /// ADR 0042: vencimiento de la invitación de operador sin aceptar. Es null en una cuenta común y en un operador
    /// que ya entró; en una cuenta eliminada marca una invitación revocada, que no figura en Cuentas.
    /// </summary>
    public DateTime? OperatorInvitationExpiresAtUtc { get; private set; }

    public static Result<ApplicationUser> Create(string? displayName, string culture, string timeZoneId,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (createdAtUtc is { } created) RequireUtc(created);
        if (!IsValidDisplayName(displayName))
        {
            return UserErrors.InvalidDisplayName;
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            Culture = culture,
            TimeZoneId = timeZoneId,
            CreatedAtUtc = createdAtUtc,
        };
        user.SetLegacyName(displayName);
        user.UserName = user.Id.ToString("D", CultureInfo.InvariantCulture);
        return user;
    }

    public Result Rename(string? displayName)
    {
        // Hasta A6 el editor manda el combinado incluso al guardar sólo preferencias.
        if (string.Equals(DisplayName, displayName, StringComparison.Ordinal)) return Result.Success();
        if (!IsValidDisplayName(displayName))
        {
            return UserErrors.InvalidDisplayName;
        }

        SetLegacyName(displayName);
        return Result.Success();
    }

    /// <summary>Completa sólo las partes vacías desde una identidad externa ya autorizada.</summary>
    public void FillMissingNames(string? firstName, string? lastName)
    {
        if (firstName?.Length > TextLimits.FirstName || lastName?.Length > TextLimits.LastName)
            throw new ArgumentException("External name parts must fit their individual limits.");
        var changed = false;
        if (FirstName is null && !string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName;
            changed = true;
        }
        if (LastName is null && !string.IsNullOrWhiteSpace(lastName))
        {
            LastName = lastName;
            changed = true;
        }
        if (!changed) return;
        DeriveDisplayName();
        MarkProfileSaved();
    }

    private void SetLegacyName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) displayName = null;
        var separator = displayName?.IndexOf(' ') ?? -1;
        FirstName = separator < 0 ? displayName : displayName![..separator];
        LastName = separator < 0 ? null : displayName![(separator + 1)..];
        DeriveDisplayName();
    }

    private void DeriveDisplayName() =>
        DisplayName = FirstName is null ? LastName : LastName is null ? FirstName : FirstName + " " + LastName;

    public void UpdatePreferences(string culture, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        Culture = culture;
        TimeZoneId = timeZoneId;
    }

    /// <summary>Un guardado del perfil, aunque no cambie nada: la versión sube y la fila se escribe.</summary>
    public void MarkProfileSaved() => ProfileVersion++;

    public Result Suspend()
    {
        if (Status != UserStatus.Active)
        {
            return UserErrors.InvalidTransition;
        }

        Status = UserStatus.Suspended;
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (Status != UserStatus.Suspended)
        {
            return UserErrors.InvalidTransition;
        }

        Status = DeletionScheduledForUtc is null ? UserStatus.Active : UserStatus.PendingDeletion;
        return Result.Success();
    }

    public void RememberBusinessTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The business tenant id cannot be empty.", nameof(tenantId));
        }

        LastBusinessTenantId = tenantId;
    }

    public void GrantPlatformOperator() => IsPlatformOperator = true;

    public void RevokePlatformOperator() => IsPlatformOperator = false;

    private bool HasPendingOperatorInvitation => Status == UserStatus.Active && OperatorInvitationExpiresAtUtc is not null;

    /// <summary>ADR 0042: la cuenta nueva de un operador invitado queda pendiente hasta su primer ingreso.</summary>
    public void InvitePlatformOperator(DateTime expiresAtUtc)
    {
        RequireUtc(expiresAtUtc);
        if (Status != UserStatus.Active || IsPlatformOperator || OperatorInvitationExpiresAtUtc is not null)
            throw new InvalidOperationException("Only a new account can be invited as a platform operator.");
        IsPlatformOperator = true;
        OperatorInvitationExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Reenviar: una invitación sin aceptar, vigente o vencida, vence de nuevo en la fecha dada.</summary>
    public Result RenewOperatorInvitation(DateTime expiresAtUtc)
    {
        RequireUtc(expiresAtUtc);
        if (!HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        OperatorInvitationExpiresAtUtc = expiresAtUtc;
        return Result.Success();
    }

    /// <summary>El primer ingreso con código a Plataforma acepta la invitación, si todavía no venció.</summary>
    public Result AcceptOperatorInvitation(DateTime acceptedAtUtc)
    {
        RequireUtc(acceptedAtUtc);
        if (!HasPendingOperatorInvitation || acceptedAtUtc >= OperatorInvitationExpiresAtUtc)
            return UserErrors.InvalidTransition;
        OperatorInvitationExpiresAtUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Revocar: la identidad de una invitación sin aceptar queda eliminada y anónima, sin pasar por la gracia. Conserva
    /// el vencimiento como marca de invitación revocada, que no figura en Cuentas.
    /// </summary>
    public Result DiscardOperatorInvitation(DateTime discardedAtUtc, string deletedDisplayName)
    {
        RequireUtc(discardedAtUtc);
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedDisplayName);
        if (!HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        Anonymize(discardedAtUtc, deletedDisplayName);
        return Result.Success();
    }

    public Result RequestDeletion(string reason, DateTime requestedAtUtc, int graceDays, Guid? operatorId = null)
    {
        RequireUtc(requestedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(graceDays, 1);
        if (DeletionScheduledForUtc is not null) return AccountDeletionErrors.AlreadyPending;
        if (Status != UserStatus.Active && !(Status == UserStatus.Suspended && operatorId is not null))
            return UserErrors.InvalidTransition;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > TextLimits.Description)
            return AccountDeletionErrors.InvalidReason;
        DeletionRequestedAtUtc = requestedAtUtc;
        DeletionScheduledForUtc = requestedAtUtc.AddDays(graceDays);
        DeletionReason = reason;
        DeletionRequestedByOperatorId = operatorId;
        if (Status == UserStatus.Active) Status = UserStatus.PendingDeletion;
        return Result.Success();
    }

    public Result CancelDeletion(DateTime cancelledAtUtc)
    {
        RequireUtc(cancelledAtUtc);
        if (Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (Status != UserStatus.PendingDeletion || DeletionScheduledForUtc is null) return UserErrors.InvalidTransition;
        if (cancelledAtUtc >= DeletionScheduledForUtc) return AccountDeletionErrors.GraceExpired;
        Status = UserStatus.Active;
        DeletionRequestedAtUtc = null;
        DeletionScheduledForUtc = null;
        DeletionReason = null;
        DeletionRequestedByOperatorId = null;
        return Result.Success();
    }

    public Result CompleteDeletion(DateTime deletedAtUtc, string deletedDisplayName)
    {
        RequireUtc(deletedAtUtc);
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedDisplayName);
        if (Status == UserStatus.Deleted) return Result.Success();
        if (Status is not (UserStatus.PendingDeletion or UserStatus.Suspended)
            || DeletionScheduledForUtc is null || deletedAtUtc < DeletionScheduledForUtc) return UserErrors.InvalidTransition;
        Anonymize(deletedAtUtc, deletedDisplayName);
        return Result.Success();
    }

    private void Anonymize(DateTime deletedAtUtc, string deletedDisplayName)
    {
        Status = UserStatus.Deleted;
        DeletedAtUtc = deletedAtUtc;
        DisplayName = deletedDisplayName;
        FirstName = LastName = null;
        Email = NormalizedEmail = PhoneNumber = PasswordHash = null;
        EmailConfirmed = PhoneNumberConfirmed = TwoFactorEnabled = IsPlatformOperator = false;
        Culture = TimeZoneId = string.Empty;
        LastBusinessTenantId = null;
        DeletionReason = null;
        SecurityStamp = Guid.CreateVersion7().ToString("N");
        DeletionLeaseId = null;
        DeletionLeaseExpiresAtUtc = null;
        LockoutEnd = null;
        AccessFailedCount = 0;
    }

    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }

    private static bool IsValidDisplayName(string? displayName)
    {
        if (displayName is null) return true;
        var separator = displayName.IndexOf(' ');
        return separator < 0 ? displayName.Length <= TextLimits.FirstName
            : separator <= TextLimits.FirstName && displayName.Length - separator - 1 <= TextLimits.LastName;
    }
}
