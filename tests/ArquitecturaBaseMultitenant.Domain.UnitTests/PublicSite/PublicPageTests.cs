using ArquitecturaBaseMultitenant.Domain.PublicSite;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.PublicSite;

public sealed class PublicPageTests
{
    [Fact]
    public void New_page_is_draft_and_only_publishes_after_content_is_valid()
    {
        var page = PublicPage.Create();

        Assert.False(page.IsPublished);
        Assert.True(page.UpdateContent("Grupo Delta", "Una descripción", "hola@example.com",
            "+541145678900", "Buenos Aires", "https://example.com").IsSuccess);
        Assert.True(page.Publish().IsSuccess);
        Assert.True(page.IsPublished);
        Assert.True(page.Unpublish().IsSuccess);
        Assert.False(page.IsPublished);
    }

    [Fact]
    public void Moderation_block_keeps_page_draft_and_prevents_republication()
    {
        var page = PublicPage.Create();
        Assert.True(page.UpdateContent("Grupo Delta", null, null, null, null, null).IsSuccess);
        Assert.True(page.Publish().IsSuccess);

        var blockedAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        page.ApplyModerationBlock("Datos de contacto falsos.", Guid.NewGuid(), blockedAtUtc);

        Assert.False(page.IsPublished);
        Assert.Equal(blockedAtUtc, page.PublishBlockedAtUtc);
        Assert.Equal("Datos de contacto falsos.", page.PublishBlockedReason);
        Assert.Equal(PublicPageErrors.PublishBlocked, page.Publish().Error);
    }

    [Fact]
    public void Logo_accepts_supported_signatures_and_rejects_mismatched_or_large_files()
    {
        var page = PublicPage.Create();
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

        Assert.True(page.SetLogo(png, "image/png").IsSuccess);
        Assert.NotSame(png, page.LogoBytes);
        Assert.Equal(PublicPageErrors.InvalidLogo, page.SetLogo(png, "image/jpeg").Error);
        Assert.Equal(PublicPageErrors.InvalidLogo,
            page.SetLogo(new byte[PublicPage.MaximumLogoBytes + 1], "image/png").Error);
        Assert.True(page.SetLogo(null, null).IsSuccess);
        Assert.Null(page.LogoBytes);
    }
}
