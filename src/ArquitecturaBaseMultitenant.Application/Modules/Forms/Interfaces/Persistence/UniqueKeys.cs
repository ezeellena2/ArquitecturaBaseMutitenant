namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;

/// <summary>
/// Las restricciones únicas del módulo, compartidas por la migración y sus casos de uso. Las dos de las opciones son diferidas
/// (<c>DEFERRABLE INITIALLY DEFERRED</c>, C4): un guardado puede intercambiar códigos o posiciones, y se comprueban al confirmar.
/// El servicio las valida antes, bajo el bloqueo del catálogo; la restricción queda de respaldo.
/// </summary>
public static class UniqueKeys
{
    /// <summary>El código de una opción, único por catálogo sin distinguir mayúsculas (<c>CodeNormalized</c>).</summary>
    public const string FormCatalogItemCode = "AK_FormCatalogItems_TenantId_CatalogId_CodeNormalized";
    /// <summary>La posición de una opción, única por catálogo; la maneja el servidor.</summary>
    public const string FormCatalogItemPosition = "AK_FormCatalogItems_TenantId_CatalogId_Position";
}
