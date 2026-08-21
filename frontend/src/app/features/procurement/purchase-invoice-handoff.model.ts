export type PurchaseInvoiceHandoffStatus = 'Recorded' | 'Cancelled' | string;

export interface PurchaseInvoiceHandoffSourceRequest {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  quantity: number;
}

export interface PurchaseInvoiceHandoffCreateRequest {
  purchaseOrderId: string;
  supplierInvoiceReference: string;
  supplierInvoiceDate: string;
  notes?: string | null;
  sources: PurchaseInvoiceHandoffSourceRequest[];
  declaredEvidence?: PurchaseInvoiceDeclaredEvidenceRequest | null;
}

export interface PurchaseInvoiceDeclaredEvidenceAllocationRequest {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  quantity: number;
}

export interface PurchaseInvoiceDeclaredEvidenceLineRequest {
  purchaseOrderLineId: string;
  quantity: number;
  unitPrice: number;
  discountAmount?: number | null;
  taxRatePercentage?: number | null;
  taxCode?: string | null;
  taxAmount?: number | null;
  netAmount?: number | null;
  grossAmount?: number | null;
  description?: string | null;
  allocations: PurchaseInvoiceDeclaredEvidenceAllocationRequest[];
}

export interface PurchaseInvoiceDeclaredEvidenceRequest {
  supplierInvoiceReference?: string | null;
  supplierInvoiceDate?: string | null;
  currencyCode: string;
  subtotalAmount?: number | null;
  discountAmount?: number | null;
  taxAmount?: number | null;
  grossAmount?: number | null;
  lines: PurchaseInvoiceDeclaredEvidenceLineRequest[];
}

export interface PurchaseInvoiceDeclaredEvidenceCaptureRequest {
  evidence: PurchaseInvoiceDeclaredEvidenceRequest;
  reason?: string | null;
}

export interface PurchaseInvoiceHandoffActionRequest {
  reason?: string | null;
}

export interface PurchaseInvoiceHandoffEligibleLineResponse {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  purchaseOrderLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  receivedDate: string;
  acceptedQuantity: number;
  alreadyHandedOffQuantity: number;
  remainingHandoffQuantity: number;
  unitPrice: number;
  taxRatePercentage: number | null;
  taxAmount: number | null;
}

export interface PurchaseInvoiceHandoffEligibleSourceResponse {
  purchaseOrderId: string;
  companyId: string;
  branchId: string | null;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currencyId: string;
  currencyCode: string;
  currencyName: string;
  lines: PurchaseInvoiceHandoffEligibleLineResponse[];
}

export interface PurchaseInvoiceHandoffLineResponse {
  id: string;
  purchaseOrderLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  handoffQuantity: number;
  unitPrice: number;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  lineAmount: number;
}

export interface PurchaseInvoiceHandoffSourceResponse {
  id: string;
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  purchaseOrderLineId: string;
  quantity: number;
}

export interface PurchaseInvoiceHandoffListItemResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  purchaseOrderId: string;
  status: PurchaseInvoiceHandoffStatus;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  supplierInvoiceReference: string;
  supplierInvoiceDate: string;
  totalHandoffQuantity: number;
  totalHandoffAmount: number;
  lineCount: number;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface PurchaseInvoiceHandoffResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  purchaseOrderId: string;
  createdByActorId: string;
  status: PurchaseInvoiceHandoffStatus;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  supplierInvoiceReference: string;
  supplierInvoiceDate: string;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
  lines: PurchaseInvoiceHandoffLineResponse[];
  sources: PurchaseInvoiceHandoffSourceResponse[];
  version: string;
  canCancel: boolean;
  declaredEvidence?: PurchaseInvoiceDeclaredEvidenceResponse | null;
}

export interface PurchaseInvoiceDeclaredEvidenceAllocationResponse {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  quantity: number;
}

export interface PurchaseInvoiceDeclaredEvidenceLineResponse {
  id: string;
  purchaseOrderLineId: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number | null;
  taxRatePercentage: number | null;
  taxCode: string | null;
  taxAmount: number | null;
  netAmount: number | null;
  grossAmount: number | null;
  description: string | null;
  allocations: PurchaseInvoiceDeclaredEvidenceAllocationResponse[];
}

export interface PurchaseInvoiceDeclaredEvidenceResponse {
  id: string;
  versionNumber: number;
  supplierInvoiceReference: string | null;
  supplierInvoiceDate: string | null;
  currencyCode: string;
  subtotalAmount: number | null;
  discountAmount: number | null;
  taxAmount: number | null;
  grossAmount: number | null;
  recordedAt: string;
  recordedByActorId: string;
  lines: PurchaseInvoiceDeclaredEvidenceLineResponse[];
}

export interface PurchaseInvoiceHandoffHistoryResponse {
  evidenceId: string;
  purchaseInvoiceHandoffId: string;
  occurredAt: string;
  fromStatus: PurchaseInvoiceHandoffStatus;
  toStatus: PurchaseInvoiceHandoffStatus;
  action: string;
  actorId: string;
  reason: string | null;
  correlationId: string;
}

export interface PurchaseInvoiceHandoffAuditResponse {
  evidenceId: string;
  purchaseInvoiceHandoffId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  sessionId: string;
  authorizationPath: string;
  decision: string;
  reason: string | null;
  beforeStatus: PurchaseInvoiceHandoffStatus | null;
  afterStatus: PurchaseInvoiceHandoffStatus | null;
  companyId: string;
  branchId: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
}
