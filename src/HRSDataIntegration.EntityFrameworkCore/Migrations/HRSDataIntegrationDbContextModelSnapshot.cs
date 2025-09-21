using System;
using HRSDataIntegration.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace HRSDataIntegration.Migrations;

[DbContext(typeof(HRSDataIntegrationDbContext))]
partial class HRSDataIntegrationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
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

        modelBuilder.Entity("Monitoring.Targets.AlertPolicy", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("ChannelsJson")
                .HasMaxLength(4000)
                .HasColumnType("nvarchar(4000)");

            b.Property<string>("ConcurrencyStamp")
                .HasColumnType("nvarchar(40)")
                .HasMaxLength(40);

            b.Property<DateTime>("CreationTime")
                .HasColumnType("datetime2");

            b.Property<Guid?>("CreatorId")
                .HasColumnType("uniqueidentifier");

            b.Property<Guid?>("DeleterId")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("DeletionTime")
                .HasColumnType("datetime2");

            b.Property<bool>("Enabled")
                .HasColumnType("bit");

            b.Property<string>("ExtraProperties")
                .HasColumnType("nvarchar(max)");

            b.Property<bool>("IsDeleted")
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime?>("LastModificationTime")
                .HasColumnType("datetime2");

            b.Property<Guid?>("LastModifierId")
                .HasColumnType("uniqueidentifier");

            b.Property<int>("NotifyAfterFailures")
                .HasColumnType("int");

            b.Property<int>("RecoverQuietMinutes")
                .HasColumnType("int");

            b.Property<int>("RepeatMinutes")
                .HasColumnType("int");

            b.Property<bool>("SuppressDuringMaintenance")
                .HasColumnType("bit");

            b.Property<Guid>("TargetId")
                .HasColumnType("uniqueidentifier");

            b.HasKey("Id");

            b.HasIndex("TargetId")
                .IsUnique();

            b.ToTable("MonitoringAlertPolicies", (string)null);
        });

        modelBuilder.Entity("Monitoring.Targets.MaintenanceWindow", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("ConcurrencyStamp")
                .HasColumnType("nvarchar(40)")
                .HasMaxLength(40);

            b.Property<DateTime>("CreationTime")
                .HasColumnType("datetime2");

            b.Property<Guid?>("CreatorId")
                .HasColumnType("uniqueidentifier");

            b.Property<Guid?>("DeleterId")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("DeletionTime")
                .HasColumnType("datetime2");

            b.Property<DateTime>("EndUtc")
                .HasColumnType("datetime2");

            b.Property<string>("ExtraProperties")
                .HasColumnType("nvarchar(max)");

            b.Property<bool>("IsDeleted")
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<DateTime?>("LastModificationTime")
                .HasColumnType("datetime2");

            b.Property<Guid?>("LastModifierId")
                .HasColumnType("uniqueidentifier");

            b.Property<string>("Reason")
                .HasMaxLength(256)
                .HasColumnType("nvarchar(256)");

            b.Property<DateTime>("StartUtc")
                .HasColumnType("datetime2");

            b.Property<Guid?>("TargetId")
                .HasColumnType("uniqueidentifier");

            b.HasKey("Id");

            b.HasIndex("TargetId", "StartUtc", "EndUtc")
                .HasDatabaseName("IX_Target_Start_End");

            b.ToTable("MonitoringMaintenance", (string)null);
        });

        modelBuilder.Entity("Monitoring.Targets.OutageWindow", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uniqueidentifier");

            b.Property<DateTime?>("EndedAt")
                .HasColumnType("datetime2");

            b.Property<int>("FailureCount")
                .HasColumnType("int");

            b.Property<int>("AlertsSent")
                .HasColumnType("int")
                .HasDefaultValue(0);

            b.Property<DateTime?>("LastAlertAt")
                .HasColumnType("datetime2");

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

        modelBuilder.Entity("Monitoring.Targets.AlertPolicy", b =>
        {
            b.HasOne("Monitoring.Targets.MonitoringTarget", null)
                .WithOne()
                .HasForeignKey("Monitoring.Targets.AlertPolicy", "TargetId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("Monitoring.Targets.MaintenanceWindow", b =>
        {
            b.HasOne("Monitoring.Targets.MonitoringTarget", null)
                .WithMany()
                .HasForeignKey("TargetId")
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
