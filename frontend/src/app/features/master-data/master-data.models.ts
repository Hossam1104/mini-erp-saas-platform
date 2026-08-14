import { TranslationKey } from '../../core/i18n/language.service';

export * from './price-list.model';
export * from './master-data-import.models';

export type MasterDataResourceKey = 'categories' | 'units' | 'products' | 'suppliers' | 'customers' | 'currencies' | 'payment-terms' | 'taxes' | 'exchange-rates';

export type MasterDataLifecycleState = 'Active' | 'Inactive' | string;

export interface MasterDataRecordBase {
  id: string;
  tenantId: string;
  lifecycleState: MasterDataLifecycleState;
  version: string;
}

export interface CategoryRecord extends MasterDataRecordBase {
  code: string;
  englishName: string | null;
  arabicName: string | null;
  parentCategoryId: string | null;
  trackingDefaultEnabled: boolean;
}

export interface UnitOfMeasureRecord extends MasterDataRecordBase {
  code: string;
  englishName: string | null;
  arabicName: string | null;
}

export interface ProductBarcodeRecord {
  id: string;
  productId: string;
  value: string;
  version: string;
}

export interface ProductRecord extends MasterDataRecordBase {
  sku: string;
  englishName: string | null;
  arabicName: string | null;
  description: string | null;
  categoryId: string;
  baseUnitOfMeasureId: string;
  trackingDefaultEnabled: boolean;
  trackingEnabledOverride: boolean | null;
  trackingEnabled: boolean;
  isSellable: boolean;
  isPurchasable: boolean;
  isInventoryRelevant: boolean;
  barcodes: ProductBarcodeRecord[];
}

export interface PartyContactRecord {
  id: string;
  supplierId?: string;
  customerId?: string;
  name: string;
  email: string | null;
  phone: string | null;
  version: string;
}

export interface SupplierRecord extends MasterDataRecordBase {
  code: string;
  englishLegalName: string | null;
  arabicLegalName: string | null;
  englishTradingName: string | null;
  arabicTradingName: string | null;
  registrationReference: string | null;
  contacts: PartyContactRecord[];
}

export interface CustomerRecord extends MasterDataRecordBase {
  code: string;
  englishLegalName: string | null;
  arabicLegalName: string | null;
  englishTradingName: string | null;
  arabicTradingName: string | null;
  contacts: PartyContactRecord[];
}

export interface CurrencyRecord extends MasterDataRecordBase {
  code: string;
  englishName: string | null;
  arabicName: string | null;
  revision: number;
}

export interface PaymentTermInstallmentRecord {
  sequence: number;
  percentage: number;
  days: number;
  months: number;
}

export interface PaymentTermVersionRecord {
  id: string;
  versionNumber: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  baseDateRule: string;
  scheduleMode: string;
  dueOffsetDays: number;
  dueOffsetMonths: number;
  installments: PaymentTermInstallmentRecord[];
  earlySettlementDiscountEnabled: boolean;
  earlySettlementDiscountPercentage: number | null;
  earlySettlementDiscountDays: number;
  earlySettlementDiscountMonths: number;
  code: string;
  englishName: string | null;
  arabicName: string | null;
}

export interface PaymentTermRecord extends MasterDataRecordBase {
  code: string;
  englishName: string | null;
  arabicName: string | null;
  currentVersionNumber: number;
  versions: PaymentTermVersionRecord[];
}

export type TaxDirection = 'Purchase' | 'Sales' | 'Both';
export type TaxRoundingMode = 'ToEven' | 'AwayFromZero';

export interface TaxRateVersionRecord {
  id: string;
  versionNumber: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  ratePercentage: number;
}

export interface TaxRecord extends MasterDataRecordBase {
  code: string;
  categoryCode: string;
  categoryEnglishName: string | null;
  categoryArabicName: string | null;
  englishName: string | null;
  arabicName: string | null;
  applicability: TaxDirection | string;
  currentVersionNumber: number;
  rateVersions: TaxRateVersionRecord[];
}

export type ExchangeRateProvenance = 'Manual' | 'Configured';

export interface ExchangeRateVersionRecord {
  id: string;
  versionNumber: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  rate: number;
  rateScale: number;
  provenance: ExchangeRateProvenance | string;
  sourceNotes: string | null;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
}

export interface ExchangeRateRecord extends MasterDataRecordBase {
  sourceCurrencyId: string;
  targetCurrencyId: string;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  currentVersionNumber: number;
  versions: ExchangeRateVersionRecord[];
}

export interface ExchangeRateReferenceResponse {
  id: string;
  tenantId: string;
  sourceCurrencyId: string;
  targetCurrencyId: string;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  lifecycleState: string;
  versionNumber: number;
  versionId: string;
  effectiveOn: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  rate: number;
  rateScale: number;
  provenance: ExchangeRateProvenance | string;
  sourceNotes: string | null;
  referenceValue: string;
  version: string;
}

export interface TaxCalculationRequest {
  effectiveOn: string;
  transactionDirection: Exclude<TaxDirection, 'Both'>;
  taxableBase: number;
  currencyCode: string;
  roundingScale: number;
  roundingMode: TaxRoundingMode;
  sourceLineage: string;
}

export interface TaxCalculationResponse extends TaxCalculationRequest {
  taxId: string;
  tenantId: string;
  code: string;
  categoryCode: string;
  applicability: TaxDirection | string;
  rateVersionId: string;
  rateVersionNumber: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  ratePercentage: number;
  taxAmount: number;
  referenceValue: string;
}

export type MasterDataRecord =
  | CategoryRecord
  | UnitOfMeasureRecord
  | ProductRecord
  | SupplierRecord
  | CustomerRecord
  | CurrencyRecord
  | PaymentTermRecord
  | TaxRecord
  | ExchangeRateRecord;

export interface ContactDraft {
  name: string;
  email: string;
  phone: string;
}

export interface CategoryDraft {
  code: string;
  englishName: string;
  arabicName: string;
  parentCategoryId: string;
  trackingDefaultEnabled: boolean;
}

export interface UnitDraft {
  code: string;
  englishName: string;
  arabicName: string;
}

export interface ProductDraft {
  sku: string;
  englishName: string;
  arabicName: string;
  description: string;
  categoryId: string;
  baseUnitOfMeasureId: string;
  barcodes: string;
  trackingEnabledOverride: 'inherit' | 'enabled' | 'disabled';
  isSellable: boolean;
  isPurchasable: boolean;
  isInventoryRelevant: boolean;
}

export interface PartyDraft {
  code: string;
  englishLegalName: string;
  arabicLegalName: string;
  englishTradingName: string;
  arabicTradingName: string;
  registrationReference: string;
  contacts: ContactDraft[];
}

export interface CurrencyDraft {
  code: string;
  englishName: string;
  arabicName: string;
}

export interface PaymentTermInstallmentDraft {
  sequence: number;
  percentage: number;
  days: number;
  months: number;
}

export interface PaymentTermDraft {
  code: string;
  englishName: string;
  arabicName: string;
  effectiveFrom: string;
  effectiveTo: string;
  baseDateRule: 'DocumentDate' | 'InvoiceDate' | 'DeliveryDate' | 'ReceiptDate';
  scheduleMode: 'SingleDueDate' | 'Installments';
  dueOffsetDays: number;
  dueOffsetMonths: number;
  installments: PaymentTermInstallmentDraft[];
  earlySettlementDiscountEnabled: boolean;
  earlySettlementDiscountPercentage: number;
  earlySettlementDiscountDays: number;
  earlySettlementDiscountMonths: number;
}

export interface TaxDraft {
  code: string;
  categoryCode: string;
  categoryEnglishName: string;
  categoryArabicName: string;
  englishName: string;
  arabicName: string;
  applicability: TaxDirection;
  effectiveFrom: string;
  effectiveTo: string;
  ratePercentage: number;
}

export interface ExchangeRateDraft {
  sourceCurrencyId: string;
  targetCurrencyId: string;
  effectiveFrom: string;
  effectiveTo: string;
  rate: number;
  rateScale: number;
  provenance: ExchangeRateProvenance;
  sourceNotes: string;
}

export type MasterDataDraft = CategoryDraft | UnitDraft | ProductDraft | PartyDraft | CurrencyDraft | PaymentTermDraft | TaxDraft | ExchangeRateDraft;

export type MasterDataWritePayload =
  | {
      code: string;
      englishName: string | null;
      arabicName: string | null;
      parentCategoryId: string | null;
      trackingDefaultEnabled: boolean;
    }
  | {
      code: string;
      englishName: string | null;
      arabicName: string | null;
    }
  | {
      sku: string;
      englishName: string | null;
      arabicName: string | null;
      description: string | null;
      categoryId: string;
      baseUnitOfMeasureId: string;
      barcodes: string[];
      trackingEnabledOverride: boolean | null;
      isSellable: boolean;
      isPurchasable: boolean;
      isInventoryRelevant: boolean;
    }
  | {
      code: string;
      englishLegalName: string | null;
      arabicLegalName: string | null;
      englishTradingName: string | null;
      arabicTradingName: string | null;
      registrationReference?: string | null;
      contacts: Array<{ name: string; email: string | null; phone: string | null }>;
    }
  | {
      code: string;
      englishName: string | null;
      arabicName: string | null;
    }
  | {
      code: string;
      englishName: string | null;
      arabicName: string | null;
      effectiveFrom: string;
      effectiveTo: string | null;
      baseDateRule: string;
      scheduleMode: string;
      dueOffset: { days: number; months: number };
      installments: Array<{ sequence: number; percentage: number; days: number; months: number }>;
      earlySettlementDiscount: { enabled: boolean; percentage: number | null; days: number; months: number };
    }
  | {
      code: string;
      categoryCode: string;
      categoryEnglishName: string | null;
      categoryArabicName: string | null;
      englishName: string | null;
      arabicName: string | null;
      applicability: TaxDirection;
      rateVersion: { effectiveFrom: string; effectiveTo: string | null; ratePercentage: number };
    }
  | {
      sourceCurrencyId: string;
      targetCurrencyId: string;
      effectiveFrom: string;
      effectiveTo: string | null;
      rate: number;
      rateScale: number;
      provenance: ExchangeRateProvenance;
      sourceNotes: string | null;
    };

export interface MasterDataAuditEntry {
  evidenceId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  sessionId: string;
  authorizationPath: string;
  operation: string;
  policyOutcome: string;
  decision: string;
  reason: string;
  beforeSummary: string | null;
  afterSummary: string | null;
  approverId: string | null;
}

export interface MasterDataResourceDefinition {
  key: MasterDataResourceKey;
  labelKey: TranslationKey;
  leadKey: TranslationKey;
  endpoint: string;
  accent: string;
}

export const RESOURCE_DEFINITIONS: readonly MasterDataResourceDefinition[] = [
  {
    key: 'categories',
    labelKey: 'categories',
    leadKey: 'categoryLead',
    endpoint: '/master-data/categories',
    accent: 'mint',
  },
  {
    key: 'units',
    labelKey: 'unitsOfMeasure',
    leadKey: 'unitLead',
    endpoint: '/master-data/units-of-measure',
    accent: 'gold',
  },
  {
    key: 'products',
    labelKey: 'products',
    leadKey: 'productLead',
    endpoint: '/master-data/products',
    accent: 'blue',
  },
  {
    key: 'suppliers',
    labelKey: 'suppliers',
    leadKey: 'supplierLead',
    endpoint: '/master-data/suppliers',
    accent: 'orange',
  },
  {
    key: 'customers',
    labelKey: 'businessCustomers',
    leadKey: 'customerLead',
    endpoint: '/master-data/customers',
    accent: 'violet',
  },
  {
    key: 'currencies',
    labelKey: 'currencies',
    leadKey: 'currencyLead',
    endpoint: '/master-data/currencies',
    accent: 'gold',
  },
  {
    key: 'payment-terms',
    labelKey: 'paymentTerms',
    leadKey: 'paymentTermLead',
    endpoint: '/master-data/payment-terms',
    accent: 'orange',
  },
  {
    key: 'taxes',
    labelKey: 'taxes',
    leadKey: 'taxLead',
    endpoint: '/master-data/taxes',
    accent: 'violet',
  },
  {
    key: 'exchange-rates',
    labelKey: 'exchangeRates',
    leadKey: 'exchangeRateLead',
    endpoint: '/master-data/exchange-rates',
    accent: 'blue',
  },
] as const;

export function isMasterDataResourceKey(value: string | null): value is MasterDataResourceKey {
  return RESOURCE_DEFINITIONS.some((definition) => definition.key === value);
}

export function resourceDefinition(key: MasterDataResourceKey): MasterDataResourceDefinition {
  return RESOURCE_DEFINITIONS.find((definition) => definition.key === key) ?? RESOURCE_DEFINITIONS[0];
}
