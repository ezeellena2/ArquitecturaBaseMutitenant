namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>
/// Un catálogo de permisos con lo que incluye cada uno (decisión 260): el permiso que no sirve sin el «Ver» de su misma
/// área lo incluye, y quien lo tiene tiene también lo incluido. Al componerse falla por un código repetido, por una
/// inclusión declarada para un código que no es del catálogo, y por lo incluido que no es de su mismo catálogo
/// (organización, empresa o plataforma) o de su misma área: el prefijo hasta el último punto, así
/// <c>company.members.manage</c> solo incluye <c>company.members.*</c>. Lo que permiten unos permisos es
/// <see cref="Effective"/>, la única función de lo efectivo.
/// </summary>
public sealed class PermissionInclusions
{
    private readonly Dictionary<string, int> positions;
    private readonly Dictionary<string, IReadOnlyList<string>> closures;

    private PermissionInclusions(IReadOnlyList<string> all, Dictionary<string, IReadOnlyList<string>> closures)
    {
        All = all;
        positions = all.Select((code, position) => (code, position))
            .ToDictionary(pair => pair.code, pair => pair.position, StringComparer.Ordinal);
        this.closures = closures;
    }

    /// <summary>Los códigos del catálogo, en su orden.</summary>
    public IReadOnlyList<string> All { get; }

    /// <summary>
    /// Compone el catálogo con sus partes (cada una es un catálogo propio: lo incluido nunca cruza de una a otra) y con lo
    /// que incluye cada permiso, directo; la cadena la arma esta clase.
    /// </summary>
    public static PermissionInclusions Compose(IReadOnlyCollection<IReadOnlyCollection<string>> catalogs,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> includes)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(includes);

        var all = new List<string>();
        var catalogOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (codes, catalog) in catalogs.Select((codes, catalog) => (codes, catalog)))
        {
            foreach (var code in codes)
            {
                if (!catalogOf.TryAdd(code, catalog))
                    throw new InvalidOperationException($"The permission '{code}' is repeated in the catalog.");
                all.Add(code);
            }
        }

        foreach (var (code, included) in includes)
        {
            if (!catalogOf.TryGetValue(code, out var catalog))
                throw new InvalidOperationException($"The permission '{code}' declares inclusions but is not in the catalog.");
            foreach (var other in included)
            {
                if (!catalogOf.TryGetValue(other, out var otherCatalog) || otherCatalog != catalog)
                    throw new InvalidOperationException(
                        $"The permission '{code}' can only include permissions of its own catalog, not '{other}'.");
                if (!string.Equals(AreaOf(other), AreaOf(code), StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"The permission '{code}' can only include permissions of its own area, not '{other}'.");
            }
        }

        var closures = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var code in all)
            closures.Add(code, Close(code, includes, all));
        return new PermissionInclusions(all.AsReadOnly(), closures);
    }

    public bool Contains(string code) => positions.ContainsKey(code);

    /// <summary>
    /// Lo que incluye <paramref name="code"/>, directo o en cadena, en el orden del catálogo; vacío si no incluye nada o no
    /// es del catálogo.
    /// </summary>
    public IReadOnlyList<string> Of(string code) => closures.TryGetValue(code, out var included) ? included : [];

    /// <summary>
    /// Lo que permiten estos permisos: los del catálogo más lo que incluyen, en cadena, en el orden del catálogo. Un código
    /// que no es del catálogo no permite nada.
    /// </summary>
    public IReadOnlyList<string> Effective(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in permissions.Where(Contains))
        {
            allowed.Add(code);
            allowed.UnionWith(Of(code));
        }

        return [.. allowed.OrderBy(code => positions[code])];
    }

    /// <summary>
    /// Lo pedido más lo que incluye, para guardarlo: un código que no es del catálogo se conserva. Al guardar un rol, así la
    /// validación del dominio lo rechaza en lugar de perderlo en silencio; en la caché compartida, así lo encuentra la
    /// versión con la que convive y que sí lo conoce (ADR 0060). Lo que permite es <see cref="Effective"/>.
    /// </summary>
    public IReadOnlyList<string> WithIncluded(IEnumerable<string> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        var codes = requested.ToList();
        return [.. codes.Concat(codes.SelectMany(Of)).Distinct(StringComparer.Ordinal)];
    }

    // Lo que incluye un código, también a través de lo que incluye, sin él mismo y en el orden del catálogo.
    private static IReadOnlyList<string> Close(string code,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> includes, List<string> all)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([code]);
        while (pending.TryPop(out var current))
        {
            if (!includes.TryGetValue(current, out var next)) continue;
            foreach (var included in next)
            {
                if (!string.Equals(included, code, StringComparison.Ordinal) && reached.Add(included))
                    pending.Push(included);
            }
        }

        return [.. all.Where(reached.Contains)];
    }

    private static string AreaOf(string code) =>
        code.LastIndexOf('.') is var dot and >= 0 ? code[..dot] : code;
}
