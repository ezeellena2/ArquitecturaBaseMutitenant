using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Confirma el alta y permite continuar por su estado sin inferirlo del contenido de la página.</summary>
public sealed record BusinessSignupResponse(Guid TenantId, string OrganizationName, string Slug,
    bool RequiresApproval, Guid FirstCompanyId, TenantStatus Status);
