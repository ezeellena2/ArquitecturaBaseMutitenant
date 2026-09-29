using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara el espacio y aplica las reglas de verificación dentro del límite del servicio.</summary>
internal sealed class SignupVerificationFlow(IPersonalSpaceProvisioner personalSpaces,
    SignupVerificationCore core)
{
    internal Task<PersonalSpaceDraft> PrepareAsync(VerifySignupRequest request,
        CancellationToken cancellationToken) =>
        personalSpaces.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);

    internal Task<Result<Guid>> VerifyAsync(VerifySignupRequest request, PersonalSpaceDraft draft,
        CancellationToken cancellationToken) => core.VerifyAsync(request, draft, cancellationToken);
}
