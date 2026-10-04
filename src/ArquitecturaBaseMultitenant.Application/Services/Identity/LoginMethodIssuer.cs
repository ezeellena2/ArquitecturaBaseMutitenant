using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Agrega un correo de ingreso o reenvía su código de verificación; trabaja dentro de la transacción abierta por el
/// servicio. Un correo que ya es de otra cuenta responde igual que uno libre en cada paso (ADR 0041): queda pendiente,
/// el código se emite sin mandarse ni servir y a su dueño le llega el aviso.
/// </summary>
internal sealed class LoginMethodIssuer(LoginMethodGuard guard, ILoginMethodRepository methods,
    IUserLookup lookup, LoginCodeIssuer codes,
    IEnumerable<ILoginCodeChannel> channels, UserCultures cultures, ReauthTicketConsumer tickets,
    AccountNoticeIssuer notices, TimeProvider timeProvider)
{
    public async Task<Result<LoginMethodCodeResponse>> AddAsync(Guid userId, AddLoginEmailRequest request, CancellationToken ct)
    {
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var consumed = await tickets.ConsumeAsync(userId, ReauthAction.AddEmail, null, request.ReauthTicket ?? string.Empty, ct);
        if (consumed.IsFailure) return consumed.Error;
        var email = request.Email!;
        var destination = LoginCodeDestination.ForEmail(email);
        await codes.LockDestinationAsync(destination, ct);
        // Si ya es de otra cuenta (un correo o el contacto de Google, verificados), o ya es un correo verificado de esta,
        // la respuesta es la de uno libre: el código se emite para los límites y la espera, pero no se manda ni sirve,
        // y a su dueño le llega el aviso. El contacto del propio Google sí se puede sumar como correo.
        var owners = await lookup.FindVerifiedUsersByEmailAsync(email, ct);
        var existing = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        var owner = owners.Where(id => id != userId).Select(id => (Guid?)id).FirstOrDefault()
            ?? (existing is { VerifiedAtUtc: not null } ? existing.UserId : null);
        if (owner is { } ownerId)
        {
            var hidden = await NotifyOwnerAsync(userId, ownerId, email, ct);
            if (hidden.IsFailure) return hidden.Error;
            // Un correo ya verificado en esta misma cuenta no se repite. El de otra cuenta queda pendiente, como uno
            // libre (D4b): el índice único vale solo para los verificados.
            if (ownerId == userId) return new LoginMethodCodeResponse(Guid.CreateVersion7(), hidden.Value);
            await RemoveOwnPendingAsync(userId, email.Value, ct);
            var hiddenMethod = LoginMethod.CreateEmail(userId, email);
            methods.Add(hiddenMethod);
            return new LoginMethodCodeResponse(hiddenMethod.Id, hidden.Value);
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
        // Si el correo es de otra cuenta, el reenvío responde igual que con uno libre y vuelve a avisarle al dueño.
        var email = Email.Create(method.Value).Value;
        var owner = (await lookup.FindVerifiedUsersByEmailAsync(email, ct)).Where(id => id != userId)
            .Select(id => (Guid?)id).FirstOrDefault();
        if (owner is { } ownerId)
        {
            var hidden = await NotifyOwnerAsync(userId, ownerId, email, ct);
            return hidden.IsFailure ? hidden.Error : new LoginMethodCodeResponse(method.Id, hidden.Value);
        }
        return await SendAsync(userId, method, account.Value.Culture, ct);
    }

    // El código se emite para los límites y la espera, pero no se manda ni sirve; al dueño le llega el aviso.
    private async Task<Result<int>> NotifyOwnerAsync(Guid userId, Guid ownerId, Email email, CancellationToken ct)
    {
        var hidden = await codes.IssueAccountCodeAsync(LoginCodeDestination.ForEmail(email),
            LoginCodePurpose.VerifyDestination, userId, ct);
        if (hidden.IsFailure) return hidden.Error;
        await notices.EnqueueToOwnerAsync(ownerId, email.Value, new AccountNotice.LoginMethodInUse(), ct);
        return hidden.Value.ResendCooldownSeconds;
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
