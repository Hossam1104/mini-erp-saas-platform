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
  unitCost: number;
  currencyCode: string;
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
