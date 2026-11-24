namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Cómo se escribe un mes como su rango (decisión 205): nunca en letras.</summary>
public enum MonthRangeStyle
{
    /// <summary>«del 01/10/2026 al 31/10/2026»: dentro de una oración.</summary>
    Sentence = 0,

    /// <summary>«01/10/2026 – 31/10/2026»: como rótulo o dato, igual que <c>formatMonth</c> del front.</summary>
    Label = 1,
}
