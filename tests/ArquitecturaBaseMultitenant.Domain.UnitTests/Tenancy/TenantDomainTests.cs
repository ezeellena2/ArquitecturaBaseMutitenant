using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

public sealed class TenantDomainTests
{
    [Theory]
    [InlineData(" EXAMPLE.COM. ", "example.com")]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    public void Domain_is_normalized_for_dns(string input, string expected)
    {
        var result = OrganizationEmailDomain.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Value);
    }

    [Theory]
    [InlineData("example")]
    [InlineData("*.example.com")]
    [InlineData("https://example.com")]
    [InlineData("user@example.com")]
    [InlineData("127.0.0.1")]
    [InlineData("example..com")]
    [InlineData("-example.com")]
    [InlineData("example-.com")]
    public void Domain_rejects_values_that_cannot_own_a_public_dns_record(string input)
    {
        var result = OrganizationEmailDomain.Create(input);

        Assert.Equal(TenantDomainErrors.InvalidDomain, result.Error);
    }

    [Fact]
    public void Domain_verification_is_a_single_pending_to_verified_transition()
    {
        var organizationId = Guid.CreateVersion7();
        var domain = OrganizationEmailDomain.Create("empresa.example").Value;
        var createdAt = new DateTime(2026, 10, 2, 17, 0, 0, DateTimeKind.Utc);
        var tenantDomain = TenantDomain.Create(organizationId, domain, "verification-value", createdAt).Value;

        Assert.Equal(TenantDomainStatus.Pending, tenantDomain.Status);
        Assert.Null(tenantDomain.VerifiedAtUtc);
        Assert.True(tenantDomain.Verify(createdAt.AddMinutes(1)).IsSuccess);
        Assert.Equal(TenantDomainStatus.Verified, tenantDomain.Status);
        Assert.Equal(createdAt.AddMinutes(1), tenantDomain.VerifiedAtUtc);
        Assert.Equal(TenantDomainErrors.AlreadyVerified, tenantDomain.Verify(createdAt.AddMinutes(2)).Error);
    }

    [Fact]
    public void Removing_a_verified_domain_keeps_existing_managed_login_methods()
    {
        var organizationId = Guid.CreateVersion7();
        var domain = OrganizationEmailDomain.Create("empresa.example").Value;
        var createdAt = new DateTime(2026, 10, 2, 17, 0, 0, DateTimeKind.Utc);
        var tenantDomain = TenantDomain.Create(organizationId, domain, "verification-value", createdAt).Value;
        Assert.True(tenantDomain.Verify(createdAt.AddMinutes(1)).IsSuccess);
        var method = LoginMethod.CreateEmail(Guid.CreateVersion7(),
            Email.Create("persona@empresa.example").Value);
        Assert.True(method.Verify(createdAt).IsSuccess);
        method.AssignManagedByOrganization(organizationId);

        Assert.True(tenantDomain.Remove().IsSuccess);

        Assert.Equal(TenantDomainStatus.Removed, tenantDomain.Status);
        Assert.Equal(string.Empty, tenantDomain.VerificationToken);
        Assert.Equal(organizationId, method.ManagedByTenantId);
        Assert.False(method.CanSignIn(channelAvailable: true, managedMembershipActive: false));
        Assert.Equal(TenantDomainErrors.AlreadyRemoved, tenantDomain.Remove().Error);
    }
}
