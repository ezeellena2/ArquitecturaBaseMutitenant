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
/// servicio. Un correo que ya es de una cuenta responde igual que uno libre (ADR 0041).
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
        // Si ya es de una cuenta (un correo o el contacto de Google, verificados), la respuesta es la de uno libre:
        // el código se emite para los límites y la espera, pero no se manda ni sirve, y a su dueño le llega el aviso.
        var owners = await lookup.FindVerifiedUsersByEmailAsync(email, ct);
        if (owners.Count > 0)
        {
            var hidden = await codes.IssueAccountCodeAsync(destination, LoginCodePurpose.VerifyDestination, userId, ct);
            if (hidden.IsFailure) return hidden.Error;
            await notices.EnqueueToOwnerAsync(owners[0], email.Value, new AccountNotice.LoginMethodInUse(), ct);
            return new LoginMethodCodeResponse(Guid.CreateVersion7(), hidden.Value.ResendCooldownSeconds);
        }

        // Un correo agregado y sin verificar, de esta cuenta o de otra, se reemplaza: es pedir otro código.
        if (await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct) is { } existing
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
        return await SendAsync(userId, method, account.Value.Culture, ct);
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
