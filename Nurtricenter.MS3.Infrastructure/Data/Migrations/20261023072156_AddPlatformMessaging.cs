using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtricenter.MS3.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "OutgoingIntegrationEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogPlans",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalDays = table.Column<int>(type: "integer", nullable: false),
                    MealsPerDay = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogPlans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRecipes",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRecipes", x => x.RecipeId);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MessageCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingIntegrationEvents_Status",
                table: "OutgoingIntegrationEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedOn",
                table: "InboxMessages",
                column: "ProcessedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogPlans");

            migrationBuilder.DropTable(
                name: "CatalogRecipes");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutgoingIntegrationEvents_Status",
                table: "OutgoingIntegrationEvents");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "OutgoingIntegrationEvents");
        }
    }
}
