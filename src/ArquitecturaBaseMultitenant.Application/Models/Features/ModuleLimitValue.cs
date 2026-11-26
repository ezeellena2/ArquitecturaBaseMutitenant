using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>El máximo de la plataforma que lee un módulo: Count o Amount, uno solo cargado según la clase del tope.</summary>
public sealed record ModuleLimitValue(int? Count, Money? Amount);
