using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;
/// <summary>Probe del filtro HTTP real alrededor de una reserva durable, sin ruta productiva extra.</summary>
[ApiController, Route("test/whatsapp-quota"), AllowAnonymous]
public sealed class WhatsAppQuotaProbeController(IUnitOfWork unit, IWhatsAppCodeQuotaRepository quotas, TimeProvider clock,
    ProblemDetailsFactory problems, ILoginMethodReader methods, IServiceProvider provider, IRequestInfo requestInfo, IWhatsAppQuotaIpHasher hasher) : ControllerBase
{
    [HttpPost("{numberId}"), Idempotent]
    public async Task<IActionResult> Post(string numberId, CancellationToken ct)
    {
        var issuer = new WhatsAppCodeQuotaIssuer(quotas, new Settings(numberId), clock, methods,
            provider.GetRequiredService<LoginMethodAvailabilityPolicy>(), requestInfo, hasher, provider.GetRequiredService<MessageQuotaGuard>(), provider.GetRequiredService<IMessageQuotaKeyHasher>());
        var result = await unit.ExecuteInTransactionAsync(c => issuer.ReserveAsync(
            LoginCodeDestination.ForPhone(PhoneNumber.Create("+5491134567890").Value, "whatsapp"), LoginCodePurpose.Signup, null, c), CommitPolicy.OnSuccess, ct);
        if (result.IsFailure)
        {
            var problem = ProblemDetailsMapper.FromError(result.Error, problems, HttpContext);
            return StatusCode(problem.Status!.Value, problem);
        }
        return Accepted(new { reserved = true });
    }
    private sealed class Settings(string numberId) : IWhatsAppChannelSettings
    {
        public bool IsEnabled => true;
        public string PhoneNumberId => numberId;
        public string? ContactPhone => null;
        public IReadOnlyList<string> ConfiguredCountries => ["AR"];
        // El probe no prueba propiedad: cap 2 deja un envío nuevo con la protección predeterminada.
        public int DailyAuthCodeLimit => 2;
        public Result ValidateNewDestination(LoginCodeDestination destination, IReadOnlyList<string> allowedCountries) => Result.Success();
    }
}
