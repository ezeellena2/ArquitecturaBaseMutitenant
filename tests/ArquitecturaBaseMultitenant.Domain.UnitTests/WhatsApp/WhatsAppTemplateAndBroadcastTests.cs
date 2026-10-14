using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

/// <summary>
/// B6 de la E11: el nombre de Meta sale del que escribe la persona; los datos del texto van numerados desde {{1}} sin saltear
/// ninguno; una difusión se cancela solo mientras está programada; un destinatario pasa de enviado a respondió o a baja.
/// </summary>
public sealed class WhatsAppTemplateAndBroadcastTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Promo de Otoño", "promo_de_otono")]
    [InlineData("  ¡Turno confirmado! ", "turno_confirmado")]
    [InlineData("Aviso 2", "aviso_2")]
    [InlineData("¡¡¡", null)]
    public void The_Meta_name_is_lowercase_without_accents_and_with_underscores(string name, string? expected) =>
        Assert.Equal(expected, WhatsAppTemplateBase.ToMetaName(name));

    [Theory]
    [InlineData("Hola", 0)]
    [InlineData("Hola {{1}}, tu turno es el {{2}}", 2)]
    [InlineData("{{1}} y otra vez {{1}}", 1)]
    [InlineData("Hola {{2}}", -1)]
    [InlineData("{{1}} {{3}}", -1)]
    public void Placeholders_count_only_when_numbered_from_one_without_gaps(string body, int expected) =>
        Assert.Equal(expected, WhatsAppTemplateBase.Placeholders(body));

    [Fact]
    public void A_template_keeps_its_name_its_culture_and_renders_its_values()
    {
        var template = WhatsAppTemplate.Create("Aviso de turno", "es-AR", WhatsAppTemplateCategory.Utility, "Tu turno es el {{1}}.",
            [new WhatsAppTemplateVariable("fecha", "lunes")]);
        Assert.Equal(("Aviso de turno", "aviso_de_turno", "es-AR", "es_AR"), (template.Name, template.MetaName, template.Language, template.MetaLanguage));
        Assert.Equal("Tu turno es el lunes 20.", WhatsAppTemplateBase.Render(template.Body, ["lunes 20"]));
        Assert.Throws<ArgumentException>(() => WhatsAppTemplate.Create("Sin datos", "es-AR", WhatsAppTemplateCategory.Utility, "Hola {{1}}", []));
    }

    [Fact]
    public void A_broadcast_is_cancelled_only_while_scheduled_and_is_due_when_its_time_comes()
    {
        var later = Broadcast(Now.AddHours(1));
        Assert.False(later.IsDue(Now));
        Assert.True(later.IsDue(Now.AddHours(1)));
        Assert.True(later.Cancel().IsSuccess);
        Assert.Equal(WhatsAppErrors.BroadcastNotCancellableCode, later.Cancel().Error.Code);

        var now = Broadcast(null);
        Assert.True(now.IsDue(Now));
        now.Start(Now);
        Assert.Equal(WhatsAppErrors.BroadcastNotCancellableCode, now.Cancel().Error.Code);
        now.Finish(Now);
        Assert.Equal((WhatsAppBroadcastStatus.Sent, Now), (now.Status, now.SentAtUtc));
        Assert.False(now.IsDue(Now));
    }

    [Fact]
    public void A_recipient_replies_or_unsubscribes_only_after_it_was_sent()
    {
        var recipient = WhatsAppBroadcastRecipient.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), PhoneNumber.Create("+5491155550000").Value, Now);
        recipient.MarkReplied(Now);
        Assert.Equal(WhatsAppRecipientStatus.Pending, recipient.Status);
        recipient.MarkSent(Guid.CreateVersion7(), Now);
        recipient.MarkReplied(Now.AddMinutes(1));
        Assert.Equal(WhatsAppRecipientStatus.Replied, recipient.Status);
        recipient.MarkUnsubscribed(Now.AddMinutes(2));
        Assert.Equal(WhatsAppRecipientStatus.Unsubscribed, recipient.Status);
        recipient.Fail("WhatsApp.Send.Rejected", Now.AddMinutes(3));
        Assert.Equal(WhatsAppRecipientStatus.Unsubscribed, recipient.Status);
    }

    private static WhatsAppBroadcast Broadcast(DateTime? scheduledAtUtc) => WhatsAppBroadcast.Create("Promo", Guid.CreateVersion7(), "Promo", ["20%"],
        WhatsAppAudienceKind.AllAccepting, null, scheduledAtUtc, 2, new Money(0.12m, CurrencyCode.Create("USD").Value));
}
