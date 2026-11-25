namespace ArquitecturaBaseMultitenant.ArchitectureTests.Modules.WhatsApp;

/// <summary>
/// Revisión adversarial de la B8 (ADR 0065 §5): la guarda de que ningún lector filtra, ordena ni compara el contenido tiene que
/// verlo también en la sintaxis de consulta, la que ya usan los lectores de WhatsApp (<c>from … join … orderby … select</c> en
/// <c>WhatsAppConversationReader</c> y <c>WhatsAppContactReader</c>). Sobre un <c>bytea</c> cifrado, un <c>where</c> o un
/// <c>orderby</c> por el texto no da error: compara bytes que nunca coinciden y devuelve un resultado vacío o desordenado.
/// </summary>
public sealed class WhatsAppChatContentQuerySyntaxTests
{
    [Theory]
    [InlineData("from message in rows where message.Text.Contains(search) select message.Id")]
    [InlineData("from chat in chats orderby chat.LastMessagePreview, chat.Id select chat.Id")]
    [InlineData("from message in rows join other in others on message.Text equals other.Text select message.Id")]
    [InlineData("from message in rows group message by message.Text into texts select texts.Key")]
    public void Detector_rejects_content_in_a_query_syntax_filter_or_order(string source) =>
        Assert.NotEmpty(WhatsAppChatContentTests.FilteredContent(source));
}
