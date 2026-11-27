using System.Globalization;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>
/// El texto canónico del valor de una opción (C6): el número con punto, sin separador de miles ni ceros de más, y la fecha ISO.
/// Es un valor del contrato, no un texto para una persona: lo que se muestra lo formatea la pantalla con la cultura del pedido.
/// </summary>
public static class FormCatalogValueText
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>El número tal cual, con todos sus dígitos y sin notación científica: 1500, 2500.5, -7.</summary>
    public static string Number(decimal value) =>
        (value / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);
}
