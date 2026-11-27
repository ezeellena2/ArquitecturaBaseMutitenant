using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Controllers;

/// <summary>
/// Los catálogos de la organización (E12). Contrato publicado en la K1: cada ruta responde 501 hasta que la B8 la conecta con
/// su servicio. Un catálogo ajeno o con <c>forms</c> apagada responde 404, igual que uno inexistente.
/// </summary>
[ApiController]
[Route("api/organization/forms/catalogs")]
[Tags("Forms Catalogs")]
[Access(Access.Business)]
[FeatureGate("forms")]
public sealed class BusinessFormCatalogsController : ControllerBase
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
    [ProducesProblem(501)]
    public IActionResult List([FromQuery] FormCatalogListHttpQuery query) => Pending();

    [HttpGet("filter-counts")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogFilterCountsResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult GetFilterCounts() => Pending();

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
    [ProducesProblem(501)]
    public IActionResult Create([FromBody] CreateFormCatalogHttpRequest request) => Pending();

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult Get([FromRoute] Guid id) => Pending();

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
    [ProducesProblem(501)]
    public IActionResult Update([FromRoute] Guid id, [FromBody] UpdateFormCatalogHttpRequest request) => Pending();

    [HttpGet("{id:guid}/items")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogItemRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListItems([FromRoute] Guid id, [FromQuery] FormCatalogItemListHttpQuery query) => Pending();

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
    [ProducesProblem(501)]
    public IActionResult PreviewImport([FromRoute] Guid id, [FromForm] FormCatalogImportPreviewHttpRequest request) => Pending();

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
    [ProducesProblem(501)]
    public IActionResult ApplyImport([FromRoute] Guid id, [FromForm] ApplyFormCatalogImportHttpRequest request) => Pending();

    [HttpGet("{id:guid}/export")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<byte[]>(200, Spreadsheet)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(409)]
    [ProducesProblem(429)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult Export([FromRoute] Guid id) => Pending();

    [HttpGet("template")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<byte[]>(200, Spreadsheet)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult GetTemplate([FromQuery] FormCatalogTemplateHttpQuery query) => Pending();

    [HttpGet("sources/members")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<FormCatalogSourceResponse>(200)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult GetMembersSource() => Pending();

    [HttpGet("sources/members/roles")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogReference>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListRoles([FromQuery] FormCatalogLookupHttpQuery query) => Pending();

    [HttpGet("sources/members/companies")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogReference>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListCompanies([FromQuery] FormCatalogLookupHttpQuery query) => Pending();

    [HttpGet("sources/contacts/tags")]
    [HasPermission(Permissions.Forms.CatalogsManage)]
    [ProducesResponseType<PagedResult<FormCatalogTagRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListContactTags([FromQuery] FormCatalogLookupHttpQuery query) => Pending();

    [HttpGet("sample-members")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogSampleMemberRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListSampleMembers([FromQuery] FormCatalogLookupHttpQuery query) => Pending();

    [HttpGet("sample-contacts")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<PagedResult<FormCatalogSampleContactRow>>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult ListSampleContacts([FromQuery] FormCatalogLookupHttpQuery query) => Pending();

    [HttpPost("previews")]
    [HasPermission(Permissions.Forms.CatalogsRead)]
    [ProducesResponseType<FormCatalogPreviewResponse>(200)]
    [ProducesProblem(400)]
    [ProducesProblem(401)]
    [ProducesProblem(403)]
    [ProducesProblem(404)]
    [ProducesProblem(500)]
    [ProducesProblem(501)]
    public IActionResult Preview([FromBody] FormCatalogPreviewHttpRequest request) => Pending();

    private IActionResult Pending() => Result.Failure(ContractErrors.NotImplemented).ToActionResult(this);
}
