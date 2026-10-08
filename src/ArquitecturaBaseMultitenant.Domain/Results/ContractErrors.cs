namespace ArquitecturaBaseMultitenant.Domain.Results;

/// <summary>Estado técnico temporal de una ruta publicada antes de implementar su conducta.</summary>
public static class ContractErrors
{
    public const string NotImplementedCode = "General.Contract.NotImplemented";
    public static readonly Error NotImplemented = Error.NotImplemented(NotImplementedCode, "This endpoint is not implemented yet.");
}
