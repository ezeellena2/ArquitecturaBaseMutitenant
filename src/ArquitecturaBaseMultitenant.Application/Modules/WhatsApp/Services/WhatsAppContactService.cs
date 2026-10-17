using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Los contactos de la organización del claim (ADR 0038, D3): ver, editar, etiquetar, cambiar el consentimiento a mano e
/// importar. La importación se revisa antes sin guardar; sin «Sí» queda «Sin respuesta» y una baja no se deshace importando.
/// </summary>
internal sealed class WhatsAppContactService(IWhatsAppContactReader reader, IWhatsAppCustomerRepository customers,
    WhatsAppContactImportResolver imports, IWhatsAppContactSpreadsheet spreadsheet, IUserDisplayNameReader names, IWhatsAppBroadcastReader broadcasts,
    IUnitOfWork unitOfWork,
    IRequestValidator validator, TimeProvider clock, ILogger<WhatsAppContactService> logger) : IWhatsAppContactService
{
    private const string SpreadsheetMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int RejectedPage = 100;

    public Task<Result<WhatsAppContactListResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppContactListResponse>(logger, clock, "WhatsAppContactList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var counts = await reader.CountAsync(cancellationToken);
            return new WhatsAppContactListResponse(await reader.ListAsync(request, cancellationToken), counts.Total, counts.Accepting);
        });

    public Task<Result<WhatsAppContactResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppContactResponse>(logger, clock, "WhatsAppContactGet", async () =>
            await reader.FindAsync(id, cancellationToken) is { } contact ? contact : WhatsAppErrors.ContactNotFound);

    public Task<Result<PagedResult<WhatsAppContactConversationResponse>>> ListConversationsAsync(Guid id, WhatsAppContactChildListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppContactConversationResponse>>(logger, clock, "WhatsAppContactConversations", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindAsync(id, cancellationToken) is null) return WhatsAppErrors.ContactNotFound;
            var page = await reader.ListConversationsAsync(id, request, cancellationToken);
            var people = await names.FindAsync([.. page.Items.Select(row => row.AssigneeUserId).OfType<Guid>().Distinct()], cancellationToken);
            return new PagedResult<WhatsAppContactConversationResponse>([.. page.Items.Select(row => new WhatsAppContactConversationResponse(
                row.ConversationId, row.StartedAtUtc, row.TeamId is { } team ? new(team, row.TeamName ?? string.Empty) : null,
                row.AssigneeUserId is { } taker ? new(row.AssigneeMemberId ?? taker, people.GetValueOrDefault(taker) ?? string.Empty) : null,
                row.MessageCount, row.LastMessagePreview))], page.Page, page.PageSize, page.TotalCount);
        });

    /// <summary>Las difusiones que le salieron al contacto, la más nueva primero, con cómo le fue en cada una.</summary>
    public Task<Result<PagedResult<WhatsAppContactBroadcastResponse>>> ListBroadcastsAsync(Guid id, WhatsAppContactChildListRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<WhatsAppContactBroadcastResponse>>(logger, clock, "WhatsAppContactBroadcasts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await reader.FindAsync(id, cancellationToken) is null) return WhatsAppErrors.ContactNotFound;
            return await broadcasts.ListForContactAsync(id, request, cancellationToken);
        });

    public Task<Result<WhatsAppContactResponse>> UpdateAsync(Guid id, UpdateWhatsAppContactRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppContactResponse>(logger, clock, "WhatsAppContactUpdate", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var saved = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (await customers.GetAsync(id, request.Version, ct) is not { } customer) return Result.Failure(WhatsAppErrors.ContactNotFound);
                customer.Rename(request.Name);
                customer.ReplaceTags(request.Tags!);
                if (request.Consent is { } consent) customer.ChangeConsent(consent, WhatsAppConsentSource.Manual, clock.GetUtcNow().UtcDateTime);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
            if (saved.IsFailure) return saved.Error;
            return await reader.FindAsync(id, cancellationToken) is { } contact ? contact : WhatsAppErrors.ContactNotFound;
        });

    public Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PagedResult<string>>(logger, clock, "WhatsAppContactTags", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListTagsAsync(request, cancellationToken);
        });

    public Task<Result<WhatsAppFileResponse>> GetImportSampleAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppFileResponse>(logger, clock, "WhatsAppContactImportSample", () =>
        {
            var content = spreadsheet.WriteSample(Text("Import.SheetName"),
                [Text("Import.Header.Name"), Text("Import.Header.Phone"), Text("Import.Header.Tag"), Text("Import.Header.Accepts")],
                [Text("Import.Example.Name"), Text("Import.Example.Phone"), Text("Import.Example.Tag"), Text("Import.Yes")]);
            return Task.FromResult(Result.Success(new WhatsAppFileResponse(new MemoryStream(content), SpreadsheetMediaType,
                Text("Import.SampleFileName"))));
        });

    public Task<Result<WhatsAppImportPreviewResponse>> PreviewImportAsync(ImportWhatsAppContactsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppImportPreviewResponse>(logger, clock, "WhatsAppContactImportPreview", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var plan = await imports.ResolveAsync(request, cancellationToken);
            if (plan.IsFailure) return plan.Error;
            var existing = await reader.FindExistingAsync([.. plan.Value.Valid.Select(row => row.WaId.Value)], cancellationToken);
            return new WhatsAppImportPreviewResponse(plan.Value.Rows, plan.Value.Valid.Count - existing.Count, existing.Count,
                Rejected(plan.Value.Rejected));
        });

    public Task<Result<WhatsAppImportResponse>> ImportAsync(ImportWhatsAppContactsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<WhatsAppImportResponse>(logger, clock, "WhatsAppContactImport", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var plan = await imports.ResolveAsync(request, cancellationToken);
            if (plan.IsFailure) return plan.Error;
            return await unitOfWork.ExecuteInTransactionAsync<Result<WhatsAppImportResponse>>(async ct =>
            {
                await customers.LockAsync(ct);
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var existing = (await customers.ListByWaIdsAsync([.. plan.Value.Valid.Select(row => row.WaId.Value)], ct))
                    .ToDictionary(customer => customer.WaId, StringComparer.Ordinal);
                foreach (var row in plan.Value.Valid)
                {
                    if (!existing.TryGetValue(row.WaId.Value, out var customer))
                    {
                        customer = WhatsAppCustomer.Create(row.WaId, nowUtc);
                        customers.Add(customer);
                    }
                    customer.Import(row.Name, row.Tags, row.AcceptsPromotions, nowUtc);
                }
                return new WhatsAppImportResponse(plan.Value.Valid.Count - existing.Count, existing.Count, Rejected(plan.Value.Rejected));
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    /// <summary>Las primeras filas rechazadas, con el total real.</summary>
    private static PagedResult<WhatsAppImportRejectionResponse> Rejected(IReadOnlyList<WhatsAppImportRejectionResponse> rejected) =>
        new([.. rejected.Take(RejectedPage)], PagedRequest.DefaultPage, RejectedPage, rejected.Count);

    private static string Text(string key) => WhatsAppTexts.Get(key);
}
