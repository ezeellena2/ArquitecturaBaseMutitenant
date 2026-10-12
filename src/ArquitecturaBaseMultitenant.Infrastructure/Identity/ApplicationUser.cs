using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.Common;
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
        // Los llamadores legacy conservan partes compuestas al repetir el mismo combinado.
        if (string.Equals(DisplayName, displayName, StringComparison.Ordinal)) return Result.Success();
        if (!IsValidDisplayName(displayName))
        {
            return UserErrors.InvalidDisplayName;
        }

        SetLegacyName(displayName);
        return Result.Success();
    }

    /// <summary>Reemplaza las dos partes manuales y deriva el único nombre que leen menús, listas y avisos.</summary>
    public Result UpdateNames(string? firstName, string? lastName)
    {
        if (firstName?.Length > TextLimits.FirstName || lastName?.Length > TextLimits.LastName)
            return UserErrors.InvalidDisplayName;
        FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName;
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName;
        DeriveDisplayName();
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

    public Result Suspend() => ApplyLifecycle(Lifecycle.Suspend());

    public Result Reactivate() => ApplyLifecycle(Lifecycle.Reactivate());

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
    public Result DiscardOperatorInvitation(DateTime discardedAtUtc)
    {
        RequireUtc(discardedAtUtc);
        if (!HasPendingOperatorInvitation) return PlatformOperatorErrors.NoPendingInvitation;
        Anonymize(discardedAtUtc);
        return Result.Success();
    }

    private AccountLifecycle Lifecycle => new(Status, DeletionRequestedAtUtc, DeletionScheduledForUtc,
        DeletionReason, DeletionRequestedByOperatorId, DeletedAtUtc);

    private Result ApplyLifecycle(Result<AccountLifecycle> result)
    {
        if (result.IsFailure) return result.Error;
        var lifecycle = result.Value;
        Status = lifecycle.Status;
        DeletionRequestedAtUtc = lifecycle.DeletionRequestedAtUtc;
        DeletionScheduledForUtc = lifecycle.DeletionScheduledForUtc;
        DeletionReason = lifecycle.DeletionReason;
        DeletionRequestedByOperatorId = lifecycle.DeletionRequestedByOperatorId;
        DeletedAtUtc = lifecycle.DeletedAtUtc;
        return Result.Success();
    }

    public Result RequestDeletion(string reason, DateTime requestedAtUtc, int graceDays, Guid? operatorId = null) =>
        ApplyLifecycle(Lifecycle.RequestDeletion(reason, requestedAtUtc, graceDays, operatorId));

    public Result CancelDeletion(DateTime cancelledAtUtc) => ApplyLifecycle(Lifecycle.CancelDeletion(cancelledAtUtc));

    public Result CompleteDeletion(DateTime deletedAtUtc)
    {
        RequireUtc(deletedAtUtc);
        if (Status == UserStatus.Deleted) return Result.Success();
        var completed = ApplyLifecycle(Lifecycle.CompleteDeletion(deletedAtUtc));
        if (completed.IsSuccess) Anonymize(deletedAtUtc);
        return completed;
    }

    private void Anonymize(DateTime deletedAtUtc)
    {
        Status = UserStatus.Deleted;
        DeletedAtUtc = deletedAtUtc;
        DisplayName = null;
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
