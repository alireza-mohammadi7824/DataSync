using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

[DbContext(typeof(HRSDataIntegrationDbContext))]
[Migration("20240613000001_Monitoring_History_Init")]
public partial class Monitoring_History_Init : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MonitoringStatusHistory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromStatus = table.Column<int>(type: "int", nullable: false),
                ToStatus = table.Column<int>(type: "int", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                TriggerSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ResponseTimeMs = table.Column<int>(type: "int", nullable: true),
                ErrorSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonitoringStatusHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonitoringStatusHistory_MonitoringTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "MonitoringTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MonitoringOutages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureCount = table.Column<int>(type: "int", nullable: false),
                TotalDurationSec = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MonitoringOutages", x => x.Id);
                table.ForeignKey(
                    name: "FK_MonitoringOutages_MonitoringTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "MonitoringTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

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
            name: "IX_MonitoringTargets_IsActive_NextDueAt",
            table: "MonitoringTargets",
            columns: new[] { "IsActive", "NextDueAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_MonitoringTargets_IsActive_NextDueAt",
            table: "MonitoringTargets");

        migrationBuilder.DropTable(
            name: "MonitoringStatusHistory");

        migrationBuilder.DropTable(
            name: "MonitoringOutages");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.4");

        modelBuilder.Entity("Monitoring.Targets.MonitoringTarget", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("Category")
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<int>("CheckIntervalSeconds")
                .HasColumnType("int");

            b.Property<int>("ConsecutiveFailures")
                .HasColumnType("int");

            b.Property<string>("ConcurrencyStamp")
                .HasColumnType("nvarchar(40)")
                .HasMaxLength(40);

            b.Property<DateTime>("CreationTime")
                .HasColumnType("datetime2");

            b.Property<Guid?>("CreatorId")
                .HasColumnType("uniqueidentifier");

            b.Property<int>("CurrentStatus")
                .HasColumnType("int");

            b.Property<Guid?>("DeleterId")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("DeletionTime")
                .HasColumnType("datetime2");

            b.Property<string>("Endpoint")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("nvarchar(512)");

            b.Property<string>("ExtraProperties")
                .HasColumnType("nvarchar(max)");

            b.Property<DateTime?>("FirstDownAt")
                .HasColumnType("datetime2");

            b.Property<bool>("IsActive")
                .HasColumnType("bit");

            b.Property<bool>("IsDeleted")
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime?>("LastCheckedAt")
                .HasColumnType("datetime2");

            b.Property<Guid?>("LastModifierId")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("LastModificationTime")
                .HasColumnType("datetime2");

            b.Property<DateTime?>("LastStatusChangeAt")
                .HasColumnType("datetime2");

            b.Property<DateTime?>("LastUpAt")
                .HasColumnType("datetime2");

            b.Property<int>("MaxRetryAttempts")
                .HasColumnType("int");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            b.Property<DateTime>("NextDueAt")
                .HasColumnType("datetime2");

            b.Property<int>("RetryDelaySeconds")
                .HasColumnType("int");

            b.Property<string>("SettingsJson")
                .HasMaxLength(4000)
                .HasColumnType("nvarchar(4000)");

            b.Property<int>("TimeoutSeconds")
                .HasColumnType("int");

            b.Property<int>("Type")
                .HasColumnType("int");

            b.HasKey("Id");

            b.HasIndex("IsActive");
            b.HasIndex("IsActive", "NextDueAt")
                .HasDatabaseName("IX_MonitoringTargets_IsActive_NextDueAt");

            b.ToTable("MonitoringTargets", (string)null);
        });

        modelBuilder.Entity("Monitoring.Targets.OutageWindow", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("EndedAt")
                .HasColumnType("datetime2");

            b.Property<int>("FailureCount")
                .HasColumnType("int");

            b.Property<DateTime>("StartedAt")
                .HasColumnType("datetime2");

            b.Property<int?>("TotalDurationSec")
                .HasColumnType("int");

            b.Property<Guid>("TargetId")
                .HasColumnType("uniqueidentifier");

            b.HasKey("Id");

            b.HasIndex("TargetId", "StartedAt")
                .HasDatabaseName("IX_Target_StartedAt")
                .IsDescending(true, true);

            b.ToTable("MonitoringOutages", (string)null);

            b.HasOne("Monitoring.Targets.MonitoringTarget", null)
                .WithMany()
                .HasForeignKey("TargetId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("Monitoring.Targets.ServiceStatusHistory", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime>("ChangedAt")
                .HasColumnType("datetime2");

            b.Property<string>("ErrorSummary")
                .HasMaxLength(512)
                .HasColumnType("nvarchar(512)");

            b.Property<int>("FromStatus")
                .HasColumnType("int");

            b.Property<int?>("ResponseTimeMs")
                .HasColumnType("int");

            b.Property<string>("TriggerSource")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("nvarchar(32)");

            b.Property<int>("ToStatus")
                .HasColumnType("int");

            b.Property<Guid>("TargetId")
                .HasColumnType("uniqueidentifier");

            b.HasKey("Id");

            b.HasIndex("TargetId", "ChangedAt")
                .HasDatabaseName("IX_Target_ChangedAt")
                .IsDescending(true, true);

            b.ToTable("MonitoringStatusHistory", (string)null);

            b.HasOne("Monitoring.Targets.MonitoringTarget", null)
                .WithMany()
                .HasForeignKey("TargetId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });
    }
}
