using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.PublicSite;

public sealed class PublicPageTests
{
    [Fact]
    public void Publication_records_utc_only_when_publishing_a_draft_and_republication_records_the_new_instant()
    {
        var page = PublicPage.Create();
        page.UpdateContent("Publicación", null, null, null, null, null);
        var firstUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.Null(page.PublishedAtUtc);
        Assert.True(page.Publish(firstUtc).IsSuccess);
        Assert.Equal(firstUtc, page.PublishedAtUtc);
        Assert.True(page.Publish(firstUtc.AddHours(1)).IsSuccess);
        Assert.Equal(firstUtc, page.PublishedAtUtc);
        page.Unpublish();
        Assert.True(page.Publish(firstUtc.AddDays(1)).IsSuccess);
        Assert.Equal(firstUtc.AddDays(1), page.PublishedAtUtc);
        Assert.Throws<ArgumentException>(() => page.Publish(DateTime.SpecifyKind(firstUtc, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void New_page_is_draft_and_only_publishes_after_content_is_valid()
    {
        var page = PublicPage.Create();

        Assert.False(page.IsPublished);
        Assert.True(page.UpdateContent("Grupo Delta", "Una descripción", Email.Create("hola@example.com").Value,
            PhoneNumber.Create("+541145678900").Value, "Buenos Aires", "https://example.com").IsSuccess);
        Assert.True(page.Publish(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
        Assert.True(page.IsPublished);
        Assert.True(page.Unpublish().IsSuccess);
        Assert.False(page.IsPublished);
    }

    [Fact]
    public void Moderation_block_keeps_page_draft_and_prevents_republication()
    {
        var page = PublicPage.Create();
        Assert.True(page.UpdateContent("Grupo Delta", null, null, null, null, null).IsSuccess);
        Assert.True(page.Publish(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);

        var blockedAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        page.ApplyModerationBlock("Datos de contacto falsos.", Guid.NewGuid(), blockedAtUtc);

        Assert.False(page.IsPublished);
        Assert.Equal(blockedAtUtc, page.PublishBlockedAtUtc);
        Assert.Equal("Datos de contacto falsos.", page.PublishBlockedReason);
        Assert.Equal(PublicPageErrors.PublishBlocked, page.Publish(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)).Error);
    }

    [Fact]
    public void Lifting_moderation_keeps_the_page_draft_until_the_organization_publishes_it()
    {
        var page = PublicPage.Create();
        Assert.True(page.UpdateContent("Grupo Delta", "Contenido conservado", null, null, null, null).IsSuccess);
        Assert.True(page.Publish(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
        Assert.True(page.ApplyModerationBlock("Revisar contenido", Guid.NewGuid(),
            new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);

        Assert.True(page.AllowPublish().IsSuccess);
        Assert.False(page.IsPublished);
        Assert.Null(page.PublishBlockedAtUtc);
        Assert.Null(page.PublishBlockedReason);
        Assert.Null(page.PublishBlockedByUserId);
        Assert.Equal("Contenido conservado", page.Description);
        Assert.True(page.Publish(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
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
