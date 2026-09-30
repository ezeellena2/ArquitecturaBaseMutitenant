using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Emite códigos dentro de la transacción del caso de uso. No abre otra UoW:
/// el servicio que llama debe encolar el mensaje en el mismo límite.
/// </summary>
internal sealed class LoginCodeIssuer(
    ILoginCodeRepository loginCodes,
    ILoginCodeGenerator codeGenerator,
    ILoginCodeHasher codeHasher,
    IOptions<LoginCodeOptions> options,
    TimeProvider timeProvider)
{
    public Task<Result<IssuedLoginCode>> IssueLoginCodeAsync(LoginCodeDestination destination,
        CancellationToken cancellationToken) =>
        IssueAsync(destination, LoginCodePurpose.Login, requestedByUserId: null, cancellationToken);

    public Task<Result<IssuedLoginCode>> IssueSignupCodeAsync(LoginCodeDestination destination,
        CancellationToken cancellationToken) =>
        IssueAsync(destination, LoginCodePurpose.Signup, requestedByUserId: null, cancellationToken);

    public Task<Result<IssuedLoginCode>> IssueAccountCodeAsync(LoginCodeDestination destination,
        LoginCodePurpose purpose, Guid userId, CancellationToken cancellationToken)
    {
        if (purpose is not (LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate))
            throw new ArgumentOutOfRangeException(nameof(purpose));
        if (userId == Guid.Empty) throw new ArgumentException("The account is required.", nameof(userId));
        return IssueAsync(destination, purpose, userId, cancellationToken);
    }

    private async Task<Result<IssuedLoginCode>> IssueAsync(LoginCodeDestination destination,
        LoginCodePurpose purpose, Guid? requestedByUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await loginCodes.LockDestinationAsync(destination, cancellationToken);

        var settings = options.Value;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var limitError = await CheckLimitsAsync(destination, settings, nowUtc, cancellationToken);
        if (limitError is not null)
        {
            return limitError;
        }

        foreach (var active in await loginCodes.ListActiveAsync(destination, purpose, requestedByUserId,
                     nowUtc, cancellationToken))
        {
            active.Invalidate(nowUtc);
        }

        var code = codeGenerator.Generate();
        var row = LoginCode.Issue(destination, purpose, requestedByUserId,
            codeHasher.Hash(destination, purpose, code), nowUtc,
            TimeSpan.FromMinutes(settings.LifetimeMinutes), settings.MaxAttempts);
        loginCodes.Add(row);
        return new IssuedLoginCode(row, code, settings.LifetimeMinutes, settings.ResendCooldownSeconds);
    }

    private async Task<Error?> CheckLimitsAsync(LoginCodeDestination destination, LoginCodeOptions settings,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromMinutes(settings.RequestWindowMinutes);
        var cooldown = TimeSpan.FromSeconds(settings.ResendCooldownSeconds);
        var since = nowUtc - (window > cooldown ? window : cooldown);
        var requestTimes = await loginCodes.ListRequestTimesSinceAsync(destination, since, cancellationToken);
        var windowTimes = requestTimes.Where(value => value > nowUtc - window).Order().ToArray();
        if (windowTimes.Length >= settings.MaxRequestsPerWindow)
        {
            return LoginCodeErrors.TooManyRequests(SecondsUntil(windowTimes[0] + window, nowUtc));
        }

        var latest = requestTimes.Count > 0 ? requestTimes.Max() : (DateTime?)null;
        var resendAllowedAtUtc = latest + cooldown;
        return resendAllowedAtUtc > nowUtc
            ? LoginCodeErrors.ResendTooSoon(SecondsUntil(resendAllowedAtUtc.Value, nowUtc))
            : null;
    }

    internal static int SecondsUntil(DateTime momentUtc, DateTime nowUtc) =>
        Math.Max(1, (int)Math.Ceiling((momentUtc - nowUtc).TotalSeconds));
}

/// <summary>Clase sin ToString sensible: el código claro se usa sólo al encolar el mensaje.</summary>
internal sealed class IssuedLoginCode(LoginCode row, string code, int lifetimeMinutes, int resendCooldownSeconds)
{
    public LoginCode LoginCode { get; } = row;
    public string Code { get; } = code;
    public DateTime IssuedAtUtc => LoginCode.CreatedAtUtc;
    public int LifetimeMinutes { get; } = lifetimeMinutes;
    public int ResendCooldownSeconds { get; } = resendCooldownSeconds;
}
