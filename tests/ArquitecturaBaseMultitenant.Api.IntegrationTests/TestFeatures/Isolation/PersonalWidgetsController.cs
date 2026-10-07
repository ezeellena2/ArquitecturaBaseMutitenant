using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Ejemplo privado Consumer: sólo declara el acceso; la persona no recibe ni pide roles.</summary>
[ApiController]
[Route("test/personal-widgets")]
[Access(Access.Consumer)]
public sealed class PersonalWidgetsController(IPersonalWidgetService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<PersonalWidgetResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] PersonalWidgetListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(ToRequest(query), cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonalWidgetResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePersonalWidgetHttpRequest request, CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreatePersonalWidgetRequest(request.Name), cancellationToken))
            .ToCreatedResult(this, nameof(Get), id => new { id });

    [HttpPut("{id:guid}")]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonalWidgetHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdatePersonalWidgetRequest(id, request.Name, request.Version), cancellationToken))
            .ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeletePersonalWidgetHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.DeleteAsync(new DeletePersonalWidgetRequest(id, request.Version), cancellationToken)).ToActionResult(this);

    internal static ListPersonalWidgetsRequest ToRequest(PersonalWidgetListHttpQuery query) => new()
    {
        Page = query.Page ?? PagedRequest.DefaultPage,
        PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
        Sort = query.Sort,
        Search = query.Search,
    };
}

/// <summary>
/// Lector Business sintético del mismo dato privado, sólo para probar el acceso y el aislamiento.
/// Cada módulo real agrega HasPermission con sus propias claves en el catálogo.
/// </summary>
[ApiController]
[Route("test/business-widgets")]
[Access(Access.Business)]
public sealed class BusinessWidgetsController(IPersonalWidgetService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<PersonalWidgetResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PersonalWidgetListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(PersonalWidgetsController.ToRequest(query), cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PersonalWidgetResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);
}
