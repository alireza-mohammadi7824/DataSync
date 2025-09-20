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

            b.ToTable("MonitoringTargets", (string)null);
        });
    }
}
