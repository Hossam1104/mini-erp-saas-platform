export type SalesQuotationStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Sent'
  | 'Expired'
  | 'Converted'
  | 'Withdrawn'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export type SalesOrderStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'CreditHold'
  | 'Confirmed'
  | 'Rejected'
  | 'ReturnedForChange'
  | 'Cancelled';

export type SalesCreditOutcome = 'Eligible' | 'Warning' | 'Blocked' | 'Pending' | 'Unknown' | 'Overridden';

export interface SalesQuotationLineRequest {
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  unitPriceOverride?: number | null;
  discountPercent?: number;
  notes?: string | null;
  taxId?: string | null;
}

export interface SalesQuotationCreateRequest {
  companyId: string;
  branchId: string | null;
  customerId: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  priceListId: string | null;
  customerContactId: string | null;
  notes: string | null;
  customerReference: string | null;
  lines: SalesQuotationLineRequest[];
  exchangeRateId?: string | null;
}

export interface SalesQuotationEditRequest extends Omit<SalesQuotationCreateRequest, 'customerId' | 'quotationDate'> {}

export interface SalesOrderEditRequest {
  currencyId: string;
  priceListId: string | null;
  lines: SalesQuotationLineRequest[];
  exchangeRateId?: string | null;
}

export interface SalesActionRequest { reason?: string | null; }

export interface SalesCreditOverrideRequest {
  reason: string;
  expiresAt: string;
  scope?: string | null;
  sourceReference?: string | null;
}

export interface SalesQuotationSummaryResponse {
  id: string;
  number: string;
  companyId: string;
  branchId: string | null;
  customerId: string;
  customerCode: string;
  customerName: string;
  createdByActorId: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  currencyCode: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  total: number;
  status: SalesQuotationStatus;
  revisionNumber: number;
  version: string;
  updatedAt: string;
}

export interface SalesQuotationLineResponse {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  quantity: number;
  unitPrice: number;
  resolvedUnitPrice: number;
  discountPercent: number;
  discountAmount: number;
  taxAmount: number;
  lineTotal: number;
  priceListId: string | null;
  priceVersionNumber: number | null;
  priceEffectiveFrom: string | null;
  priceProvenance: string;
  priceSourceReference: string | null;
  manualPriceApplied: boolean;
  commercialAuthorityPolicyId: string | null;
  commercialAuthorityActorId: string | null;
  commercialAuthorityEvidence: string | null;
  notes: string | null;
  taxId?: string | null;
  taxCode?: string | null;
  taxRateVersionId?: string | null;
  taxRateVersionNumber?: number | null;
  taxEffectiveFrom?: string | null;
  taxEffectiveTo?: string | null;
  taxRatePercentage?: number | null;
  taxableBase?: number | null;
  taxReferenceValue?: string | null;
}

export interface SalesExchangeRateEvidence {
  exchangeRateId: string;
  exchangeRateVersionId: string;
  versionNumber: number;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  rate: number;
  rateScale: number;
  provenance: string;
  sourceNotes: string | null;
  effectiveOn: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  referenceValue: string;
}

export interface SalesQuotationResponse extends SalesQuotationSummaryResponse {
  tenantId: string;
  customerContactId: string | null;
  notes: string | null;
  customerReference: string | null;
  lines: SalesQuotationLineResponse[];
  createdAt: string;
  exchangeRateEvidence?: SalesExchangeRateEvidence | null;
  approvalState?: SalesApprovalStateResponse | null;
}

export interface SalesApprovalDecisionResponse {
  stageKey: string;
  actorId: string;
  delegatedFromActorId: string | null;
  decidedAt: string;
  policyId: string;
  policyVersion: number;
  revisionNumber: number;
  documentVersion: string;
}

export interface SalesApprovalStateResponse {
  policyId: string;
  policyVersion: number;
  currentStageIndex: number;
  currentStageKey: string | null;
  currentStageRequiredApprovals: number;
  currentStageApprovalCount: number;
  currentStageApproverIds: string[];
  decisions: SalesApprovalDecisionResponse[];
}

export interface SalesQuotationRevisionResponse {
  id: string;
  quotationId: string;
  revisionNumber: number;
  status: SalesQuotationStatus;
  snapshotHash: string;
  actorId: string;
  occurredAt: string;
  reason: string | null;
  snapshot: SalesQuotationResponse;
}

export interface SalesOrderSummaryResponse {
  id: string;
  number: string;
  companyId: string;
  branchId: string | null;
  customerId: string;
  customerCode: string;
  customerName: string;
  createdByActorId: string;
  sourceQuotationId: string;
  sourceQuotationNumber: string;
  sourceQuotationRevision: number;
  currencyId: string;
  currencyCode: string;
  total: number;
  status: SalesOrderStatus;
  creditOutcome: SalesCreditOutcome;
  version: string;
  updatedAt: string;
  revisionNumber?: number;
}

export interface SalesOrderResponse extends SalesOrderSummaryResponse {
  tenantId: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  creditReason: string | null;
  creditEvaluatedAt: string | null;
  creditOverrideExpiresAt: string | null;
  lines: SalesQuotationLineResponse[];
  createdAt: string;
  exchangeRateEvidence?: SalesExchangeRateEvidence | null;
  revisionNumber?: number;
  approvalState?: SalesApprovalStateResponse | null;
}

export interface SalesHistoryResponse {
  id: string;
  documentType: string;
  documentId: string;
  action: string;
  fromStatus: string | null;
  toStatus: string | null;
  actorId: string;
  occurredAt: string;
  reason: string | null;
  policyId: string | null;
  policyVersion: number | null;
  creditOutcome: string | null;
  snapshotHash: string | null;
  snapshotJson?: string | null;
}

export interface SalesAuditResponse {
  id: string;
  operationId: string;
  documentType: string;
  documentId: string;
  actorId: string;
  occurredAt: string;
  decision: string;
  reason: string | null;
  beforeSummary: string | null;
  afterSummary: string | null;
  idempotencyKey: string | null;
  correlationId: string;
}

export interface SalesCreditResponse {
  documentId: string;
  customerId: string;
  companyId: string;
  /** Finance-authoritative currency used for exposure, commitment, and limit evaluation. */
  currencyCode: string | null;
  transactionCurrencyCode?: string | null;
  transactionAmount?: number | null;
  convertedOrderCommitment?: number | null;
  exchangeRateEvidence?: SalesExchangeRateEvidence | null;
  orderRevisionNumber?: number | null;
  openReceivables: number | null;
  overdueReceivables: number | null;
  netReceivableExposure: number | null;
  proposedExposure: number | null;
  creditLimit: number | null;
  outcome: SalesCreditOutcome;
  reason: string | null;
  asOfDate: string;
  evaluatedAt: string;
  overrideExpiresAt: string | null;
}

export type SalesFulfillmentLineStatus = 'AwaitingReservation' | 'PartiallyReserved' | 'Reserved' | 'PartiallyDelivered' | 'Delivered' | 'Backordered';
export type SalesDeliveryStatus = 'Draft' | 'Posted' | 'Failed' | 'Unknown';
export type SalesInvoiceEligibilityStatus = 'Eligible' | 'PartiallyEligible' | 'Blocked' | 'Unknown';
export type SalesInvoiceRequestStatus = 'Pending' | 'Posted' | 'Failed' | 'Unknown';

export interface SalesReservationRequest { warehouseId: string; lines: Array<{ orderLineId: string; quantity: number; trackingIdentity?: string | null }>; }
export interface SalesFulfillmentLineResponse { orderLineId: string; orderedQuantity: number; reservedQuantity: number; unallocatedQuantity: number; fulfilledQuantity: number; deliveredQuantity: number; invoicedQuantity: number; remainingFulfillableQuantity: number; remainingInvoiceableQuantity: number; status: SalesFulfillmentLineStatus; }
export interface SalesDeliveryResponse { id: string; tenantId: string; orderId: string; orderRevisionNumber: number; companyId: string; branchId: string | null; customerId: string; warehouseId: string; status: SalesDeliveryStatus; errorCode: string | null; lines: Array<{ orderLineId: string; reservationId: string; quantity: number }>; movementIds: string[]; createdAt: string; postedAt: string | null; version: string; }
export interface SalesFulfillmentResponse { orderId: string; orderRevisionNumber: number; lines: SalesFulfillmentLineResponse[]; deliveries: SalesDeliveryResponse[]; invoiceRequests: SalesInvoiceRequestResponse[]; }
export interface SalesInvoiceEligibilityRequest { deliveryId?: string | null; paymentTermId: string; invoiceDate: string; lines: Array<{ orderLineId: string; quantity: number }>; }
export interface SalesInvoiceEligibilityResponse { orderId: string; orderRevisionNumber: number; status: SalesInvoiceEligibilityStatus; code: string; totalAmount: number; currencyCode: string; lines: Array<{ orderLineId: string; deliveredQuantity: number; invoicedQuantity: number; requestedQuantity: number; remainingInvoiceableQuantity: number; amount: number; status: string }>; invoiceDate: string; }
export interface SalesInvoiceRequestResponse { id: string; tenantId: string; orderId: string; orderRevisionNumber: number; deliveryId: string | null; status: SalesInvoiceRequestStatus; errorCode: string | null; financeOpenItemId: string | null; amount: number; lines: Array<{ orderLineId: string; quantity: number }>; createdAt: string; postedAt: string | null; version: string; }
