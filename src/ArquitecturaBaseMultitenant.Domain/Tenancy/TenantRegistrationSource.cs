namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Origen del alta global, independiente de quién usa la organización.</summary>
public enum TenantRegistrationSource
{
    SelfService,
    Platform,
}
