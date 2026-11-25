using ArquitecturaBaseMultitenant.Application.Common.Logging;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Durable discovery is the authority; request lifetime is never a worker cancellation source.</summary>
internal sealed partial class AssistantTurnWorker(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<AssistantOptions> options, AiKeyRingReadiness keyRing, ILogger<AssistantTurnWorker> logger) : PeriodicWorker(options.Value.WorkerInterval, clock, logger)
{
    protected override bool Enabled => options.Value.TurnWorkerEnabled;
    protected override async Task RunIterationAsync(CancellationToken stoppingToken)
    {
        // 0064 §11: una copia con otro anillo, o que todavía no lo pudo comprobar, no toma turnos.
        if (!await keyRing.MatchesAsync(stoppingToken)) return;
        IReadOnlyList<AssistantTurnDiscovery> ready;
        await using (var discovery = scopes.CreateAsyncScope())
            ready = await discovery.ServiceProvider.GetRequiredService<IAssistantTurnRepository>().ListReadyAsync(Clock.GetUtcNow().UtcDateTime, stoppingToken);
        await Parallel.ForEachAsync(ready, new ParallelOptions { MaxDegreeOfParallelism = AssistantTechnicalLimits.TurnsPerCopy, CancellationToken = stoppingToken },
            async (candidate, ct) =>
            {
                // Un turno que falla fuera de su ejecución (sus datos, su cierre) se anota y se reintenta en la vuelta siguiente:
                // si saliera, Parallel cancelaría los turnos de otras personas que corren a la par y los cerraría como cortados.
                try { await ProcessAsync(candidate, ct); }
                catch (Exception exception) when (!ct.IsCancellationRequested) { ExceptionLog.Write(logger, "AssistantTurn", exception); }
            });
    }

    private async Task ProcessAsync(AssistantTurnDiscovery candidate, CancellationToken hostToken)
    {
        // 0064 §11: una actividad por turno, hija de la traza de quien lo pidió; lo que llama adentro (el proveedor, la base) cuelga de ella.
        using var activity = AssistantTracing.StartTurn(candidate.TurnId, candidate.TraceParent);
        var lease = Guid.NewGuid();
        // Un turno de un motor más nuevo no se arma con este: solo se cierra, vencido y sin llamar al proveedor (0064 §6, B12).
        if (!candidate.KnownEngine) { await RecoverAsync(candidate, lease, activity); return; }
        await using var scope = scopes.CreateAsyncScope();
        var actors = scope.ServiceProvider.GetRequiredService<IExecutionActorFactory>();
        var prepared = await actors.RevalidateAsync(candidate.Actor, hostToken);
        if (prepared.IsFailure) { await RecoverAsync(candidate, lease, activity); return; }
        using var actor = scope.ServiceProvider.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value);
        var service = scope.ServiceProvider.GetRequiredService<IAssistantTurnService>();
        var claimed = await service.ClaimAsync(candidate.TurnId, lease, hostToken);
        if (claimed.IsFailure)
        {
            if (claimed.Error.Code != AssistantErrors.TurnInProgress.Code) await RecoverAsync(candidate, lease, activity);
            return;
        }
        var turn = claimed.Value;
        if (turn.RecoverOnly)
        {
            using var finish = new CancellationTokenSource(options.Value.FinalizationTimeout, Clock);
            AssistantTracing.MarkOutcome(activity, "Failed", AssistantErrors.ProviderUnavailable.Code);
            await service.CompleteAsync(turn.TurnId, lease, "Failed", AssistantErrors.ProviderUnavailable.Code, finish.Token);
            return;
        }
        using var deadline = CreateDeadline(turn.DeadlineAtUtc, Clock);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(hostToken, deadline.Token);
        Task monitor;
        using (ExecutionContext.SuppressFlow()) monitor = Task.Run(() => MonitorAsync(candidate, lease, execution), CancellationToken.None);
        string terminal = "Complete";
        string? code = null;
        // Lo que dijo el proveedor (0064 §7), aparte de las demás fallas: solo eso se anota para quien paga.
        string? providerFailure = null;
        // La versión de la configuración de quien paga, leída antes de armar el cliente: si alguien la guarda mientras el turno
        // espera, lo que responda el proveedor es de la clave de antes y no se anota sobre la nueva (B13). Sin ella, no se anota.
        uint? settingsVersion = null;
        // Un paso reservado que no terminó pudo llegar al proveedor y se cobra (0064 §1): cortado, es incierto, no «no disponible».
        var inFlight = false;
        try
        {
            settingsVersion = await SettingsVersionAsync(scope.ServiceProvider, turn.Binding.Mode, execution.Token);
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
                    inFlight = true;
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
                    // Cortado justo al terminar el stream (el plazo o el apagado): el paso salió y se cobra, así que es incierto.
                    if (execution.IsCancellationRequested)
                    {
                        if (terminal == "Complete") { terminal = "Failed"; code = Interrupted(inFlight); }
                        break;
                    }
                    var ended = await service.CheckpointAsync(turn.TurnId, lease, null, counts, true, execution.Token);
                    if (ended.IsFailure) { terminal = "Failed"; code = ended.Error.Code; break; }
                    inFlight = false;
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
        // El plazo del turno, el apagado o la persona cortan con el token de quien llama: no es una respuesta del proveedor.
        catch (OperationCanceledException) { terminal = "Failed"; code = Interrupted(inFlight); }
        // AssistantChatClient entrega cada falla del proveedor con su estado HTTP o su transporte, sin el cuerpo (0064 §7).
        catch (HttpRequestException failure) { terminal = "Failed"; code = providerFailure = AssistantProviderFailureClassifier.Classify(failure); }
        catch (Exception exception) { terminal = "Failed"; code = Interrupted(inFlight); ExceptionLog.Write(logger, "AssistantTurn", exception); }
        finally
        {
            execution.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }
        AssistantTracing.MarkOutcome(activity, terminal, code);
        // Separate bounded finalization token, independent of both browser and provider cancellation.
        using var finalization = new CancellationTokenSource(options.Value.FinalizationTimeout, Clock);
        try
        {
            // Sin conexión no salió nada: la llamada marcada no se cobra (0064 §7).
            if (providerFailure == AssistantErrors.ProviderUnavailableCode)
                await service.CheckpointAsync(turn.TurnId, lease, null, NothingSent, true, finalization.Token);
            var closed = await service.CompleteAsync(turn.TurnId, lease, terminal, code, finalization.Token);
            if (closed.IsFailure) await RecoverAsync(candidate, lease, activity);
            else if (settingsVersion is { } version && (terminal == "Complete" || providerFailure is not null))
                await RecordProviderOutcomeAsync(scope.ServiceProvider, turn.Binding.Mode, terminal == "Complete" ? null : providerFailure, version, finalization.Token);
        }
        catch (Exception exception) { ExceptionLog.Write(logger, "AssistantTurn", exception); }
    }
    private static readonly AssistantCallUsage NothingSent = new(0, 0, 0, 0, 0, 0, 0, true);
    private static string Interrupted(bool inFlight) => inFlight ? AssistantErrors.ProviderUncertainCode : AssistantErrors.ProviderUnavailable.Code;
    /// <summary>Si no se puede leer, el turno sigue igual (una falla pasajera de la base no lo cancela) y su resultado no se anota.</summary>
    private async Task<uint?> SettingsVersionAsync(IServiceProvider services, AssistantMode funding, CancellationToken ct)
    {
        try
        {
            var read = await services.GetRequiredService<IAssistantSettingsService>().GetSettingsVersionAsync(funding, ct);
            return read.IsSuccess ? read.Value : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { ExceptionLog.Write(logger, "AssistantProviderOutcome", exception); return null; }
    }
    /// <summary>
    /// Fuera del turno, que ya quedó cerrado: la falla queda anotada para quien paga, o se limpia si respondió, sin cambiar la
    /// versión de su configuración. Si no se puede escribir, el turno no cambia.
    /// </summary>
    private async Task RecordProviderOutcomeAsync(IServiceProvider services, AssistantMode funding, string? failure, uint settingsVersion, CancellationToken ct)
    {
        try
        {
            var recorded = await services.GetRequiredService<IAssistantSettingsService>().RecordProviderOutcomeAsync(funding, failure, settingsVersion, ct);
            if (recorded.IsFailure) LogFailure(logger, recorded.Error.Code);
        }
        catch (Exception exception) { ExceptionLog.Write(logger, "AssistantProviderOutcome", exception); }
    }
    private async Task MonitorAsync(AssistantTurnDiscovery candidate, Guid lease, CancellationTokenSource execution)
    {
        var renewedAt = Clock.GetTimestamp();
        try
        {
            while (!execution.IsCancellationRequested)
            {
                await Task.Delay(options.Value.ExecutionCheckInterval, Clock, execution.Token);
                await using var scope = scopes.CreateAsyncScope();
                var actor = await scope.ServiceProvider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(candidate.Actor, execution.Token);
                if (actor.IsFailure) { execution.Cancel(); break; }
                using var entered = scope.ServiceProvider.GetRequiredService<IExecutionActorScope>().Enter(actor.Value);
                var service = scope.ServiceProvider.GetRequiredService<IAssistantTurnService>();
                var checkedTurn = await service.CheckExecutionAsync(candidate.TurnId, lease, execution.Token);
                if (checkedTurn.IsFailure || !checkedTurn.Value) { execution.Cancel(); break; }
                if (Clock.GetElapsedTime(renewedAt) >= options.Value.LeaseHeartbeatInterval)
                {
                    var renewed = await service.HeartbeatAsync(candidate.TurnId, lease, execution.Token);
                    if (renewed.IsFailure) { execution.Cancel(); break; }
                    renewedAt = Clock.GetTimestamp();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { ExceptionLog.Write(logger, "AssistantTurn", exception); execution.Cancel(); }
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
    private async Task RecoverAsync(AssistantTurnDiscovery candidate, Guid lease, System.Diagnostics.Activity? activity)
    {
        // Se cierra sin completarse: en la traza es un error.
        AssistantTracing.MarkOutcome(activity, "Failed", null);
        // Trusted technical closure uses original scope only; no actor, chat, or provider is resolved here.
        using var timeout = new CancellationTokenSource(options.Value.FinalizationTimeout, Clock);
        await using var scope = scopes.CreateAsyncScope();
        async Task Recover(IServiceProvider provider, CancellationToken ct)
        {
            var result = await provider.GetRequiredService<IAssistantTurnService>().RecoverAsync(candidate.TurnId, lease, ct);
            if (result.IsFailure) LogFailure(logger, result.Error.Code);
        }
        if (candidate.Actor.TenantId is { } tenantId)
            await scope.ServiceProvider.GetRequiredService<TenantJobRunner>().RunAsync([tenantId], Recover, timeout.Token);
        else await Recover(scope.ServiceProvider, timeout.Token);
    }
    internal static CancellationTokenSource CreateDeadline(DateTime deadlineAtUtc, TimeProvider clock)
    {
        var remaining = deadlineAtUtc - clock.GetUtcNow().UtcDateTime;
        if (remaining > TimeSpan.Zero) return new CancellationTokenSource(remaining, clock);
        var expired = new CancellationTokenSource();
        expired.Cancel();
        return expired;
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistant turn worker failed: {Failure}")]
    private static partial void LogFailure(ILogger logger, string failure);
}
