using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;
public sealed class WhatsAppChannelsTests
{
    [Fact]
    public async Task Disabled_channels_enqueue_nothing_and_operator_is_email_only()
    {
        var settings = new WhatsAppCodeQuotaTests.Settings { IsEnabled = false };
        var templates = new Templates(); var outbox = new Outbox();
        var login = new WhatsAppLoginCodeChannel(settings, templates, outbox);
        login.EnqueueSignup("+5491134567890", "123456", 10, null!);
        login.EnqueueVerification(Guid.NewGuid(), "+5491134567890", "123456", 10, null!);
        await new WhatsAppAccountNoticeChannel(settings, templates, outbox).EnqueueAsync(null, "+5491134567890", new AccountNotice.AccountDeleted(), null!, TestContext.Current.CancellationToken);
        await new WhatsAppInvitationChannel(settings, templates, outbox).EnqueueAsync(InvitationDeliveryDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value), null!, null!, null, TestContext.Current.CancellationToken);
        Assert.Equal(0, outbox.Calls);
        settings.IsEnabled = true;
        await new WhatsAppAccountNoticeChannel(settings, templates, outbox).EnqueueAsync(null, "+5491134567890",
            new AccountNotice.OperatorInvited(null, [], new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), "UTC", "https://example.test/"), null!, TestContext.Current.CancellationToken);
        Assert.Equal(0, outbox.Calls);
    }
    [Fact]
    public async Task Verified_existing_phone_bypasses_new_country_policy_and_user_correlation_is_preserved()
    {
        var settings = new WhatsAppCodeQuotaTests.Settings(); var templates = new Templates(); var outbox = new Outbox();
        var userId = Guid.NewGuid();
        var login = new WhatsAppLoginCodeChannel(settings, templates, outbox);
        login.EnqueueRenderedLoginCode(login.RenderLoginCode("+14155552671", "123456", 10, null!), userId);
        Assert.Equal(userId, outbox.UserId);
        var invitation = new WhatsAppInvitationChannel(settings, templates, outbox);
        await invitation.EnqueueAsync(InvitationDeliveryDestination.ForPhone(PhoneNumber.Create("+14155552671").Value), null!, null!, userId, TestContext.Current.CancellationToken);
        await new WhatsAppAccountNoticeChannel(settings, templates, outbox).EnqueueAsync(userId, "+14155552671", new AccountNotice.AccountDeleted(), null!, TestContext.Current.CancellationToken);
        Assert.Equal(3, outbox.Calls);
        Assert.Equal(userId, outbox.UserId);
        Assert.Equal("whatsapp", outbox.Channel);
        await invitation.EnqueueAsync(InvitationDeliveryDestination.ForEmail(Email.Create("identity@example.test").Value), null!, null!, userId, TestContext.Current.CancellationToken);
        Assert.Equal(3, outbox.Calls);
    }
    private sealed class Outbox : IOutbox
    {
        public int Calls { get; private set; } public Guid? UserId { get; private set; } public string? Channel { get; private set; }
        public void Enqueue(string channel, string payload, Guid? userId = null) { Calls++; UserId = userId; Channel = channel; }
    }
    private sealed class Templates : IWhatsAppTemplateRenderer
    {
        public WhatsAppOutboundMessage RenderCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => new("id", destination, "codigo_ingreso", "es", [code], code, true);
        public Task<WhatsAppOutboundMessage> RenderInvitationAsync(string destination, InvitationNotice notice, CultureProfile culture, CancellationToken ct) => Task.FromResult(new WhatsAppOutboundMessage("id", destination, "invitacion_organizacion", "es", []));
        public Task<WhatsAppOutboundMessage?> RenderNoticeAsync(string destination, AccountNotice notice, CultureProfile culture, CancellationToken ct) => Task.FromResult<WhatsAppOutboundMessage?>(new("id", destination, "cuenta_eliminada", "es", []));
    }
}
