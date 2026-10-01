using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Authentication;

/// <summary>
/// Comprueba que la auditoría de ingreso conserve el resultado y su código estable de error. Protege la
/// ausencia de direcciones y códigos de ingreso en el rastro.
/// </summary>
public sealed class LoginAuditTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 9, 29, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Successful_attempt_keeps_actor_method_and_instant_without_an_address_or_code()
    {
        var userId = Guid.CreateVersion7();

        var audit = LoginAudit.Success(userId, LoginAuditMethod.Google, OccurredAtUtc);

        Assert.Equal(userId, audit.UserId);
        Assert.Equal(LoginAuditMethod.Google, audit.Method);
        Assert.Equal(OccurredAtUtc, audit.OccurredAtUtc);
        Assert.True(audit.Succeeded);
        Assert.Null(audit.FailureCode);
        Assert.Null(typeof(LoginAudit).GetProperty("Identifier"));
        Assert.Null(typeof(LoginAudit).GetProperty("IpAddress"));
        Assert.Null(typeof(LoginAudit).GetProperty("UserAgent"));
    }

    [Fact]
    public void Failed_attempt_keeps_only_a_stable_error_code()
    {
        var error = Error.Validation("Auth.LoginCode.Invalid", "The secret ana@example.com was rejected.");

        var audit = LoginAudit.Failure(null, LoginAuditMethod.Code, error, OccurredAtUtc);

        Assert.False(audit.Succeeded);
        Assert.Null(audit.UserId);
        Assert.Equal(LoginAuditMethod.Code, audit.Method);
        Assert.Equal("Auth.LoginCode.Invalid", audit.FailureCode);
        Assert.DoesNotContain("ana@example.com", audit.FailureCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_requires_a_utc_instant()
    {
        var local = new DateTime(2026, 9, 29, 13, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => LoginAudit.Success(Guid.CreateVersion7(), LoginAuditMethod.Code, local));
    }
}
