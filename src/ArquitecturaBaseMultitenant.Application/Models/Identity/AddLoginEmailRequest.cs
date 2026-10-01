using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Transporta el correo que se quiere agregar a una cuenta hacia el servicio de métodos de ingreso. Su
/// representación de texto oculta los datos sensibles para evitar exponerlos en logs.
/// </summary>
public sealed record AddLoginEmailRequest(Email? Email);
