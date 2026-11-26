using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>El tope de una clave para una organización, o el máximo de la plataforma sin organización (ADR 0069 §4).</summary>
public sealed record ModuleLimitCeiling(string Key, int? Count, Money? Amount)
{
    /// <summary>El valor de entrada que declara el módulo; null si la organización no elige y vale el tope.</summary>
    public int? Default { get; init; }

    /// <summary>
    /// Lo que vale en cada uso: el menor entre lo que eligió la organización (o, sin elección, el de entrada) y el tope.
    /// Solo un Count tiene valor efectivo; pedirlo a un Amount es un bug.
    /// </summary>
    public int Effective(int? organizationValue)
    {
        if (Count is not { } ceiling)
            throw new InvalidOperationException($"Module limit '{Key}' is not a Count.");
        return (organizationValue ?? Default) is { } chosen ? Math.Min(chosen, ceiling) : ceiling;
    }
}
