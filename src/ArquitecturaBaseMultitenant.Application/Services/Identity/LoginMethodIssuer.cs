using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Agrega un correo de ingreso o reenvía su código de verificación; trabaja dentro de la transacción abierta por el
/// servicio. Rechaza la propiedad verificada antes de emitir; una reserva pendiente ajena se recupera por posesión.
/// </summary>
internal sealed class LoginMethodIssuer(LoginMethodGuard guard, ILoginMethodRepository methods,
    IUserLookup lookup, LoginCodeIssuer codes,
    IEnumerable<ILoginCodeChannel> channels, UserCultureResolver cultures,
    TimeProvider timeProvider, LoginMethodLinker linker)
{
    public async Task<Result<LoginMethodCodeResponse>> AddAsync(Guid userId, AddLoginEmailRequest request, CancellationToken ct)
    {
        await linker.LockEmailAsync(request.Email!, ct);
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var email = request.Email!;
        var destination = LoginCodeDestination.ForEmail(email);
        await codes.LockDestinationAsync(destination, ct);
        // La propiedad verificada, incluido el contacto de Google, bloquea el alta. El contacto del propio Google
        // sí se puede sumar como correo; un método de correo propio ya verificado no se repite.
        var owners = await lookup.FindVerifiedUsersByEmailAsync(email, ct);
        var existing = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        var owner = owners.Where(id => id != userId).Select(id => (Guid?)id).FirstOrDefault()
            ?? (existing is { VerifiedAtUtc: not null } ? existing.UserId : null);
        if (owner is { } ownerId)
        {
            return FieldErrors.On(ownerId == userId ? LoginMethodErrors.AlreadyVerified : LoginMethodErrors.AlreadyUsed, "email");
        }

        // Un correo agregado y sin verificar, de esta cuenta o de otra, se reemplaza: es pedir otro código.
        await RemoveOwnPendingAsync(userId, email.Value, ct);
        if (existing is { VerifiedAtUtc: null } && existing.UserId != userId
            && await methods.GetByIdAsync(existing.MethodId, ct) is { VerifiedAtUtc: null } pending)
            methods.Remove(pending);
        var method = LoginMethod.CreateEmail(userId, email);
        var sent = await SendAsync(userId, method, account.Value.Culture, ct);
        if (sent.IsSuccess) methods.Add(method);
        return sent;
    }

    public async Task<Result<LoginMethodCodeResponse>> SendAsync(Guid userId, Guid methodId, CancellationToken ct)
    {
        var hint = await methods.GetByIdForUserAsync(userId, methodId, ct);
        if (hint is null) return LoginMethodErrors.NotFound;
        await linker.LockEmailAsync(Email.Create(hint.Value).Value, ct);
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var method = await methods.GetByIdForUserAsync(userId, methodId, ct);
        if (method is null) return LoginMethodErrors.NotFound;
        if (method.VerifiedAtUtc is not null) return LoginMethodErrors.AlreadyVerified;
        var destination = LoginCodeDestination.ForEmail(Email.Create(method.Value).Value);
        await codes.LockDestinationAsync(destination, ct);
        var latestCode = await codes.GetLatestAsync(destination,
            LoginCodePurpose.VerifyDestination, userId, ct);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (latestCode is null || !latestCode.IsActive(nowUtc))
        {
            methods.Remove(method);
            return latestCode is { FailedAttempts: var attempts, MaxAttempts: var maxAttempts }
                && attempts >= maxAttempts
                ? LoginCodeErrors.TooManyAttempts
                : latestCode is { ConsumedAtUtc: not null }
                    ? LoginCodeErrors.AlreadyUsed
                    : LoginCodeErrors.Expired;
        }
        // Una verificación concurrente de otra cuenta también bloquea el reenvío.
        var email = Email.Create(method.Value).Value;
        var owner = (await lookup.FindVerifiedUsersByEmailAsync(email, ct)).Where(id => id != userId)
            .Select(id => (Guid?)id).FirstOrDefault();
        if (owner is not null)
        {
            return FieldErrors.On(LoginMethodErrors.AlreadyUsed, "email");
        }
        return await SendAsync(userId, method, account.Value.Culture, ct);
    }

    private async Task RemoveOwnPendingAsync(Guid userId, string value, CancellationToken ct)
    {
        foreach (var own in (await methods.ListByUserIdAsync(userId, ct)).Where(method =>
                     method.Type == LoginMethodType.Email && method.Value == value && method.VerifiedAtUtc is null))
            methods.Remove(own);
    }

    private async Task<Result<LoginMethodCodeResponse>> SendAsync(Guid userId, LoginMethod method,
        string culture, CancellationToken ct)
    {
        var channel = channels.FirstOrDefault(value => value.Key == LoginCodeChannel.Email);
        if (method.Type != LoginMethodType.Email || channel is null) return LoginMethodErrors.NotFound;
        var issued = await codes.IssueAccountCodeAsync(LoginCodeDestination.ForEmail(Email.Create(method.Value).Value),
            LoginCodePurpose.VerifyDestination, userId, ct);
        if (issued.IsFailure) return issued.Error;
        var code = issued.Value;
        channel.EnqueueVerification(userId, method.Value, code.Code, code.LifetimeMinutes,
            await cultures.ResolveAsync(culture, null, ct));
        code.LoginCode.MarkSent(code.IssuedAtUtc);
        return new LoginMethodCodeResponse(method.Id, code.ResendCooldownSeconds);
    }
}
