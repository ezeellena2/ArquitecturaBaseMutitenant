using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>El tope de una clave para una organización, o el máximo de la plataforma sin organización (ADR 0069 §4).</summary>
public sealed record ModuleLimitCeiling(string Key, int? Count, Money? Amount);
