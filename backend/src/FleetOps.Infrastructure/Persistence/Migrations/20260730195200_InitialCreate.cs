using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetryReadingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anomalies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cargo_manifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoyageNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    validation_errors = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_manifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vessels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImoNumber = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HomePort = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GrossTonnage = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vessels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cargo_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CargoManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerNumber = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrossWeightKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    OriginPort = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DestinationPort = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HazardClass = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_line_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cargo_line_items_cargo_manifests_CargoManifestId",
                        column: x => x.CargoManifestId,
                        principalTable: "cargo_manifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    SpeedOverGroundKn = table.Column<double>(type: "double precision", nullable: false),
                    EngineRpm = table.Column<int>(type: "integer", nullable: false),
                    FuelFlowLitresPerHour = table.Column<double>(type: "double precision", nullable: false),
                    EngineTempC = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_readings_vessels_VesselId",
                        column: x => x.VesselId,
                        principalTable: "vessels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anomalies_VesselId",
                table: "anomalies",
                column: "VesselId");

            migrationBuilder.CreateIndex(
                name: "ix_anomalies_open",
                table: "anomalies",
                columns: new[] { "IsAcknowledged", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cargo_line_items_CargoManifestId",
                table: "cargo_line_items",
                column: "CargoManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_line_items_ContainerNumber",
                table: "cargo_line_items",
                column: "ContainerNumber");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_manifests_VesselId_VoyageNumber",
                table: "cargo_manifests",
                columns: new[] { "VesselId", "VoyageNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_vessel_recorded_at",
                table: "telemetry_readings",
                columns: new[] { "VesselId", "RecordedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vessels_ImoNumber",
                table: "vessels",
                column: "ImoNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vessels_Status",
                table: "vessels",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anomalies");

            migrationBuilder.DropTable(
                name: "cargo_line_items");

            migrationBuilder.DropTable(
                name: "telemetry_readings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "cargo_manifests");

            migrationBuilder.DropTable(
                name: "vessels");
        }
    }
}
