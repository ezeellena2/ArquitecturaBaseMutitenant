namespace ArquitecturaBaseMultitenant.Application.Models.Retention;

/// <summary>
/// Un lote de una purga por fecha (ADR 0062): cuántas filas borró y desde dónde sigue el próximo lote, o null si ya no
/// queda nada que mirar. El cursor avanza también sobre las filas que se conservan, como la última de cada cuenta, para que
/// una corrida no las vuelva a recorrer en cada lote.
/// </summary>
public sealed record RetentionBatch(int Removed, RetentionCursor? Next);

/// <summary>La última fila que miró un lote, por su fecha y su Id, en el orden en que recorre la tabla.</summary>
public sealed record RetentionCursor(DateTime AtUtc, Guid Id);
