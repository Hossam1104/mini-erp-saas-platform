export type PurchaseOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Issued'
  | 'Confirmed'
  | 'PartiallyConfirmed'
  | 'ChangedPendingApproval'
  | 'Rejected'
  | 'NoResponse'
  | 'ReturnedForChange'
  | 'Cancelled'
  | string;

export type PurchaseOrderConfirmationStatus = 'Confirmed' | 'PartiallyConfirmed' | 'Rejected' | 'NoResponse';
export type PurchaseOrderSupplierChangeStatus = 'PendingApproval' | 'Approved' | 'Rejected';

export interface PurchaseOrderSupplierResponse { id: string; code: string; name: string; }
export interface PurchaseOrderCurrencyResponse { id: string; code: string; name: string; }
export interface PurchaseOrderPaymentTermResponse { id: string; code: string; name: string; version: number; }

export interface PurchaseOrderSourceResponse {
  purchaseRequestId: string;
  supplierQuotationId: string;
  sourceDecisionId: string;
  purchaseRequestReference: string;
  purchaseRequestPurpose: string | null;
  supplierQuotationReference: string;
  supplier: PurchaseOrderSupplierResponse;
  currency: PurchaseOrderCurrencyResponse;
  paymentTerm: PurchaseOrderPaymentTermResponse | null;
  sourceDecisionRationale: string;
  selectedAt: string;
}

export interface PurchaseOrderSourceLineResponse {
  supplierQuotationLineId: string;
  purchaseRequestLineId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  requestedQuantity: number;
  selectedQuantity: number;
  unitPrice: number;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxCode: string | null;
  taxName: string | null;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  requestedNeedByDate: string;
  offeredDeliveryDate: string | null;
}

export interface PurchaseOrderSourceOptionResponse {
  source: PurchaseOrderSourceResponse;
  companyId: string;
  branchId: string | null;
  lines: PurchaseOrderSourceLineResponse[];
  purchaseRequestVersion: string;
}

export interface PurchaseOrderCreateRequest { sourceDecisionId: string; }

export interface PurchaseOrderLineEditRequest {
  purchaseOrderLineId: string;
  orderedQuantity: number;
  unitPrice: number;
  deliveryDate: string | null;
  notes: string | null;
}

export interface PurchaseOrderEditRequest {
  notes: string | null;
  lines: PurchaseOrderLineEditRequest[];
}

export interface PurchaseOrderActionRequest { reason?: string | null; }

export interface PurchaseOrderEvidenceReferenceWriteRequest {
  referenceId: string | null;
  fileName: string | null;
  contentType: string | null;
  description: string | null;
  source: string | null;
  externalReference: string | null;
}

export interface PurchaseOrderConfirmationLineRequest {
  purchaseOrderLineId: string;
  confirmedQuantity: number;
  expectedDeliveryDate: string | null;
  proposedQuantity: number | null;
  proposedUnitPrice: number | null;
  proposedDeliveryDate: string | null;
  changeReason: string | null;
}

export interface PurchaseOrderConfirmationRequest {
  status: PurchaseOrderConfirmationStatus;
  responseDate: string;
  supplierReference: string | null;
  supplierContact: string | null;
  reason: string | null;
  notes: string | null;
  lines: PurchaseOrderConfirmationLineRequest[];
  evidence: PurchaseOrderEvidenceReferenceWriteRequest[];
}

export interface PurchaseOrderApprovalRecord {
  policyId: string;
  policyVersion: number;
  stageIndex: number;
  stageKey: string;
  requiredApprovals: number;
  recordedApprovals: number;
  allowDelegation: boolean;
  allowRequesterCancellationWhilePending: boolean;
  isReapproval: boolean;
}

export interface PurchaseOrderLineResponse {
  id: string;
  sourceQuotationLineId: string;
  purchaseRequestLineId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  orderedQuantity: number;
  confirmedQuantity: number;
  remainingQuantity: number;
  unitPrice: number;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxCode: string | null;
  taxName: string | null;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  requestedNeedByDate: string;
  deliveryDate: string | null;
  notes: string | null;
  version: string;
}

export interface PurchaseOrderEvidenceReferenceResponse {
  id: string;
  referenceId: string;
  fileName: string | null;
  contentType: string | null;
  description: string | null;
  source: string;
  externalReference: string | null;
  recordedByActorId: string;
  recordedAt: string;
  version?: string | null;
}

export interface PurchaseOrderSupplierChangeResponse {
  id: string;
  confirmationId: string;
  purchaseOrderLineId: string;
  previousOrderedQuantity: number;
  proposedQuantity: number | null;
  previousUnitPrice: number;
  proposedUnitPrice: number | null;
  previousDeliveryDate: string | null;
  proposedDeliveryDate: string | null;
  status: PurchaseOrderSupplierChangeStatus;
  reason: string;
  actorId: string;
  proposedAt: string;
  decidedAt: string | null;
  decisionReason: string | null;
  version?: string | null;
}

export interface PurchaseOrderConfirmationLineResponse {
  id: string;
  purchaseOrderLineId: string;
  orderedQuantityAtResponse: number;
  confirmedQuantity: number;
  remainingQuantity: number;
  expectedDeliveryDate: string | null;
  proposedQuantity: number | null;
  proposedUnitPrice: number | null;
  proposedDeliveryDate: string | null;
  changeReason: string | null;
  version?: string | null;
}

export interface PurchaseOrderConfirmationResponse {
  id: string;
  purchaseOrderId: string;
  status: PurchaseOrderConfirmationStatus | string;
  responseDate: string;
  supplierReference: string | null;
  supplierContact: string | null;
  reason: string | null;
  notes: string | null;
  recordedByActorId: string;
  recordedAt: string;
  purchaseOrderVersion: string;
  lines: PurchaseOrderConfirmationLineResponse[];
  evidence: PurchaseOrderEvidenceReferenceResponse[];
  changes: PurchaseOrderSupplierChangeResponse[];
  version: string;
}

export interface PurchaseOrderListItemResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  status: PurchaseOrderStatus;
  supplierCode: string;
  supplierName: string;
  supplierQuotationReference: string;
  currencyCode: string;
  total: number;
  lineCount: number;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface PurchaseOrderResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  createdByActorId: string;
  status: PurchaseOrderStatus;
  source: PurchaseOrderSourceResponse;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  approvedAt: string | null;
  issuedAt: string | null;
  cancelledAt: string | null;
  latestConfirmationId: string | null;
  latestConfirmationStatus: PurchaseOrderConfirmationStatus | string | null;
  approval: PurchaseOrderApprovalRecord | null;
  lines: PurchaseOrderLineResponse[];
  pendingChanges: PurchaseOrderSupplierChangeResponse[];
  version: string;
  canEdit: boolean;
  canSubmit: boolean;
  canApprove: boolean;
  canReject: boolean;
  canReturnForChange: boolean;
  canIssue: boolean;
  canCancel: boolean;
  canCaptureConfirmation: boolean;
  canApproveSupplierChange: boolean;
  canRejectSupplierChange: boolean;
}

export interface PurchaseOrderHistoryResponse {
  evidenceId: string;
  purchaseOrderId: string;
  occurredAt: string;
  fromStatus: PurchaseOrderStatus;
  toStatus: PurchaseOrderStatus;
  action: string;
  actorId: string;
  reason: string | null;
  correlationId: string;
  policyId: string | null;
  policyVersion: number | null;
  stageKey: string | null;
  delegatedFromActorId: string | null;
}

export interface PurchaseOrderAuditResponse {
  evidenceId: string;
  purchaseOrderId: string;
  occurredAt: string;
  operationId: string;
  correlationId: string;
  tenantId: string;
  actorId: string;
  sessionId: string;
  authorizationPath: string;
  decision: string;
  reason: string | null;
  beforeStatus: PurchaseOrderStatus | null;
  afterStatus: PurchaseOrderStatus | null;
  companyId: string;
  branchId: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
}
