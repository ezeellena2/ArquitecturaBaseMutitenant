namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record VerifyLoginMethodRequest(Guid MethodId, string? Code)
{
    public override string ToString() => nameof(VerifyLoginMethodRequest);
}
