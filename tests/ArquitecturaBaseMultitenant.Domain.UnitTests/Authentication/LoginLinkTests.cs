using ArquitecturaBaseMultitenant.Domain.Authentication;
namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;
public sealed class LoginLinkTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void A_link_proves_one_method_once_and_expires_at_ten_minutes()
    {
        var user = Guid.CreateVersion7(); var method = Guid.CreateVersion7();
        var link = LoginLink.Issue(user, method, "hash", Now);
        Assert.Equal(user, link.UserId); Assert.Equal(method, link.MethodId);
        Assert.True(link.IsActive(Now.AddMinutes(9)));
        Assert.False(link.IsActive(Now.AddMinutes(10)));
        Assert.True(link.Redeem(Now).IsSuccess);
        Assert.Equal(LoginLinkErrors.InvalidCode, link.Redeem(Now).Error.Code);
        Assert.Equal(Now, link.ConsumedAtUtc);
    }
    [Fact]
    public void Superseded_and_expired_links_share_invalid_error_without_consumption()
    {
        var link = LoginLink.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now);
        link.Invalidate(Now); link.Invalidate(Now.AddMinutes(1));
        Assert.Equal(Now, link.InvalidatedAtUtc);
        Assert.Equal(LoginLinkErrors.InvalidCode, link.Redeem(Now).Error.Code);
        Assert.Null(link.ConsumedAtUtc);
        var expired = LoginLink.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash2", Now);
        Assert.Equal(LoginLinkErrors.InvalidCode, expired.Redeem(Now.AddMinutes(10)).Error.Code);
    }
}
