namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// El turno de un mensaje en la cola (ADR 0066 §6): lo que contesta a alguien sale antes que los avisos. Se guarda como
/// <c>smallint</c>.
/// </summary>
public enum OutboxPriority : short
{
    /// <summary>Contesta a alguien que espera: el operador, el bot, un código o una confirmación.</summary>
    Reply = 0,

    /// <summary>Un aviso: el valor por defecto.</summary>
    Notice = 1,
}
