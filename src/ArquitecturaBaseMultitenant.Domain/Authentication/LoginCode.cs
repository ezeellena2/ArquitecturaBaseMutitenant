using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Código de un solo uso enviado a un destino por un canal registrado.
/// Solo se guarda su hash; vence, limita intentos y no se reutiliza entre propósitos.
/// </summary>
public sealed class LoginCode : Entity
{
    // Para EF Core.
    private LoginCode()
    {
        Destination = string.Empty;
        CodeHash = string.Empty;
    }

    private LoginCode(
        LoginCodeDestination destination,
        LoginCodePurpose purpose,
        Guid? requestedByUserId,
        string codeHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        int maxAttempts)
    {
        Destination = destination.Value;
        Channel = destination.Channel;
        Purpose = purpose;
        RequestedByUserId = requestedByUserId;
        CodeHash = codeHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        MaxAttempts = maxAttempts;
    }

    /// <summary>El correo normalizado o el número en formato internacional.</summary>
    public string Destination { get; private set; }

    public string Channel { get; private set; } = string.Empty;

    public LoginCodePurpose Purpose { get; private set; }

    /// <summary>
    /// La cuenta que pidió verificar un destino o reautenticarse; null en ingreso y registro.
    /// </summary>
    public Guid? RequestedByUserId { get; private set; }
    public ReauthAction? ReauthAction { get; private set; }
    public Guid? SourceMethodId { get; private set; }
    public Guid? TargetMethodId { get; private set; }

    /// <summary>
    /// El hash del nonce del navegador que pidió un código de «Recuperar mi cuenta». Solo ese navegador puede
    /// verificarlo y registrar el pedido.
    /// </summary>
    public string? NonceHash { get; private set; }

    public string CodeHash { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Cuándo se encoló el mensaje con el código, o null si no se mandó nada: en InviteOnly, a un destino sin cuenta
    /// se le emite el código igual pero no se le manda. Si la cola descarta el mensaje, la fecha queda igual: cuenta
    /// lo que se intentó mandar.
    /// </summary>
    public DateTime? SentAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public DateTime? InvalidatedAtUtc { get; private set; }

    public int FailedAttempts { get; private set; }

    public int MaxAttempts { get; private set; }

    public int AttemptsLeft => Math.Max(0, MaxAttempts - FailedAttempts);

    /// <summary>
    /// Emite un código. Verificar un destino o reautenticarse requiere cuenta;
    /// ingresar o registrarse todavía no la tiene.
    /// </summary>
    public static LoginCode Issue(
        LoginCodeDestination destination,
        LoginCodePurpose purpose,
        Guid? requestedByUserId,
        string codeHash,
        DateTime nowUtc,
        TimeSpan lifetime,
        int maxAttempts)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The issue instant must be UTC.", nameof(nowUtc));
        }

        switch (purpose)
        {
            case LoginCodePurpose.Login or LoginCodePurpose.Signup or LoginCodePurpose.BusinessSignup
                or LoginCodePurpose.AccountRecovery
                when requestedByUserId is not null:
                throw new ArgumentException("A login, signup or recovery code cannot be requested by an account.", nameof(requestedByUserId));

            case LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate
                when requestedByUserId is null || requestedByUserId == Guid.Empty:
                throw new ArgumentException(
                    "The code needs the account that requested it.", nameof(requestedByUserId));

            case LoginCodePurpose.Login or LoginCodePurpose.Signup or LoginCodePurpose.BusinessSignup
                or LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate or LoginCodePurpose.AccountRecovery:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown login code purpose.");
        }

        return new LoginCode(destination, purpose, requestedByUserId, codeHash, nowUtc, nowUtc + lifetime, maxAttempts);
    }

    public bool IsActive(DateTime nowUtc) =>
        ConsumedAtUtc is null && InvalidatedAtUtc is null && nowUtc < ExpiresAtUtc && FailedAttempts < MaxAttempts;

    /// <summary>Lo invalida porque se pidió un código nuevo. Conserva el primer momento de invalidación.</summary>
    public void Invalidate(DateTime nowUtc)
    {
        InvalidatedAtUtc ??= nowUtc;
    }

    /// <summary>Registra que el mensaje con el código se encoló para el destino. Conserva el primer envío.</summary>
    public void MarkSent(DateTime nowUtc)
    {
        SentAtUtc ??= nowUtc;
    }

    /// <summary>
    /// Verifica un código de ingreso o de registro. Otro propósito no gasta intentos.
    /// </summary>
    public Result Verify(LoginCodePurpose expectedPurpose, string codeHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(codeHash);

        return expectedPurpose is LoginCodePurpose.Login or LoginCodePurpose.Signup or LoginCodePurpose.BusinessSignup
            && Purpose == expectedPurpose
            ? Check(codeHash, nowUtc)
            : LoginCodeErrors.Invalid(attemptsLeft: null);
    }

    /// <summary>
    /// Verifica un código de una cuenta para la cuenta <paramref name="userId"/>.
    /// Si el código es de ingreso o lo pidió otra cuenta, la respuesta es la misma que cuando no hay código y no se
    /// toca ningún contador: así no se sabe que otra cuenta está vinculando ese destino, ni se le gastan los intentos.
    /// Un código que no se mandó, o cuyo destino ya es de otra cuenta (<paramref name="usable"/> en falso), falla como
    /// uno mal escrito y gasta el intento (ADR 0041).
    /// </summary>
    public Result VerifyFor(Guid userId, LoginCodePurpose expectedPurpose, string codeHash, DateTime nowUtc,
        bool usable = true)
    {
        ArgumentNullException.ThrowIfNull(codeHash);

        return expectedPurpose is LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate
            && Purpose == expectedPurpose && RequestedByUserId == userId
            ? Check(codeHash, nowUtc, usable: usable && SentAtUtc is not null)
            : LoginCodeErrors.Invalid(attemptsLeft: null);
    }

    public void BindReauthentication(ReauthAction action, Guid sourceMethodId, Guid? targetMethodId)
    {
        if (Purpose != LoginCodePurpose.Reauthenticate || ReauthAction is not null || !Enum.IsDefined(action)
            || sourceMethodId == Guid.Empty)
            throw new InvalidOperationException("Only a new reauthentication code can be bound to an action.");
        ReauthAction = action;
        SourceMethodId = sourceMethodId;
        TargetMethodId = targetMethodId;
    }

    public bool MatchesReauthentication(ReauthAction action, Guid sourceMethodId, Guid? targetMethodId) =>
        ReauthAction == action && SourceMethodId == sourceMethodId && TargetMethodId == targetMethodId;

    public void BindRecoveryNonce(string nonceHash)
    {
        if (Purpose != LoginCodePurpose.AccountRecovery || NonceHash is not null || string.IsNullOrWhiteSpace(nonceHash))
            throw new InvalidOperationException("Only a new recovery code can be bound to a nonce.");
        NonceHash = nonceHash;
    }

    /// <summary>
    /// Verifica un código de recuperación. Con otro nonce responde como si no hubiera código y no gasta intentos. Un
    /// código que no se mandó, porque el correo ya era de una cuenta, falla como un código mal escrito: los intentos
    /// restantes no delatan que no hubo código.
    /// </summary>
    public Result VerifyRecovery(string nonceHash, string codeHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(codeHash);

        return Purpose == LoginCodePurpose.AccountRecovery && MatchesNonce(nonceHash)
            ? Check(codeHash, nowUtc, usable: SentAtUtc is not null)
            : LoginCodeErrors.Invalid(attemptsLeft: null);
    }

    /// <summary>
    /// Si el código de recuperación se verificó con este nonce y no pasó, desde la verificación, otro tanto como su
    /// vigencia: habilita registrar el pedido.
    /// </summary>
    public bool IsVerifiedRecovery(string nonceHash, DateTime nowUtc) =>
        Purpose == LoginCodePurpose.AccountRecovery && InvalidatedAtUtc is null && ConsumedAtUtc is { } consumedAtUtc
        && nowUtc < consumedAtUtc + (ExpiresAtUtc - CreatedAtUtc) && MatchesNonce(nonceHash);

    private bool MatchesNonce(string nonceHash) =>
        NonceHash is not null && nonceHash is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(NonceHash), Encoding.UTF8.GetBytes(nonceHash));

    private Result Check(string codeHash, DateTime nowUtc, bool usable = true)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The verification instant must be UTC.", nameof(nowUtc));
        }

        if (ConsumedAtUtc is not null)
        {
            return LoginCodeErrors.AlreadyUsed;
        }

        if (InvalidatedAtUtc is not null)
        {
            return LoginCodeErrors.Invalid(attemptsLeft: null);
        }

        if (nowUtc >= ExpiresAtUtc)
        {
            return LoginCodeErrors.Expired;
        }

        if (FailedAttempts >= MaxAttempts)
        {
            return LoginCodeErrors.TooManyAttempts;
        }

        // Comparación en tiempo constante: no revela cuántos caracteres coinciden.
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(codeHash))
            | !usable)
        {
            FailedAttempts++;

            return FailedAttempts >= MaxAttempts ? LoginCodeErrors.TooManyAttempts : LoginCodeErrors.Invalid(AttemptsLeft);
        }

        ConsumedAtUtc = nowUtc;

        return Result.Success();
    }
}
