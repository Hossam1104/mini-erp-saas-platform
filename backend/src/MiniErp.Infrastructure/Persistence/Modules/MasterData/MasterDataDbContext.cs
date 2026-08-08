using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// The Master Data module-owned EF context. It is internal so ordinary
/// application callers can only obtain the explicit Tenant-bound application
/// persistence contract.
/// </summary>
internal sealed class MasterDataDbContext : TenantPersistenceDbContext
{
    internal MasterDataDbContext(
        DbContextOptions options,
        TenantContext tenantContext)
        : base(options, tenantContext, TenantOwnershipVerifierRegistry.CreateMasterData())
    {
    }

    internal DbSet<MasterDataCategoryEntity> Categories => Set<MasterDataCategoryEntity>();

    internal DbSet<MasterDataUnitOfMeasureEntity> UnitsOfMeasure => Set<MasterDataUnitOfMeasureEntity>();

    internal DbSet<MasterDataConversionEntity> Conversions => Set<MasterDataConversionEntity>();

    internal DbSet<MasterDataAuditEventEntity> AuditEvents => Set<MasterDataAuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var category = modelBuilder.Entity<MasterDataCategoryEntity>();
        category.ToTable("Categories", "masterdata");
        category.HasKey(item => item.Id);
        category.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(category.Property(item => item.TenantId));
        category.Property(item => item.Code).HasMaxLength(128).IsRequired();
        category.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false);
        category.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        category.Property(item => item.NameKey).HasMaxLength(256).IsRequired();
        category.Property(item => item.ParentCategoryId);
        category.Property(item => item.LifecycleState).IsRequired();
        ConfigureVersion(category.Property(item => item.Version));
        category.HasIndex(item => new { item.TenantId, item.Code }).IsUnique();
        category.HasIndex(item => new { item.TenantId, item.NameKey }).IsUnique();
        category.HasOne<MasterDataCategoryEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ParentCategoryId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        category.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        category.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var unit = modelBuilder.Entity<MasterDataUnitOfMeasureEntity>();
        unit.ToTable("UnitsOfMeasure", "masterdata");
        unit.HasKey(item => item.Id);
        unit.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(unit.Property(item => item.TenantId));
        unit.Property(item => item.Code).HasMaxLength(128).IsRequired();
        unit.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false);
        unit.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        unit.Property(item => item.NameKey).HasMaxLength(256).IsRequired();
        unit.Property(item => item.LifecycleState).IsRequired();
        ConfigureVersion(unit.Property(item => item.Version));
        unit.HasIndex(item => new { item.TenantId, item.Code }).IsUnique();
        unit.HasIndex(item => new { item.TenantId, item.NameKey }).IsUnique();
        unit.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        unit.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var conversion = modelBuilder.Entity<MasterDataConversionEntity>();
        conversion.ToTable("UnitConversions", "masterdata");
        conversion.HasKey(item => item.Id);
        conversion.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(conversion.Property(item => item.TenantId));
        conversion.Property(item => item.FromUnitOfMeasureId).IsRequired();
        conversion.Property(item => item.ToUnitOfMeasureId).IsRequired();
        conversion.Property(item => item.Factor).HasPrecision(28, 8).IsRequired();
        ConfigureVersion(conversion.Property(item => item.Version));
        conversion.HasIndex(item => new
        {
            item.TenantId,
            item.FromUnitOfMeasureId,
            item.ToUnitOfMeasureId
        }).IsUnique();
        conversion.HasOne<MasterDataUnitOfMeasureEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.FromUnitOfMeasureId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        conversion.HasOne<MasterDataUnitOfMeasureEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ToUnitOfMeasureId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        conversion.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var audit = modelBuilder.Entity<MasterDataAuditEventEntity>();
        audit.ToTable("AuditEvents", "masterdata");
        audit.HasKey(item => item.EvidenceId);
        audit.Property(item => item.EvidenceId).ValueGeneratedNever();
        audit.Property(item => item.OccurredAt).IsRequired();
        audit.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        audit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        ConfigureTenant(audit.Property(item => item.TenantId));
        audit.Property(item => item.ActorId).IsRequired();
        audit.Property(item => item.SessionId).IsRequired();
        audit.Property(item => item.AuthorizationPath).IsRequired();
        audit.Property(item => item.ResourceKind).IsRequired();
        audit.Property(item => item.ResourceId);
        audit.Property(item => item.BusinessCode).HasMaxLength(128);
        audit.Property(item => item.Operation).IsRequired();
        audit.Property(item => item.PolicyOutcome).IsRequired();
        audit.Property(item => item.Decision).IsRequired();
        audit.Property(item => item.Reason).IsRequired();
        audit.Property(item => item.BeforeSummary).HasMaxLength(2048);
        audit.Property(item => item.AfterSummary).HasMaxLength(2048);
        audit.Property(item => item.ApproverId);
        audit.Property(item => item.ScopePolicyId).HasMaxLength(64).IsRequired();
        audit.Property(item => item.ScopePolicyVersion).IsRequired();
        audit.Property(item => item.ScopeAnchorKind);
        audit.Property(item => item.ScopeAnchorId);
        ConfigureVersion(audit.Property(item => item.Version));
        audit.HasIndex(item => new { item.TenantId, item.OccurredAt });
        audit.HasIndex(item => new { item.TenantId, item.ResourceKind, item.ResourceId });
        audit.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }

    private void ConfigureTenant(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TenantId> property)
    {
        property.HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();
    }

    private void ConfigureVersion(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<byte[]> property)
    {
        property.IsRequired().IsConcurrencyToken();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            property.IsRowVersion();
        }
        else
        {
            property.ValueGeneratedNever();
        }
    }
}
