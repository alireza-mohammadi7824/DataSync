using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

public partial class Monitoring_AlertPoliciesV2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MonitoringAlertPolicies");

        migrationBuilder.CreateTable(
            name: "MonitoringAlertPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OnDown = table.Column<bool>(type: "bit", nullable: false),
                OnUp = table.Column<bool>(type: "bit", nullable: false),
                MinDownDurationSeconds = table.Column<int>(type: "int", nullable: false),
                CooldownSeconds = table.Column<int>(type: "int", nullable: false),
                Emails = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false, defaultValue: string.Empty),
                WebhookUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonitoringAlertPolicies", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "MonitoringTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringAlertPolicies_TargetId",
            table: "MonitoringAlertPolicies",
            column: "TargetId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MonitoringAlertPolicies");

        migrationBuilder.CreateTable(
            name: "MonitoringAlertPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Enabled = table.Column<bool>(type: "bit", nullable: false),
                NotifyAfterFailures = table.Column<int>(type: "int", nullable: false),
                RepeatMinutes = table.Column<int>(type: "int", nullable: false),
                RecoverQuietMinutes = table.Column<int>(type: "int", nullable: false),
                ChannelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                SuppressDuringMaintenance = table.Column<bool>(type: "bit", nullable: false),
                ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonitoringAlertPolicies", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonitoringAlertPolicies_MonitoringTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "MonitoringTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringAlertPolicies_TargetId",
            table: "MonitoringAlertPolicies",
            column: "TargetId",
            unique: true);
    }
}
