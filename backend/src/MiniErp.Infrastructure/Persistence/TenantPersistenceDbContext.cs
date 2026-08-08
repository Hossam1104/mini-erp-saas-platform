using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

internal class TenantPersistenceDbContext : DbContext
{
    private readonly TenantContext _tenantContext;
    private readonly TenantPersistenceGuard _guard;
    private readonly TenantOwnershipVerifierRegistry _verifierRegistry;

    public TenantPersistenceDbContext(
        DbContextOptions options,
        TenantContext tenantContext,
        TenantOwnershipVerifierRegistry? verifierRegistry = null)
        : base(options)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _verifierRegistry = verifierRegistry ?? TenantOwnershipVerifierRegistry.CreateDefault();
        _guard = new TenantPersistenceGuard(_tenantContext, _verifierRegistry);
    }

    public DbSet<TenantOwnedRecord> TenantOwnedRecords => Set<TenantOwnedRecord>();

    internal TenantContext TenantContext => _tenantContext;

    internal void ValidateTenantOwnershipVerifiers()
    {
        _verifierRegistry.ValidateModel(Model);
    }

    // EF Core parameterizes context instance properties in model-level query
    // filters, so each explicit session gets its own trusted Tenant boundary.
    protected TenantId TrustedTenantId => _tenantContext.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantOwnershipVerifiers();
        PrepareApplicationConcurrencyTokens();
        _guard.Validate(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantOwnershipVerifiers();
        PrepareApplicationConcurrencyTokens();
        await _guard.ValidateAsync(ChangeTracker, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var record = modelBuilder.Entity<TenantOwnedRecord>();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            record.ToTable("TenantOwnedRecords", "tenancy");
        }
        else
        {
            record.ToTable("TenantOwnedRecords");
        }

        record.HasKey(item => item.Id);
        record.Property(item => item.Id).ValueGeneratedNever();
        record.Property(item => item.TenantId)
            .HasConversion(item => item.Value, value => new TenantId(value))
            .IsRequired();
        record.Property(item => item.RelatedTenantId)
            .HasConversion(
                item => item.HasValue ? item.Value.Value : (Guid?)null,
                value => value.HasValue ? new TenantId(value.Value) : (TenantId?)null);
        record.Property(item => item.BusinessKey).HasMaxLength(200).IsRequired();
        record.Property(item => item.RelationshipKind).IsRequired();
        var version = record.Property(item => item.Version)
            .IsRequired()
            .IsConcurrencyToken();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            version.IsRowVersion();
        }
        else
        {
            // SQLite is the local validation provider and has no SQL Server
            // rowversion primitive. Keep the value application-owned there so
            // the same model can be created without a provider-specific
            // generated-value column.
            version.ValueGeneratedNever();
        }
        record.HasIndex(item => new { item.TenantId, item.BusinessKey }).IsUnique();

        record.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }

    private void PrepareApplicationConcurrencyTokens()
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            var version = entry.Properties.SingleOrDefault(property =>
                string.Equals(property.Metadata.Name, "Version", StringComparison.Ordinal)
                && property.Metadata.ClrType == typeof(byte[]));
            if (version is null)
            {
                continue;
            }

            if ((entry.State == EntityState.Added && version.CurrentValue is byte[] addedVersion
                    && addedVersion.Length == 0)
                || (entry.State == EntityState.Modified && version.CurrentValue is byte[] current
                    && version.OriginalValue is byte[] original
                    && current.SequenceEqual(original)))
            {
                version.CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
