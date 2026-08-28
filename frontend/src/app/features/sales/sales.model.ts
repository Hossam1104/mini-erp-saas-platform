export type SalesQuotationStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Sent'
  | 'Expired'
  | 'Converted'
  | 'Withdrawn'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export type SalesOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'CreditHold'
  | 'Confirmed'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export type SalesCreditOutcome = 'Eligible' | 'Warning' | 'Blocked' | 'Pending' | 'Unknown' | 'Overridden';

export interface SalesQuotationLineRequest {
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  unitPriceOverride?: number | null;
  discountPercent?: number;
  notes?: string | null;
  taxId?: string | null;
}

export interface SalesQuotationCreateRequest {
  companyId: string;
  branchId: string | null;
  customerId: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  priceListId: string | null;
  customerContactId: string | null;
  notes: string | null;
  customerReference: string | null;
  lines: SalesQuotationLineRequest[];
  exchangeRateId?: string | null;
}

export interface SalesQuotationEditRequest extends Omit<SalesQuotationCreateRequest, 'customerId' | 'quotationDate'> {}

export interface SalesActionRequest { reason?: string | null; }

export interface SalesCreditOverrideRequest {
  reason: string;
  expiresAt: string;
  scope?: string | null;
  sourceReference?: string | null;
}

export interface SalesQuotationSummaryResponse {
  id: string;
  number: string;
  companyId: string;
  branchId: string | null;
  customerId: string;
  customerCode: string;
  customerName: string;
  createdByActorId: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  currencyCode: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  total: number;
  status: SalesQuotationStatus;
  revisionNumber: number;
  version: string;
  updatedAt: string;
}

export interface SalesQuotationLineResponse {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  quantity: number;
  unitPrice: number;
  resolvedUnitPrice: number;
  discountPercent: number;
  discountAmount: number;
  taxAmount: number;
  lineTotal: number;
  priceListId: string | null;
  priceVersionNumber: number | null;
  priceEffectiveFrom: string | null;
  priceProvenance: string;
  priceSourceReference: string | null;
  manualPriceApplied: boolean;
  commercialAuthorityPolicyId: string | null;
  commercialAuthorityActorId: string | null;
  commercialAuthorityEvidence: string | null;
  notes: string | null;
  taxId?: string | null;
  taxCode?: string | null;
  taxRateVersionId?: string | null;
  taxRateVersionNumber?: number | null;
  taxEffectiveFrom?: string | null;
  taxEffectiveTo?: string | null;
  taxRatePercentage?: number | null;
  taxableBase?: number | null;
  taxReferenceValue?: string | null;
}

export interface SalesExchangeRateEvidence {
  exchangeRateId: string;
  exchangeRateVersionId: string;
  versionNumber: number;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  rate: number;
  rateScale: number;
  provenance: string;
  sourceNotes: string | null;
  effectiveOn: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  referenceValue: string;
}

export interface SalesQuotationResponse extends SalesQuotationSummaryResponse {
  tenantId: string;
  customerContactId: string | null;
  notes: string | null;
  customerReference: string | null;
  lines: SalesQuotationLineResponse[];
  createdAt: string;
  exchangeRateEvidence?: SalesExchangeRateEvidence | null;
}

export interface SalesQuotationRevisionResponse {
  id: string;
  quotationId: string;
  revisionNumber: number;
  status: SalesQuotationStatus;
  snapshotHash: string;
  actorId: string;
  occurredAt: string;
  reason: string | null;
  snapshot: SalesQuotationResponse;
}

export interface SalesOrderSummaryResponse {
  id: string;
  number: string;
  companyId: string;
  branchId: string | null;
  customerId: string;
  customerCode: string;
  customerName: string;
  createdByActorId: string;
  sourceQuotationId: string;
  sourceQuotationNumber: string;
  sourceQuotationRevision: number;
  currencyId: string;
  currencyCode: string;
  total: number;
  status: SalesOrderStatus;
  creditOutcome: SalesCreditOutcome;
  version: string;
  updatedAt: string;
}

export interface SalesOrderResponse extends SalesOrderSummaryResponse {
  tenantId: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  creditReason: string | null;
  creditEvaluatedAt: string | null;
  creditOverrideExpiresAt: string | null;
  lines: SalesQuotationLineResponse[];
  createdAt: string;
  exchangeRateEvidence?: SalesExchangeRateEvidence | null;
}

export interface SalesHistoryResponse {
  id: string;
  documentType: string;
  documentId: string;
  action: string;
  fromStatus: string | null;
  toStatus: string | null;
  actorId: string;
  occurredAt: string;
  reason: string | null;
  policyId: string | null;
  policyVersion: number | null;
  creditOutcome: string | null;
  snapshotHash: string | null;
}

export interface SalesAuditResponse {
  id: string;
  operationId: string;
  documentType: string;
  documentId: string;
  actorId: string;
  occurredAt: string;
  decision: string;
  reason: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
  correlationId: string;
}

export interface SalesCreditResponse {
  documentId: string;
  customerId: string;
  companyId: string;
  currencyCode: string;
  openReceivables: number | null;
  overdueReceivables: number | null;
  netReceivableExposure: number | null;
  proposedExposure: number | null;
  creditLimit: number | null;
  outcome: SalesCreditOutcome;
  reason: string | null;
  asOfDate: string;
  evaluatedAt: string;
  overrideExpiresAt: string | null;
}
