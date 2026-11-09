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
