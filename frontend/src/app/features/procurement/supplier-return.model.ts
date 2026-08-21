export type SupplierReturnStatus = string;

export interface SupplierReturnEvidenceReferenceWriteRequest {
  referenceId: string;
  fileName?: string | null;
  contentType?: string | null;
  description?: string | null;
  source?: string | null;
}

export interface SupplierReturnLineCreateRequest {
  goodsReceiptLineId: string;
  returnQuantity: number;
  notes?: string | null;
}

export interface SupplierReturnCreateRequest {
  goodsReceiptId: string;
  returnDate: string;
  reasonCode: string;
  condition: string;
  commercialOutcome: string;
  reasonDetail?: string | null;
  notes?: string | null;
  lines: SupplierReturnLineCreateRequest[];
  evidence?: SupplierReturnEvidenceReferenceWriteRequest[];
}

export interface SupplierReturnActionRequest { reason?: string | null; }
export interface SupplierReturnInventoryHandoffRequest { handoffReference: string; notes?: string | null; }
export interface SupplierReturnFinanceReferenceRequest { financeReference: string; currencyCode?: string | null; amount?: number | null; notes?: string | null; }

export interface SupplierReturnEligibleLineResponse {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  purchaseOrderId: string;
  purchaseOrderLineId: string;
  warehouseId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  acceptedQuantity: number;
  alreadyReturnedQuantity: number;
  eligibleReturnQuantity: number;
  receivedDate: string;
}

export interface SupplierReturnEligibleSourceResponse {
  goodsReceiptId: string;
  purchaseOrderId: string;
  supplierConfirmationId: string | null;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  lines: SupplierReturnEligibleLineResponse[];
}

export interface SupplierReturnEvidenceReferenceResponse {
  id: string;
  referenceId: string;
  fileName: string | null;
  contentType: string | null;
  description: string | null;
  source: string;
  recordedAt: string;
}

export interface SupplierReturnLineResponse {
  id: string;
  goodsReceiptLineId: string;
  purchaseOrderLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  acceptedQuantityAtReturn: number;
  returnQuantity: number;
  eligibleQuantityAfter: number | null;
  notes: string | null;
}

export interface SupplierReturnListItemResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  goodsReceiptId: string;
  purchaseOrderId: string;
  warehouseId: string;
  supplierCode: string;
  supplierName: string;
  status: SupplierReturnStatus;
  reasonCode: string;
  commercialOutcome: string;
  totalReturnQuantity: number;
  returnDate: string;
  createdAt: string;
  version: string;
}

export interface SupplierReturnResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  goodsReceiptId: string;
  purchaseOrderId: string;
  supplierConfirmationId: string | null;
  warehouseId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  status: SupplierReturnStatus;
  reasonCode: string;
  condition: string;
  commercialOutcome: string;
  reasonDetail: string | null;
  notes: string | null;
  returnDate: string;
  createdAt: string;
  updatedAt: string;
  cancelledAt: string | null;
  reversedAt: string | null;
  correctionOfId: string | null;
  inventoryHandoffId: string | null;
  inventoryHandoffReference: string | null;
  financeReference: string | null;
  financeCurrencyCode: string | null;
  financeAmount: number | null;
  lines: SupplierReturnLineResponse[];
  evidence: SupplierReturnEvidenceReferenceResponse[];
  version: string;
  canSubmit: boolean;
  canApprove: boolean;
  canCancel: boolean;
  canReverse: boolean;
  canCorrect: boolean;
}

export interface SupplierReturnHistoryResponse {
  evidenceId: string;
  supplierReturnId: string;
  occurredAt: string;
  fromStatus: string;
  toStatus: string;
  action: string;
  actorId: string;
  reason: string | null;
  correlationId: string;
}

export interface SupplierReturnAuditResponse {
  evidenceId: string;
  supplierReturnId: string;
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

export interface SupplierReturnReportResponse {
  returnCount: number;
  totalReturnQuantity: number;
  openReturnCount: number;
  openReturnQuantity: number;
  pendingInventoryCount: number;
  pendingFinanceCount: number;
  returns: SupplierReturnListItemResponse[];
}
