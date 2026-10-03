using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Confirma el alta, su estado y el correo principal de la cuenta, al que se avisa la aprobación.</summary>
public sealed record BusinessSignupResponse(Guid TenantId, string OrganizationName, string Slug,
    bool RequiresApproval, Guid FirstCompanyId, TenantStatus Status, Email? NotificationEmail);
