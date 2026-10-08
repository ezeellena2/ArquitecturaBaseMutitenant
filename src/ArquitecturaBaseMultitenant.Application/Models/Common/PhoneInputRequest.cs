namespace ArquitecturaBaseMultitenant.Application.Models.Common;

/// <summary>País y número sin normalizar; el parser de Application produce el value object.</summary>
public sealed record PhoneInputRequest(string? Country, string? Number)
{
    public override string ToString() => nameof(PhoneInputRequest);
}
