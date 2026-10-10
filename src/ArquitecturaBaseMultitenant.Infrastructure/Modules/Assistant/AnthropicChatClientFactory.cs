using System.Security.Cryptography;
using Anthropic;
using Anthropic.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AnthropicChatClientFactory(IAssistantConfigurationPolicy policy, IAssistantSettingsReader settings,
    IAssistantKeyProtector protector, IOptions<AssistantOptions> options, ICurrentUser user, ApplicationDbContext context, IAssistantModelReader models) : IChatClientFactory
{
    public async Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
    {
        context.RequireNoTransaction();
        var resolved = await policy.ResolveAsync(cancellationToken);
        if (resolved.IsFailure) return resolved.Error;
        if (options.Value.UseFakeChatClient) return new FakeChatClient();
        var configuration = resolved.Value;
        try
        {
            var material = configuration.Mode == AssistantMode.OwnKey
                ? await settings.GetOrganizationKeyAsync(cancellationToken) : await settings.GetPlatformKeyAsync(cancellationToken);
            var key = material.Ciphertext is not null
                ? configuration.Mode == AssistantMode.OwnKey ? protector.UnprotectOrganization(material.Ciphertext) : protector.UnprotectPlatform(material.Ciphertext)
                : configuration.Mode == AssistantMode.PlatformKey && !material.HasOverride ? options.Value.Platform.ApiKey : null;
            if (string.IsNullOrWhiteSpace(key)) return AssistantErrors.SettingsNotConfigured;
            return Result.Success(Build(key, configuration.Model.ProviderModelId, configuration.Effort, AssistantTechnicalLimits.MaxOutputTokens));
        }
        catch (CryptographicException) { return AssistantErrors.SettingsNotConfigured; }
    }
    public async Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
    {
        context.RequireNoTransaction();
        if (user.UserId is null || user.Access is not (Domain.Users.Access.Business or Domain.Users.Access.Platform))
            throw new InvalidOperationException("Key validation requires an authorized configuration identity.");
        var platform = await settings.FindPlatformAsync(cancellationToken);
        var candidates = (await models.ListAllAsync(cancellationToken)).Where(value => AssistantModelCompatibilityPolicy.Supports(value, AssistantEffort.Low)).ToArray();
        var model = candidates.FirstOrDefault(value => value.Id == platform.DefaultModelId)
            ?? candidates.OrderBy(value => value.InputPricePerMillion.Amount + value.OutputPricePerMillion.Amount).ThenBy(value => value.Id).FirstOrDefault();
        if (model is null) return AssistantErrors.ModelNotAllowed;
        if (options.Value.UseFakeChatClient) return Result.Success();
        return await ValidateDraftAsync(key, model.ProviderModelId, null, cancellationToken);
    }
    internal static async Task<Result> ValidateDraftAsync(string key, string model, HttpClient? transport, CancellationToken cancellationToken)
    {
        try
        {
            using var client = Build(key, model, AssistantEffort.Low, AssistantTechnicalLimits.TitleMaxOutputTokens, transport);
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
    internal static IChatClient Build(string key, string model, AssistantEffort effort, int maxOutputTokens, HttpClient? transport = null)
    {
        var ownedTransport = transport ?? new HttpClient();
        var client = new AnthropicClient { ApiKey = key, MaxRetries = 0, Timeout = TimeSpan.FromSeconds(30), HttpClient = ownedTransport };
        return new AssistantChatClient(client.AsIChatClient(model, maxOutputTokens, AnthropicThinkingMode.Adaptive), client, ownedTransport, model, effort, maxOutputTokens);
    }
}
