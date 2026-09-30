namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Entrega el código para verificar un método de ingreso sin incluirlo al representar el pedido como texto.</summary>
public sealed record VerifyLoginMethodRequest(Guid MethodId, string? Code)
{
    public override string ToString() => nameof(VerifyLoginMethodRequest);
}
