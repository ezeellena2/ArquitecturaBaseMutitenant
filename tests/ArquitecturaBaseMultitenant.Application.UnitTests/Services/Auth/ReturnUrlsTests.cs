using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Auth;

/// <summary>
/// Comprueba que acceso y organización se obtengan únicamente de un retorno local válido del protocolo.
/// Rechaza destinos externos o selecciones ambiguas.
/// </summary>
public sealed class ReturnUrlsTests
{
    [Fact]
    public void Link_is_a_fixed_deletion_return_but_not_an_OTP_authorization()
    { Assert.True(ReturnUrls.IsDeletionReturn("/login/enlace")); Assert.False(ReturnUrls.IsLoginReturn("/login/enlace")); }
    [Theory]
    [InlineData("/login/enlace?access=platform")]
    [InlineData("/login/enlace#token")]
    [InlineData("//example.test/login/enlace")]
    [InlineData("https://example.test/login/enlace")]
    public void Deletion_link_continuation_rejects_queries_fragments_and_external_destinations(string path) =>
        Assert.False(ReturnUrls.IsDeletionReturn(path));
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
