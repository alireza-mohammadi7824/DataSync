using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

public partial class Monitoring_SoftDeleteAndIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGlobal",
            table: "MonitoringMaintenance",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE MonitoringMaintenance SET IsGlobal = CASE WHEN TargetId IS NULL THEN 1 ELSE 0 END");

        migrationBuilder.DropIndex(
            name: "IX_Target_ChangedAt",
            table: "MonitoringStatusHistory");

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringStatusHistory_Target_ChangedAt",
            table: "MonitoringStatusHistory",
            columns: new[] { "TargetId", "ChangedAt" });

        migrationBuilder.DropIndex(
            name: "IX_Target_StartedAt",
            table: "MonitoringOutages");

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringOutages_Target_Start",
            table: "MonitoringOutages",
            columns: new[] { "TargetId", "StartedAt" },
            descending: new[] { true, true });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringOutages_Target_End",
            table: "MonitoringOutages",
            columns: new[] { "TargetId", "EndedAt" });

        migrationBuilder.DropIndex(
            name: "IX_Target_Start_End",
            table: "MonitoringMaintenance");

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringMaintenance_Global_Start_End",
            table: "MonitoringMaintenance",
            columns: new[] { "IsGlobal", "StartUtc", "EndUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringMaintenance_Target_Start_End",
            table: "MonitoringMaintenance",
            columns: new[] { "TargetId", "StartUtc", "EndUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringTargets_Type_IsActive",
            table: "MonitoringTargets",
            columns: new[] { "Type", "IsActive" });

        migrationBuilder.DropForeignKey(
            name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
            table: "MonitoringAlertPolicies");

        migrationBuilder.AddForeignKey(
            name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
            table: "MonitoringAlertPolicies",
            column: "TargetId",
            principalTable: "MonitoringTargets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MonitoringRunLocks_MonitoringTargets_TargetId",
            table: "MonitoringRunLocks",
            column: "TargetId",
            principalTable: "MonitoringTargets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MonitoringRunLocks_MonitoringTargets_TargetId",
            table: "MonitoringRunLocks");

        migrationBuilder.DropForeignKey(
            name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
            table: "MonitoringAlertPolicies");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringTargets_Type_IsActive",
            table: "MonitoringTargets");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringOutages_Target_End",
            table: "MonitoringOutages");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringOutages_Target_Start",
            table: "MonitoringOutages");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringMaintenance_Global_Start_End",
            table: "MonitoringMaintenance");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringMaintenance_Target_Start_End",
            table: "MonitoringMaintenance");

        migrationBuilder.DropIndex(
            name: "IX_MonitoringStatusHistory_Target_ChangedAt",
            table: "MonitoringStatusHistory");

        migrationBuilder.DropColumn(
            name: "IsGlobal",
            table: "MonitoringMaintenance");

        migrationBuilder.CreateIndex(
            name: "IX_Target_ChangedAt",
            table: "MonitoringStatusHistory",
            columns: new[] { "TargetId", "ChangedAt" },
            descending: new[] { true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Target_StartedAt",
            table: "MonitoringOutages",
            columns: new[] { "TargetId", "StartedAt" },
            descending: new[] { true, true });

        migrationBuilder.CreateIndex(
            name: "IX_Target_Start_End",
            table: "MonitoringMaintenance",
            columns: new[] { "TargetId", "StartUtc", "EndUtc" });

        migrationBuilder.AddForeignKey(
            name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
            table: "MonitoringAlertPolicies",
            column: "TargetId",
            principalTable: "MonitoringTargets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
