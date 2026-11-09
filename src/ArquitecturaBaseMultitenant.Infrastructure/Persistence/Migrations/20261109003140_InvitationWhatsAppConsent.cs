using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvitationWhatsAppConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // «Cargar teléfonos» (179): quién confirmó que la persona aceptó recibir mensajes por WhatsApp, y cuándo. Nulos en
            // las invitaciones por correo y en las anteriores: no se completan.
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentConfirmedAtUtc",
                schema: "tenant",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsentConfirmedByUserId",
                schema: "tenant",
                table: "Invitations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Quién confirmó el consentimiento y cuándo es la prueba de la decisión 179: se niega antes que borrarlo. La
            // tabla fuerza la RLS también para su dueño, que corre la migración sin organización: sin NO FORCE la guarda no
            // vería ninguna invitación. Va en la transacción de la migración: si se niega, la RLS sigue forzada. El SQL va en
            // una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."Invitations" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."Invitations" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."Invitations" WHERE "ConsentConfirmedByUserId" IS NOT NULL OR "ConsentConfirmedAtUtc" IS NOT NULL) THEN RAISE EXCEPTION 'A confirmed WhatsApp consent cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."Invitations" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropColumn(
                name: "ConsentConfirmedAtUtc",
                schema: "tenant",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "ConsentConfirmedByUserId",
                schema: "tenant",
                table: "Invitations");
        }
    }
}
