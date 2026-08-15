export type PurchaseRequestStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export type PurchaseRequestHistoryAction =
  | 'Created'
  | 'Edited'
  | 'Submitted'
  | 'ApprovalRecorded'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export interface PurchaseRequestLineWriteRequest {
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  needByDate: string;
  purpose: string | null;
}

export interface PurchaseRequestWriteRequest {
  companyId: string;
  branchId: string | null;
  purpose: string | null;
  lines: PurchaseRequestLineWriteRequest[];
}

export interface PurchaseRequestActionRequest {
  reason?: string | null;
}

export interface PurchaseRequestLineResponse {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  quantity: number;
  needByDate: string;
  purpose: string;
  version: string;
}

export interface PurchaseRequestApprovalResponse {
  policyId: string;
  policyVersion: number;
  stageIndex: number;
  stageKey: string;
  requiredApprovals: number;
  recordedApprovals: number;
  allowDelegation: boolean;
  allowRequesterCancellationWhilePending: boolean;
}

export interface PurchaseRequestResponse {
  id: string;
  tenantId: string;
  companyId: string;
  branchId: string | null;
  requesterId: string;
  status: PurchaseRequestStatus;
  purpose: string | null;
  lines: PurchaseRequestLineResponse[];
  approval: PurchaseRequestApprovalResponse | null;
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  approvedAt: string | null;
  cancelledAt: string | null;
  version: string;
  canEdit: boolean;
  canSubmit: boolean;
  canApprove: boolean;
  canReject: boolean;
  canReturnForChange: boolean;
  canCancel: boolean;
}

export interface PurchaseRequestListItemResponse {
  id: string;
  companyId: string;
  branchId: string | null;
  requesterId: string;
  status: PurchaseRequestStatus;
  purpose: string | null;
  lineCount: number;
  createdAt: string;
  updatedAt: string;
  version: string;
}

export interface PurchaseRequestHistoryResponse {
  evidenceId: string;
  purchaseRequestId: string;
  occurredAt: string;
  fromStatus: PurchaseRequestStatus;
  toStatus: PurchaseRequestStatus;
  action: PurchaseRequestHistoryAction;
  actorId: string;
  reason: string | null;
  correlationId: string;
  policyId: string | null;
  policyVersion: number | null;
  stageKey: string | null;
  delegatedFromActorId: string | null;
}

export interface PurchaseRequestAuditResponse {
  evidenceId: string;
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
