using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Las fuentes de los catálogos con filtros (E12, C2, C12, §4 y §4b), sin SQL configurable (ADR 0053): la definición cerrada de
/// «Miembros», las listas de roles, empresas, etiquetas y ejemplos, y la vista previa, que valida los filtros contra la fuente y
/// no escribe nada. Los miembros los lee el núcleo y los contactos llegan por un puerto neutral: Forms nunca referencia a
/// WhatsApp, y sin el canal no hay contactos. Ninguna lista lleva correos ni teléfonos.
/// </summary>
internal sealed class FormCatalogSourceService(IMemberDirectoryReader members, IStructuredContactSource contacts,
    FormCatalogFiltersPolicy filters, FormCatalogPreviewBuilder preview, IRequestValidator validator, TimeProvider clock,
    ILogger<FormCatalogSourceService> logger) : IFormCatalogSourceService
{
    public Task<Result<FormCatalogSourceResponse>> GetMembersSourceAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "GetFormCatalogMembersSource", () => Task.FromResult(Result.Success(MembersCatalogSource.Definition)));

    public Task<Result<PagedResult<FormCatalogReference>>> ListRolesAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await members.ListRolesAsync(request, cancellationToken);
            return Result.Success(Map(page, row => new FormCatalogReference(row.Id, row.Name)));
        });
    }

    public Task<Result<PagedResult<FormCatalogReference>>> ListCompaniesAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogCompanies", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await members.ListCompaniesAsync(request, cancellationToken);
            return Result.Success(Map(page, row => new FormCatalogReference(row.Id, row.Name)));
        });
    }

    public Task<Result<PagedResult<FormCatalogTagRow>>> ListContactTagsAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogContactTags", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await contacts.ListTagsAsync(request, cancellationToken);
            return Result.Success(Map(page, tag => new FormCatalogTagRow(tag.Name, tag.Contacts)));
        });
    }

    public Task<Result<PagedResult<FormCatalogSampleMemberRow>>> ListSampleMembersAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogSampleMembers", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await members.ListSamplesAsync(request, cancellationToken);
            return Result.Success(Map(page, row => new FormCatalogSampleMemberRow(row.MemberId, row.Name,
                [.. row.Companies.Take(FormCatalogLimits.MaxSampleMemberNames)], [.. row.Roles.Take(FormCatalogLimits.MaxSampleMemberNames)])));
        });
    }

    public Task<Result<PagedResult<FormCatalogSampleContactRow>>> ListSampleContactsAsync(ListFormCatalogLookupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, clock, "ListFormCatalogSampleContacts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await contacts.ListPageAsync(request, cancellationToken);
            return Result.Success(Map(page, contact => new FormCatalogSampleContactRow(contact.Id, contact.Name)));
        });
    }

    public Task<Result<FormCatalogPreviewResponse>> PreviewAsync(FormCatalogPreviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<FormCatalogPreviewResponse>(logger, clock, "PreviewFormCatalog", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var source = request.Source!.Value;
            var decided = await filters.DecideAsync(source, request.Filters!,
                FormCatalogFiltersPolicy.SampleOf(source, request.SampleMemberId, request.SampleContactId), cancellationToken);
            if (decided.IsFailure) return decided.Error;
            return await preview.BuildAsync(source, decided.Value, cancellationToken);
        });
    }

    private static PagedResult<TOut> Map<TIn, TOut>(PagedResult<TIn> page, Func<TIn, TOut> map) =>
        new([.. page.Items.Select(map)], page.Page, page.PageSize, page.TotalCount);
}
