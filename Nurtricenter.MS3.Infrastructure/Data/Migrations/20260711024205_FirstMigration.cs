using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nurtricenter.MS3.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FirstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Invoice_InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Invoice_TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Invoice_IsPaid = table.Column<bool>(type: "boolean", nullable: true),
                    Invoice_Id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChargedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyProductionOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductionOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Label_TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Label_PatientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Label_DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Validation_ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Validation_SupervisorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Validation_IsApproved = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionItems",
                columns: table => new
                {
                    DailyProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionItems", x => new { x.DailyProductionOrderId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductionItems_DailyProductionOrders_DailyProductionOrderId",
                        column: x => x.DailyProductionOrderId,
                        principalTable: "DailyProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PatientId",
                table: "Contracts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlCharges_PatientId",
                table: "ControlCharges",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionOrders_ProductionDate",
                table: "DailyProductionOrders",
                column: "ProductionDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PatientId",
                table: "Packages",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ProductionOrderId",
                table: "Packages",
                column: "ProductionOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "ControlCharges");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "ProductionItems");

            migrationBuilder.DropTable(
                name: "DailyProductionOrders");
        }
    }
}
