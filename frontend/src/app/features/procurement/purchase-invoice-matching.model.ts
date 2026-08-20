export type PurchaseInvoiceMatchResult = 'NotMatchReady' | 'ExactMatch' | 'WithinTolerance' | 'ExceptionHold' | 'ResolvedException' | string;
export type PurchaseInvoiceMatchLifecycle = 'Current' | 'Superseded' | string;

export interface PurchaseInvoiceExchangeRateReferenceRequest {
  exchangeRateId: string;
  effectiveOn?: string | null;
}

export interface PurchaseInvoiceMatchEvaluateRequest {
  exchangeRateReference?: PurchaseInvoiceExchangeRateReferenceRequest | null;
}

export interface PurchaseInvoiceMatchResolveRequest {
  reason?: string | null;
}

export interface PurchaseInvoiceMatchVarianceResponse {
  classification: string;
  purchaseOrderLineId: string | null;
  goodsReceiptLineId: string | null;
  expectedValue: number | null;
  actualValue: number | null;
  variance: number | null;
  allowedTolerance: number;
  currencyCode: string | null;
  details: string | null;
}

export interface PurchaseInvoiceMatchPolicyResponse {
  policyId: string;
  version: number;
  quantityAbsoluteTolerance: number;
  quantityPercentageTolerance: number;
  priceAbsoluteTolerance: number;
  pricePercentageTolerance: number;
  amountAbsoluteTolerance: number;
  amountPercentageTolerance: number;
  taxAbsoluteTolerance: number;
  taxPercentageTolerance: number;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface PurchaseInvoiceMatchResolutionPolicyResponse {
  policyId: string;
  version: number;
  allowResolution: boolean;
  requireDifferentActor: boolean;
  requireReason: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface PurchaseInvoiceMatchExchangeRateResponse {
  exchangeRateId: string;
  exchangeRateVersionId: string;
  versionNumber: number;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  rate: number;
  scale: number;
  provenance: string | null;
  source: string | null;
  effectiveOn: string;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface PurchaseInvoiceMatchListItemResponse {
  id: string;
  purchaseInvoiceHandoffId: string;
  purchaseOrderId: string;
  lifecycle: PurchaseInvoiceMatchLifecycle;
  result: PurchaseInvoiceMatchResult;
  evaluatedAt: string;
  resolvedByActorId: string | null;
  varianceCount: number;
  version: string;
}

export interface PurchaseInvoiceMatchResponse extends PurchaseInvoiceMatchListItemResponse {
  tenantId: string;
  companyId: string;
  branchId: string | null;
  evaluatedByActorId: string;
  resolvedAt: string | null;
  resolutionReason: string | null;
  sourceFingerprint: string;
  purchaseOrderVersion: string;
  handoffVersion: string;
  declaredEvidenceId: string | null;
  declaredEvidenceVersion: number | null;
  policy: PurchaseInvoiceMatchPolicyResponse;
  resolutionPolicy: PurchaseInvoiceMatchResolutionPolicyResponse | null;
  appliedExchangeRate: PurchaseInvoiceMatchExchangeRateResponse | null;
  variances: PurchaseInvoiceMatchVarianceResponse[];
  sourceSnapshot: string | null;
}

export interface PurchaseInvoiceMatchHistoryResponse {
  id: string;
  matchEvaluationId: string;
  purchaseInvoiceHandoffId: string;
  result: PurchaseInvoiceMatchResult;
  action: string;
  actorId: string;
  reason: string | null;
  occurredAt: string;
  correlationId: string;
}

export interface PurchaseInvoiceMatchAuditResponse {
  id: string;
  matchEvaluationId: string;
  purchaseInvoiceHandoffId: string;
  operationId: string;
  tenantId: string;
  actorId: string;
  decision: string;
  reason: string | null;
  occurredAt: string;
  idempotencyKey: string | null;
  requestFingerprint: string | null;
}
