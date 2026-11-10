namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>
/// Un aviso por correo con el armado del tablero Mensajes y sus textos ya traducidos y formateados: título, párrafos,
/// datos, botón, notas y su pie propio. El renderer del núcleo los escapa, los ubica y suma el pie de todos los correos;
/// así un módulo arma sus avisos sin una plantilla propia.
/// </summary>
public sealed record EmailNotice(string Subject, string Title, IReadOnlyList<string> Paragraphs)
{
    /// <summary>La grilla de datos, como «Organización: Grupo Delta».</summary>
    public IReadOnlyList<EmailNoticeDetail> Details { get; init; } = [];

    public EmailNoticeAction? Action { get; init; }

    /// <summary>Las aclaraciones en gris, debajo del botón.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Por qué le llega a esa persona («Te llega porque estás en el equipo Ventas…»), arriba del pie común.</summary>
    public string? Footer { get; init; }

    public override string ToString() => "EmailNotice [redacted]";
}

public sealed record EmailNoticeDetail(string Label, string Value);

public sealed record EmailNoticeAction(string Label, string Url)
{
    public override string ToString() => "EmailNoticeAction [redacted]";
}
