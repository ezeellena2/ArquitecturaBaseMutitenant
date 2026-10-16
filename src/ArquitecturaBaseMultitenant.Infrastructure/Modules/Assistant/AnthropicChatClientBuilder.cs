using Anthropic;
using Anthropic.Exceptions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// Arma el cliente del SDK de Anthropic (E9): cada cliente recibe un HttpClient propio del transporte nombrado, sobre el
/// handler que reutiliza el factory, así la clave de un pedido nunca queda en encabezados que comparta otro. El transporte
/// no suma reintentos ni loggers HTTP, el SDK tampoco reintenta, y ningún cliente se arma con una transacción abierta.
/// </summary>
internal sealed class AnthropicChatClientBuilder(IHttpClientFactory transports, IOptions<AssistantOptions> options, ApplicationDbContext context)
{
    internal const string TransportName = "Assistant.Anthropic";

    public IChatClient Build(string key, string model, AssistantEffort effort, int maxOutputTokens)
    {
        context.RequireNoTransaction();
        return Build(key, model, effort, maxOutputTokens, transports.CreateClient(TransportName), options.Value.ProviderTimeout);
    }

    public Task<Result> ValidateDraftAsync(string key, string model, CancellationToken cancellationToken)
    {
        context.RequireNoTransaction();
        return ValidateDraftAsync(key, model, transports.CreateClient(TransportName), cancellationToken, options.Value.ProviderTimeout);
    }

    internal static async Task<Result> ValidateDraftAsync(string key, string model, HttpClient transport, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        try
        {
            using var client = Build(key, model, AssistantEffort.Low, AssistantTechnicalLimits.TitleMaxOutputTokens, transport, timeout);
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Reply OK.")], cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (AnthropicUnauthorizedException) { return AssistantErrors.SettingsKeyRejected; }
        catch (AnthropicForbiddenException) { return AssistantErrors.SettingsKeyRejected; }
        catch (AnthropicBadRequestException) { return AssistantErrors.SettingsKeyRejected; }
        catch (AnthropicException) { return AssistantErrors.ProviderUnavailable; }
        catch (HttpRequestException error) when (error.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.BadRequest)
        { return AssistantErrors.SettingsKeyRejected; }
        catch (HttpRequestException) { return AssistantErrors.ProviderUnavailable; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return AssistantErrors.ProviderUnavailable; }
    }

    internal static IChatClient Build(string key, string model, AssistantEffort effort, int maxOutputTokens, HttpClient transport, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var client = new AnthropicClient { ApiKey = key, MaxRetries = 0, Timeout = timeout ?? new AssistantOptions().ProviderTimeout, HttpClient = transport };
        return new AssistantChatClient(client.AsIChatClient(model, maxOutputTokens, AnthropicThinkingMode.Adaptive), client, transport, model, effort, maxOutputTokens);
    }
}
