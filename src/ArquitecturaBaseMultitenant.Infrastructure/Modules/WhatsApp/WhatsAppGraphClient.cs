using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>
/// La Graph API del alta embebida (v4) y del medio de pago. El cliente HTTP no registra URLs: el canje lleva el secreto de
/// la app y el código en la consulta. De Meta se conserva solo el código numérico del error.
/// </summary>
internal sealed class WhatsAppGraphClient(HttpClient client, IOptions<WhatsAppOptions> options) : IWhatsAppGraphClient
{
    /// <summary>El código de Meta para un parámetro inválido: en el canje, el código del alta venció o ya se usó.</summary>
    private const string InvalidParameter = "100";

    public async Task<Result<WhatsAppSignupResult>> CompleteSignupAsync(WhatsAppSignupInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.AppSecret))
            return WhatsAppErrors.NumberMetaRejected("configuration");
        var exchange = await SendAsync(HttpMethod.Get, "oauth/access_token?client_id=" + Uri.EscapeDataString(settings.AppId)
            + "&client_secret=" + Uri.EscapeDataString(settings.AppSecret) + "&code=" + Uri.EscapeDataString(input.Code), null, null, ct);
        if (exchange.IsFailure)
            return exchange.Error.Metadata?.GetValueOrDefault("metaCode") as string == InvalidParameter
                ? WhatsAppErrors.NumberSignupExpired : exchange.Error;
        using var exchanged = exchange.Value;
        if (Text(exchanged.RootElement, "access_token") is not { Length: > 0 } token) return WhatsAppErrors.NumberMetaRejected("token");
        var subscribed = await SendAsync(HttpMethod.Post, Uri.EscapeDataString(input.WabaId) + "/subscribed_apps", token, null, ct);
        if (subscribed.IsFailure) return subscribed.Error;
        subscribed.Value.Dispose();
        // En coexistencia el FINISH de Meta puede traer solo la cuenta: el número es el que la cuenta tiene registrado.
        var phoneNumberId = input.PhoneNumberId;
        if (phoneNumberId is null)
        {
            var numbers = await SendAsync(HttpMethod.Get, Uri.EscapeDataString(input.WabaId) + "/phone_numbers?fields=id", token, null, ct);
            if (numbers.IsFailure) return numbers.Error;
            using var list = numbers.Value;
            phoneNumberId = list.RootElement.ValueKind == JsonValueKind.Object && list.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0 ? Text(data[0], "id") : null;
            if (phoneNumberId is not { Length: > 0 and <= ArquitecturaBaseMultitenant.Domain.Common.TextLimits.ShortName })
                return WhatsAppErrors.NumberMetaRejected("phone_numbers");
        }
        string? pin = null;
        if (!input.Coexistence)
        {
            pin = string.Create(6, 0, static (digits, _) => { for (var index = 0; index < digits.Length; index++) digits[index] = (char)('0' + RandomNumberGenerator.GetInt32(10)); });
            var registered = await SendAsync(HttpMethod.Post, Uri.EscapeDataString(phoneNumberId) + "/register", token,
                new { messaging_product = "whatsapp", pin }, ct);
            if (registered.IsFailure) return registered.Error;
            registered.Value.Dispose();
        }
        var phone = await SendAsync(HttpMethod.Get, Uri.EscapeDataString(phoneNumberId) + "?fields=display_phone_number,verified_name", token, null, ct);
        if (phone.IsFailure) return phone.Error;
        using var phoneData = phone.Value;
        var display = PhoneNumber.Create("+" + string.Concat((Text(phoneData.RootElement, "display_phone_number") ?? string.Empty).Where(char.IsAsciiDigit)));
        if (display.IsFailure) return WhatsAppErrors.NumberMetaRejected("display_phone_number");
        var payment = await HasPaymentAsync(input.WabaId, token, ct);
        if (payment.IsFailure) return payment.Error;
        return new WhatsAppSignupResult(phoneNumberId, token, pin, display.Value, Text(phoneData.RootElement, "verified_name"), payment.Value);
    }

    public async Task<Result<bool>> HasPaymentAsync(string wabaId, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wabaId);
        var account = await SendAsync(HttpMethod.Get, Uri.EscapeDataString(wabaId) + "?fields=primary_funding_id,timezone_id", token, null, ct);
        if (account.IsFailure) return account.Error;
        using var data = account.Value;
        return Text(data.RootElement, "primary_funding_id") is { Length: > 0 };
    }

    private async Task<Result<JsonDocument>> SendAsync(HttpMethod method, string path, string? token, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, options.Value.GraphApiVersion + "/" + path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await client.SendAsync(request, ct);
            JsonDocument document;
            try { document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct); }
            catch (JsonException) { return WhatsAppErrors.NumberMetaRejected("invalid_response"); }
            if (response.IsSuccessStatusCode) return document;
            using (document)
            {
                // Solo el código numérico de Meta, tal como vino; nunca su mensaje.
                var code = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var value) && value.TryGetInt32(out _)
                    ? value.GetRawText() : "http";
                return WhatsAppErrors.NumberMetaRejected(code);
            }
        }
        catch (HttpRequestException) { return WhatsAppErrors.NumberMetaRejected("network"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return WhatsAppErrors.NumberMetaRejected("timeout"); }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
