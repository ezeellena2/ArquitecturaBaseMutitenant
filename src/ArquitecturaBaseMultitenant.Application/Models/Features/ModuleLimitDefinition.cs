namespace ArquitecturaBaseMultitenant.Application.Models.Features;

/// <summary>
/// Un tope que declara un módulo, con la clave <c>&lt;módulo&gt;.&lt;campo&gt;</c> y el módulo como la clave de su
/// feature. En un Count, Minimum va de 0 en adelante y Default, si está, no es menor que el mínimo; Default nulo dice
/// que la organización no elige y vale el tope. En un Amount los dos van nulos: lo valida el validador del módulo.
/// </summary>
public sealed record ModuleLimitDefinition(string Key, ModuleLimitKind Kind, int? Default, int? Minimum);
