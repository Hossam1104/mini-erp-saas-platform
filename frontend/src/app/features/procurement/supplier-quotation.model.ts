export type SupplierQuotationStatus = 'Draft' | 'Submitted' | 'Withdrawn' | 'Disqualified' | 'Superseded' | string;

export interface SupplierQuotationLineWriteRequest {
  purchaseRequestLineId: string;
  quotedQuantity: number;
  unitPrice: number;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxId: string | null;
  taxReference: string | null;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  offeredDeliveryDate: string | null;
  offeredDeliveryLeadTime: string | null;
  notes: string | null;
}

export interface SupplierQuotationEvidenceReferenceWriteRequest {
  referenceId: string | null;
  fileName: string | null;
  contentType: string | null;
  description: string | null;
  source: string | null;
  externalReference: string | null;
}

export interface SupplierQuotationWriteRequest {
  supplierId: string;
  supplierQuotationReference: string | null;
  offerDate: string;
  validUntil: string | null;
  currencyId: string;
  paymentTermId: string | null;
  deliveryTerms: string | null;
  offeredDeliveryDate: string | null;
  offeredDeliveryLeadTime: string | null;
  notes: string | null;
  lines: SupplierQuotationLineWriteRequest[];
  evidence: SupplierQuotationEvidenceReferenceWriteRequest[];
}

export interface SupplierQuotationActionRequest {
  reason?: string | null;
}

export interface SupplierQuotationSupplierResponse {
  id: string;
  code: string;
  name: string;
}

export interface SupplierQuotationCurrencyResponse {
  id: string;
  code: string;
  name: string;
}

export interface SupplierQuotationPaymentTermResponse {
  id: string;
  code: string;
  name: string;
  version: number;
}

export interface SupplierQuotationLineResponse {
  id: string;
  purchaseRequestLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  requestedQuantity: number;
  quotedQuantity: number;
  unitPrice: number;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxId: string | null;
  taxCode: string | null;
  taxName: string | null;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  taxReference: string | null;
  requestedNeedByDate: string;
  offeredDeliveryDate: string | null;
  offeredDeliveryLeadTime: string | null;
  notes: string | null;
  version: string;
}

export interface SupplierQuotationEvidenceReferenceResponse {
  id: string;
  referenceId: string;
  fileName: string | null;
  contentType: string | null;
  description: string | null;
  source: string;
  externalReference: string | null;
  recordedByActorId: string;
  recordedAt: string;
}

export interface SupplierQuotationResponse {
  id: string;
  tenantId: string;
  purchaseRequestId: string;
  companyId: string;
  branchId: string | null;
  createdByActorId: string;
  supplier: SupplierQuotationSupplierResponse;
  status: SupplierQuotationStatus;
  supplierQuotationReference: string;
  offerDate: string;
  validUntil: string | null;
  currency: SupplierQuotationCurrencyResponse;
  paymentTerm: SupplierQuotationPaymentTermResponse | null;
  deliveryTerms: string | null;
  offeredDeliveryDate: string | null;
  offeredDeliveryLeadTime: string | null;
  notes: string | null;
  lines: SupplierQuotationLineResponse[];
  evidence: SupplierQuotationEvidenceReferenceResponse[];
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  isSelected: boolean;
  version: string;
  canEdit: boolean;
  canSubmit: boolean;
  canWithdraw: boolean;
  canDisqualify: boolean;
}

export interface SupplierQuotationListItemResponse {
  id: string;
  purchaseRequestId: string;
  supplier: SupplierQuotationSupplierResponse;
  status: SupplierQuotationStatus;
  supplierQuotationReference: string;
  offerDate: string;
  validUntil: string | null;
  currency: SupplierQuotationCurrencyResponse;
  commercialTotal: number;
  coveredLineCount: number;
  requestedLineCount: number;
  hasEvidence: boolean;
  version: string;
}

export interface SupplierQuotationHistoryResponse {
  evidenceId: string;
  supplierQuotationId: string;
  occurredAt: string;
  fromStatus: SupplierQuotationStatus;
  toStatus: SupplierQuotationStatus;
  action: string;
  actorId: string;
  reason: string | null;
  correlationId: string;
  policyId: string | null;
  policyVersion: number | null;
  stageKey: string | null;
  delegatedFromActorId: string | null;
}

export interface SupplierQuotationAuditResponse {
  evidenceId: string;
  supplierQuotationId: string;
  purchaseRequestId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  sessionId: string;
  authorizationPath: string;
  decision: string;
  reason: string | null;
  beforeStatus: string | null;
  afterStatus: string | null;
  companyId: string;
  branchId: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
}

export interface SupplierQuotationComparisonLineResponse {
  purchaseRequestLineId: string;
  productSku: string;
  productName: string;
  requestedQuantity: number;
  quotedQuantity: number | null;
  unitPrice: number | null;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  requestedNeedByDate: string;
  offeredDeliveryDate: string | null;
  isCovered: boolean;
  qualificationIssue: string | null;
}

export interface SupplierQuotationComparisonItemResponse {
  supplierQuotationId: string;
  supplier: SupplierQuotationSupplierResponse;
  status: SupplierQuotationStatus;
  supplierQuotationReference: string;
  offerDate: string;
  validUntil: string | null;
  currency: SupplierQuotationCurrencyResponse;
  commercialTotal: number;
  coveredLineCount: number;
  requestedLineCount: number;
  hasEvidence: boolean;
  isDirectlyComparableToAll: boolean;
  paymentTermCode: string | null;
  deliveryTerms: string | null;
  offeredDeliveryDate: string | null;
  offeredDeliveryLeadTime: string | null;
  lines: SupplierQuotationComparisonLineResponse[];
  qualificationIssues: string[];
}

export interface SupplierQuotationCurrencyComparisonGroupResponse {
  currencyId: string;
  currencyCode: string;
  supplierQuotationIds: string[];
  directlyComparableWithinGroup: boolean;
}

export interface SupplierSourceDecisionResponse {
  id: string;
  tenantId: string;
  purchaseRequestId: string;
  selectedQuotationId: string;
  supplier: SupplierQuotationSupplierResponse;
  supplierQuotationReference: string;
  actorId: string;
  selectedAt: string;
  rationale: string;
  policyId: string | null;
  policyVersion: number | null;
  stageKey: string | null;
  comparisonSnapshotReference: string;
  version: string;
}

export interface SupplierSourceDecisionHistoryResponse {
  id: string;
  tenantId: string;
  sourceDecisionId: string;
  purchaseRequestId: string;
  previousSelectedQuotationId: string | null;
  selectedQuotationId: string;
  actorId: string;
  selectedAt: string;
  rationale: string;
  policyId: string | null;
  policyVersion: number | null;
  stageKey: string | null;
  comparisonSnapshotReference: string;
}

export interface SupplierQuotationComparisonResponse {
  purchaseRequestId: string;
  hasMixedCurrencies: boolean;
  directCurrencyComparisonAvailable: boolean;
  comparisonBasis: string;
  currencyGroups: SupplierQuotationCurrencyComparisonGroupResponse[];
  quotations: SupplierQuotationComparisonItemResponse[];
  currentSourceDecision: SupplierSourceDecisionResponse | null;
}
