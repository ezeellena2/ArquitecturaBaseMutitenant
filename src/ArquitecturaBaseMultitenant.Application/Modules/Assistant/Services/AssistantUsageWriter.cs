using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Cierra contabilidad del binding ya admitido, aunque se haya revocado el acceso a datos. Sin chats/proveedor/UOW propia.</summary>
internal sealed class AssistantUsageWriter(IAssistantFinanceRepository repository, TimeProvider clock)
{
    public async Task<Result<AssistantCallAdmission>> BeginCallAsync(AssistantFinancialBinding binding, Guid operationId,
        int sequence, bool title, CancellationToken ct)
    {
        await repository.LockAsync(binding, operationId, ct);
        var operation = await repository.GetOperationAsync(binding, operationId, ct);
        if (operation is null) return AssistantErrors.TurnNotFound;
        var calls = await repository.ListCallsAsync(operationId, ct);
        if (calls.SingleOrDefault(call => call.Sequence == sequence) is { } replay)
        {
            if (replay.IsTitle != title) throw new InvalidOperationException("A call sequence cannot change kind.");
            return new AssistantCallAdmission(replay, false);
        }
        if (operation.State is AssistantFinancialState.Completed or AssistantFinancialState.Released
            || sequence != calls.Count + 1 || sequence > AssistantTechnicalLimits.MaxCallsPerTurn
            || calls.Any(call => call.IsTitle) || title && operation.TitlePrice is null)
            return AssistantErrors.TurnInProgress;
        if (operation.State == AssistantFinancialState.Reserved)
        {
            if (operation.Funding == AssistantKeyFunding.PlatformKey)
                (await repository.GetPlatformQuotaAsync(operation.UserId, operation.DayUtc, ct)
                    ?? throw new InvalidOperationException("The reservation lost its quota.")).Consume();
            else
                (await repository.GetOwnQuotaAsync(operation.UserId, operation.DayUtc, ct)
                    ?? throw new InvalidOperationException("The reservation lost its quota.")).Consume();
            operation.MarkInvoked();
        }
        var price = AssistantPriceSnapshotBuilder.Copy(title ? operation.TitlePrice! : operation.Price);
        var call = AssistantFinancialCall.Create(operationId, sequence, title, price, AssistantTechnicalLimits.MaxInputTokens,
            title ? AssistantTechnicalLimits.TitleMaxOutputTokens : AssistantTechnicalLimits.MaxOutputTokens,
            AssistantCostPolicy.ReserveCall(price, title));
        repository.Add(call);
        return new AssistantCallAdmission(call, true);
    }
    public async Task<Result> RecordUsageAsync(AssistantFinancialBinding binding, Guid operationId, int sequence,
        AssistantCallUsage usage, CancellationToken ct)
    {
        await repository.LockAsync(binding, operationId, ct);
        var operation = await repository.GetOperationAsync(binding, operationId, ct);
        if (operation is null) return AssistantErrors.TurnNotFound;
        if (operation.State != AssistantFinancialState.Invoked) return Result.Success();
        var call = (await repository.ListCallsAsync(operationId, ct)).SingleOrDefault(row => row.Sequence == sequence);
        if (call is null) return AssistantErrors.TurnNotFound;
        var calculated = AssistantCostPolicy.Calculate(call.Price, usage, call.IsTitle);
        call.ReplaceUsage(calculated.InputTokens, calculated.OutputTokens, calculated.CacheReadTokens,
            calculated.CacheWrite5MinutesTokens, calculated.CacheWrite1HourTokens, calculated.CacheWriteUnknownTokens,
            calculated.ReasoningTokens, calculated.IsEstimated, calculated.Cost);
        return Result.Success();
    }
    public async Task<Result> CompleteAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct)
    {
        await repository.LockAsync(binding, operationId, ct);
        var operation = await repository.GetOperationAsync(binding, operationId, ct);
        if (operation is null) return AssistantErrors.TurnNotFound;
        if (operation.State is AssistantFinancialState.Completed or AssistantFinancialState.Released) return Result.Success();
        if (operation.State == AssistantFinancialState.Reserved)
        {
            if (operation.Funding == AssistantKeyFunding.PlatformKey)
                (await repository.GetPlatformQuotaAsync(operation.UserId, operation.DayUtc, ct)
                    ?? throw new InvalidOperationException("The reservation lost its quota.")).Release();
            else
                (await repository.GetOwnQuotaAsync(operation.UserId, operation.DayUtc, ct)
                    ?? throw new InvalidOperationException("The reservation lost its quota.")).Release();
        }
        else foreach (var call in await repository.ListCallsAsync(operationId, ct))
        {
            call.FinalizeUsage();
            if (operation.Access != Access.Platform && !operation.ContentDeleted) repository.Add(AssistantUsageFact.Create(operation, call));
        }
        operation.Complete(clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
