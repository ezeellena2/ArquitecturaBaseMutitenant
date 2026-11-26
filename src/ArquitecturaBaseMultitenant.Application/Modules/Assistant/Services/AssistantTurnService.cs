using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnService(AssistantTurnWriter writer, AssistantTurnBuilder builder, AssistantTurnToolBuilder tools,
    IAssistantAttachmentContentReader attachments, ICurrentUser user, AssistantRequestTimeBuilder requestTime,
    IAssistantReadOnlyScope readOnly, AssistantProposalWriter proposals, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<AssistantTurnService> logger) : IAssistantTurnService
{
    public Task<Result<AssistantTurnExecution>> ClaimAsync(Guid id, Guid lease, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantTurnExecution>(logger, clock, "AssistantTurnClaim", async () =>
        {
            var result = await unitOfWork.ExecuteInTransactionAsync(c => writer.ClaimAsync(id, lease, c), CommitPolicy.OnSuccess, ct);
            if (result.IsFailure || result.Value.RecoverOnly) return result;
            var messages = result.Value.Messages.ToList();
            var context = result.Value.ContextMessages ?? [];
            for (var index = 0; index < context.Count; index++)
            {
                if (context[index].Attachments.Count == 0) continue;
                var content = await attachments.ReadAsync(user.UserId!.Value, user.Access!.Value, result.Value.ConversationId,
                    context[index].Attachments.Select(row => row.Id).ToArray(), ct);
                if (content.IsFailure) return content.Error;
                foreach (var file in content.Value) messages[index + 1].Contents.Add(new Microsoft.Extensions.AI.DataContent(file.Bytes, file.MediaType));
            }
            // 0068 §7: el mensaje con una propuesta lleva la acción y lo que escribió el modelo, nunca lo resuelto ni los nombres.
            // La conversación entra entera (o el turno falla con ContextTooLarge), así la última propuesta nunca queda afuera.
            var written = await builder.ProposalHistoryAsync(result.Value.ConversationId, context.Select(row => row.Id).ToArray(), ct);
            for (var index = 0; index < context.Count; index++)
                if (written.TryGetValue(context[index].Id, out var proposal))
                    messages[index + 1].Contents.Add(new Microsoft.Extensions.AI.TextContent(proposal));
            // 0064 §9: la hora del pedido y la zona del perfil, al final del último mensaje de la persona, como el canal (0054 §1).
            var asked = -1;
            for (var index = 0; index < context.Count; index++) if (context[index].Role == AssistantMessageRole.User) asked = index;
            if (asked >= 0 && result.Value.Requester is { } requester)
                messages[asked + 1].Contents.Add(new Microsoft.Extensions.AI.TextContent(
                    await requestTime.BuildAsync(context[asked].CreatedAtUtc, requester, ct)));
            // 0068 §7: la lista precargada de lo sellado que se sigue ofreciendo, al final del pedido y como resultado de una
            // herramienta: es un dato, nunca una instrucción. Sin nada que ofrecer, el turno va sin las herramientas de acciones.
            AssistantToolExecution? preloaded = null;
            if (result.Value.OfferedActions is { Count: > 0 } offered)
                preloaded = await readOnly.RunAsync<AssistantTurnToolBuilder, AssistantToolExecution?>((inner, c) =>
                    inner.PreloadActionsAsync(new(offered, result.Value.Binding.ProviderModelId, false), c), ct);
            if (preloaded is not null)
            {
                messages.Add(new(Microsoft.Extensions.AI.ChatRole.Assistant,
                    [new Microsoft.Extensions.AI.FunctionCallContent(AssistantActionTools.PreloadCallId, AssistantActionTools.ListKey)]));
                messages.Add(new(Microsoft.Extensions.AI.ChatRole.Tool,
                    [new Microsoft.Extensions.AI.FunctionResultContent(AssistantActionTools.PreloadCallId, preloaded.Data)]));
            }
            return result.Value with { Tools = await tools.BuildAsync(preloaded is not null, ct), Messages = messages, ContextMessages = null };
        });
    public Task<Result> BeginCallAsync(Guid id, Guid lease, int sequence, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnBeginCall", () => unitOfWork.ExecuteInTransactionAsync(c => writer.BeginCallAsync(id, lease, sequence, c), CommitPolicy.OnSuccess, ct));
    public Task<Result<AssistantTitleCall?>> BeginTitleAsync(Guid id, Guid lease, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantTitleCall?>(logger, clock, "AssistantTurnBeginTitle", () => unitOfWork.ExecuteInTransactionAsync(c => writer.BeginTitleAsync(id, lease, c), CommitPolicy.OnSuccess, ct));
    public Task<Result> FinishTitleAsync(Guid id, Guid lease, uint version, string? title, AssistantCallUsage? counts, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnFinishTitle", () => unitOfWork.ExecuteInTransactionAsync(c => writer.FinishTitleAsync(id, lease, version, title, counts, c), CommitPolicy.OnSuccess, ct));
    public Task<Result> CheckpointAsync(Guid id, Guid lease, string? delta, AssistantCallUsage? counts, bool ended, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnCheckpoint", () => unitOfWork.ExecuteInTransactionAsync(c => writer.CheckpointAsync(id, lease, delta, counts, ended, c), CommitPolicy.OnSuccess, ct));
    public Task<Result> HeartbeatAsync(Guid id, Guid lease, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnHeartbeat", () => unitOfWork.ExecuteInTransactionAsync(c => writer.HeartbeatAsync(id, lease, c), CommitPolicy.OnSuccess, ct));
    public Task<Result> RecordToolAsync(Guid id, Guid lease, string key, AssistantToolState state, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnToolState", () => unitOfWork.ExecuteInTransactionAsync(c => writer.ToolStateAsync(id, lease, key, tools.Label(key), state, c), CommitPolicy.OnSuccess, ct));
    public Task<Result<AssistantToolExecution>> ExecuteToolAsync(Guid id, Guid lease, string key, JsonElement arguments, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantToolExecution>(logger, clock, "AssistantTurnTool", async () =>
        {
            var allowed = await builder.CheckAsync(id, lease, ct);
            if (allowed.IsFailure) return allowed.Error;
            if (!allowed.Value) return Domain.Assistant.AssistantErrors.TurnInProgress;
            // 0068 §6 y §8: las herramientas de acciones leen lo que selló el turno; una propuesta por turno, la guardada.
            var actions = AssistantActionTools.Handles(key) ? await builder.ActionsAsync(id, ct) : null;
            // ADR 0068 §5: la herramienta corre en su alcance READ ONLY y descartado; lo que deje en memoria nunca llega a la
            // transacción que después ata los sujetos.
            var result = await readOnly.RunAsync<AssistantTurnToolBuilder, Result<AssistantToolExecution>>(
                (inner, c) => inner.ExecuteAsync(key, arguments, actions, c), ct);
            if (result.IsFailure) return result;
            allowed = await builder.CheckAsync(id, lease, ct);
            if (allowed.IsFailure) return allowed.Error;
            if (!allowed.Value) return Domain.Assistant.AssistantErrors.TurnInProgress;
            // 0068 §8: la propuesta aceptada se guarda con el mensaje, en la misma transacción que ata sus sujetos.
            var accepted = result.Value.Action is { Outcome: AssistantActionToolData.Accepted } action ? action : null;
            var bound = await unitOfWork.ExecuteInTransactionAsync(c => writer.BindSubjectsAsync(id, lease, result.Value.SubjectUserIds,
                accepted is null ? null : (turn, inner) => proposals.SaveAsync(turn, accepted, inner), c), CommitPolicy.OnSuccess, ct);
            return bound.IsFailure ? bound.Error : result;
        });
    public Task<Result> CompleteAsync(Guid id, Guid lease, string status, string? code, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnComplete", () => unitOfWork.ExecuteInTransactionAsync(c => writer.CompleteAsync(id, lease, status, code, c), CommitPolicy.OnSuccess, ct));
    public Task<Result> RecoverAsync(Guid id, Guid lease, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnRecover", () => unitOfWork.ExecuteInTransactionAsync(c => writer.RecoverAsync(id, lease, c), CommitPolicy.OnSuccess, ct));
    public Task<Result<bool>> CheckExecutionAsync(Guid id, Guid lease, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnCheck", () => builder.CheckAsync(id, lease, ct));
    public Task<Result<AssistantTurnRead>> ReadAsync(Guid id, int after, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnRead", () => builder.ReadAsync(id, after, ct));
    public Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantTurnReplay", () => builder.ReplayAsync(reference, ct));
}
