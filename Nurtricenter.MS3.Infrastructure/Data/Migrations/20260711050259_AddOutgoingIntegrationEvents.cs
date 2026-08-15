using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtricenter.MS3.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutgoingIntegrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutgoingIntegrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetService = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingIntegrationEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingIntegrationEvents_CreatedAt",
                table: "OutgoingIntegrationEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingIntegrationEvents_EventType",
                table: "OutgoingIntegrationEvents",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutgoingIntegrationEvents");
        }
    }
}
