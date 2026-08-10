using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.BusinessParties;

/// <summary>
/// Business Parties module-owned EF context. It is internal so ordinary
/// application callers can only obtain explicit Tenant-bound Business Parties
/// persistence contracts.
/// </summary>
internal sealed class BusinessPartiesDbContext : TenantPersistenceDbContext
{
    internal BusinessPartiesDbContext(
        DbContextOptions options,
        TenantContext tenantContext)
        : base(options, tenantContext, TenantOwnershipVerifierRegistry.CreateBusinessParties())
    {
    }

    internal DbSet<BusinessPartiesSupplierEntity> Suppliers => Set<BusinessPartiesSupplierEntity>();

    internal DbSet<BusinessPartiesSupplierContactEntity> SupplierContacts => Set<BusinessPartiesSupplierContactEntity>();

    internal DbSet<BusinessPartiesCustomerEntity> Customers => Set<BusinessPartiesCustomerEntity>();

    internal DbSet<BusinessPartiesCustomerContactEntity> CustomerContacts => Set<BusinessPartiesCustomerContactEntity>();

    internal DbSet<BusinessPartiesAuditEventEntity> AuditEvents => Set<BusinessPartiesAuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var supplier = modelBuilder.Entity<BusinessPartiesSupplierEntity>();
        supplier.ToTable("Suppliers", "businessparties");
        supplier.HasKey(item => item.Id);
        supplier.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(supplier.Property(item => item.TenantId));
        supplier.Property(item => item.Code).HasMaxLength(128).IsRequired();
        supplier.Property(item => item.CodeKey).HasMaxLength(128).IsRequired();
        supplier.Property(item => item.EnglishLegalName).HasMaxLength(256).IsRequired();
        supplier.Property(item => item.ArabicLegalName).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.EnglishLegalNameKey).HasMaxLength(256).IsRequired();
        supplier.Property(item => item.ArabicLegalNameKey).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.EnglishTradingName).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.ArabicTradingName).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.EnglishTradingNameKey).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.ArabicTradingNameKey).HasMaxLength(256).IsRequired(false);
        supplier.Property(item => item.RegistrationReference).HasMaxLength(128).IsRequired(false);
        supplier.Property(item => item.RegistrationKey).HasMaxLength(128).IsRequired(false);
        supplier.Property(item => item.LifecycleState).IsRequired();
        ConfigureVersion(supplier.Property(item => item.Version));
        supplier.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        supplier.HasIndex(item => new { item.TenantId, item.RegistrationKey })
            .IsUnique()
            .HasFilter("[RegistrationKey] IS NOT NULL");
        supplier.HasIndex(item => new { item.TenantId, item.EnglishLegalNameKey }).IsUnique();
        supplier.HasIndex(item => new { item.TenantId, item.ArabicLegalNameKey })
            .IsUnique()
            .HasFilter("[ArabicLegalNameKey] IS NOT NULL");
        supplier.HasIndex(item => new { item.TenantId, item.EnglishTradingNameKey })
            .IsUnique()
            .HasFilter("[EnglishTradingNameKey] IS NOT NULL");
        supplier.HasIndex(item => new { item.TenantId, item.ArabicTradingNameKey })
            .IsUnique()
            .HasFilter("[ArabicTradingNameKey] IS NOT NULL");
        supplier.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        supplier.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var contact = modelBuilder.Entity<BusinessPartiesSupplierContactEntity>();
        contact.ToTable("SupplierContacts", "businessparties");
        contact.HasKey(item => item.Id);
        contact.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(contact.Property(item => item.TenantId));
        contact.Property(item => item.SupplierId).IsRequired();
        contact.Property(item => item.Name).HasMaxLength(256).IsRequired();
        contact.Property(item => item.Email).HasMaxLength(256).IsRequired(false);
        contact.Property(item => item.Phone).HasMaxLength(256).IsRequired(false);
        ConfigureVersion(contact.Property(item => item.Version));
        contact.HasIndex(item => new { item.TenantId, item.SupplierId });
        contact.HasOne<BusinessPartiesSupplierEntity>()
            .WithMany(item => item.Contacts)
            .HasForeignKey(item => new { item.TenantId, item.SupplierId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        contact.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var customer = modelBuilder.Entity<BusinessPartiesCustomerEntity>();
        customer.ToTable("Customers", "businessparties");
        customer.HasKey(item => item.Id);
        customer.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(customer.Property(item => item.TenantId));
        customer.Property(item => item.Code).HasMaxLength(128).IsRequired();
        customer.Property(item => item.CodeKey).HasMaxLength(128).IsRequired();
        customer.Property(item => item.EnglishLegalName).HasMaxLength(256).IsRequired();
        customer.Property(item => item.ArabicLegalName).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.EnglishLegalNameKey).HasMaxLength(256).IsRequired();
        customer.Property(item => item.ArabicLegalNameKey).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.EnglishTradingName).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.ArabicTradingName).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.EnglishTradingNameKey).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.ArabicTradingNameKey).HasMaxLength(256).IsRequired(false);
        customer.Property(item => item.LifecycleState).IsRequired();
        ConfigureVersion(customer.Property(item => item.Version));
        customer.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        customer.HasIndex(item => new { item.TenantId, item.EnglishLegalNameKey }).IsUnique();
        customer.HasIndex(item => new { item.TenantId, item.ArabicLegalNameKey })
            .IsUnique()
            .HasFilter("[ArabicLegalNameKey] IS NOT NULL");
        customer.HasIndex(item => new { item.TenantId, item.EnglishTradingNameKey })
            .IsUnique()
            .HasFilter("[EnglishTradingNameKey] IS NOT NULL");
        customer.HasIndex(item => new { item.TenantId, item.ArabicTradingNameKey })
            .IsUnique()
            .HasFilter("[ArabicTradingNameKey] IS NOT NULL");
        customer.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        customer.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var customerContact = modelBuilder.Entity<BusinessPartiesCustomerContactEntity>();
        customerContact.ToTable("CustomerContacts", "businessparties");
        customerContact.HasKey(item => item.Id);
        customerContact.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(customerContact.Property(item => item.TenantId));
        customerContact.Property(item => item.CustomerId).IsRequired();
        customerContact.Property(item => item.Name).HasMaxLength(256).IsRequired();
        customerContact.Property(item => item.Email).HasMaxLength(256).IsRequired(false);
        customerContact.Property(item => item.Phone).HasMaxLength(256).IsRequired(false);
        ConfigureVersion(customerContact.Property(item => item.Version));
        customerContact.HasIndex(item => new { item.TenantId, item.CustomerId });
        customerContact.HasOne<BusinessPartiesCustomerEntity>()
            .WithMany(item => item.Contacts)
            .HasForeignKey(item => new { item.TenantId, item.CustomerId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        customerContact.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var audit = modelBuilder.Entity<BusinessPartiesAuditEventEntity>();
        audit.ToTable("AuditEvents", "businessparties");
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
        audit.Property(item => item.BusinessCode).HasMaxLength(128).IsRequired(false);
        audit.Property(item => item.Operation).IsRequired();
        audit.Property(item => item.PolicyOutcome).IsRequired();
        audit.Property(item => item.Decision).IsRequired();
        audit.Property(item => item.Reason).IsRequired();
        audit.Property(item => item.BeforeSummary).HasMaxLength(2048).IsRequired(false);
        audit.Property(item => item.AfterSummary).HasMaxLength(2048).IsRequired(false);
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
