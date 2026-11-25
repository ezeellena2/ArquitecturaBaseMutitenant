using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.WhatsApp;

public sealed class WhatsAppFailureTextTests
{
    [Fact]
    public void A_failed_message_shows_the_reason_text_in_the_reader_language_never_the_code()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
            Assert.Equal("WhatsApp no pudo entregarlo a ese número.", WhatsAppConversationResponseBuilder.FailureText("WhatsApp.Send.Undeliverable"));
            Assert.Equal("No se pudo mandar por un motivo que no conocemos.", WhatsAppConversationResponseBuilder.FailureText("Http.Unexpected"));
            Assert.Null(WhatsAppConversationResponseBuilder.FailureText(null));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("WhatsApp couldn't deliver it to that number.", WhatsAppConversationResponseBuilder.FailureText("WhatsApp.Send.Undeliverable"));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void Every_send_code_has_its_own_text()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
            var other = WhatsAppConversationResponseBuilder.FailureText("Http.Unexpected");
            // Los diez motivos (ADR 0066 §3), cada uno con su texto y sin repetirse.
            var texts = WhatsAppSendFailures.All.Select(WhatsAppConversationResponseBuilder.FailureText).ToArray();
            Assert.All(texts, text => Assert.NotEqual(other, text));
            Assert.Equal(texts.Length, texts.Distinct(StringComparer.Ordinal).Count());
            // Los del tablero (WA-Chats-Base2): «No se pudo enviar» lleva debajo uno de estos.
            Assert.Equal("Ese número no puede recibir mensajes de esta cuenta.", WhatsAppConversationResponseBuilder.FailureText(WhatsAppSendFailures.NotAllowed));
            Assert.Equal("WhatsApp rechazó el mensaje.", WhatsAppConversationResponseBuilder.FailureText(WhatsAppSendFailures.Rejected));
            // Lo que quedó «Sin confirmar» no es un motivo de «No se pudo enviar».
            Assert.Equal(other, WhatsAppConversationResponseBuilder.FailureText("WhatsApp.Send.Uncertain"));
            var page = new PagedResult<WhatsAppChatMessageResponse>([new(Guid.NewGuid(), WhatsAppChatMessageKind.Text, WhatsAppMessageDirection.Outbound,
                "Lucía", "Hola", null, WhatsAppChatMessageStatus.Failed, WhatsAppSendFailures.Unavailable, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc))], 1, 10, 1);
            Assert.Equal("No pudimos comunicarnos con WhatsApp, ni con los reintentos. Probá de nuevo en un rato.",
                WhatsAppConversationResponseBuilder.WithFailureTexts(page).Items[0].FailureReason);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
