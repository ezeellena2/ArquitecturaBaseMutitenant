using System.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// 0064 §11: las trazas del asistente (su <c>AddSource</c> va en el módulo de la Api). El turno guarda el contexto W3C de quien lo
/// pidió y el worker lo continúa con una actividad por turno; un turno fallido queda en error con su código. Las etiquetas llevan
/// solo ids y códigos, nunca una pregunta, una respuesta, un archivo ni el texto de un error del proveedor.
/// </summary>
public static class AssistantTracing
{
    public const string SourceName = "ArquitecturaBaseMultitenant.Assistant";

    private static readonly ActivitySource Source = new(SourceName);

    /// <summary>El contexto que se guarda en el turno: solo uno W3C entra en su columna.</summary>
    internal static string? Current() => Activity.Current is { IdFormat: ActivityIdFormat.W3C, Id: { Length: 55 } id } ? id : null;

    /// <summary>Un contexto guardado que no se puede leer se ignora: el turno abre su propia traza.</summary>
    internal static Activity? StartTurn(Guid turnId, string? traceParent)
    {
        var activity = Source.StartActivity("assistant.turn", ActivityKind.Consumer,
            ActivityContext.TryParse(traceParent, null, isRemote: true, out var parent) ? parent : default);
        activity?.SetTag("assistant.turn.id", turnId.ToString("D"));
        return activity;
    }

    /// <summary>Un turno que no terminó completo queda en error, con su código; uno completo queda como está.</summary>
    internal static void MarkOutcome(Activity? activity, string terminal, string? code)
    {
        if (activity is null || terminal == "Complete") return;
        activity.SetTag("assistant.turn.status", terminal);
        activity.SetStatus(ActivityStatusCode.Error, code);
    }
}
