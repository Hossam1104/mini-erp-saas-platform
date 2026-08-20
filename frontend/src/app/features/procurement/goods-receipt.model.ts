export type GoodsReceiptStatus = 'Recorded' | 'Cancelled' | string;

export interface GoodsReceiptWarehouseOptionResponse {
  warehouseId: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface GoodsReceiptLineCreateRequest {
  purchaseOrderLineId: string;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  damagedQuantity?: number | null;
  damageNotes?: string | null;
  notes?: string | null;
}

export interface GoodsReceiptCreateRequest {
  purchaseOrderId: string;
  warehouseId: string;
  receivedDate: string;
  referenceNote?: string | null;
  notes?: string | null;
  lines: GoodsReceiptLineCreateRequest[];
}

export interface GoodsReceiptActionRequest {
  reason?: string | null;
}

export interface GoodsReceiptEligibleLineResponse {
  purchaseOrderLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  unitPrice: number;
  confirmedQuantity: number;
  alreadyReceivedQuantity: number;
  remainingReceivableQuantity: number;
}

export interface GoodsReceiptEligibleSourceResponse {
  purchaseOrderId: string;
  companyId: string;
  branchId: string | null;
  status: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  currencyCode: string;
  lines: GoodsReceiptEligibleLineResponse[];
}

export interface GoodsReceiptLineResponse {
  id: string;
  purchaseOrderLineId: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  orderedQuantityAtReceipt: number;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  damagedQuantity: number | null;
  damageNotes: string | null;
  remainingReceivableQuantityAfter: number;
  notes: string | null;
}

export interface GoodsReceiptListItemResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  purchaseOrderId: string;
  warehouseId: string;
  status: GoodsReceiptStatus;
  supplierCode: string;
  supplierName: string;
  receivedDate: string;
  referenceNote: string | null;
  totalReceivedQuantity: number;
  totalAcceptedQuantity: number;
  totalRejectedQuantity: number;
  totalDamagedQuantity: number;
  lineCount: number;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface GoodsReceiptResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  purchaseOrderId: string;
  warehouseId: string;
  receivedByActorId: string;
  status: GoodsReceiptStatus;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  receivedDate: string;
  referenceNote: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
  lines: GoodsReceiptLineResponse[];
  version: string;
  canCancel: boolean;
}

export interface GoodsReceiptHistoryResponse {
  evidenceId: string;
  goodsReceiptId: string;
  occurredAt: string;
  fromStatus: GoodsReceiptStatus;
  toStatus: GoodsReceiptStatus;
  action: string;
  actorId: string;
  reason: string | null;
  correlationId: string;
}

export interface GoodsReceiptAuditResponse {
  evidenceId: string;
  goodsReceiptId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  sessionId: string;
  authorizationPath: string;
  decision: string;
  reason: string | null;
  beforeStatus: GoodsReceiptStatus | null;
  afterStatus: GoodsReceiptStatus | null;
  companyId: string;
  branchId: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
}
