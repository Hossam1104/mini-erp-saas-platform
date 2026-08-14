import { MasterDataLifecycleState } from './master-data.models';

export type PriceListProvenance = 'Manual' | 'Configured';

export type OrganizationScopeKind = 'Company' | 'Branch';

export interface PriceListWriteRequest {
  code: string;
  englishName: string;
  arabicName: string | null;
  currencyId: string;
  customerId: string | null;
  organizationScopeKind: OrganizationScopeKind | null;
  organizationScopeId: string | null;
  priority: number;
}

export interface PriceListPriceWriteRequest {
  productId: string;
  unitOfMeasureId: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  price: number;
  priceScale: number;
  provenance: PriceListProvenance;
  sourceReference: string | null;
}

export interface PriceListPriceVersionRecord {
  id: string;
  versionNumber: number;
  productId: string;
  productSku: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  currencyId: string;
  currencyCode: string;
  customerId: string | null;
  organizationScopeKind: OrganizationScopeKind | null;
  organizationScopeId: string | null;
  priority: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  price: number;
  priceScale: number;
  provenance: PriceListProvenance | string;
  sourceReference: string | null;
  version: string;
}

export interface PriceListRecord {
  id: string;
  tenantId: string;
  code: string;
  englishName: string;
  arabicName: string | null;
  currencyId: string;
  currencyCode: string;
  customerId: string | null;
  organizationScopeKind: OrganizationScopeKind | null;
  organizationScopeId: string | null;
  priority: number;
  lifecycleState: MasterDataLifecycleState;
  currentVersionNumber: number;
  prices: PriceListPriceVersionRecord[];
  version: string;
}

export interface PriceListReferenceQuery {
  priceListId?: string;
  productId: string;
  unitOfMeasureId: string;
  currencyId: string;
  customerId?: string;
  organizationScopeKind?: OrganizationScopeKind;
  organizationScopeId?: string;
  effectiveOn: string;
}

export interface PriceListReferenceRecord {
  priceListId: string;
  tenantId: string;
  priceListCode: string;
  priceVersionId: string;
  priceVersionNumber: number;
  productId: string;
  productSku: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  currencyId: string;
  currencyCode: string;
  customerId: string | null;
  organizationScopeKind: OrganizationScopeKind | null;
  organizationScopeId: string | null;
  priority: number;
  effectiveOn: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  price: number;
  priceScale: number;
  provenance: PriceListProvenance | string;
  sourceReference: string | null;
  referenceValue: string;
  priceVersion: string;
  masterVersion: string;
}
