namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Identidades de organizaciones y espacios personales usados por los tests de RLS.</summary>
internal sealed class TenantFixture
{
    public Guid BusinessATenantId { get; } = Guid.NewGuid();
    public Guid BusinessBTenantId { get; } = Guid.NewGuid();
    public Guid KevinPersonalTenantId { get; } = Guid.NewGuid();
    public Guid CarlaPersonalTenantId { get; } = Guid.NewGuid();
}
