using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

[DbContext(typeof(HRSDataIntegrationDbContext))]
[Migration("20240613000003_Monitoring_Alerts_ExtendOutage")]
public partial class Monitoring_Alerts_ExtendOutage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AlertsSent",
            table: "MonitoringOutages",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastAlertAt",
            table: "MonitoringOutages",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AlertsSent",
            table: "MonitoringOutages");

        migrationBuilder.DropColumn(
            name: "LastAlertAt",
            table: "MonitoringOutages");
    }
}
