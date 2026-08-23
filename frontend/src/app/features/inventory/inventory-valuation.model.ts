export type InventoryValuationEventStatus = 'Pending' | 'Applied' | 'Blocked';

export interface InventoryValuationPolicy {
  id: string;
  tenantId: string;
  companyId: string;
  functionalCurrencyId: string;
  functionalCurrencyCode: string;
  scopeMode: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  versionNumber: number;
  unitCostScale: number;
  amountScale: number;
  roundingMode: string;
  goodsReceiptCostBasis: string;
  positiveAdjustmentCostBasis: string;
  supplierReturnCostBasis: string;
  isActive: boolean;
  version: string;
}

export interface InventoryValuationState {
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  productId: string;
  unitOfMeasureId: string;
  trackingIdentity: string | null;
  policyId: string;
  policyVersionNumber: number;
  functionalCurrencyCode: string;
  quantity: number;
  value: number;
  averageUnitCost: number;
  lastAppliedLedgerSequence: number;
  updatedAt: string;
  version: string;
}

export interface InventoryValuationEvent {
  id: string;
  tenantId: string;
  movementId: string;
  sourceType: string;
  sourceDocumentId: string;
  sourceLineId: string;
  correctionOfMovementId: string | null;
  goodsReceiptId: string | null;
  goodsReceiptLineId: string | null;
  supplierReturnId: string | null;
  supplierReturnLineId: string | null;
  purchaseOrderId: string | null;
  purchaseOrderLineId: string | null;
  transferId: string | null;
  transferLineId: string | null;
  sourceReference: string | null;
  ledgerSequence: number;
  status: InventoryValuationEventStatus;
  statusCode: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  productId: string;
  unitOfMeasureId: string;
  trackingIdentity: string | null;
  policyId: string;
  policyVersionNumber: number;
  functionalCurrencyCode: string;
  quantity: number;
  direction: string;
  transactionUnitCost: number | null;
  transactionCurrencyCode: string | null;
  exchangeRateId: string | null;
  exchangeRateVersionId: string | null;
  exchangeRateVersionNumber: number | null;
  exchangeRate: number | null;
  exchangeRateScale: number | null;
  exchangeRateProvenance: string | null;
  effectiveOn: string;
  baseUnitCost: number | null;
  priorQuantity: number;
  priorValue: number;
  newQuantity: number;
  newValue: number;
  movementValue: number | null;
  unitCostScale: number;
  amountScale: number;
  roundingMode: string;
  correctionOfValuationEventId: string | null;
  sourceRevisionId: string | null;
  isBackdated: boolean;
  pendingReason: string | null;
  correlationId: string;
  actorId: string;
  occurredAt: string;
  version: string;
}

export interface InventoryValuationReconciliation {
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  productId: string;
  unitOfMeasureId: string;
  trackingIdentity: string | null;
  functionalCurrencyCode: string;
  policyId: string | null;
  status: string;
  physicalOnHandQuantity: number;
  valuedQuantity: number;
  quantityDifference: number;
  valuedAmount: number;
  averageUnitCost: number;
  latestLedgerSequence: number | null;
  lastAppliedLedgerSequence: number;
  eligibleMovementCount: number;
  appliedMovementCount: number;
  pendingMovementCount: number;
  blockedMovementCount: number;
  oldestPendingLedgerSequence: number | null;
  inTransitQuantity: number;
  inTransitValue: number;
  financeHandoffStatus: string;
  asOf: string;
  freshAsOf: string;
  differenceReason: string | null;
}

export interface InventoryFinanceValuationHandoff {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  movementId: string;
  ledgerSequence: number;
  sourceType: string;
  sourceDocumentId: string;
  sourceLineId: string;
  valuationEvidenceId: string;
  valuationEvidenceVersion: number;
  quantity: number;
  baseUnitCost: number;
  baseAmount: number;
  policyId: string;
  policyVersionNumber: number;
  functionalCurrencyCode: string;
  transactionUnitCost: number | null;
  transactionCurrencyCode: string | null;
  exchangeRateId: string | null;
  exchangeRateVersionId: string | null;
  exchangeRateVersionNumber: number | null;
  exchangeRate: number | null;
  exchangeRateScale: number | null;
  exchangeRateProvenance: string | null;
  productId: string;
  unitOfMeasureId: string;
  trackingIdentity: string | null;
  correctionOfMovementId: string | null;
  status: string;
  contractVersion: string;
  correlationId: string;
  asOf: string;
  createdAt: string;
  version: string;
}

export interface InventoryValuationProcessRequest {
  companyId: string;
  branchId?: string | null;
  warehouseId?: string | null;
  productId?: string | null;
  unitOfMeasureId?: string | null;
  trackingIdentity?: string | null;
  asOfDate?: string | null;
}
