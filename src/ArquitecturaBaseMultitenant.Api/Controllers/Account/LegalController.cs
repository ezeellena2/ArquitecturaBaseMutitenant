using System.Globalization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

[ApiController]
[AllowAnonymous]
[Route("api/legal")]
[Tags("Legal")]
public sealed class LegalController(ILegalService service) : ControllerBase
{
    [HttpGet("terms")]
    [ProducesResponseType<LegalDocumentRow>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTerms(CancellationToken cancellationToken) =>
        (await service.GetCurrentAsync(LegalDocumentKind.Terms, CultureInfo.CurrentUICulture.Name,
            cancellationToken)).ToActionResult(this);

    [HttpGet("privacy")]
    [ProducesResponseType<LegalDocumentRow>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPrivacy(CancellationToken cancellationToken) =>
        (await service.GetCurrentAsync(LegalDocumentKind.Privacy, CultureInfo.CurrentUICulture.Name,
            cancellationToken)).ToActionResult(this);
}
