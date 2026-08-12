import { TranslationKey } from '../../core/i18n/language.service';

export type MasterDataResourceKey = 'categories' | 'units' | 'products' | 'suppliers' | 'customers';

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

export type MasterDataRecord =
  | CategoryRecord
  | UnitOfMeasureRecord
  | ProductRecord
  | SupplierRecord
  | CustomerRecord;

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

export type MasterDataDraft = CategoryDraft | UnitDraft | ProductDraft | PartyDraft;

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
] as const;

export function isMasterDataResourceKey(value: string | null): value is MasterDataResourceKey {
  return RESOURCE_DEFINITIONS.some((definition) => definition.key === value);
}

export function resourceDefinition(key: MasterDataResourceKey): MasterDataResourceDefinition {
  return RESOURCE_DEFINITIONS.find((definition) => definition.key === key) ?? RESOURCE_DEFINITIONS[0];
}
