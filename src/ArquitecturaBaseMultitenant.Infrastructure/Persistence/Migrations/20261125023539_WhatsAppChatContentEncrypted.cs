using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR 0065 (B8): el contenido del chat pasa a <c>bytea</c>, cifrado por el conversor de EF con el propósito de su esquema: el
    /// texto de los mensajes y la vista previa de los chats, de la organización y de Plataforma, y los de la copia vinculada. Los
    /// chats guardados son de prueba (0065 §7): la migración los vacía, porque el texto en claro no se convierte en un cifrado, y
    /// el resto de cada fila queda. EF no sabe pasar <c>text</c> a <c>bytea</c>, así que cada columna cambia con <c>USING NULL</c>;
    /// un <c>ALTER</c> no pasa por la RLS, así que alcanza a todas las organizaciones de una vez. Excepciones anotadas de esta
    /// etapa (todavía no hay despliegue, 123): la 0060 pide agregar la columna cifrada, escribirla un despliegue después y borrar la
    /// de texto al final, y acá va en un solo paso (lo admite la 0065 mientras no haya datos reales); y el <c>Down</c> vuelve a
    /// <c>text</c> vacío en lugar de negarse con datos, porque la versión anterior no lee el cifrado.
    /// </summary>
    public partial class WhatsAppChatContentEncrypted : Migration
    {
        private static readonly (string Schema, string Table, string Column, int Length)[] Content =
        [
            ("tenant", "WhatsAppChatMessages", "Text", 4096),
            ("tenant", "WhatsAppChats", "LastMessagePreview", 120),
            ("platform", "PlatformWhatsAppChatMessages", "Text", 4096),
            ("platform", "PlatformWhatsAppChats", "LastMessagePreview", 120),
            ("engagement", "WhatsAppLinkedMessages", "Text", 4096),
            ("engagement", "WhatsAppConversationLinks", "LastMessagePreview", 120),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table, column, _) in Content)
                migrationBuilder.Sql($"ALTER TABLE {schema}.\"{table}\" ALTER COLUMN \"{column}\" TYPE bytea USING NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table, column, length) in Content)
                migrationBuilder.Sql($"ALTER TABLE {schema}.\"{table}\" ALTER COLUMN \"{column}\" TYPE character varying({length}) USING NULL;");
        }
    }
}
