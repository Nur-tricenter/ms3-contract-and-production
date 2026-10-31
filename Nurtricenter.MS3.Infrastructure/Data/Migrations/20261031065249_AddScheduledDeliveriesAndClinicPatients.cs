using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nurtricenter.MS3.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledDeliveriesAndClinicPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicPatients",
                columns: table => new
                {
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicPatients", x => x.PatientId);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledDeliveries",
                columns: table => new
                {
                    DeliveryDayId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hour = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    MenuJson = table.Column<string>(type: "text", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledDeliveries", x => x.DeliveryDayId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledDeliveries_DeliveryDate",
                table: "ScheduledDeliveries",
                column: "DeliveryDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicPatients");

            migrationBuilder.DropTable(
                name: "ScheduledDeliveries");
        }
    }
}
