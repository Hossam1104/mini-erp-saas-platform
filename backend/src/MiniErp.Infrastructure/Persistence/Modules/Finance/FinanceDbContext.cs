#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinanceDbContext : TenantPersistenceDbContext
{
    internal FinanceDbContext(DbContextOptions options, TenantContext tenantContext)
        : base(options, tenantContext, TenantOwnershipVerifierRegistry.CreateFinance()) { }

    internal DbSet<FinanceAccountEntity> Accounts => Set<FinanceAccountEntity>();
    internal DbSet<FinanceFiscalCalendarEntity> Calendars => Set<FinanceFiscalCalendarEntity>();
    internal DbSet<FinanceFiscalYearEntity> FiscalYears => Set<FinanceFiscalYearEntity>();
    internal DbSet<FinanceFiscalPeriodEntity> FiscalPeriods => Set<FinanceFiscalPeriodEntity>();
    internal DbSet<FinanceCostCenterEntity> CostCenters => Set<FinanceCostCenterEntity>();
    internal DbSet<FinancePostingRuleEntity> PostingRules => Set<FinancePostingRuleEntity>();
    internal DbSet<FinanceJournalEntity> Journals => Set<FinanceJournalEntity>();
    internal DbSet<FinanceJournalLineEntity> JournalLines => Set<FinanceJournalLineEntity>();
    internal DbSet<FinanceAuditEntity> AuditEvents => Set<FinanceAuditEntity>();
    internal DbSet<FinanceIdempotencyEntity> Idempotency => Set<FinanceIdempotencyEntity>();
    internal DbSet<FinanceSourceEffectEntity> SourceEffects => Set<FinanceSourceEffectEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureBase(modelBuilder.Entity<FinanceAccountEntity>(), "Accounts");
        var account = modelBuilder.Entity<FinanceAccountEntity>();
        account.Property(item => item.CompanyId).IsRequired(); account.Property(item => item.Code).HasMaxLength(64).IsRequired(); account.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(); account.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false); account.Property(item => item.ParentAccountId).IsRequired(false); account.Property(item => item.AccountType).IsRequired(); account.Property(item => item.IsPostingAccount).IsRequired(); account.Property(item => item.Lifecycle).IsRequired(); account.Property(item => item.CurrencyBehavior).IsRequired(); account.Property(item => item.EffectiveFrom).IsRequired(); account.Property(item => item.EffectiveTo).IsRequired(false); account.HasIndex(item => new { item.TenantId, item.CompanyId, item.Code }).IsUnique(); account.HasIndex(item => new { item.TenantId, item.CompanyId, item.Id }).IsUnique();
        account.HasOne<FinanceAccountEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.ParentAccountId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);

        ConfigureBase(modelBuilder.Entity<FinanceFiscalCalendarEntity>(), "FiscalCalendars");
        var calendar = modelBuilder.Entity<FinanceFiscalCalendarEntity>(); calendar.Property(item => item.CompanyId).IsRequired(); calendar.Property(item => item.Name).HasMaxLength(256).IsRequired(); calendar.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(); calendar.Property(item => item.Lifecycle).IsRequired(); calendar.HasIndex(item => new { item.TenantId, item.CompanyId, item.Lifecycle }).IsUnique().HasFilter("Lifecycle = 1");

        ConfigureBase(modelBuilder.Entity<FinanceFiscalYearEntity>(), "FiscalYears");
        var year = modelBuilder.Entity<FinanceFiscalYearEntity>(); year.Property(item => item.CalendarId).IsRequired(); year.Property(item => item.CompanyId).IsRequired(); year.Property(item => item.YearNumber).IsRequired(); year.Property(item => item.StartDate).IsRequired(); year.Property(item => item.EndDate).IsRequired(); year.Property(item => item.State).IsRequired(); year.HasIndex(item => new { item.TenantId, item.CalendarId, item.YearNumber }).IsUnique(); year.HasOne<FinanceFiscalCalendarEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.CalendarId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        ConfigureBase(modelBuilder.Entity<FinanceFiscalPeriodEntity>(), "FiscalPeriods");
        var period = modelBuilder.Entity<FinanceFiscalPeriodEntity>(); period.Property(item => item.FiscalYearId).IsRequired(); period.Property(item => item.CompanyId).IsRequired(); period.Property(item => item.Sequence).IsRequired(); period.Property(item => item.Code).HasMaxLength(64).IsRequired(); period.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false); period.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false); period.Property(item => item.StartDate).IsRequired(); period.Property(item => item.EndDate).IsRequired(); period.Property(item => item.State).IsRequired(); period.HasIndex(item => new { item.TenantId, item.FiscalYearId, item.Sequence }).IsUnique(); period.HasIndex(item => new { item.TenantId, item.FiscalYearId, item.Code }).IsUnique(); period.HasOne<FinanceFiscalYearEntity>().WithMany().HasForeignKey(item => new { item.TenantId, item.FiscalYearId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        ConfigureBase(modelBuilder.Entity<FinanceCostCenterEntity>(), "CostCenters");
        var cost = modelBuilder.Entity<FinanceCostCenterEntity>(); cost.Property(item => item.CompanyId).IsRequired(); cost.Property(item => item.Code).HasMaxLength(64).IsRequired(); cost.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(); cost.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false); cost.Property(item => item.Lifecycle).IsRequired(); cost.Property(item => item.EffectiveFrom).IsRequired(); cost.Property(item => item.EffectiveTo).IsRequired(false); cost.HasIndex(item => new { item.TenantId, item.CompanyId, item.Code }).IsUnique();

        ConfigureBase(modelBuilder.Entity<FinancePostingRuleEntity>(), "PostingRules");
        var rule = modelBuilder.Entity<FinancePostingRuleEntity>(); rule.Property(item => item.CompanyId).IsRequired(); rule.Property(item => item.SourceContract).HasMaxLength(128).IsRequired(); rule.Property(item => item.SourceEvent).HasMaxLength(128).IsRequired(); rule.Property(item => item.VersionNumber).IsRequired(); rule.Property(item => item.DebitAccountId).IsRequired(); rule.Property(item => item.DebitAccountCode).HasMaxLength(64).IsRequired(); rule.Property(item => item.CreditAccountId).IsRequired(); rule.Property(item => item.CreditAccountCode).HasMaxLength(64).IsRequired(); rule.Property(item => item.CostCenterRequired).IsRequired(); rule.Property(item => item.EffectiveFrom).IsRequired(); rule.Property(item => item.EffectiveTo).IsRequired(false); rule.Property(item => item.Lifecycle).IsRequired(); rule.HasIndex(item => new { item.TenantId, item.CompanyId, item.SourceContract, item.SourceEvent, item.VersionNumber }).IsUnique();

        ConfigureBase(modelBuilder.Entity<FinanceJournalEntity>(), "Journals");
        var journal = modelBuilder.Entity<FinanceJournalEntity>(); journal.Property(item => item.CompanyId).IsRequired(); journal.Property(item => item.JournalSequence).IsRequired(); journal.Property(item => item.JournalNumber).HasMaxLength(64).IsRequired(); journal.Property(item => item.JournalDate).IsRequired(); journal.Property(item => item.PostingDate).IsRequired(); journal.Property(item => item.FiscalYearId).IsRequired(false); journal.Property(item => item.FiscalPeriodId).IsRequired(false); journal.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(); journal.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false); journal.Property(item => item.ExchangeRate).HasPrecision(28, 12).IsRequired(false); journal.Property(item => item.ExchangeRateId).IsRequired(false); journal.Property(item => item.ExchangeRateVersionId).IsRequired(false); journal.Property(item => item.ExchangeRateVersionNumber).IsRequired(false); journal.Property(item => item.SourceContract).HasMaxLength(128).IsRequired(); journal.Property(item => item.SourceEvent).HasMaxLength(128).IsRequired(); journal.Property(item => item.SourceEvidenceId).IsRequired(false); journal.Property(item => item.SourceEvidenceVersion).IsRequired(false); journal.Property(item => item.PostingRuleId).IsRequired(false); journal.Property(item => item.PostingRuleVersionNumber).IsRequired(false); journal.Property(item => item.Description).HasMaxLength(2048).IsRequired(); journal.Property(item => item.Status).IsRequired(); journal.Property(item => item.CreatedBy).IsRequired(); journal.Property(item => item.SubmittedBy).IsRequired(false); journal.Property(item => item.ApprovedBy).IsRequired(false); journal.Property(item => item.PostedBy).IsRequired(false); journal.Property(item => item.ReversedBy).IsRequired(false); journal.Property(item => item.ReversalOfJournalId).IsRequired(false); journal.Property(item => item.ReversalJournalId).IsRequired(false); journal.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); journal.Property(item => item.CreatedAt).IsRequired(); journal.Property(item => item.PostedAt).IsRequired(false); journal.HasIndex(item => new { item.TenantId, item.CompanyId, item.JournalSequence }).IsUnique(); journal.HasIndex(item => new { item.TenantId, item.SourceContract, item.SourceEvidenceId, item.SourceEvidenceVersion }).IsUnique().HasFilter(Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "[SourceEvidenceId] IS NOT NULL" : "SourceEvidenceId IS NOT NULL");

        ConfigureBase(modelBuilder.Entity<FinanceJournalLineEntity>(), "JournalLines");
        var line = modelBuilder.Entity<FinanceJournalLineEntity>(); line.Property(item => item.JournalId).IsRequired(); line.Property(item => item.LineNumber).IsRequired(); line.Property(item => item.AccountId).IsRequired(); line.Property(item => item.AccountCode).HasMaxLength(64).IsRequired(); line.Property(item => item.AccountName).HasMaxLength(256).IsRequired(); line.Property(item => item.Debit).HasPrecision(28, 8).IsRequired(); line.Property(item => item.Credit).HasPrecision(28, 8).IsRequired(); line.Property(item => item.FunctionalDebit).HasPrecision(28, 8).IsRequired(); line.Property(item => item.FunctionalCredit).HasPrecision(28, 8).IsRequired(); line.Property(item => item.TransactionAmount).HasPrecision(28, 8).IsRequired(false); line.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false); line.Property(item => item.CostCenterId).IsRequired(false); line.Property(item => item.CostCenterCode).HasMaxLength(64).IsRequired(false); line.Property(item => item.Description).HasMaxLength(1024).IsRequired(false); line.HasIndex(item => new { item.TenantId, item.JournalId, item.LineNumber }).IsUnique(); line.HasOne<FinanceJournalEntity>().WithMany(item => item.Lines).HasForeignKey(item => new { item.TenantId, item.JournalId }).HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        ConfigureBase(modelBuilder.Entity<FinanceAuditEntity>(), "AuditEvents");
        var audit = modelBuilder.Entity<FinanceAuditEntity>(); audit.Property(item => item.OperationId).HasMaxLength(128).IsRequired(); audit.Property(item => item.ResourceType).HasMaxLength(128).IsRequired(); audit.Property(item => item.ResourceId).IsRequired(); audit.Property(item => item.ActorId).IsRequired(); audit.Property(item => item.SessionId).IsRequired(); audit.Property(item => item.Result).HasMaxLength(64).IsRequired(); audit.Property(item => item.Reason).HasMaxLength(2048).IsRequired(false); audit.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired(); audit.Property(item => item.IdempotencyKey).HasMaxLength(256).IsRequired(false); audit.Property(item => item.OccurredAt).IsRequired();

        ConfigureBase(modelBuilder.Entity<FinanceIdempotencyEntity>(), "IdempotencyEntries");
        var idem = modelBuilder.Entity<FinanceIdempotencyEntity>(); idem.Property(item => item.ActorId).IsRequired(); idem.Property(item => item.OperationId).HasMaxLength(128).IsRequired(); idem.Property(item => item.Key).HasMaxLength(256).IsRequired(); idem.Property(item => item.Fingerprint).HasMaxLength(128).IsRequired(); idem.Property(item => item.ResourceType).HasMaxLength(128).IsRequired(); idem.Property(item => item.ResourceId).IsRequired(); idem.Property(item => item.SnapshotJson).HasMaxLength(262144).IsRequired(); idem.Property(item => item.CreatedAt).IsRequired(); idem.HasIndex(item => new { item.TenantId, item.ActorId, item.OperationId, item.Key }).IsUnique();

        ConfigureBase(modelBuilder.Entity<FinanceSourceEffectEntity>(), "SourceEffects");
        var effect = modelBuilder.Entity<FinanceSourceEffectEntity>(); effect.Property(item => item.CompanyId).IsRequired(); effect.Property(item => item.SourceContract).HasMaxLength(128).IsRequired(); effect.Property(item => item.SourceEvidenceId).IsRequired(); effect.Property(item => item.SourceEvidenceVersion).IsRequired(); effect.Property(item => item.JournalId).IsRequired(); effect.Property(item => item.CreatedAt).IsRequired(); effect.HasIndex(item => new { item.TenantId, item.CompanyId, item.SourceContract, item.SourceEvidenceId, item.SourceEvidenceVersion }).IsUnique();
    }

    private void ConfigureBase<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity, string tableName) where TEntity : FinanceEntity
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") entity.ToTable(tableName, "finance"); else entity.ToTable(tableName);
        entity.HasKey("Id"); entity.Property("Id").ValueGeneratedNever(); entity.Property(item => item.TenantId).HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();
        var version = entity.Property<byte[]>("Version").IsRequired().IsConcurrencyToken(); if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer") version.IsRowVersion(); else version.ValueGeneratedNever();
        entity.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }
}

#pragma warning restore CS1591
