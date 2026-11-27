using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppContactProfileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dos versiones conviven (ADR 0060): las dos columnas aceptan nulos y la versión anterior no las lee. Una copia vieja
            // que procesa un mensaje guardado por la nueva no vacía su nombre de perfil; lo vacía la purga de 90 días de la nueva.
            migrationBuilder.AddColumn<string>(
                name: "EncryptedProfileName",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileNameTakenAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un contacto que ya tomó el nombre de su perfil perdería la marca, y al volver a subir se llenaría otra vez un
            // nombre que una persona cambió o vació; un mensaje sin procesar perdería el nombre que trae: se niega antes que
            // borrarlos, antes de cualquier DDL (guía de migración, paso 4). WhatsAppCustomers fuerza la RLS también para su
            // dueño, que corre la migración sin organización: sin NO FORCE la guarda no vería ningún contacto. Va en la
            // transacción de la migración: si se niega, la RLS sigue forzada. El SQL va en una línea: no lleva \r aunque el
            // archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE identity."WhatsAppMessages", tenant."WhatsAppCustomers" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."WhatsAppCustomers" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM identity."WhatsAppMessages" WHERE "EncryptedProfileName" IS NOT NULL) OR EXISTS (SELECT 1 FROM tenant."WhatsAppCustomers" WHERE "ProfileNameTakenAtUtc" IS NOT NULL) THEN RAISE EXCEPTION 'A WhatsApp profile name cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."WhatsAppCustomers" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "EncryptedProfileName",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "ProfileNameTakenAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers");
        }
    }
}
