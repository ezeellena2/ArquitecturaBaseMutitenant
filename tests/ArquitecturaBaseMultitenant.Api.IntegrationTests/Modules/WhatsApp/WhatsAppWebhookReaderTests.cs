using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Webhook;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// KAN-17: el lector del webhook acepta cualquier E.164 válido (incluidas líneas fijas con WhatsApp Business) y registra
/// los ítems descartados sin exponer teléfonos ni datos personales.
/// </summary>
public sealed class WhatsAppWebhookReaderTests
{
    [Fact]
    public void Inbound_messages_and_echoes_from_landlines_are_accepted()
    {
        var logger = new FakeLogger<WhatsAppWebhookReader>();
        var reader = new WhatsAppWebhookReader(new LibPhoneNumberParser(), logger);
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    changes = new object[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                metadata = new { phone_number_id = "pn-landline" },
                                contacts = new[] { new { wa_id = "541143215678", profile = new { name = "Estudio Sur" } } },
                                messages = new object[]
                                {
                                    new { id = "wamid.ar.raw", from = "541143215678", timestamp = "1791400000", type = "text", text = new { body = "Hola desde fijo" } },
                                    new { id = "wamid.ar.plus", from = "+541143215679", timestamp = "1791400001", type = "text", text = new { body = "Con más" } },
                                    new { id = "wamid.es.landline", from = "34912345678", timestamp = "1791400002", type = "text", text = new { body = "Fijo España" } },
                                    new { id = "wamid.ar.mobile", from = "5491134567890", timestamp = "1791400003", type = "text", text = new { body = "Móvil" } },
                                },
                            },
                        },
                        new
                        {
                            field = "smb_message_echoes",
                            value = new
                            {
                                metadata = new { phone_number_id = "pn-landline" },
                                message_echoes = new object[]
                                {
                                    new { id = "wamid.echo.ar", from = "5491100000000", to = "541143215678", timestamp = "1791400004", type = "text", text = new { body = "Respuesta a fijo AR" } },
                                    new { id = "wamid.echo.es", from = "5491100000000", to = "34912345678", timestamp = "1791400005", type = "text", text = new { body = "Respuesta a fijo ES" } },
                                },
                            },
                        },
                    },
                },
            },
        });

        var result = reader.Read(Encoding.UTF8.GetBytes(body));

        Assert.True(result.IsSuccess);
        Assert.Equal(["+541143215678", "+541143215679", "+34912345678", "+5491134567890"],
            result.Value.Messages.Select(message => message.Sender).ToArray());
        Assert.Equal("Estudio Sur", result.Value.Messages[0].ProfileName);
        Assert.Equal("Hola desde fijo", result.Value.Messages[0].Text);
        Assert.Equal(WhatsAppMessageKind.Text, result.Value.Messages[0].Kind);
        Assert.Equal(["+541143215678", "+34912345678"],
            result.Value.MessageEchoes.Select(echo => echo.Recipient).ToArray());
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public void Invalid_phones_and_missing_fields_are_skipped_and_logged_without_raw_phone_numbers()
    {
        const string invalidFrom = "54999999999999999";
        const string invalidTo = "59888888888888888";
        var logger = new FakeLogger<WhatsAppWebhookReader>();
        var reader = new WhatsAppWebhookReader(new LibPhoneNumberParser(), logger);
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    changes = new object[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                metadata = new { phone_number_id = "pn-invalid" },
                                messages = new object[]
                                {
                                    new { id = "wamid.bad.from", from = invalidFrom, timestamp = "1791400000", type = "text", text = new { body = "Inválido" } },
                                    new { unexpected = true },
                                    new { id = "wamid.ok", from = "541143215678", timestamp = "1791400001", type = "text", text = new { body = "Válido" } },
                                },
                            },
                        },
                        new
                        {
                            field = "smb_message_echoes",
                            value = new
                            {
                                metadata = new { phone_number_id = "pn-invalid" },
                                message_echoes = new object[]
                                {
                                    new { id = "wamid.bad.to", from = "5491100000000", to = invalidTo, timestamp = "1791400002", type = "text", text = new { body = "Eco inválido" } },
                                },
                            },
                        },
                    },
                },
            },
        });

        var result = reader.Read(Encoding.UTF8.GetBytes(body));

        Assert.True(result.IsSuccess);
        Assert.Equal("+541143215678", Assert.Single(result.Value.Messages).Sender);
        Assert.Empty(result.Value.MessageEchoes);
        var warnings = logger.Collector.GetSnapshot().Where(record => record.Level == LogLevel.Warning).ToArray();
        Assert.Equal(3, warnings.Length);
        Assert.All(warnings, record =>
        {
            Assert.DoesNotContain(invalidFrom, record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(invalidTo, record.Message, StringComparison.Ordinal);
            if (record.StructuredState is not null)
            {
                Assert.All(record.StructuredState, field =>
                {
                    Assert.DoesNotContain(invalidFrom, field.Value ?? string.Empty, StringComparison.Ordinal);
                    Assert.DoesNotContain(invalidTo, field.Value ?? string.Empty, StringComparison.Ordinal);
                });
            }
        });
    }
}
