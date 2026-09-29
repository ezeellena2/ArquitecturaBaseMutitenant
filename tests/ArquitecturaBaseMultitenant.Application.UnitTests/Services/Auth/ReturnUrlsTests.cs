using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

public sealed class ReturnUrlsTests
{
    [Theory]
    [InlineData("/connect/authorize?client_id=web", Access.Consumer, null)]
    [InlineData("/connect/authorize?access=business", Access.Business, null)]
    [InlineData("/connect/authorize?access=%62usiness&tenant=89a04808-4809-47ee-a651-b2dc84a151dd",
        Access.Business, "89a04808-4809-47ee-a651-b2dc84a151dd")]
    public void Reads_access_and_requested_organization_only_from_the_local_authorize_url(
        string returnUrl, Access access, string? tenantText)
    {
        Assert.True(ReturnUrls.TryReadAccessSelection(returnUrl, out var selection));
        Assert.Equal(access, selection.Access);
        Assert.Equal(tenantText is null ? null : Guid.Parse(tenantText), selection.TenantId);
    }

    [Theory]
    [InlineData("https://other.test/connect/authorize?access=business")]
    [InlineData("/connect/authorize?access=admin")]
    [InlineData("/connect/authorize?access=business&access=consumer")]
    [InlineData("/connect/authorize?access=business&tenant=not-a-guid")]
    public void Rejects_nonlocal_or_ambiguous_access_selection(string returnUrl) =>
        Assert.False(ReturnUrls.TryReadAccessSelection(returnUrl, out _));
}
