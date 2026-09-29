namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Opciones del inicio del desafío; el callback usa sólo las propiedades protegidas de Google.</summary>
public sealed class ExternalLoginQuery
{
    public string? ReturnUrl { get; set; }

    public string? Access { get; set; }

    public bool Signup { get; set; }

    public bool AcceptedTerms { get; set; }

    public string? ReturnTo { get; set; }

    public string? Culture { get; set; }

    public string? TimeZoneId { get; set; }

    public override string ToString() => nameof(ExternalLoginQuery);
}
