using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

[DbContext(typeof(HRSDataIntegrationDbContext))]
[Migration("20240613000004_Monitoring_RunLocks")]
public partial class Monitoring_RunLocks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MonitoringRunLocks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                NodeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonitoringRunLocks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringRunLocks_TargetId",
            table: "MonitoringRunLocks",
            column: "TargetId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MonitoringRunLocks");
    }
}
