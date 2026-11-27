using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Controllers;

/// <summary>
/// Los catálogos de la organización (E12): el listado, el detalle con sus opciones, guardar todo junto, la carga de una vez, la
/// exportación y la plantilla, y las fuentes con filtros. Un catálogo ajeno o con <c>forms</c> apagada responde 404, igual que uno
/// inexistente.
/// </summary>
[ApiController]
[Route("api/organization/forms/catalogs")]
[Tags("Forms Catalogs")]
[Access(Access.Business)]
[FeatureGate("forms")]
public sealed class BusinessFormCatalogsController(
    IFormCatalogService catalogs,
    IFormCatalogImportService imports,
    IFormCatalogSourceService sources) : ControllerBase
{
    private const string Spreadsheet = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> List([FromQuery] FormCatalogListHttpQuery query, CancellationToken cancellationToken) =>
        (await catalogs.ListAsync(new ListFormCatalogsRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            Sort = query.Sort,
            Source = query.Source,
            Status = query.Status,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("filter-counts")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogFilterCountsResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetFilterCounts(CancellationToken cancellationToken) =>
        (await catalogs.GetFilterCountsAsync(cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<FormCatalogCreatedResponse>(201)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(422)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Create([FromBody] CreateFormCatalogHttpRequest request, CancellationToken cancellationToken) =>
        (await catalogs.CreateAsync(new CreateFormCatalogRequest(request.Name, request.Description, request.Source, request.ValueType,
            ToItems(request.Items), ToFilters(request.Filters), request.SampleMemberId, request.SampleContactId), cancellationToken))
        .ToCreatedResult(this, nameof(Get), created => new { id = created.Catalog.Id });

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await catalogs.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPut("{id:guid}")]
    [ResponseVersion("Version")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<FormCatalogUpdatedResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateFormCatalogHttpRequest request,
        CancellationToken cancellationToken) =>
        (await catalogs.UpdateAsync(new UpdateFormCatalogRequest(id, request.Name, request.Description, request.IsActive, request.Version,
            ToItems(request.Items), request.Moves?.Select(move => move is null ? null! : new FormCatalogMoveInput(move.ItemId, move.Direction)).ToArray(),
            request.Sort is null ? null : new FormCatalogItemSortInput(request.Sort.Column, request.Sort.Descending), request.DeleteItems,
            ToFilters(request.Filters), request.SampleMemberId, request.SampleContactId), cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}/items")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogItemRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListItems([FromRoute] Guid id, [FromQuery] FormCatalogItemListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await catalogs.ListItemsAsync(id, new ListFormCatalogItemsRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Search = query.Search,
            Status = query.Status,
        }, cancellationToken)).ToActionResult(this);

    [HttpPost("{id:guid}/imports/previews")]
    [FormCatalogFileSlot(FormWorkLeaseKind.Preview)]
    [Consumes("multipart/form-data")]
    [ResponseVersion("Version")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<FormCatalogImportPreviewResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(429)]
    [ProducesProblem(500)]
    public async Task<IActionResult> PreviewImport([FromRoute] Guid id, [FromForm] FormCatalogImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        (await imports.PreviewAsync(new FormCatalogImportPreviewRequest
        {
            CatalogId = id,
            FileName = request.File?.FileName,
            Content = request.File?.OpenReadStream(),
            Mode = request.Mode,
            Version = request.Version,
            Page = request.Page ?? PagedRequest.DefaultPage,
            PageSize = request.PageSize ?? PagedRequest.DefaultPageSize,
        }, cancellationToken)).ToActionResult(this);

    [HttpPost("{id:guid}/imports")]
    [Idempotent]
    [FormCatalogFileSlot(FormWorkLeaseKind.Apply)]
    [Consumes("multipart/form-data")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<VersionResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(413)]
    [ProducesProblem(422)]
    [ProducesProblem(429)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ApplyImport([FromRoute] Guid id, [FromForm] ApplyFormCatalogImportHttpRequest request,
        CancellationToken cancellationToken) =>
        (await imports.ApplyAsync(new ApplyFormCatalogImportRequest(id, request.File?.FileName, request.File?.OpenReadStream(), request.Mode,
            request.Sha256, request.Version, request.SkipErrorRows, request.ConfirmedDeactivations), cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}/export")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<byte[]>(200, Spreadsheet)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(429)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Export([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await imports.ExportAsync(id, cancellationToken)).ToFileResult(this);

    [HttpGet("template")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<byte[]>(200, Spreadsheet)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetTemplate([FromQuery] FormCatalogTemplateHttpQuery query, CancellationToken cancellationToken) =>
        (await imports.GetTemplateAsync(new FormCatalogTemplateRequest(query.ValueType), cancellationToken)).ToFileResult(this);

    [HttpGet("sources/members")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<FormCatalogSourceResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> GetMembersSource(CancellationToken cancellationToken) =>
        (await sources.GetMembersSourceAsync(cancellationToken)).ToActionResult(this);

    [HttpGet("sources/members/roles")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogReference>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListRoles([FromQuery] FormCatalogLookupHttpQuery query, CancellationToken cancellationToken) =>
        (await sources.ListRolesAsync(ToLookups(query), cancellationToken)).ToActionResult(this);

    [HttpGet("sources/members/companies")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogReference>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListCompanies([FromQuery] FormCatalogLookupHttpQuery query, CancellationToken cancellationToken) =>
        (await sources.ListCompaniesAsync(ToLookups(query), cancellationToken)).ToActionResult(this);

    [HttpGet("sources/contacts/tags")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogTagRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListContactTags([FromQuery] FormCatalogLookupHttpQuery query, CancellationToken cancellationToken) =>
        (await sources.ListContactTagsAsync(ToLookups(query), cancellationToken)).ToActionResult(this);

    [HttpGet("sample-members")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogSampleMemberRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListSampleMembers([FromQuery] FormCatalogLookupHttpQuery query, CancellationToken cancellationToken) =>
        (await sources.ListSampleMembersAsync(ToLookups(query), cancellationToken)).ToActionResult(this);

    [HttpGet("sample-contacts")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogSampleContactRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> ListSampleContacts([FromQuery] FormCatalogLookupHttpQuery query, CancellationToken cancellationToken) =>
        (await sources.ListSampleContactsAsync(ToLookups(query), cancellationToken)).ToActionResult(this);

    [HttpPost("previews")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogPreviewResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    public async Task<IActionResult> Preview([FromBody] FormCatalogPreviewHttpRequest request, CancellationToken cancellationToken) =>
        (await sources.PreviewAsync(new FormCatalogPreviewRequest(request.Source, ToFilters(request.Filters), request.SampleMemberId,
            request.SampleContactId), cancellationToken)).ToActionResult(this);

    private static ListFormCatalogLookupsRequest ToLookups(FormCatalogLookupHttpQuery query) => new()
    {
        Page = query.Page ?? PagedRequest.DefaultPage,
        PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
        Search = query.Search,
        Sort = query.Sort,
    };

    private static FormCatalogItemInput[]? ToItems(IReadOnlyList<FormCatalogItemHttpInput>? items) =>
        items?.Select(item => item is null ? null! : new FormCatalogItemInput(item.ItemId, item.LocalId, item.Code, item.Text, item.Detail,
            item.Value, item.IsActive)).ToArray();

    private static FormCatalogFiltersInput? ToFilters(FormCatalogFiltersHttpInput? filters) => filters is null
        ? null
        : new(filters.SourceVersion,
            filters.Conditions?.Select(condition => condition is null ? null! : new FormCatalogConditionInput(condition.Column,
                condition.Operator, condition.Values)).ToArray(),
            filters.ExcludeRecipient, filters.Detail,
            filters.Sort is null ? null : new FormCatalogFilterSortInput(filters.Sort.Column, filters.Sort.Descending));
}
