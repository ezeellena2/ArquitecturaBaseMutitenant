namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
/// <summary>
/// Los identificadores de las respuestas que el bot ofrece: botones y filas de la lista del menú. El webhook acepta solo
/// estos; nunca guarda un texto arbitrario recibido como respuesta.
/// </summary>
public static class BotButtons
{
    public const string SignIn = "bot.sign-in";
    public const string Recover = "bot.recover";
    public const string PromotionsYes = "bot.promotions.yes";
    public const string PromotionsNo = "bot.promotions.no";
    public const string LinkAccount = "bot.link";
    private const string OptionPrefix = "bot.option.";

    public static string Option(Guid id) => OptionPrefix + id.ToString("N");

    /// <summary>La opción del menú que eligió, si la respuesta es una fila de la lista.</summary>
    public static Guid? ParseOption(string? reply) =>
        reply is { Length: 43 } && reply.StartsWith(OptionPrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(reply.AsSpan(OptionPrefix.Length), "N", out var id) ? id : null;

    public static bool IsKnown(string? reply) => reply is SignIn or Recover or PromotionsYes or PromotionsNo or LinkAccount || ParseOption(reply) is not null;
}
