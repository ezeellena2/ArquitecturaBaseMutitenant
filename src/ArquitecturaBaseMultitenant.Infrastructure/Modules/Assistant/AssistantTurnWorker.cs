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
        var run = new TurnRun(scope.ServiceProvider, service, candidate, turn, lease);
        var outcome = await RunTurnAsync(run, hostToken);
        AssistantTracing.MarkOutcome(activity, outcome.Terminal, outcome.Code);
        await FinalizeAsync(run, outcome, activity);
    }

    /// <summary>
    /// Corre el turno con su plazo y el monitor que lo corta, y devuelve cómo terminó. El plazo del turno, el apagado o la persona
    /// cortan con el token de quien llama: no es una respuesta del proveedor. AssistantChatClient entrega cada falla del proveedor
    /// con su estado HTTP o su transporte, sin el cuerpo (0064 §7).
    /// </summary>
    private async Task<TurnOutcome> RunTurnAsync(TurnRun run, CancellationToken hostToken)
    {
        using var deadline = CreateDeadline(run.Turn.DeadlineAtUtc, Clock);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(hostToken, deadline.Token);
        Task monitor;
        using (ExecutionContext.SuppressFlow()) monitor = Task.Run(() => MonitorAsync(run.Candidate, run.Lease, execution), CancellationToken.None);
        var outcome = new TurnOutcome();
        try
        {
            outcome.SettingsVersion = await SettingsVersionAsync(run.Services, run.Turn.Binding.Mode, execution.Token);
            // Resolve factory only after actor.Enter and after claim committed, without an ambient transaction.
            run.Services.GetRequiredService<IAssistantChatContext>().Binding = run.Turn.Binding;
            var clientResult = await run.Services.GetRequiredService<IChatClientFactory>().CreateAsync(execution.Token);
            if (clientResult.IsFailure) outcome.Fail(clientResult.Error.Code);
            else
            {
                using var client = clientResult.Value;
                await RunCallsAsync(run, client, outcome, execution);
            }
        }
        catch (OperationCanceledException) { outcome.Interrupt(); }
        catch (HttpRequestException failure) { outcome.FailByProvider(AssistantProviderFailureClassifier.Classify(failure)); }
        catch (Exception exception) { outcome.Interrupt(); ExceptionLog.Write(logger, "AssistantTurn", exception); }
        finally
        {
            execution.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }
        return outcome;
    }

    /// <summary>Los pasos del turno, de a una llamada al proveedor y sus herramientas, hasta la respuesta final o un corte.</summary>
    private static async Task RunCallsAsync(TurnRun run, IChatClient client, TurnOutcome outcome, CancellationTokenSource execution)
    {
        var (service, turn, lease) = (run.Service, run.Turn, run.Lease);
        var messages = turn.Messages.ToList();
        for (var sequence = 1; sequence <= AssistantTechnicalLimits.MaxCallsPerTurn; sequence++)
        {
            var authorized = await service.CheckExecutionAsync(turn.TurnId, lease, execution.Token);
            if (authorized.IsFailure) { outcome.Fail(authorized.Error.Code); return; }
            if (!authorized.Value) { outcome.Stop(); return; }
            if (!AssistantTurnContextGuard.Fits(messages, turn.Tools, turn.Binding)) { outcome.Fail(AssistantErrors.ProviderContextTooLarge.Code); return; }
            var begun = await service.BeginCallAsync(turn.TurnId, lease, sequence, execution.Token);
            if (begun.IsFailure) { outcome.Fail(begun.Error.Code); return; }
            outcome.InFlight = true;
            var (updates, counts) = await StreamAsync(run, client, messages, outcome, execution);
            // Cortado justo al terminar el stream (el plazo o el apagado): el paso salió y se cobra, así que es incierto.
            if (execution.IsCancellationRequested)
            {
                if (outcome.IsComplete) outcome.Interrupt();
                return;
            }
            var ended = await service.CheckpointAsync(turn.TurnId, lease, null, counts, true, execution.Token);
            if (ended.IsFailure) { outcome.Fail(ended.Error.Code); return; }
            outcome.InFlight = false;
            var response = updates.ToChatResponse();
            var calls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToList();
            if (calls.Count == 0)
            {
                await GenerateTitleAsync(run.Services, service, turn, lease, execution.Token);
                return;
            }
            if (sequence == AssistantTechnicalLimits.MaxCallsPerTurn || calls.Count > AssistantTechnicalLimits.MaxCallsPerTurn)
            { outcome.Fail(AssistantErrors.ProviderUnavailable.Code); return; }
            messages.AddMessages(response);
            if (await RunToolsAsync(run, calls, outcome, execution.Token) is not { } results) return;
            messages.Add(new ChatMessage(ChatRole.Tool, results));
        }
    }

    /// <summary>
    /// Una llamada al proveedor, en stream: cada parte y sus cantidades quedan anotadas; si una anotación falla, el turno queda
    /// fallado y se corta. SDK-only context: retain every content block and let the official assembler merge streamed
    /// reasoning/signature fragments. Only TextContent reaches durable storage.
    /// </summary>
    private static async Task<(List<ChatResponseUpdate> Updates, AssistantCallUsage? Counts)> StreamAsync(TurnRun run, IChatClient client,
        List<ChatMessage> messages, TurnOutcome outcome, CancellationTokenSource execution)
    {
        var updates = new List<ChatResponseUpdate>();
        AssistantCallUsage? counts = null;
        await foreach (var update in client.GetStreamingResponseAsync(messages,
            new ChatOptions { Tools = run.Turn.Tools.ToList(), MaxOutputTokens = AssistantTechnicalLimits.MaxOutputTokens }, execution.Token))
        {
            var text = string.Concat(update.Contents.OfType<TextContent>().Select(content => content.Text));
            updates.Add(update);
            // El cliente no envía cache_control, así que no hay escrituras de caché (0, 0, 0) y el desglose está completo (KAN-73).
            foreach (var usage in update.Contents.OfType<UsageContent>())
                counts = new AssistantCallUsage(usage.Details.InputTokenCount ?? counts?.InputTokens,
                    usage.Details.OutputTokenCount ?? counts?.OutputTokens,
                    usage.Details.CachedInputTokenCount ?? counts?.CacheReadTokens ?? 0,
                    0, 0, 0, usage.Details.ReasoningTokenCount ?? counts?.ReasoningTokens, true);
            var checkpoint = await run.Service.CheckpointAsync(run.Turn.TurnId, run.Lease, text, counts, false, execution.Token);
            if (checkpoint.IsFailure) { outcome.Fail(checkpoint.Error.Code); execution.Cancel(); break; }
        }
        return (updates, counts);
    }

    /// <summary>Las herramientas que pidió el proveedor, de a una y anotadas; null si una falla, con el turno fallado.</summary>
    private static async Task<List<AIContent>?> RunToolsAsync(TurnRun run, IReadOnlyList<FunctionCallContent> calls, TurnOutcome outcome,
        CancellationToken ct)
    {
        var (service, turn, lease) = (run.Service, run.Turn, run.Lease);
        var results = new List<AIContent>();
        foreach (var function in calls)
        {
            if (!turn.Tools.Any(tool => tool.Name == function.Name)) { outcome.Fail(AssistantErrors.QuickQueryNotAvailable.Code); return null; }
            var running = await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Running, ct);
            if (running.IsFailure) { outcome.Fail(running.Error.Code); return null; }
            var arguments = JsonSerializer.SerializeToElement(function.Arguments ?? new Dictionary<string, object?>());
            var result = await service.ExecuteToolAsync(turn.TurnId, lease, function.Name, arguments, ct);
            if (result.IsFailure)
            {
                outcome.Fail(result.Error.Code);
                await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Failed, ct);
                return null;
            }
            var completed = await service.RecordToolAsync(turn.TurnId, lease, function.Name, AssistantToolState.Complete, ct);
            if (completed.IsFailure) { outcome.Fail(completed.Error.Code); return null; }
            results.Add(new FunctionResultContent(function.CallId, result.Value.Data));
        }
        return results;
    }

    /// <summary>
    /// Cierra el turno con un plazo propio, independiente del navegador y del proveedor. Sin conexión no salió nada: la llamada
    /// marcada no se cobra (0064 §7). Cerrado, anota para quien paga lo que respondió el proveedor, si leyó su configuración.
    /// </summary>
    private async Task FinalizeAsync(TurnRun run, TurnOutcome outcome, System.Diagnostics.Activity? activity)
    {
        using var finalization = new CancellationTokenSource(options.Value.FinalizationTimeout, Clock);
        try
        {
            if (outcome.ProviderFailure == AssistantErrors.ProviderUnavailableCode)
                await run.Service.CheckpointAsync(run.Turn.TurnId, run.Lease, null, NothingSent, true, finalization.Token);
            var closed = await run.Service.CompleteAsync(run.Turn.TurnId, run.Lease, outcome.Terminal, outcome.Code, finalization.Token);
            if (closed.IsFailure) await RecoverAsync(run.Candidate, run.Lease, activity);
            else if (outcome.SettingsVersion is { } version && (outcome.IsComplete || outcome.ProviderFailure is not null))
                await RecordProviderOutcomeAsync(run.Services, run.Turn.Binding.Mode, outcome.IsComplete ? null : outcome.ProviderFailure,
                    version, finalization.Token);
        }
        catch (Exception exception) { ExceptionLog.Write(logger, "AssistantTurn", exception); }
    }

    /// <summary>Lo que el turno usa mientras corre, del scope del actor que lo reclamó.</summary>
    private sealed record TurnRun(IServiceProvider Services, IAssistantTurnService Service, AssistantTurnDiscovery Candidate,
        AssistantTurnExecution Turn, Guid Lease);

    /// <summary>
    /// Cómo termina un turno: el estado y el código con que se cierra; la falla que dijo el proveedor, aparte de las demás,
    /// porque solo esa se anota para quien paga (0064 §7); si un paso reservado no terminó, que pudo llegar al proveedor y se
    /// cobra (0064 §1), y la versión de la configuración de quien paga leída antes de armar el cliente: si alguien la guarda
    /// mientras el turno espera, lo que responda el proveedor es de la clave de antes y no se anota sobre la nueva (B13).
    /// </summary>
    private sealed class TurnOutcome
    {
        internal string Terminal { get; private set; } = "Complete";
        internal string? Code { get; private set; }
        internal string? ProviderFailure { get; private set; }
        internal bool InFlight { get; set; }
        internal uint? SettingsVersion { get; set; }
        internal bool IsComplete => Terminal == "Complete";
        internal void Fail(string code) => (Terminal, Code) = ("Failed", code);
        internal void Stop() => Terminal = "Stopped";
        internal void FailByProvider(string code) { Fail(code); ProviderFailure = code; }
        /// <summary>Cortado: incierto si un paso reservado pudo llegar al proveedor; si no, no disponible.</summary>
        internal void Interrupt() => Fail(InFlight ? AssistantErrors.ProviderUncertainCode : AssistantErrors.ProviderUnavailable.Code);
    }

    private static readonly AssistantCallUsage NothingSent = new(0, 0, 0, 0, 0, 0, 0, true);
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
                        usage = new AssistantCallUsage(counts.InputTokenCount, counts.OutputTokenCount, counts.CachedInputTokenCount ?? 0,
                            0, 0, 0, counts.ReasoningTokenCount, true);
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
