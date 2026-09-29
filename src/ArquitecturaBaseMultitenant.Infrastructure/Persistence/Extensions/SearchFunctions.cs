using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Traducción de la función inmutable usada por los índices trigram de PostgreSQL.</summary>
public static class SearchFunctions
{
    [DbFunction("f_unaccent", Schema = "public")]
    public static string Unaccent(string value) => throw new NotSupportedException("Database query only.");
}
