using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVesselMmsi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MmsiNumber",
                table: "vessels",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vessels_MmsiNumber",
                table: "vessels",
                column: "MmsiNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vessels_MmsiNumber",
                table: "vessels");

            migrationBuilder.DropColumn(
                name: "MmsiNumber",
                table: "vessels");
        }
    }
}
