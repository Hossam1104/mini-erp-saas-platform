#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class ProcurementDbContext : TenantPersistenceDbContext
{
    internal ProcurementDbContext(
        DbContextOptions options,
        TenantContext tenantContext)
        : base(options, tenantContext, TenantOwnershipVerifierRegistry.CreateProcurement())
    {
    }

    internal DbSet<PurchaseRequestEntity> PurchaseRequests => Set<PurchaseRequestEntity>();

    internal DbSet<PurchaseRequestLineEntity> PurchaseRequestLines => Set<PurchaseRequestLineEntity>();

    internal DbSet<PurchaseRequestHistoryEntity> PurchaseRequestHistory => Set<PurchaseRequestHistoryEntity>();

    internal DbSet<PurchaseRequestAuditEntity> PurchaseRequestAudit => Set<PurchaseRequestAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var request = modelBuilder.Entity<PurchaseRequestEntity>();
        ConfigureTable(request, "PurchaseRequests");
        request.HasKey(item => item.Id);
        request.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(request.Property(item => item.TenantId));
        request.Property(item => item.CompanyId).IsRequired();
        request.Property(item => item.BranchId).IsRequired(false);
        request.Property(item => item.RequesterId).IsRequired();
        request.Property(item => item.Purpose).HasMaxLength(2048).IsRequired(false);
        request.Property(item => item.Status).IsRequired();
        request.Property(item => item.CreatedAt).IsRequired();
        request.Property(item => item.UpdatedAt).IsRequired();
        request.Property(item => item.SubmittedAt).IsRequired(false);
        request.Property(item => item.ApprovedAt).IsRequired(false);
        request.Property(item => item.CancelledAt).IsRequired(false);
        request.Property(item => item.ApprovalPolicySnapshotJson).HasMaxLength(32768).IsRequired();
        request.Property(item => item.CurrentApprovalStageIndex).IsRequired();
        request.Property(item => item.CurrentStageApprovalCount).IsRequired();
        request.Property(item => item.CurrentStageApproverIdsJson).HasMaxLength(8192).IsRequired();
        ConfigureVersion(request.Property(item => item.Version));
        request.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        request.HasIndex(item => new { item.TenantId, item.CompanyId, item.BranchId, item.CreatedAt });
        request.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });
        request.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var line = modelBuilder.Entity<PurchaseRequestLineEntity>();
        ConfigureTable(line, "PurchaseRequestLines");
        line.HasKey(item => item.Id);
        line.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(line.Property(item => item.TenantId));
        line.Property(item => item.PurchaseRequestId).IsRequired();
        line.Property(item => item.ProductId).IsRequired();
        line.Property(item => item.ProductSku).HasMaxLength(128).IsRequired();
        line.Property(item => item.ProductName).HasMaxLength(256).IsRequired();
        line.Property(item => item.UnitOfMeasureId).IsRequired();
        line.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired();
        line.Property(item => item.Quantity).HasPrecision(28, 8).IsRequired();
        line.Property(item => item.NeedByDate).IsRequired();
        line.Property(item => item.Purpose).HasMaxLength(2048).IsRequired();
        ConfigureVersion(line.Property(item => item.Version));
        line.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        line.HasIndex(item => new { item.TenantId, item.PurchaseRequestId });
        line.HasOne<PurchaseRequestEntity>()
            .WithMany(item => item.Lines)
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        line.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var history = modelBuilder.Entity<PurchaseRequestHistoryEntity>();
        ConfigureTable(history, "PurchaseRequestHistory");
        history.HasKey(item => item.Id);
        history.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(history.Property(item => item.TenantId));
        history.Property(item => item.PurchaseRequestId).IsRequired();
        history.Property(item => item.FromStatus).IsRequired();
        history.Property(item => item.ToStatus).IsRequired();
        history.Property(item => item.Action).IsRequired();
        history.Property(item => item.ActorId).IsRequired();
        history.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false);
        history.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        history.Property(item => item.PolicyId).HasMaxLength(256).IsRequired(false);
        history.Property(item => item.PolicyVersion).IsRequired(false);
        history.Property(item => item.StageKey).HasMaxLength(128).IsRequired(false);
        history.Property(item => item.DelegatedFromActorId).IsRequired(false);
        history.Property(item => item.OccurredAt).IsRequired();
        ConfigureVersion(history.Property(item => item.Version));
        history.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        history.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.OccurredAt });
        history.HasOne<PurchaseRequestEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        history.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var audit = modelBuilder.Entity<PurchaseRequestAuditEntity>();
        ConfigureTable(audit, "PurchaseRequestAudit");
        audit.HasKey(item => item.Id);
        audit.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(audit.Property(item => item.TenantId));
        audit.Property(item => item.PurchaseRequestId).IsRequired();
        audit.Property(item => item.OccurredAt).IsRequired();
        audit.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        audit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        audit.Property(item => item.ActorId).IsRequired();
        audit.Property(item => item.SessionId).IsRequired();
        audit.Property(item => item.AuthorizationPath).HasMaxLength(64).IsRequired();
        audit.Property(item => item.Decision).HasMaxLength(64).IsRequired();
        audit.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false);
        audit.Property(item => item.BeforeStatus).IsRequired(false);
        audit.Property(item => item.AfterStatus).IsRequired(false);
        audit.Property(item => item.CompanyId).IsRequired();
        audit.Property(item => item.BranchId).IsRequired(false);
        audit.Property(item => item.BeforeSummary).HasMaxLength(2048).IsRequired(false);
        audit.Property(item => item.AfterSummary).HasMaxLength(2048).IsRequired(false);
        audit.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false);
        ConfigureVersion(audit.Property(item => item.Version));
        audit.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        audit.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.OccurredAt });
        audit.HasOne<PurchaseRequestEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        audit.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }

    private void ConfigureTable<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            entity.ToTable(tableName, "procurement");
        }
        else
        {
            entity.ToTable(tableName);
        }
    }

    private static void ConfigureTenant(PropertyBuilder<TenantId> property) =>
        property.HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();

    private void ConfigureVersion(PropertyBuilder<byte[]> property)
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

#pragma warning restore CS1591
