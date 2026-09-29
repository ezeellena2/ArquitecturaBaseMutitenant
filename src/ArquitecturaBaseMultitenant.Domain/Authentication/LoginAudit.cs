using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Intento append-only sin dirección, código ni detalle de una excepción.</summary>
public sealed class LoginAudit : Entity
{
    private LoginAudit() { }

    private LoginAudit(Guid? userId, LoginAuditMethod method, bool succeeded, string? failureCode, DateTime occurredAtUtc)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        if (userId == Guid.Empty || succeeded && userId is null)
        {
            throw new ArgumentException("A successful login needs a valid user id.", nameof(userId));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));
        }

        UserId = userId;
        Method = method;
        Succeeded = succeeded;
        FailureCode = failureCode;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid? UserId { get; private set; }
    public LoginAuditMethod Method { get; private set; }
    public bool Succeeded { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static LoginAudit Success(Guid userId, LoginAuditMethod method, DateTime occurredAtUtc) =>
        new(userId, method, succeeded: true, failureCode: null, occurredAtUtc);

    public static LoginAudit Failure(Guid? userId, LoginAuditMethod method, Error error, DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error == Error.None)
        {
            throw new ArgumentException("A failed login needs an error code.", nameof(error));
        }

        return new LoginAudit(userId, method, succeeded: false, error.Code, occurredAtUtc);
    }
}
