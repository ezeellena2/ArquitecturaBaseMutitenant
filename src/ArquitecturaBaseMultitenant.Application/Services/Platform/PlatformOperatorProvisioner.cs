using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Crea, dentro de la UoW del llamador, la identidad exclusiva de un operador: cuenta marcada como operadora, su
/// correo verificado como método principal y sus permisos, sin espacio Personal ni organizaciones. El correo queda
/// verificado porque lo da un operador con <c>platform.operators.manage</c>; el primer ingreso con código lo prueba.
/// Cultura y zona horaria salen de los datos de referencia, como en el seed del primer operador.
/// </summary>
internal sealed class PlatformOperatorProvisioner(IUserRepository users, ILoginMethodRepository methods,
    IPlatformPermissionAssignmentRepository assignments, UserCultures cultures, ICountryCatalog countries)
{
    public async Task<Result<Guid>> CreateAsync(Email email, IReadOnlyCollection<string> permissions, DateTime nowUtc,
        CancellationToken ct)
    {
        var culture = await cultures.ResolveAsync(null, null, ct);
        var timeZone = (await countries.FindAsync(culture.Entry.CountryCode, ct))?.DefaultTimeZoneId
            ?? throw new InvalidOperationException("The default culture country has no time zone in reference data.");
        var userId = (await users.CreateAsync(null, culture.Entry.Code, timeZone, ct)).Id;
        var assignment = PlatformPermissionAssignment.Create(userId, permissions);
        if (assignment.IsFailure) return assignment.Error;
        await users.SetPlatformOperatorAsync(userId, true, ct);
        var method = LoginMethod.CreateEmail(userId, email);
        method.Verify(nowUtc);
        if (method.MakePrimary().IsFailure)
            throw new InvalidOperationException("A verified operator email could not become primary.");
        methods.Add(method);
        await users.SetPrimaryEmailAsync(userId, email, ct);
        assignments.Add(assignment.Value);
        return userId;
    }
}
