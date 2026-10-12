using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Durable discovery is the authority; request lifetime is never a worker cancellation source.</summary>
internal sealed partial class AssistantTurnWorker(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<AssistantOptions> options, ILogger<AssistantTurnWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.TurnWorkerEnabled || System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider") return;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100), clock);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<AssistantTurnDiscovery> ready;
                await using (var discovery = scopes.CreateAsyncScope())
                    ready = await discovery.ServiceProvider.GetRequiredService<IAssistantTurnRepository>().ListReadyAsync(clock.GetUtcNow().UtcDateTime, stoppingToken);
                await Parallel.ForEachAsync(ready, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stoppingToken },
                    async (candidate, ct) => await ProcessAsync(candidate, ct));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailure(logger, exception.GetType().Name); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    private async Task ProcessAsync(AssistantTurnDiscovery candidate, CancellationToken hostToken)
    {
        var lease = Guid.NewGuid();
        await using var scope = scopes.CreateAsyncScope();
        var actors = scope.ServiceProvider.GetRequiredService<IExecutionActorFactory>();
        var prepared = await actors.RevalidateAsync(candidate.Actor, hostToken);
        if (prepared.IsFailure) { await RecoverAsync(candidate, lease); return; }
        using var actor = scope.ServiceProvider.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value);
        var service = scope.ServiceProvider.GetRequiredService<IAssistantTurnService>();
        var claimed = await service.ClaimAsync(candidate.TurnId, lease, hostToken);
        if (claimed.IsFailure)
        {
            if (claimed.Error.Code != AssistantErrors.TurnInProgress.Code) await RecoverAsync(candidate, lease);
            return;
        }
        var turn = claimed.Value;
        if (turn.RecoverOnly)
        {
            using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await service.CompleteAsync(turn.TurnId, lease, "Failed", AssistantErrors.ProviderUnavailable.Code, finish.Token);
            return;
        }
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
        execution.CancelAfter(TimeSpan.FromMinutes(3));
        Task monitor;
        using (ExecutionContext.SuppressFlow()) monitor = Task.Run(() => MonitorAsync(candidate, lease, execution), CancellationToken.None);
        string terminal = "Complete";
        string? code = null;
        try
        {
            // Resolve factory only after actor.Enter and after claim committed, without an ambient transaction.
            scope.ServiceProvider.GetRequiredService<IAssistantChatContext>().Binding = turn.Binding;
            var clientResult = await scope.ServiceProvider.GetRequiredService<IChatClientFactory>().CreateAsync(execution.Token);
            if (clientResult.IsFailure) { terminal = "Failed"; code = clientResult.Error.Code; }
            else
            {
                using var client = clientResult.Value;
                var messages = turn.Messages.ToList();
                for (var sequence = 1; sequence <= AssistantTechnicalLimits.MaxCallsPerTurn; sequence++)
                {
                    var authorized = await service.CheckExecutionAsync(turn.TurnId, lease, execution.Token);
                    if (authorized.IsFailure) { terminal = "Failed"; code = authorized.Error.Code; break; }
                    if (!authorized.Value) { terminal = "Stopped"; break; }
                    if (!AssistantTurnContextGuard.Fits(messages, turn.Tools, turn.Binding))
                    { terminal = "Failed"; code = AssistantErrors.ProviderContextTooLarge.Code; break; }
                    var begun = await service.BeginCallAsync(turn.TurnId, lease, sequence, execution.Token);
                    if (begun.IsFailure) { terminal = "Failed"; code = begun.Error.Code; break; }
                    // SDK-only context: retain every content block and let the official assembler merge
                    // streamed reasoning/signature fragments. Only TextContent reaches durable storage.
                    var updates = new List<ChatResponseUpdate>();
                    AssistantCallUsage? counts = null;
                    await foreach (var update in client.GetStreamingResponseAsync(messages,
                        new ChatOptions { Tools = turn.Tools.ToList(), MaxOutputTokens = AssistantTechnicalLimits.MaxOutputTokens }, execution.Token))
                    {
                        var text = string.Concat(update.Contents.OfType<TextContent>().Select(content => content.Text));
                        updates.Add(update);
                        foreach (var usage in update.Contents.OfType<UsageContent>())
                            counts = new AssistantCallUsage(usage.Details.InputTokenCount ?? counts?.InputTokens,
                                usage.Details.OutputTokenCount ?? counts?.OutputTokens,
                                usage.Details.CachedInputTokenCount ?? counts?.CacheReadTokens,
                                null, null, null, usage.Details.ReasoningTokenCount ?? counts?.ReasoningTokens, false);
                        var checkpoint = await service.CheckpointAsync(turn.TurnId, lease, text, counts, false, execution.Token);
                        if (checkpoint.IsFailure) { terminal = "Failed"; code = checkpoint.Error.Code; execution.Cancel(); break; }
                    }
                    if (execution.IsCancellationRequested) break;
                    var ended = await service.CheckpointAsync(turn.TurnId, lease, null, counts, true, execution.Token);
                    if (ended.IsFailure) { terminal = "Failed"; code = ended.Error.Code; break; }
                    var response = updates.ToChatResponse();
                    var calls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToList();
                    if (calls.Count == 0)
                    {
                        await GenerateTitleAsync(scope.ServiceProvider, service, turn, lease, execution.Token);
                        break;
                    }
                    if (sequence == AssistantTechnicalLimits.MaxCallsPerTurn || calls.Count > turn.Tools.Count)
                    { terminal = "Failed"; code = AssistantErrors.ProviderUnavailable.Code; break; }
                    messages.AddMessages(response);
                    var results = new List<AIContent>();
                    foreach (var function in calls)
                    {
                        if (!turn.Tools.Any(tool => tool.Name == function.Name))
                        { terminal = "Failed"; code = AssistantErrors.QuickQueryNotAvailable.Code; break; }
                        var running = await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Running, execution.Token);
                        if (running.IsFailure) { terminal = "Failed"; code = running.Error.Code; break; }
                        var arguments = JsonSerializer.SerializeToElement(function.Arguments ?? new Dictionary<string, object?>());
                        var result = await service.ExecuteToolAsync(turn.TurnId, lease, function.Name, arguments, execution.Token);
                        if (result.IsFailure)
                        {
                            terminal = "Failed"; code = result.Error.Code;
                            await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Failed, execution.Token);
                            break;
                        }
                        var completed = await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Complete, execution.Token);
                        if (completed.IsFailure) { terminal = "Failed"; code = completed.Error.Code; break; }
                        results.Add(new FunctionResultContent(function.CallId, result.Value.Data));
                    }
                    if (terminal == "Failed") break;
                    messages.Add(new ChatMessage(ChatRole.Tool, results));
                }
            }
        }
        catch (OperationCanceledException) { terminal = "Failed"; code = AssistantErrors.ProviderUnavailable.Code; }
        catch (Exception exception) { terminal = "Failed"; code = AssistantErrors.ProviderUnavailable.Code; LogFailure(logger, exception.GetType().Name); }
        finally
        {
            execution.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }
        // Separate bounded finalization token, independent of both browser and provider cancellation.
        using var finalization = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var closed = await service.CompleteAsync(turn.TurnId, lease, terminal, code, finalization.Token);
            if (closed.IsFailure) await RecoverAsync(candidate, lease);
        }
        catch (Exception exception) { LogFailure(logger, exception.GetType().Name); }
    }
    private async Task MonitorAsync(AssistantTurnDiscovery candidate, Guid lease, CancellationTokenSource execution)
    {
        var renewedAt = clock.GetTimestamp();
        try
        {
            while (!execution.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), clock, execution.Token);
                await using var scope = scopes.CreateAsyncScope();
                var actor = await scope.ServiceProvider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(candidate.Actor, execution.Token);
                if (actor.IsFailure) { execution.Cancel(); break; }
                using var entered = scope.ServiceProvider.GetRequiredService<IExecutionActorScope>().Enter(actor.Value);
                var service = scope.ServiceProvider.GetRequiredService<IAssistantTurnService>();
                var checkedTurn = await service.CheckExecutionAsync(candidate.TurnId, lease, execution.Token);
                if (checkedTurn.IsFailure || !checkedTurn.Value) { execution.Cancel(); break; }
                if (clock.GetElapsedTime(renewedAt) >= TimeSpan.FromSeconds(10))
                {
                    var renewed = await service.HeartbeatAsync(candidate.TurnId, lease, execution.Token);
                    if (renewed.IsFailure) { execution.Cancel(); break; }
                    renewedAt = clock.GetTimestamp();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { LogFailure(logger, exception.GetType().Name); execution.Cancel(); }
    }
    private static async Task GenerateTitleAsync(IServiceProvider services, IAssistantTurnService service,
        AssistantTurnExecution turn, Guid lease, CancellationToken ct)
    {
        var admitted = await service.BeginTitleAsync(turn.TurnId, lease, ct);
        if (admitted.IsFailure || admitted.Value is not { } titleCall) return;
        string? title = null;
        AssistantCallUsage? usage = null;
        try
        {
            services.GetRequiredService<IAssistantChatContext>().Binding = titleCall.Binding;
            var provided = await services.GetRequiredService<IChatClientFactory>().CreateAsync(ct);
            if (provided.IsSuccess)
            {
                using var client = provided.Value;
                if (AssistantTurnContextGuard.Fits(titleCall.Messages, [], titleCall.Binding))
                {
                    var response = await client.GetResponseAsync(titleCall.Messages,
                        new ChatOptions { MaxOutputTokens = AssistantTechnicalLimits.TitleMaxOutputTokens }, ct);
                    title = response.Text;
                    if (response.Usage is { } counts)
                        usage = new AssistantCallUsage(counts.InputTokenCount, counts.OutputTokenCount, counts.CachedInputTokenCount,
                            null, null, null, counts.ReasoningTokenCount, false);
                }
                else usage = new AssistantCallUsage(0, 0, 0, 0, 0, 0, 0, true);
            }
            else usage = new AssistantCallUsage(0, 0, 0, 0, 0, 0, 0, true);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* The admitted call remains conservatively billable. */ }
        await service.FinishTitleAsync(turn.TurnId, lease, titleCall.ConversationVersion, title, usage, ct);
        services.GetRequiredService<IAssistantChatContext>().Binding = turn.Binding;
    }
    private async Task RecoverAsync(AssistantTurnDiscovery candidate, Guid lease)
    {
        // Trusted technical closure uses original scope only; no actor, chat, or provider is resolved here.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var scope = scopes.CreateAsyncScope();
        using var entered = candidate.Actor.TenantId is { } tenantId ? scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId) : null;
        var result = await scope.ServiceProvider.GetRequiredService<IAssistantTurnService>().RecoverAsync(candidate.TurnId, lease, timeout.Token);
        if (result.IsFailure) LogFailure(logger, result.Error.Code);
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistant turn worker failed: {Failure}")]
    private static partial void LogFailure(ILogger logger, string failure);
}
