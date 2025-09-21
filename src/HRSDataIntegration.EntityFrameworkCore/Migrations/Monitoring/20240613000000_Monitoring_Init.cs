using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRSDataIntegration.Migrations.Monitoring;

[DbContext(typeof(HRSDataIntegrationDbContext))]
[Migration("20240613000000_Monitoring_Init")]
public partial class Monitoring_Init : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MonitoringTargets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Endpoint = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                SettingsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CheckIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                MaxRetryAttempts = table.Column<int>(type: "int", nullable: false),
                RetryDelaySeconds = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CurrentStatus = table.Column<int>(type: "int", nullable: false),
                LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastStatusChangeAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextDueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                FirstDownAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastUpAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                table.PrimaryKey("PK_MonitoringTargets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MonitoringTargets_IsActive",
            table: "MonitoringTargets",
            column: "IsActive");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MonitoringTargets");
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

            b.ToTable("MonitoringTargets", (string)null);
        });
    }
}
