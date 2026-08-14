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

    internal DbSet<MasterDataProductEntity> Products => Set<MasterDataProductEntity>();

    internal DbSet<MasterDataProductBarcodeEntity> ProductBarcodes => Set<MasterDataProductBarcodeEntity>();

    internal DbSet<MasterDataCurrencyEntity> Currencies => Set<MasterDataCurrencyEntity>();

    internal DbSet<MasterDataPaymentTermEntity> PaymentTerms => Set<MasterDataPaymentTermEntity>();

    internal DbSet<MasterDataPaymentTermVersionEntity> PaymentTermVersions => Set<MasterDataPaymentTermVersionEntity>();

    internal DbSet<MasterDataPaymentTermInstallmentEntity> PaymentTermInstallments => Set<MasterDataPaymentTermInstallmentEntity>();

    internal DbSet<MasterDataExchangeRateEntity> ExchangeRates => Set<MasterDataExchangeRateEntity>();

    internal DbSet<MasterDataExchangeRateVersionEntity> ExchangeRateVersions => Set<MasterDataExchangeRateVersionEntity>();

    internal DbSet<MasterDataPriceListEntity> PriceLists => Set<MasterDataPriceListEntity>();

    internal DbSet<MasterDataPriceListPriceEntity> PriceListPrices => Set<MasterDataPriceListPriceEntity>();

    internal DbSet<MasterDataTaxEntity> Taxes => Set<MasterDataTaxEntity>();

    internal DbSet<MasterDataTaxRateVersionEntity> TaxRateVersions => Set<MasterDataTaxRateVersionEntity>();

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
        category.Property(item => item.TrackingDefaultEnabled).IsRequired();
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

        var product = modelBuilder.Entity<MasterDataProductEntity>();
        product.ToTable("Products", "masterdata");
        product.HasKey(item => item.Id);
        product.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(product.Property(item => item.TenantId));
        product.Property(item => item.Sku).HasMaxLength(128).IsRequired();
        product.Property(item => item.SkuKey).HasMaxLength(128).IsRequired();
        product.Property(item => item.EnglishName).HasMaxLength(256).IsRequired();
        product.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        product.Property(item => item.Description).HasMaxLength(2048).IsRequired(false);
        product.Property(item => item.CategoryId).IsRequired();
        product.Property(item => item.BaseUnitOfMeasureId).IsRequired();
        product.Property(item => item.TrackingEnabledOverride).IsRequired(false);
        product.Property(item => item.IsSellable).IsRequired();
        product.Property(item => item.IsPurchasable).IsRequired();
        product.Property(item => item.IsInventoryRelevant).IsRequired();
        product.Property(item => item.LifecycleState).IsRequired();
        ConfigureVersion(product.Property(item => item.Version));
        product.HasIndex(item => new { item.TenantId, item.SkuKey }).IsUnique();
        product.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        product.HasOne<MasterDataCategoryEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.CategoryId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        product.HasOne<MasterDataUnitOfMeasureEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.BaseUnitOfMeasureId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        product.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var barcode = modelBuilder.Entity<MasterDataProductBarcodeEntity>();
        barcode.ToTable("ProductBarcodes", "masterdata");
        barcode.HasKey(item => item.Id);
        barcode.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(barcode.Property(item => item.TenantId));
        barcode.Property(item => item.ProductId).IsRequired();
        barcode.Property(item => item.Value).HasMaxLength(128).IsRequired();
        barcode.Property(item => item.ComparisonKey).HasMaxLength(128).IsRequired();
        ConfigureVersion(barcode.Property(item => item.Version));
        barcode.HasIndex(item => new { item.TenantId, item.ComparisonKey }).IsUnique();
        barcode.HasIndex(item => new { item.TenantId, item.ProductId });
        barcode.HasOne<MasterDataProductEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProductId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        barcode.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var currency = modelBuilder.Entity<MasterDataCurrencyEntity>();
        currency.ToTable("Currencies", "masterdata");
        currency.HasKey(item => item.Id);
        currency.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(currency.Property(item => item.TenantId));
        currency.Property(item => item.Code).HasMaxLength(16).IsRequired();
        currency.Property(item => item.CodeKey).HasMaxLength(16).IsRequired();
        currency.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false);
        currency.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        currency.Property(item => item.NameKey).HasMaxLength(256).IsRequired();
        currency.Property(item => item.LifecycleState).IsRequired();
        currency.Property(item => item.Revision).IsRequired();
        ConfigureVersion(currency.Property(item => item.Version));
        currency.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        currency.HasIndex(item => new { item.TenantId, item.NameKey }).IsUnique();
        currency.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        currency.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var paymentTerm = modelBuilder.Entity<MasterDataPaymentTermEntity>();
        paymentTerm.ToTable("PaymentTerms", "masterdata");
        paymentTerm.HasKey(item => item.Id);
        paymentTerm.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(paymentTerm.Property(item => item.TenantId));
        paymentTerm.Property(item => item.Code).HasMaxLength(128).IsRequired();
        paymentTerm.Property(item => item.CodeKey).HasMaxLength(128).IsRequired();
        paymentTerm.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false);
        paymentTerm.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        paymentTerm.Property(item => item.NameKey).HasMaxLength(256).IsRequired();
        paymentTerm.Property(item => item.LifecycleState).IsRequired();
        paymentTerm.Property(item => item.CurrentVersionNumber).IsRequired();
        ConfigureVersion(paymentTerm.Property(item => item.Version));
        paymentTerm.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        paymentTerm.HasIndex(item => new { item.TenantId, item.NameKey }).IsUnique();
        paymentTerm.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        paymentTerm.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var paymentTermVersion = modelBuilder.Entity<MasterDataPaymentTermVersionEntity>();
        paymentTermVersion.ToTable("PaymentTermVersions", "masterdata");
        paymentTermVersion.HasKey(item => item.Id);
        paymentTermVersion.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(paymentTermVersion.Property(item => item.TenantId));
        paymentTermVersion.Property(item => item.PaymentTermId).IsRequired();
        paymentTermVersion.Property(item => item.VersionNumber).IsRequired();
        paymentTermVersion.Property(item => item.Code).HasMaxLength(128).IsRequired();
        paymentTermVersion.Property(item => item.EnglishName).HasMaxLength(256).IsRequired(false);
        paymentTermVersion.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        paymentTermVersion.Property(item => item.EffectiveFrom).IsRequired();
        paymentTermVersion.Property(item => item.EffectiveTo).IsRequired(false);
        paymentTermVersion.Property(item => item.BaseDateRule).IsRequired();
        paymentTermVersion.Property(item => item.ScheduleMode).IsRequired();
        paymentTermVersion.Property(item => item.DueOffsetDays).IsRequired();
        paymentTermVersion.Property(item => item.DueOffsetMonths).IsRequired();
        paymentTermVersion.Property(item => item.EarlySettlementDiscountEnabled).IsRequired();
        paymentTermVersion.Property(item => item.EarlySettlementDiscountPercentage).HasPrecision(9, 6).IsRequired(false);
        paymentTermVersion.Property(item => item.EarlySettlementDiscountDays).IsRequired();
        paymentTermVersion.Property(item => item.EarlySettlementDiscountMonths).IsRequired();
        ConfigureVersion(paymentTermVersion.Property(item => item.Version));
        paymentTermVersion.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        paymentTermVersion.HasIndex(item => new { item.TenantId, item.PaymentTermId, item.VersionNumber }).IsUnique();
        paymentTermVersion.HasIndex(item => new { item.TenantId, item.PaymentTermId, item.EffectiveFrom }).IsUnique();
        paymentTermVersion.HasOne<MasterDataPaymentTermEntity>()
            .WithMany(item => item.Versions)
            .HasForeignKey(item => new { item.TenantId, item.PaymentTermId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        paymentTermVersion.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var paymentTermInstallment = modelBuilder.Entity<MasterDataPaymentTermInstallmentEntity>();
        paymentTermInstallment.ToTable("PaymentTermInstallments", "masterdata");
        paymentTermInstallment.HasKey(item => item.Id);
        paymentTermInstallment.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(paymentTermInstallment.Property(item => item.TenantId));
        paymentTermInstallment.Property(item => item.PaymentTermVersionId).IsRequired();
        paymentTermInstallment.Property(item => item.Sequence).IsRequired();
        paymentTermInstallment.Property(item => item.Percentage).HasPrecision(9, 6).IsRequired();
        paymentTermInstallment.Property(item => item.OffsetDays).IsRequired();
        paymentTermInstallment.Property(item => item.OffsetMonths).IsRequired();
        paymentTermInstallment.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        paymentTermInstallment.HasIndex(item => new { item.TenantId, item.PaymentTermVersionId, item.Sequence }).IsUnique();
        paymentTermInstallment.HasOne<MasterDataPaymentTermVersionEntity>()
            .WithMany(item => item.Installments)
            .HasForeignKey(item => new { item.TenantId, item.PaymentTermVersionId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        paymentTermInstallment.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var exchangeRate = modelBuilder.Entity<MasterDataExchangeRateEntity>();
        exchangeRate.ToTable("ExchangeRates", "masterdata");
        exchangeRate.HasKey(item => item.Id);
        exchangeRate.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(exchangeRate.Property(item => item.TenantId));
        exchangeRate.Property(item => item.SourceCurrencyId).IsRequired();
        exchangeRate.Property(item => item.TargetCurrencyId).IsRequired();
        exchangeRate.Property(item => item.LifecycleState).IsRequired();
        exchangeRate.Property(item => item.CurrentVersionNumber).IsRequired();
        ConfigureVersion(exchangeRate.Property(item => item.Version));
        exchangeRate.HasIndex(item => new { item.TenantId, item.SourceCurrencyId, item.TargetCurrencyId }).IsUnique();
        exchangeRate.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        exchangeRate.HasOne<MasterDataCurrencyEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.SourceCurrencyId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        exchangeRate.HasOne<MasterDataCurrencyEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.TargetCurrencyId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        exchangeRate.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var exchangeRateVersion = modelBuilder.Entity<MasterDataExchangeRateVersionEntity>();
        exchangeRateVersion.ToTable("ExchangeRateVersions", "masterdata");
        exchangeRateVersion.HasKey(item => item.Id);
        exchangeRateVersion.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(exchangeRateVersion.Property(item => item.TenantId));
        exchangeRateVersion.Property(item => item.ExchangeRateId).IsRequired();
        exchangeRateVersion.Property(item => item.VersionNumber).IsRequired();
        exchangeRateVersion.Property(item => item.EffectiveFrom).IsRequired();
        exchangeRateVersion.Property(item => item.EffectiveTo).IsRequired(false);
        exchangeRateVersion.Property(item => item.Rate).HasPrecision(28, 12).IsRequired();
        exchangeRateVersion.Property(item => item.RateScale).IsRequired();
        exchangeRateVersion.Property(item => item.Provenance).IsRequired();
        exchangeRateVersion.Property(item => item.SourceNotes).HasMaxLength(1024).IsRequired(false);
        exchangeRateVersion.Property(item => item.SourceCurrencyCode).HasMaxLength(16).IsRequired();
        exchangeRateVersion.Property(item => item.TargetCurrencyCode).HasMaxLength(16).IsRequired();
        ConfigureVersion(exchangeRateVersion.Property(item => item.Version));
        exchangeRateVersion.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        exchangeRateVersion.HasIndex(item => new { item.TenantId, item.ExchangeRateId, item.VersionNumber }).IsUnique();
        exchangeRateVersion.HasIndex(item => new { item.TenantId, item.ExchangeRateId, item.EffectiveFrom }).IsUnique();
        exchangeRateVersion.HasOne<MasterDataExchangeRateEntity>()
            .WithMany(item => item.Versions)
            .HasForeignKey(item => new { item.TenantId, item.ExchangeRateId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        exchangeRateVersion.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var priceList = modelBuilder.Entity<MasterDataPriceListEntity>();
        priceList.ToTable("PriceLists", "masterdata");
        priceList.HasKey(item => item.Id);
        priceList.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(priceList.Property(item => item.TenantId));
        priceList.Property(item => item.Code).HasMaxLength(128).IsRequired();
        priceList.Property(item => item.CodeKey).HasMaxLength(128).IsRequired();
        priceList.Property(item => item.EnglishName).HasMaxLength(256).IsRequired();
        priceList.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        priceList.Property(item => item.CurrencyId).IsRequired();
        priceList.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        priceList.Property(item => item.CustomerId).IsRequired(false);
        priceList.Property(item => item.OrganizationScopeKind).IsRequired(false);
        priceList.Property(item => item.OrganizationScopeId).IsRequired(false);
        priceList.Property(item => item.Priority).IsRequired();
        priceList.Property(item => item.LifecycleState).IsRequired();
        priceList.Property(item => item.CurrentVersionNumber).IsRequired();
        ConfigureVersion(priceList.Property(item => item.Version));
        priceList.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        priceList.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        priceList.HasOne<MasterDataCurrencyEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.CurrencyId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        priceList.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var priceListPrice = modelBuilder.Entity<MasterDataPriceListPriceEntity>();
        priceListPrice.ToTable("PriceListPrices", "masterdata");
        priceListPrice.HasKey(item => item.Id);
        priceListPrice.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(priceListPrice.Property(item => item.TenantId));
        priceListPrice.Property(item => item.PriceListId).IsRequired();
        priceListPrice.Property(item => item.VersionNumber).IsRequired();
        priceListPrice.Property(item => item.ProductId).IsRequired();
        priceListPrice.Property(item => item.ProductSku).HasMaxLength(128).IsRequired();
        priceListPrice.Property(item => item.UnitOfMeasureId).IsRequired();
        priceListPrice.Property(item => item.UnitOfMeasureCode).HasMaxLength(128).IsRequired();
        priceListPrice.Property(item => item.CurrencyId).IsRequired();
        priceListPrice.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired();
        priceListPrice.Property(item => item.CustomerId).IsRequired(false);
        priceListPrice.Property(item => item.OrganizationScopeKind).IsRequired(false);
        priceListPrice.Property(item => item.OrganizationScopeId).IsRequired(false);
        priceListPrice.Property(item => item.Priority).IsRequired();
        priceListPrice.Property(item => item.EffectiveFrom).IsRequired();
        priceListPrice.Property(item => item.EffectiveTo).IsRequired(false);
        priceListPrice.Property(item => item.Price).HasPrecision(28, 12).IsRequired();
        priceListPrice.Property(item => item.PriceScale).IsRequired();
        priceListPrice.Property(item => item.Provenance).IsRequired();
        priceListPrice.Property(item => item.SourceReference).HasMaxLength(1024).IsRequired(false);
        ConfigureVersion(priceListPrice.Property(item => item.Version));
        priceListPrice.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        priceListPrice.HasIndex(item => new { item.TenantId, item.PriceListId, item.VersionNumber }).IsUnique();
        priceListPrice.HasIndex(item => new { item.TenantId, item.PriceListId, item.ProductId, item.UnitOfMeasureId, item.EffectiveFrom }).IsUnique();
        priceListPrice.HasOne<MasterDataPriceListEntity>()
            .WithMany(item => item.Prices)
            .HasForeignKey(item => new { item.TenantId, item.PriceListId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        priceListPrice.HasOne<MasterDataProductEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProductId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        priceListPrice.HasOne<MasterDataUnitOfMeasureEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.UnitOfMeasureId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        priceListPrice.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var tax = modelBuilder.Entity<MasterDataTaxEntity>();
        tax.ToTable("Taxes", "masterdata");
        tax.HasKey(item => item.Id);
        tax.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(tax.Property(item => item.TenantId));
        tax.Property(item => item.Code).HasMaxLength(32).IsRequired();
        tax.Property(item => item.CodeKey).HasMaxLength(32).IsRequired();
        tax.Property(item => item.CategoryCode).HasMaxLength(64).IsRequired();
        tax.Property(item => item.CategoryCodeKey).HasMaxLength(64).IsRequired();
        tax.Property(item => item.CategoryEnglishName).HasMaxLength(256).IsRequired();
        tax.Property(item => item.CategoryArabicName).HasMaxLength(256).IsRequired(false);
        tax.Property(item => item.EnglishName).HasMaxLength(256).IsRequired();
        tax.Property(item => item.ArabicName).HasMaxLength(256).IsRequired(false);
        tax.Property(item => item.NameKey).HasMaxLength(256).IsRequired();
        tax.Property(item => item.Applicability).IsRequired();
        tax.Property(item => item.LifecycleState).IsRequired();
        tax.Property(item => item.CurrentVersionNumber).IsRequired();
        ConfigureVersion(tax.Property(item => item.Version));
        tax.HasIndex(item => new { item.TenantId, item.CodeKey }).IsUnique();
        tax.HasIndex(item => new { item.TenantId, item.NameKey }).IsUnique();
        tax.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        tax.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var taxRateVersion = modelBuilder.Entity<MasterDataTaxRateVersionEntity>();
        taxRateVersion.ToTable("TaxRateVersions", "masterdata");
        taxRateVersion.HasKey(item => item.Id);
        taxRateVersion.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(taxRateVersion.Property(item => item.TenantId));
        taxRateVersion.Property(item => item.TaxId).IsRequired();
        taxRateVersion.Property(item => item.VersionNumber).IsRequired();
        taxRateVersion.Property(item => item.EffectiveFrom).IsRequired();
        taxRateVersion.Property(item => item.EffectiveTo).IsRequired(false);
        taxRateVersion.Property(item => item.RatePercentage).HasPrecision(9, 6).IsRequired();
        ConfigureVersion(taxRateVersion.Property(item => item.Version));
        taxRateVersion.HasIndex(item => new { item.TenantId, item.Id }).IsUnique();
        taxRateVersion.HasIndex(item => new { item.TenantId, item.TaxId, item.VersionNumber }).IsUnique();
        taxRateVersion.HasIndex(item => new { item.TenantId, item.TaxId, item.EffectiveFrom }).IsUnique();
        taxRateVersion.HasOne<MasterDataTaxEntity>()
            .WithMany(item => item.RateVersions)
            .HasForeignKey(item => new { item.TenantId, item.TaxId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        taxRateVersion.HasQueryFilter(item => item.TenantId == TrustedTenantId);

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
