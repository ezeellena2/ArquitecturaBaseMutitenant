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
            case LoginCodePurpose.Login or LoginCodePurpose.Signup when requestedByUserId is not null:
                throw new ArgumentException("A login or signup code cannot be requested by an account.", nameof(requestedByUserId));

            case LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate
                when requestedByUserId is null || requestedByUserId == Guid.Empty:
                throw new ArgumentException(
                    "The code needs the account that requested it.", nameof(requestedByUserId));

            case LoginCodePurpose.Login or LoginCodePurpose.Signup
                or LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate:
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

        return expectedPurpose is LoginCodePurpose.Login or LoginCodePurpose.Signup && Purpose == expectedPurpose
            ? Check(codeHash, nowUtc)
            : LoginCodeErrors.Invalid(attemptsLeft: null);
    }

    /// <summary>
    /// Verifica un código de una cuenta para la cuenta <paramref name="userId"/>.
    /// Si el código es de ingreso o lo pidió otra cuenta, la respuesta es la misma que cuando no hay código y no se
    /// toca ningún contador: así no se sabe que otra cuenta está vinculando ese destino, ni se le gastan los intentos.
    /// </summary>
    public Result VerifyFor(Guid userId, LoginCodePurpose expectedPurpose, string codeHash, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(codeHash);

        return expectedPurpose is LoginCodePurpose.VerifyDestination or LoginCodePurpose.Reauthenticate
            && Purpose == expectedPurpose && RequestedByUserId == userId
            ? Check(codeHash, nowUtc)
            : LoginCodeErrors.Invalid(attemptsLeft: null);
    }

    private Result Check(string codeHash, DateTime nowUtc)
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
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(CodeHash), Encoding.UTF8.GetBytes(codeHash)))
        {
            FailedAttempts++;

            return FailedAttempts >= MaxAttempts ? LoginCodeErrors.TooManyAttempts : LoginCodeErrors.Invalid(AttemptsLeft);
        }

        ConsumedAtUtc = nowUtc;

        return Result.Success();
    }
}
