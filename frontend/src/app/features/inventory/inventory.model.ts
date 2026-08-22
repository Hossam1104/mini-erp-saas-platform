export interface InventoryWarehouseOption {
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  code: string;
  name: string;
  displayName: string;
  isActive: boolean;
}

export interface InventoryMovement {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  direction: string;
  quantity: number;
  unitCost: number | null;
  currencyCode: string | null;
  trackingIdentity: string | null;
  sourceType: string;
  sourceDocumentId: string;
  sourceLineId: string;
  correctionOfMovementId: string | null;
  effectiveDate: string;
  actorId: string;
  correlationId: string;
  postedAt: string;
  version: string;
  valuationStatus: string;
  goodsReceiptId: string | null;
  goodsReceiptLineId: string | null;
  supplierReturnId: string | null;
  supplierReturnLineId: string | null;
  transferId: string | null;
  transferLineId: string | null;
  sourceReference: string | null;
}

export interface InventoryAvailability {
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  trackingIdentity: string | null;
  trackingEnabled: boolean;
  onHandQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  expectedQuantity: number;
  damagedQuantity: number;
  inTransitQuantity: number;
  calculatedAt: string;
  version: string;
}

export interface InventoryOpeningRow {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  quantity: number;
  unitCost: number;
  currencyCode: string;
  trackingIdentity: string | null;
  sourceLineReference: string | null;
  status: string;
  validationCode: string | null;
  postedAt: string | null;
  version: string;
  sourceFingerprint: string;
}

export interface InventoryOpeningBalance {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  asOfDate: string;
  sourceOwner: string;
  sourceSystem: string;
  extractedAt: string;
  sourceReference: string | null;
  status: string;
  rowCount: number;
  validRowCount: number;
  quarantinedRowCount: number;
  validQuantityTotal: number;
  createdAt: string;
  updatedAt: string;
  rows: InventoryOpeningRow[];
  version: string;
}

export interface InventoryReservation {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  trackingIdentity: string | null;
  sourceType: string;
  sourceReference: string;
  requestedQuantity: number;
  reservedQuantity: number;
  unallocatedQuantity: number;
  status: string;
  actorId: string;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface InventoryOpeningCreateRequest {
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  asOfDate: string;
  sourceOwner: string;
  sourceSystem: string;
  extractedAt: string;
  sourceReference: string | null;
  rows: Array<{ productId: string; unitOfMeasureId: string; quantity: number; unitCost: number; currencyCode: string; trackingIdentity: string | null; sourceLineReference: string | null }>;
}

export interface InventoryReservationCreateRequest {
  companyId: string;
  branchId: string | null;
  warehouseId: string;
  productId: string;
  unitOfMeasureId: string;
  requestedQuantity: number;
  sourceType: string;
  sourceReference: string;
  allowPartialAllocation: boolean;
  trackingIdentity: string | null;
}

export type InventoryTransferMode = 'Direct' | 'InTransit';

export interface InventoryTransfer {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  sourceWarehouseId: string;
  sourceWarehouseCode: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseCode: string;
  destinationWarehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  quantity: number;
  mode: InventoryTransferMode;
  status: string;
  trackingIdentity: string | null;
  shippedQuantity: number;
  receivedQuantity: number;
  lostQuantity: number;
  inTransitQuantity: number;
  remainingToShipQuantity: number;
  reason: string | null;
  actorId: string;
  createdAt: string;
  updatedAt: string;
  events: InventoryTransferEvent[];
  version: string;
}

export interface InventoryTransferEvent {
  id: string;
  transferId: string;
  transferLineId: string;
  eventType: string;
  quantity: number;
  reference: string | null;
  reason: string | null;
  actorId: string;
  correlationId: string;
  occurredAt: string;
  sourceMovementId: string | null;
  destinationMovementId: string | null;
  version: string;
}

export interface InventoryTransferCreateRequest {
  companyId: string;
  branchId: string | null;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  mode: InventoryTransferMode;
  trackingIdentity: string | null;
  reason: string | null;
}

export interface InventoryTransferActionRequest {
  quantity?: number;
  reference?: string;
  reason?: string;
}

export interface InventoryCustomerReturnBoundary {
  available: boolean;
  status: string;
  messageKey: string;
  authoritativeSource: string | null;
}

export type InventoryReasonCategory = 'Adjustment' | 'CountVariance' | 'StockIssue';
export type InventoryAdjustmentDirection = 'Increase' | 'Decrease';
export type InventoryControlStatus = 'Draft' | 'Submitted' | 'PendingApproval' | 'Approved' | 'Rejected' | 'ReturnedForChange' | 'Posted' | 'Corrected' | 'RecountRequired' | 'ResnapshotRequired' | 'Blocked';

export interface InventoryReasonCode {
  id: string; tenantId: string; code: string; englishName: string; arabicName: string; category: InventoryReasonCategory;
  isActive: boolean; createdByActorId: string; createdAt: string; updatedAt: string; version: string;
}

export interface InventoryReasonCodeCreate { code: string; englishName: string; arabicName: string; category: InventoryReasonCategory; }
export interface InventoryReasonCodeUpdate { englishName: string; arabicName: string; category: InventoryReasonCategory; isActive: boolean; }

export interface InventoryAdjustmentLine {
  id: string; productId: string; productSku: string; productName: string; unitOfMeasureId: string; unitOfMeasureCode: string;
  direction: InventoryAdjustmentDirection; quantity: number; trackingIdentity: string; reasonCodeId: string; reasonCode: string;
  reasonEnglishName: string; reasonArabicName: string; evidenceReference: string | null; movementId: string | null; version: string;
}

export interface InventoryApproval {
  policyId: string; policyVersion: number; stageIndex: number; stageKey: string; requiredApprovals: number; recordedApprovals: number;
  allowDelegation: boolean; lastApproverId: string | null; delegatedFromActorId: string | null;
}

export interface InventoryAdjustment {
  id: string; tenantId: string; companyId: string; branchId: string | null; warehouseId: string; warehouseCode: string; warehouseName: string;
  requesterId: string; status: InventoryControlStatus; evidenceReference: string | null; lines: InventoryAdjustmentLine[]; approval: InventoryApproval | null;
  createdAt: string; updatedAt: string; submittedAt: string | null; approvedAt: string | null; postedAt: string | null; version: string;
}

export interface InventoryCountLine {
  id: string; priorLineId: string | null; roundGeneration: number; productId: string; productSku: string; productName: string;
  unitOfMeasureId: string; unitOfMeasureCode: string; trackingIdentity: string; expectedQuantity: number | null; countedQuantity: number | null;
  variance: number | null; varianceReasonCodeId: string | null; varianceReasonCode: string | null; varianceReasonEnglishName: string | null;
  varianceReasonArabicName: string | null; isCurrentRound: boolean; countedAt: string | null; version: string;
}

export interface InventoryCount {
  id: string; tenantId: string; companyId: string; branchId: string | null; warehouseId: string; warehouseCode: string; warehouseName: string;
  countType: 'Full' | 'Cycle'; assignedCounterId: string; reviewerId: string | null; approverId: string | null; posterId: string | null;
  status: InventoryControlStatus; currentRoundGeneration: number; snapshotCutoff: string; lines: InventoryCountLine[]; createdAt: string;
  updatedAt: string; submittedAt: string | null; approvedAt: string | null; postedAt: string | null; version: string;
}

export interface InventoryStockIssueLine {
  id: string; productId: string; productSku: string; productName: string; unitOfMeasureId: string; unitOfMeasureCode: string; quantity: number;
  trackingIdentity: string; reasonCodeId: string; reasonCode: string; reasonEnglishName: string; reasonArabicName: string;
  evidenceReference: string | null; movementId: string | null; version: string;
}

export interface InventoryStockIssue {
  id: string; tenantId: string; companyId: string; branchId: string | null; warehouseId: string; warehouseCode: string; warehouseName: string;
  requesterId: string; destinationUseDescription: string; status: InventoryControlStatus; lines: InventoryStockIssueLine[];
  approval: InventoryApproval | null; createdAt: string; updatedAt: string; submittedAt: string | null; approvedAt: string | null; postedAt: string | null; version: string;
}

export interface InventoryControlAction { reason?: string; }
export interface InventoryAdjustmentCreate { companyId: string; branchId: string | null; warehouseId: string; evidenceReference: string | null; lines: Array<{ productId: string; unitOfMeasureId: string; direction: InventoryAdjustmentDirection; quantity: number; reasonCode: string; trackingIdentity: string | null; evidenceReference: string | null }>; }
export interface InventoryCountCreate { companyId: string; branchId: string | null; warehouseId: string; countType: 'Full' | 'Cycle'; assignedCounterId: string; reviewerId: string | null; lines: Array<{ productId: string; unitOfMeasureId: string; trackingIdentity: string | null }>; }
export interface InventoryCountSubmit { observations: Array<{ countLineId: string; countedQuantity: number; varianceReasonCode?: string }>; }
export interface InventoryStockIssueCreate { companyId: string; branchId: string | null; warehouseId: string; destinationUseDescription: string; lines: Array<{ productId: string; unitOfMeasureId: string; quantity: number; reasonCode: string; trackingIdentity: string | null; evidenceReference: string | null }>; }
