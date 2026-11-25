using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests.Modules.WhatsApp;

/// <summary>
/// ADR 0065: el contenido del chat se guarda cifrado, con el propósito de su esquema, y no se busca, no se ordena ni se compara
/// en SQL; solo se pregunta si es nulo. La bandeja busca por el nombre y el número, que quedan en claro.
/// </summary>
public sealed partial class WhatsAppChatContentTests
{
    private const string Converter = "WhatsAppChatContentConverter";

    private static readonly (Type Entity, string Property)[] Content =
    [
        (typeof(WhatsAppChatMessage), nameof(WhatsAppChatMessage.Text)),
        (typeof(WhatsAppChat), nameof(WhatsAppChat.LastMessagePreview)),
        (typeof(PlatformWhatsAppChatMessage), nameof(PlatformWhatsAppChatMessage.Text)),
        (typeof(PlatformWhatsAppChat), nameof(PlatformWhatsAppChat.LastMessagePreview)),
        (typeof(WhatsAppLinkedMessage), nameof(WhatsAppLinkedMessage.Text)),
        (typeof(WhatsAppConversationLink), nameof(WhatsAppConversationLink.LastMessagePreview)),
    ];

    private static readonly string[] ChatTypes =
        ["WhatsAppChatMessage", "WhatsAppChat", "PlatformWhatsAppChatMessage", "PlatformWhatsAppChat", "WhatsAppLinkedMessage",
            "WhatsAppConversationLink"];

    [Fact]
    public void Chat_content_is_bytea_with_one_purpose_per_schema()
    {
        using var context = ArchitectureModel.CreateContext();
        var columns = Content.Select(item => context.Model.FindEntityType(item.Entity)!.FindProperty(item.Property)!).ToArray();

        Assert.All(columns, property =>
        {
            Assert.Equal("bytea", property.GetColumnType());
            Assert.Equal(Converter, property.GetValueConverter()?.GetType().Name);
        });
        // Un conversor por esquema: tenant con la organización, platform con su subpropósito, engagement con el suyo.
        var bySchema = columns.GroupBy(property => property.DeclaringType.ContainingEntityType.GetSchema()!)
            .ToDictionary(group => group.Key, group => group.Select(property => property.GetValueConverter()).Distinct().ToArray());
        Assert.Equal(["engagement", "platform", "tenant"], bySchema.Keys.Order(StringComparer.Ordinal));
        Assert.All(bySchema.Values, converters => Assert.Single(converters));
        Assert.Equal(3, bySchema.Values.Select(converters => converters[0]).Distinct().Count());
    }

    [Fact]
    public void Every_text_of_a_chat_table_is_encrypted()
    {
        using var context = ArchitectureModel.CreateContext();
        var chatTables = context.Model.GetEntityTypes().Where(entity => ChatTypes.Contains(entity.ClrType.Name)).ToArray();
        Assert.Equal(ChatTypes.Length, chatTables.Length);

        // Lo que se escribió en el chat (texto y vista previa) va cifrado; lo demás de esas tablas queda en claro (0065 §2).
        var plain = chatTables.SelectMany(entity => entity.GetProperties())
            .Where(property => property.Name is "Text" || property.Name.EndsWith("Preview", StringComparison.Ordinal))
            .Where(property => property.GetValueConverter()?.GetType().Name != Converter)
            .Select(property => property.DeclaringType.ClrType.Name + "." + property.Name);
        Assert.Empty(plain);
    }

    [Fact]
    public void No_reader_searches_sorts_or_compares_chat_content()
    {
        var root = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Infrastructure");
        var violations = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin" or "Migrations"))
            .Select(file => (File: Path.GetRelativePath(SolutionRoot.FullPath, file), Source: File.ReadAllText(file)))
            .Where(file => ChatTypes.Any(type => Regex.IsMatch(file.Source, $@"\b{type}\b")))
            .SelectMany(file => FilteredContent(file.Source).Select(hit => file.File + ": " + hit));

        Assert.Empty(violations);
    }

    [Theory]
    [InlineData("rows.Where(message => message.Text.Contains(search))")]
    [InlineData("rows.ApplySearch(request.Search, row => row.ContactName, row => row.LastMessagePreview)")]
    [InlineData("rows.OrderBy(chat => chat.LastMessagePreview)")]
    [InlineData("rows.AnyAsync(message => message.Text == text, ct)")]
    [InlineData("private static readonly SortMap<Row> Sorts = new() { [\"preview\"] = row => row.LastMessagePreview };")]
    [InlineData("rows.GroupJoin(others, message => message.Text, other => other.Text, (message, group) => message.Id)")]
    [InlineData("rows.MaxBy(chat => chat.LastMessagePreview)")]
    [InlineData("rows.DistinctBy(message => message.Text)")]
    [InlineData("rows.LastOrDefaultAsync(message => message.Text == text, ct)")]
    [InlineData("rows.TakeWhile(message => message.Text != text)")]
    public void Detector_rejects_content_in_a_filter_or_an_order(string source) => Assert.NotEmpty(FilteredContent(source));

    [Theory]
    [InlineData("rows.Where(message => message.Text != null)")]
    [InlineData("rows.CountAsync(message => message.Text == null, ct)")]
    [InlineData("rows.Where(message => message.Text is not null)")]
    [InlineData("rows.Select(message => new { message.Id, message.Text })")]
    [InlineData("rows.Where(message => message.Kind == WhatsAppChatMessageKind.Text)")]
    [InlineData("from message in rows where message.Text != null orderby message.CreatedAtUtc select message.Text")]
    [InlineData("from message in rows join other in others on message.Id equals other.Id select new { message.Text, other.Text }")]
    [InlineData("from message in rows group message.Text by message.ChatId into texts select texts.Key")]
    [InlineData("IQueryable<string> Texts<T>(IQueryable<T> rows) where T : WhatsAppChatMessageBase => from message in rows select message.Text;")]
    [InlineData("rows.GroupBy(chat => chat.TeamId).Select(group => new { group.Key, Preview = group.Max(chat => chat.StartedAtUtc) })")]
    public void Detector_accepts_reading_content_and_asking_if_it_is_null(string source) => Assert.Empty(FilteredContent(source));

    /// <summary>
    /// El contenido dentro de lo que filtra, ordena o compara, salvo si solo pregunta si es nulo: el argumento de un operador con
    /// paréntesis y, en la sintaxis de consulta, lo que sigue a <c>where</c>, <c>orderby</c>, el <c>on … equals …</c> de un
    /// <c>join</c> y el <c>by</c> de un <c>group</c>.
    /// </summary>
    internal static IEnumerable<string> FilteredContent(string source)
    {
        var filters = Operator().Matches(source).Select(call => (call.Value, Argument(source, call.Index + call.Length)))
            .Concat(QueryClause().Matches(source).Select(clause => (clause.Value, Clause(source, clause.Index + clause.Length))));
        foreach (var (name, argument) in filters)
        {
            foreach (Match content in ContentAccess().Matches(argument))
            {
                if (!NullCheck().IsMatch(argument[(content.Index + content.Length)..]))
                    yield return name + "… " + content.Value;
            }
        }
    }

    /// <summary>Lo que va entre el paréntesis que abre el operador y el que lo cierra.</summary>
    private static string Argument(string source, int start)
    {
        var depth = 1;
        for (var index = start; index < source.Length; index++)
        {
            depth += source[index] switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            if (depth == 0) return source[start..index];
        }
        return source[start..];
    }

    /// <summary>
    /// Lo que sigue a una cláusula de consulta hasta la cláusula siguiente, el fin de la sentencia o el cierre de lo que la
    /// contiene, sin contar lo que va entre paréntesis.
    /// </summary>
    private static string Clause(string source, int start)
    {
        var depth = 0;
        for (var index = start; index < source.Length; index++)
        {
            depth += source[index] switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            var startsWord = index == 0 || !(char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_');
            if (depth < 0 || depth == 0 && (source[index] == ';' || startsWord && QueryKeyword().IsMatch(source.AsSpan(index))))
                return source[start..index];
        }
        return source[start..];
    }

    [GeneratedRegex(@"\b(?:Where|Any|AnyAsync|All|AllAsync|Count|CountAsync|LongCount|LongCountAsync|OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy|GroupJoin|Distinct|DistinctBy|ExceptBy|IntersectBy|UnionBy|CountBy|Min|Max|MinAsync|MaxAsync|MinBy|MaxBy|First|FirstOrDefault|FirstAsync|FirstOrDefaultAsync|Last|LastOrDefault|LastAsync|LastOrDefaultAsync|Single|SingleOrDefault|SingleAsync|SingleOrDefaultAsync|TakeWhile|SkipWhile|ApplySearch|ApplySort|Join|Contains)\s*\(|\bSortMap\b[^;]*?\{", RegexOptions.CultureInvariant)]
    private static partial Regex Operator();

    /// <summary>
    /// Las cláusulas de consulta que filtran, ordenan o comparan: <c>where</c> (no la restricción de un genérico,
    /// <c>where T : …</c>), <c>orderby</c>, el <c>on</c> de un <c>join</c> y el <c>by</c> de un <c>group</c> (no una variable
    /// <c>group</c> de una lambda).
    /// </summary>
    [GeneratedRegex(@"\bwhere\b(?!\s+\w+\s*:)|\borderby\b|\bjoin\b[^;]*?\bon\b|\bgroup\b(?!\s*(?:=>|\.|,|\)))[^;]*?\bby\b", RegexOptions.CultureInvariant)]
    private static partial Regex QueryClause();

    [GeneratedRegex(@"^(?:select|from|where|orderby|join|let|group|into)\b", RegexOptions.CultureInvariant)]
    private static partial Regex QueryKeyword();

    [GeneratedRegex(@"(?<!Kind)\.(?:Text|LastMessagePreview)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ContentAccess();

    [GeneratedRegex(@"^\s*(?:(?:==|!=)\s*null\b|is\s+(?:not\s+)?null\b)", RegexOptions.CultureInvariant)]
    private static partial Regex NullCheck();
}
