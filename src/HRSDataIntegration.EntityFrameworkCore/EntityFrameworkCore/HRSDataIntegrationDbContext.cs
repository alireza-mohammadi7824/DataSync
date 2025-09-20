using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Monitoring;
using Monitoring.Targets;
using HRSDataIntegration.DTOs.Chart;
using HRSDataIntegration.DTOs.Personeli;
using HRSDataIntegration.DTOs;
using System.Reflection.Emit;
using System;

namespace HRSDataIntegration.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class HRSDataIntegrationDbContext :
    AbpDbContext<HRSDataIntegrationDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */


    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #region Monitoring
    public DbSet<MonitoringTarget> MonitoringTargets { get; set; }
    #endregion Monitoring
    #region Job
    public DbSet<Job> Job { get; set; }
    public DbSet<JobDetail> JobDetail { get; set; }
    public DbSet<JobGroup> JobGroup { get; set; }
    public DbSet<JobRadeh> JobRadeh { get; set; }
    public DbSet<JobRasteh> JobRasteh { get; set; }
    public DbSet<MappingId> MappingId { get; set; }
    public DbSet<DataSyncLog> DataSyncLog { get; set; }
    #endregion Job

    #region Post
    public DbSet<Post> Post { get; set; }
    public DbSet<PostDetail> PostDetail { get; set; }
    public DbSet<PostDuty> PostDuty { get; set; }
    public DbSet<PostJob> PostJob { get; set; }
    public DbSet<PostLevel> PostLevel { get; set; }
    public DbSet<PostManagingLevel> PostManagingLevel { get; set; }
    public DbSet<PostType> PostType { get; set; }
    #endregion Post

    #region Unit
    public DbSet<Unit> Unit { get; set; }
    public DbSet<UnitDetail> UnitDetail { get; set; }
    public DbSet<UnitLevel> UnitLevel { get; set; }
    public DbSet<UnitTel> UnitTel { get; set; }
    public DbSet<UnitTellType> UnitTellType { get; set; }
    public DbSet<UnitType> UnitType { get; set; }
    #endregion Unit

    #region CountryDevision
    public DbSet<CountryDivision> CountryDivision { get; set; }
    public DbSet<CountryDivisionDetail> CountryDivisionDetail { get; set; }
    public DbSet<CountryDivisionType> CountryDivisionType { get; set; }
    #endregion CountryDevision

    #region PROCINCE
    public DbSet<Province> Province { get; set; }
    public DbSet<ProvinceDetail> ProvinceDetail { get; set; }
    #endregion PROVINCE

    #region OrganChart

    public DbSet<OrganizationChart> OrganizationChart { get; set; }
    public DbSet<OrganizationChartLimitation> OrganizationChartLimitation { get; set; }
    public DbSet<OrganizationChartNode> OrganizationChartNode { get; set; }
    public DbSet<OrganizationChartNodeDetail> OrganizationChartNodeDetail { get; set; }
    public DbSet<OrganizationChartNodeDiagram> OrganizationChartNodeDiagram { get; set; }
    public DbSet<OrganizationChartNodeDiagramPointArray> OrganizationChartNodeDiagramPointArray { get; set; }

    #endregion OrganChart

    #region Personeli
    public DbSet<NotFamilyRightReason> NotFamilyRightReason { get; set; }
    public DbSet<StudyField> StudyField { get; set; }
    public DbSet<StudyBranch> StudyBranch { get; set; }
    public DbSet<EmployeePensionFund> EmployeePensionFund { get; set; }
    public DbSet<University> University { get; set; }
    public DbSet<PensionFundBranch> PensionFundBranch { get; set; }
    public DbSet<NotDependentReason> NotDependentReason { get; set; }
    public DbSet<PersonEducation> PersonEducation { get; set; }
    public DbSet<EmployeeAppointmentUnit> EmployeeAppointmentUnit { get; set; }
    public DbSet<PersonContact> PersonContact { get; set; }
    public DbSet<PersonPersonType> PersonPersonType { get; set; }
    public DbSet<PersonType> PersonType { get; set; }
    public DbSet<Language> Language { get; set; }
    #endregion Personeli

    #endregion

    public HRSDataIntegrationDbContext(DbContextOptions<HRSDataIntegrationDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Unit>()
            .HasMany(u => u.UnitDetails)
            .WithOne(ud => ud.Unit)
            .HasForeignKey(ud => ud.UnitId);

        builder.HasDefaultSchema("dbo");

        builder.Entity<EmployeeAppointmentUnit>()
         .ToTable("EmployeeAppointmentUnit", schema: "Appointment");

        builder.Entity<JobDetail>()
         .ToTable("JobDetail", schema: "OrganChart");

        builder.Entity<Job>()
         .ToTable("Job", schema: "OrganChart");

        builder.Entity<JobGroup>()
         .ToTable("JobGroup", schema: "OrganChart");

        builder.Entity<JobRadeh>()
         .ToTable("JobRadeh", schema: "OrganChart");

        builder.Entity<JobRasteh>()
        .ToTable("JobRasteh", schema: "OrganChart");

        builder.Entity<Unit>()
         .ToTable("Unit", schema: "OrganChart");

        builder.Entity<UnitDetail>()
         .ToTable("UnitDetail", schema: "OrganChart");

        builder.Entity<Post>()
        .ToTable("Post", schema: "OrganChart");

        builder.Entity<PostDetail>()
        .ToTable("PostDetail", schema: "OrganChart");
        builder.Entity<PostDuty>()
        .ToTable("PostDuty", schema: "OrganChart");
        builder.Entity<PostJob>()
        .ToTable("PostJob", schema: "OrganChart");
        builder.Entity<PostLevel>()
        .ToTable("PostLevel", schema: "OrganChart");
        builder.Entity<PostManagingLevel>()
        .ToTable("PostManagingLevel", schema: "OrganChart");
        builder.Entity<PostType>()
        .ToTable("PostType", schema: "OrganChart");

        builder.Entity<UnitLevel>()
        .ToTable("UnitLevel", schema: "OrganChart");
        builder.Entity<UnitTel>()
        .ToTable("UnitTel", schema: "OrganChart");
        builder.Entity<UnitTellType>()
        .ToTable("UnitTellType", schema: "OrganChart");
        builder.Entity<UnitType>()
        .ToTable("UnitType", schema: "OrganChart");

         builder.Entity<Province>()
        .ToTable("Province", schema: "OrganChart");

        builder.Entity<ProvinceDetail>()
        .ToTable("ProvinceDetail", schema: "OrganChart");

        builder.Entity<OrganizationChart>()
        .ToTable("OrganizationChart", schema: "OrganChart");
        builder.Entity<OrganizationChartLimitation>()
        .ToTable("OrganizationChartLimitation", schema: "OrganChart");
        builder.Entity<OrganizationChartNode>()
        .ToTable("OrganizationChartNode", schema: "OrganChart");
        builder.Entity<OrganizationChartNodeDetail>()
        .ToTable("OrganizationChartNodeDetail", schema: "OrganChart");
        builder.Entity<OrganizationChartNodeDiagram>()
        .ToTable("OrganizationChartNodeDiagram", schema: "OrganChart");
        builder.Entity<OrganizationChartNodeDiagramPointArray>()
        .ToTable("OrganizationChartNodeDiagramPointArray", schema: "OrganChart");

        builder.Entity<MonitoringTarget>(b =>
        {
            b.ToTable(MonitoringConsts.DbTablePrefix + "Targets", MonitoringConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(MonitoringTargetConsts.NameMaxLength);
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.Endpoint).IsRequired().HasMaxLength(MonitoringTargetConsts.EndpointMaxLength);
            b.Property(x => x.SettingsJson).HasMaxLength(MonitoringTargetConsts.SettingsJsonMaxLength);
            b.Property(x => x.Category).HasMaxLength(MonitoringTargetConsts.CategoryMaxLength);
            b.Property(x => x.CurrentStatus).HasConversion<int>();
            b.HasIndex(x => x.IsActive);
            b.HasQueryFilter(x => !x.IsDeleted);
        });



        builder.Entity<OrganizationChartNodeDetail>()
            .HasOne(ud => ud.OrganizationChart).WithMany(x => x.OrganizationChartNodeDetails).HasForeignKey(ud => ud.OrganizationChartId);

        builder.Entity<MappingId>()
         .ToTable("MappingId", schema: "DataConvert");

        builder.Entity<DataSyncLog>()
         .ToTable("DataSyncLog", schema: "DataConvert");

        builder.Entity<CountryDivisionDetail>()
         .ToTable("CountryDivisionDetail", schema: "OrganChart");
        builder.Entity<CountryDivision>()
        .ToTable("CountryDivision", schema: "OrganChart");
        builder.Entity<CountryDivisionType>()
        .ToTable("CountryDivisionType", schema: "OrganChart");

        builder.Entity<OrganizationChartNodeDetail>()
         .Property(e => e.ParentPath)
         .HasColumnType("hierarchyid");

        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();
        
        /* Configure your own tables/entities inside here */

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(HRSDataIntegrationConsts.DbTablePrefix + "YourEntities", HRSDataIntegrationConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});
    }
}
