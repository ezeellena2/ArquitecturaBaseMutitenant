namespace ArquitecturaBaseMultitenant.Api.Contracts.Common;

/// <summary>País elegido y número escrito; el parser del servicio produce E.164.</summary>
public sealed record PhoneInputHttpRequest(string? Country, string? Number)
{
    public override string ToString() => nameof(PhoneInputHttpRequest);
}
