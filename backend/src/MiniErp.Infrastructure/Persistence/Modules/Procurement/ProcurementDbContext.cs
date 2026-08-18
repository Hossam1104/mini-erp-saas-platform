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

    internal DbSet<SupplierQuotationEntity> SupplierQuotations => Set<SupplierQuotationEntity>();

    internal DbSet<SupplierQuotationLineEntity> SupplierQuotationLines => Set<SupplierQuotationLineEntity>();

    internal DbSet<SupplierQuotationEvidenceEntity> SupplierQuotationEvidence => Set<SupplierQuotationEvidenceEntity>();

    internal DbSet<SupplierQuotationHistoryEntity> SupplierQuotationHistory => Set<SupplierQuotationHistoryEntity>();

    internal DbSet<SupplierQuotationAuditEntity> SupplierQuotationAudit => Set<SupplierQuotationAuditEntity>();

    internal DbSet<SupplierSourceDecisionEntity> SupplierSourceDecisions => Set<SupplierSourceDecisionEntity>();

    internal DbSet<SupplierSourceDecisionHistoryEntity> SupplierSourceDecisionHistory => Set<SupplierSourceDecisionHistoryEntity>();

    internal DbSet<PurchaseOrderEntity> PurchaseOrders => Set<PurchaseOrderEntity>();

    internal DbSet<PurchaseOrderLineEntity> PurchaseOrderLines => Set<PurchaseOrderLineEntity>();

    internal DbSet<PurchaseOrderConfirmationEntity> PurchaseOrderConfirmations => Set<PurchaseOrderConfirmationEntity>();

    internal DbSet<PurchaseOrderConfirmationLineEntity> PurchaseOrderConfirmationLines => Set<PurchaseOrderConfirmationLineEntity>();

    internal DbSet<PurchaseOrderEvidenceEntity> PurchaseOrderEvidence => Set<PurchaseOrderEvidenceEntity>();

    internal DbSet<PurchaseOrderSupplierChangeEntity> PurchaseOrderSupplierChanges => Set<PurchaseOrderSupplierChangeEntity>();

    internal DbSet<PurchaseOrderHistoryEntity> PurchaseOrderHistory => Set<PurchaseOrderHistoryEntity>();

    internal DbSet<PurchaseOrderAuditEntity> PurchaseOrderAudit => Set<PurchaseOrderAuditEntity>();

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

        var quotation = modelBuilder.Entity<SupplierQuotationEntity>();
        ConfigureTable(quotation, "SupplierQuotations");
        quotation.HasKey(item => item.Id);
        quotation.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(quotation.Property(item => item.TenantId));
        quotation.Property(item => item.PurchaseRequestId).IsRequired();
        quotation.Property(item => item.CompanyId).IsRequired();
        quotation.Property(item => item.BranchId).IsRequired(false);
        quotation.Property(item => item.CreatedByActorId).IsRequired();
        quotation.Property(item => item.SupplierId).IsRequired();
        quotation.Property(item => item.SupplierCode).HasMaxLength(128).IsRequired();
        quotation.Property(item => item.SupplierName).HasMaxLength(256).IsRequired();
        quotation.Property(item => item.Status).IsRequired();
        quotation.Property(item => item.SupplierQuotationReference).HasMaxLength(256).IsRequired();
        quotation.Property(item => item.OfferDate).IsRequired();
        quotation.Property(item => item.ValidUntil).IsRequired(false);
        quotation.Property(item => item.CurrencyId).IsRequired();
        quotation.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        quotation.Property(item => item.CurrencyName).HasMaxLength(256).IsRequired();
        quotation.Property(item => item.PaymentTermId).IsRequired(false);
        quotation.Property(item => item.PaymentTermCode).HasMaxLength(128).IsRequired(false);
        quotation.Property(item => item.PaymentTermName).HasMaxLength(256).IsRequired(false);
        quotation.Property(item => item.PaymentTermVersion).IsRequired(false);
        quotation.Property(item => item.DeliveryTerms).HasMaxLength(2048).IsRequired(false);
        quotation.Property(item => item.OfferedDeliveryDate).IsRequired(false);
        quotation.Property(item => item.OfferedDeliveryLeadTime).HasMaxLength(512).IsRequired(false);
        quotation.Property(item => item.Notes).HasMaxLength(4096).IsRequired(false);
        quotation.Property(item => item.CreatedAt).IsRequired();
        quotation.Property(item => item.UpdatedAt).IsRequired();
        quotation.Property(item => item.SubmittedAt).IsRequired(false);
        ConfigureVersion(quotation.Property(item => item.Version));
        quotation.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        quotation.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.CreatedAt });
        quotation.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });
        quotation.HasOne<PurchaseRequestEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        quotation.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var quotationLine = modelBuilder.Entity<SupplierQuotationLineEntity>();
        ConfigureTable(quotationLine, "SupplierQuotationLines");
        quotationLine.HasKey(item => item.Id);
        quotationLine.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(quotationLine.Property(item => item.TenantId));
        quotationLine.Property(item => item.SupplierQuotationId).IsRequired();
        quotationLine.Property(item => item.PurchaseRequestId).IsRequired();
        quotationLine.Property(item => item.PurchaseRequestLineId).IsRequired();
        quotationLine.Property(item => item.ProductId).IsRequired();
        quotationLine.Property(item => item.ProductSku).HasMaxLength(128).IsRequired();
        quotationLine.Property(item => item.ProductName).HasMaxLength(256).IsRequired();
        quotationLine.Property(item => item.UnitOfMeasureId).IsRequired();
        quotationLine.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired();
        quotationLine.Property(item => item.RequestedQuantity).HasPrecision(28, 8).IsRequired();
        quotationLine.Property(item => item.QuotedQuantity).HasPrecision(28, 8).IsRequired();
        quotationLine.Property(item => item.UnitPrice).HasPrecision(28, 8).IsRequired();
        quotationLine.Property(item => item.DiscountAmount).HasPrecision(28, 8).IsRequired(false);
        quotationLine.Property(item => item.DiscountPercentage).HasPrecision(9, 4).IsRequired(false);
        quotationLine.Property(item => item.TaxId).IsRequired(false);
        quotationLine.Property(item => item.TaxCode).HasMaxLength(128).IsRequired(false);
        quotationLine.Property(item => item.TaxName).HasMaxLength(256).IsRequired(false);
        quotationLine.Property(item => item.TaxRatePercentage).HasPrecision(9, 4).IsRequired(false);
        quotationLine.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired(false);
        quotationLine.Property(item => item.TaxReference).HasMaxLength(256).IsRequired(false);
        quotationLine.Property(item => item.RequestedNeedByDate).IsRequired();
        quotationLine.Property(item => item.OfferedDeliveryDate).IsRequired(false);
        quotationLine.Property(item => item.OfferedDeliveryLeadTime).HasMaxLength(512).IsRequired(false);
        quotationLine.Property(item => item.Notes).HasMaxLength(2048).IsRequired(false);
        ConfigureVersion(quotationLine.Property(item => item.Version));
        quotationLine.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        quotationLine.HasIndex(item => new { item.TenantId, item.SupplierQuotationId });
        quotationLine.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.PurchaseRequestLineId });
        quotationLine.HasOne<SupplierQuotationEntity>()
            .WithMany(item => item.Lines)
            .HasForeignKey(item => new { item.TenantId, item.SupplierQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        quotationLine.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var quotationEvidence = modelBuilder.Entity<SupplierQuotationEvidenceEntity>();
        ConfigureTable(quotationEvidence, "SupplierQuotationEvidence");
        quotationEvidence.HasKey(item => item.Id);
        quotationEvidence.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(quotationEvidence.Property(item => item.TenantId));
        quotationEvidence.Property(item => item.SupplierQuotationId).IsRequired();
        quotationEvidence.Property(item => item.ReferenceId).HasMaxLength(256).IsRequired();
        quotationEvidence.Property(item => item.FileName).HasMaxLength(255).IsRequired(false);
        quotationEvidence.Property(item => item.ContentType).HasMaxLength(128).IsRequired(false);
        quotationEvidence.Property(item => item.Description).HasMaxLength(1024).IsRequired(false);
        quotationEvidence.Property(item => item.Source).HasMaxLength(256).IsRequired();
        quotationEvidence.Property(item => item.ExternalReference).HasMaxLength(1024).IsRequired(false);
        quotationEvidence.Property(item => item.RecordedByActorId).IsRequired();
        quotationEvidence.Property(item => item.RecordedAt).IsRequired();
        ConfigureVersion(quotationEvidence.Property(item => item.Version));
        quotationEvidence.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        quotationEvidence.HasIndex(item => new { item.TenantId, item.SupplierQuotationId, item.RecordedAt });
        quotationEvidence.HasOne<SupplierQuotationEntity>()
            .WithMany(item => item.Evidence)
            .HasForeignKey(item => new { item.TenantId, item.SupplierQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        quotationEvidence.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var quotationHistory = modelBuilder.Entity<SupplierQuotationHistoryEntity>();
        ConfigureTable(quotationHistory, "SupplierQuotationHistory");
        quotationHistory.HasKey(item => item.Id);
        quotationHistory.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(quotationHistory.Property(item => item.TenantId));
        quotationHistory.Property(item => item.SupplierQuotationId).IsRequired();
        quotationHistory.Property(item => item.FromStatus).IsRequired();
        quotationHistory.Property(item => item.ToStatus).IsRequired();
        quotationHistory.Property(item => item.Action).IsRequired();
        quotationHistory.Property(item => item.ActorId).IsRequired();
        quotationHistory.Property(item => item.Reason).HasMaxLength(4096).IsRequired(false);
        quotationHistory.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        quotationHistory.Property(item => item.PolicyId).HasMaxLength(256).IsRequired(false);
        quotationHistory.Property(item => item.PolicyVersion).IsRequired(false);
        quotationHistory.Property(item => item.StageKey).HasMaxLength(128).IsRequired(false);
        quotationHistory.Property(item => item.DelegatedFromActorId).IsRequired(false);
        quotationHistory.Property(item => item.OccurredAt).IsRequired();
        ConfigureVersion(quotationHistory.Property(item => item.Version));
        quotationHistory.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        quotationHistory.HasIndex(item => new { item.TenantId, item.SupplierQuotationId, item.OccurredAt });
        quotationHistory.HasOne<SupplierQuotationEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SupplierQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        quotationHistory.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var quotationAudit = modelBuilder.Entity<SupplierQuotationAuditEntity>();
        ConfigureTable(quotationAudit, "SupplierQuotationAudit");
        quotationAudit.HasKey(item => item.Id);
        quotationAudit.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(quotationAudit.Property(item => item.TenantId));
        quotationAudit.Property(item => item.SupplierQuotationId).IsRequired();
        quotationAudit.Property(item => item.PurchaseRequestId).IsRequired();
        quotationAudit.Property(item => item.OccurredAt).IsRequired();
        quotationAudit.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        quotationAudit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        quotationAudit.Property(item => item.ActorId).IsRequired();
        quotationAudit.Property(item => item.SessionId).IsRequired();
        quotationAudit.Property(item => item.AuthorizationPath).HasMaxLength(64).IsRequired();
        quotationAudit.Property(item => item.Decision).HasMaxLength(64).IsRequired();
        quotationAudit.Property(item => item.Reason).HasMaxLength(4096).IsRequired(false);
        quotationAudit.Property(item => item.BeforeStatus).IsRequired(false);
        quotationAudit.Property(item => item.AfterStatus).IsRequired(false);
        quotationAudit.Property(item => item.CompanyId).IsRequired();
        quotationAudit.Property(item => item.BranchId).IsRequired(false);
        quotationAudit.Property(item => item.BeforeSummary).HasMaxLength(4096).IsRequired(false);
        quotationAudit.Property(item => item.AfterSummary).HasMaxLength(4096).IsRequired(false);
        quotationAudit.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false);
        ConfigureVersion(quotationAudit.Property(item => item.Version));
        quotationAudit.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        quotationAudit.HasIndex(item => new { item.TenantId, item.SupplierQuotationId, item.OccurredAt });
        quotationAudit.HasIndex(item => new { item.TenantId, item.ActorId, item.OperationId, item.IdempotencyKey });
        quotationAudit.HasOne<SupplierQuotationEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SupplierQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        quotationAudit.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var sourceDecision = modelBuilder.Entity<SupplierSourceDecisionEntity>();
        ConfigureTable(sourceDecision, "SupplierSourceDecisions");
        sourceDecision.HasKey(item => item.Id);
        sourceDecision.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(sourceDecision.Property(item => item.TenantId));
        sourceDecision.Property(item => item.PurchaseRequestId).IsRequired();
        sourceDecision.Property(item => item.CompanyId).IsRequired();
        sourceDecision.Property(item => item.BranchId).IsRequired(false);
        sourceDecision.Property(item => item.SelectedQuotationId).IsRequired();
        sourceDecision.Property(item => item.SupplierId).IsRequired();
        sourceDecision.Property(item => item.SupplierCode).HasMaxLength(128).IsRequired();
        sourceDecision.Property(item => item.SupplierName).HasMaxLength(256).IsRequired();
        sourceDecision.Property(item => item.SupplierQuotationReference).HasMaxLength(256).IsRequired();
        sourceDecision.Property(item => item.ActorId).IsRequired();
        sourceDecision.Property(item => item.SelectedAt).IsRequired();
        sourceDecision.Property(item => item.Rationale).HasMaxLength(4096).IsRequired();
        sourceDecision.Property(item => item.PolicyId).HasMaxLength(256).IsRequired(false);
        sourceDecision.Property(item => item.PolicyVersion).IsRequired(false);
        sourceDecision.Property(item => item.StageKey).HasMaxLength(128).IsRequired(false);
        sourceDecision.Property(item => item.ComparisonSnapshotReference).HasMaxLength(256).IsRequired();
        sourceDecision.Property(item => item.ComparisonSnapshotJson).HasMaxLength(262144).IsRequired();
        ConfigureVersion(sourceDecision.Property(item => item.Version));
        sourceDecision.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        sourceDecision.HasIndex(item => new { item.TenantId, item.PurchaseRequestId }).IsUnique();
        sourceDecision.HasOne<PurchaseRequestEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        sourceDecision.HasOne<SupplierQuotationEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SelectedQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        sourceDecision.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var sourceDecisionHistory = modelBuilder.Entity<SupplierSourceDecisionHistoryEntity>();
        ConfigureTable(sourceDecisionHistory, "SupplierSourceDecisionHistory");
        sourceDecisionHistory.HasKey(item => item.Id);
        sourceDecisionHistory.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(sourceDecisionHistory.Property(item => item.TenantId));
        sourceDecisionHistory.Property(item => item.SourceDecisionId).IsRequired();
        sourceDecisionHistory.Property(item => item.PurchaseRequestId).IsRequired();
        sourceDecisionHistory.Property(item => item.PreviousSelectedQuotationId).IsRequired(false);
        sourceDecisionHistory.Property(item => item.SelectedQuotationId).IsRequired();
        sourceDecisionHistory.Property(item => item.ActorId).IsRequired();
        sourceDecisionHistory.Property(item => item.SelectedAt).IsRequired();
        sourceDecisionHistory.Property(item => item.Rationale).HasMaxLength(4096).IsRequired();
        sourceDecisionHistory.Property(item => item.PolicyId).HasMaxLength(256).IsRequired(false);
        sourceDecisionHistory.Property(item => item.PolicyVersion).IsRequired(false);
        sourceDecisionHistory.Property(item => item.StageKey).HasMaxLength(128).IsRequired(false);
        sourceDecisionHistory.Property(item => item.ComparisonSnapshotReference).HasMaxLength(256).IsRequired();
        sourceDecisionHistory.Property(item => item.ComparisonSnapshotJson).HasMaxLength(262144).IsRequired();
        ConfigureVersion(sourceDecisionHistory.Property(item => item.Version));
        sourceDecisionHistory.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        sourceDecisionHistory.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.SelectedAt });
        sourceDecisionHistory.HasOne<SupplierSourceDecisionEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SourceDecisionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        sourceDecisionHistory.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var purchaseOrder = modelBuilder.Entity<PurchaseOrderEntity>();
        ConfigureTable(purchaseOrder, "PurchaseOrders");
        purchaseOrder.HasKey(item => item.Id);
        purchaseOrder.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(purchaseOrder.Property(item => item.TenantId));
        purchaseOrder.Property(item => item.PurchaseRequestId).IsRequired();
        purchaseOrder.Property(item => item.SupplierQuotationId).IsRequired();
        purchaseOrder.Property(item => item.SourceDecisionId).IsRequired();
        purchaseOrder.Property(item => item.CompanyId).IsRequired();
        purchaseOrder.Property(item => item.BranchId).IsRequired(false);
        purchaseOrder.Property(item => item.CreatedByActorId).IsRequired();
        purchaseOrder.Property(item => item.SupplierId).IsRequired();
        purchaseOrder.Property(item => item.SupplierCode).HasMaxLength(128).IsRequired();
        purchaseOrder.Property(item => item.SupplierName).HasMaxLength(256).IsRequired();
        purchaseOrder.Property(item => item.SupplierQuotationReference).HasMaxLength(256).IsRequired();
        purchaseOrder.Property(item => item.CurrencyId).IsRequired();
        purchaseOrder.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        purchaseOrder.Property(item => item.CurrencyName).HasMaxLength(256).IsRequired();
        purchaseOrder.Property(item => item.PaymentTermId).IsRequired(false);
        purchaseOrder.Property(item => item.PaymentTermCode).HasMaxLength(128).IsRequired(false);
        purchaseOrder.Property(item => item.PaymentTermName).HasMaxLength(256).IsRequired(false);
        purchaseOrder.Property(item => item.PaymentTermVersion).IsRequired(false);
        purchaseOrder.Property(item => item.SourcePurchaseRequestReference).HasMaxLength(128).IsRequired();
        purchaseOrder.Property(item => item.SourcePurchaseRequestPurpose).HasMaxLength(2048).IsRequired(false);
        purchaseOrder.Property(item => item.SourceDecisionRationale).HasMaxLength(4096).IsRequired();
        purchaseOrder.Property(item => item.SourceSelectedAt).IsRequired();
        purchaseOrder.Property(item => item.Status).IsRequired();
        purchaseOrder.Property(item => item.Notes).HasMaxLength(4096).IsRequired(false);
        purchaseOrder.Property(item => item.CreatedAt).IsRequired();
        purchaseOrder.Property(item => item.UpdatedAt).IsRequired();
        purchaseOrder.Property(item => item.SubmittedAt).IsRequired(false);
        purchaseOrder.Property(item => item.ApprovedAt).IsRequired(false);
        purchaseOrder.Property(item => item.IssuedAt).IsRequired(false);
        purchaseOrder.Property(item => item.CancelledAt).IsRequired(false);
        purchaseOrder.Property(item => item.LatestConfirmationId).IsRequired(false);
        purchaseOrder.Property(item => item.LatestConfirmationStatus).IsRequired(false);
        purchaseOrder.Property(item => item.StatusBeforeSupplierChange).IsRequired(false);
        purchaseOrder.Property(item => item.ApprovalPolicySnapshotJson).HasMaxLength(32768).IsRequired();
        purchaseOrder.Property(item => item.CurrentApprovalStageIndex).IsRequired();
        purchaseOrder.Property(item => item.CurrentStageApprovalCount).IsRequired();
        purchaseOrder.Property(item => item.CurrentStageApproverIdsJson).HasMaxLength(8192).IsRequired();
        purchaseOrder.Property(item => item.IsReapproval).IsRequired();
        ConfigureVersion(purchaseOrder.Property(item => item.Version));
        purchaseOrder.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        purchaseOrder.HasIndex(item => new { item.TenantId, item.SourceDecisionId }).IsUnique();
        purchaseOrder.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });
        purchaseOrder.HasIndex(item => new { item.TenantId, item.PurchaseRequestId, item.CreatedAt });
        purchaseOrder.HasOne<PurchaseRequestEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseOrder.HasOne<SupplierQuotationEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SupplierQuotationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseOrder.HasOne<SupplierSourceDecisionEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SourceDecisionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseOrder.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var purchaseOrderLine = modelBuilder.Entity<PurchaseOrderLineEntity>();
        ConfigureTable(purchaseOrderLine, "PurchaseOrderLines");
        purchaseOrderLine.HasKey(item => item.Id);
        purchaseOrderLine.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(purchaseOrderLine.Property(item => item.TenantId));
        purchaseOrderLine.Property(item => item.PurchaseOrderId).IsRequired();
        purchaseOrderLine.Property(item => item.SourceQuotationLineId).IsRequired();
        purchaseOrderLine.Property(item => item.PurchaseRequestLineId).IsRequired();
        purchaseOrderLine.Property(item => item.ProductId).IsRequired();
        purchaseOrderLine.Property(item => item.ProductSku).HasMaxLength(128).IsRequired();
        purchaseOrderLine.Property(item => item.ProductName).HasMaxLength(256).IsRequired();
        purchaseOrderLine.Property(item => item.UnitOfMeasureId).IsRequired();
        purchaseOrderLine.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired();
        purchaseOrderLine.Property(item => item.RequestedQuantity).HasPrecision(28, 8).IsRequired();
        purchaseOrderLine.Property(item => item.OrderedQuantity).HasPrecision(28, 8).IsRequired();
        purchaseOrderLine.Property(item => item.ConfirmedQuantity).HasPrecision(28, 8).IsRequired();
        purchaseOrderLine.Property(item => item.RemainingQuantity).HasPrecision(28, 8).IsRequired();
        purchaseOrderLine.Property(item => item.UnitPrice).HasPrecision(28, 8).IsRequired();
        purchaseOrderLine.Property(item => item.DiscountAmount).HasPrecision(28, 8).IsRequired(false);
        purchaseOrderLine.Property(item => item.DiscountPercentage).HasPrecision(9, 4).IsRequired(false);
        purchaseOrderLine.Property(item => item.TaxId).IsRequired(false);
        purchaseOrderLine.Property(item => item.TaxCode).HasMaxLength(128).IsRequired(false);
        purchaseOrderLine.Property(item => item.TaxName).HasMaxLength(256).IsRequired(false);
        purchaseOrderLine.Property(item => item.TaxRatePercentage).HasPrecision(9, 4).IsRequired(false);
        purchaseOrderLine.Property(item => item.TaxAmount).HasPrecision(28, 8).IsRequired(false);
        purchaseOrderLine.Property(item => item.RequestedNeedByDate).IsRequired();
        purchaseOrderLine.Property(item => item.DeliveryDate).IsRequired(false);
        purchaseOrderLine.Property(item => item.Notes).HasMaxLength(2048).IsRequired(false);
        ConfigureVersion(purchaseOrderLine.Property(item => item.Version));
        purchaseOrderLine.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        purchaseOrderLine.HasIndex(item => new { item.TenantId, item.PurchaseOrderId });
        purchaseOrderLine.HasOne<PurchaseOrderEntity>()
            .WithMany(item => item.Lines)
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        purchaseOrderLine.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var confirmation = modelBuilder.Entity<PurchaseOrderConfirmationEntity>();
        ConfigureTable(confirmation, "PurchaseOrderConfirmations");
        confirmation.HasKey(item => item.Id);
        confirmation.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(confirmation.Property(item => item.TenantId));
        confirmation.Property(item => item.PurchaseOrderId).IsRequired();
        confirmation.Property(item => item.Status).IsRequired();
        confirmation.Property(item => item.ResponseDate).IsRequired();
        confirmation.Property(item => item.SupplierReference).HasMaxLength(256).IsRequired(false);
        confirmation.Property(item => item.SupplierContact).HasMaxLength(256).IsRequired(false);
        confirmation.Property(item => item.Reason).HasMaxLength(4096).IsRequired(false);
        confirmation.Property(item => item.Notes).HasMaxLength(4096).IsRequired(false);
        confirmation.Property(item => item.RecordedByActorId).IsRequired();
        confirmation.Property(item => item.RecordedAt).IsRequired();
        confirmation.Property(item => item.PurchaseOrderVersion).IsRequired();
        ConfigureVersion(confirmation.Property(item => item.Version));
        confirmation.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        confirmation.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.RecordedAt });
        confirmation.HasOne<PurchaseOrderEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        confirmation.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var confirmationLine = modelBuilder.Entity<PurchaseOrderConfirmationLineEntity>();
        ConfigureTable(confirmationLine, "PurchaseOrderConfirmationLines");
        confirmationLine.HasKey(item => item.Id);
        confirmationLine.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(confirmationLine.Property(item => item.TenantId));
        confirmationLine.Property(item => item.PurchaseOrderConfirmationId).IsRequired();
        confirmationLine.Property(item => item.PurchaseOrderLineId).IsRequired();
        confirmationLine.Property(item => item.OrderedQuantityAtResponse).HasPrecision(28, 8).IsRequired();
        confirmationLine.Property(item => item.ConfirmedQuantity).HasPrecision(28, 8).IsRequired();
        confirmationLine.Property(item => item.RemainingQuantity).HasPrecision(28, 8).IsRequired();
        confirmationLine.Property(item => item.ExpectedDeliveryDate).IsRequired(false);
        confirmationLine.Property(item => item.ProposedQuantity).HasPrecision(28, 8).IsRequired(false);
        confirmationLine.Property(item => item.ProposedUnitPrice).HasPrecision(28, 8).IsRequired(false);
        confirmationLine.Property(item => item.ProposedDeliveryDate).IsRequired(false);
        confirmationLine.Property(item => item.ChangeReason).HasMaxLength(4096).IsRequired(false);
        ConfigureVersion(confirmationLine.Property(item => item.Version));
        confirmationLine.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        confirmationLine.HasIndex(item => new { item.TenantId, item.PurchaseOrderConfirmationId });
        confirmationLine.HasOne<PurchaseOrderConfirmationEntity>()
            .WithMany(item => item.Lines)
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderConfirmationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        confirmationLine.HasOne<PurchaseOrderLineEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderLineId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        confirmationLine.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var evidence = modelBuilder.Entity<PurchaseOrderEvidenceEntity>();
        ConfigureTable(evidence, "PurchaseOrderEvidence");
        evidence.HasKey(item => item.Id);
        evidence.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(evidence.Property(item => item.TenantId));
        evidence.Property(item => item.PurchaseOrderConfirmationId).IsRequired();
        evidence.Property(item => item.ReferenceId).HasMaxLength(256).IsRequired();
        evidence.Property(item => item.FileName).HasMaxLength(255).IsRequired(false);
        evidence.Property(item => item.ContentType).HasMaxLength(128).IsRequired(false);
        evidence.Property(item => item.Description).HasMaxLength(1024).IsRequired(false);
        evidence.Property(item => item.Source).HasMaxLength(256).IsRequired();
        evidence.Property(item => item.ExternalReference).HasMaxLength(1024).IsRequired(false);
        evidence.Property(item => item.RecordedByActorId).IsRequired();
        evidence.Property(item => item.RecordedAt).IsRequired();
        ConfigureVersion(evidence.Property(item => item.Version));
        evidence.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        evidence.HasIndex(item => new { item.TenantId, item.PurchaseOrderConfirmationId, item.RecordedAt });
        evidence.HasOne<PurchaseOrderConfirmationEntity>()
            .WithMany(item => item.Evidence)
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderConfirmationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Cascade);
        evidence.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var change = modelBuilder.Entity<PurchaseOrderSupplierChangeEntity>();
        ConfigureTable(change, "PurchaseOrderSupplierChanges");
        change.HasKey(item => item.Id);
        change.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(change.Property(item => item.TenantId));
        change.Property(item => item.PurchaseOrderConfirmationId).IsRequired();
        change.Property(item => item.PurchaseOrderLineId).IsRequired();
        change.Property(item => item.PreviousOrderedQuantity).HasPrecision(28, 8).IsRequired();
        change.Property(item => item.ProposedQuantity).HasPrecision(28, 8).IsRequired(false);
        change.Property(item => item.PreviousUnitPrice).HasPrecision(28, 8).IsRequired();
        change.Property(item => item.ProposedUnitPrice).HasPrecision(28, 8).IsRequired(false);
        change.Property(item => item.PreviousDeliveryDate).IsRequired(false);
        change.Property(item => item.ProposedDeliveryDate).IsRequired(false);
        change.Property(item => item.Status).IsRequired();
        change.Property(item => item.Reason).HasMaxLength(4096).IsRequired();
        change.Property(item => item.ActorId).IsRequired();
        change.Property(item => item.ProposedAt).IsRequired();
        change.Property(item => item.DecidedAt).IsRequired(false);
        change.Property(item => item.DecisionReason).HasMaxLength(4096).IsRequired(false);
        ConfigureVersion(change.Property(item => item.Version));
        change.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        change.HasIndex(item => new { item.TenantId, item.PurchaseOrderLineId, item.Status });
        change.HasOne<PurchaseOrderConfirmationEntity>()
            .WithMany(item => item.Changes)
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderConfirmationId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        change.HasOne<PurchaseOrderLineEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderLineId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        change.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var purchaseOrderHistory = modelBuilder.Entity<PurchaseOrderHistoryEntity>();
        ConfigureTable(purchaseOrderHistory, "PurchaseOrderHistory");
        purchaseOrderHistory.HasKey(item => item.Id);
        purchaseOrderHistory.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(purchaseOrderHistory.Property(item => item.TenantId));
        purchaseOrderHistory.Property(item => item.PurchaseOrderId).IsRequired();
        purchaseOrderHistory.Property(item => item.FromStatus).IsRequired();
        purchaseOrderHistory.Property(item => item.ToStatus).IsRequired();
        purchaseOrderHistory.Property(item => item.Action).IsRequired();
        purchaseOrderHistory.Property(item => item.ActorId).IsRequired();
        purchaseOrderHistory.Property(item => item.Reason).HasMaxLength(4096).IsRequired(false);
        purchaseOrderHistory.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        purchaseOrderHistory.Property(item => item.PolicyId).HasMaxLength(256).IsRequired(false);
        purchaseOrderHistory.Property(item => item.PolicyVersion).IsRequired(false);
        purchaseOrderHistory.Property(item => item.StageKey).HasMaxLength(128).IsRequired(false);
        purchaseOrderHistory.Property(item => item.DelegatedFromActorId).IsRequired(false);
        purchaseOrderHistory.Property(item => item.OccurredAt).IsRequired();
        ConfigureVersion(purchaseOrderHistory.Property(item => item.Version));
        purchaseOrderHistory.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        purchaseOrderHistory.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.OccurredAt });
        purchaseOrderHistory.HasOne<PurchaseOrderEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseOrderHistory.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var purchaseOrderAudit = modelBuilder.Entity<PurchaseOrderAuditEntity>();
        ConfigureTable(purchaseOrderAudit, "PurchaseOrderAudit");
        purchaseOrderAudit.HasKey(item => item.Id);
        purchaseOrderAudit.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(purchaseOrderAudit.Property(item => item.TenantId));
        purchaseOrderAudit.Property(item => item.PurchaseOrderId).IsRequired();
        purchaseOrderAudit.Property(item => item.OccurredAt).IsRequired();
        purchaseOrderAudit.Property(item => item.OperationId).HasMaxLength(128).IsRequired();
        purchaseOrderAudit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        purchaseOrderAudit.Property(item => item.ActorId).IsRequired();
        purchaseOrderAudit.Property(item => item.SessionId).IsRequired();
        purchaseOrderAudit.Property(item => item.AuthorizationPath).HasMaxLength(64).IsRequired();
        purchaseOrderAudit.Property(item => item.Decision).HasMaxLength(64).IsRequired();
        purchaseOrderAudit.Property(item => item.Reason).HasMaxLength(4096).IsRequired(false);
        purchaseOrderAudit.Property(item => item.BeforeStatus).IsRequired(false);
        purchaseOrderAudit.Property(item => item.AfterStatus).IsRequired(false);
        purchaseOrderAudit.Property(item => item.CompanyId).IsRequired();
        purchaseOrderAudit.Property(item => item.BranchId).IsRequired(false);
        purchaseOrderAudit.Property(item => item.BeforeSummary).HasMaxLength(4096).IsRequired(false);
        purchaseOrderAudit.Property(item => item.AfterSummary).HasMaxLength(4096).IsRequired(false);
        purchaseOrderAudit.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false);
        purchaseOrderAudit.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired(false);
        purchaseOrderAudit.Property(item => item.ReplayResponseSchemaVersion).IsRequired(false);
        purchaseOrderAudit.Property(item => item.ReplayResponseSnapshotJson).HasMaxLength(1_048_576).IsRequired(false);
        ConfigureVersion(purchaseOrderAudit.Property(item => item.Version));
        purchaseOrderAudit.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        purchaseOrderAudit.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.OccurredAt });
        purchaseOrderAudit.HasIndex(item => new { item.TenantId, item.ActorId, item.OperationId, item.IdempotencyKey });
        purchaseOrderAudit.HasOne<PurchaseOrderEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PurchaseOrderId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseOrderAudit.HasQueryFilter(item => item.TenantId == TrustedTenantId);
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
