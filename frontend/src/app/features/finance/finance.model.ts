export interface FinanceCompany {
  tenantId: string;
  companyId: string;
  companyName: string;
  functionalCurrencyCode: string;
  branchId: string | null;
  isActive: boolean;
}

export interface FinanceAccount {
  id: string;
  companyId: string;
  code: string;
  englishName: string;
  arabicName: string | null;
  parentAccountId: string | null;
  accountType: string;
  isPostingAccount: boolean;
  lifecycle: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  version: string;
}

export interface FinanceFiscalCalendar {
  id: string;
  companyId: string;
  name: string;
  functionalCurrencyCode: string;
  lifecycle: string;
  version: string;
}

export interface FinanceFiscalYear {
  id: string;
  calendarId: string;
  yearNumber: number;
  startDate: string;
  endDate: string;
  state: string;
}

export interface FinanceFiscalPeriod {
  id: string;
  fiscalYearId: string;
  sequence: number;
  code: string;
  englishName: string | null;
  arabicName: string | null;
  startDate: string;
  endDate: string;
  state: string;
  version: string;
}

export interface FinancePostingRule {
  id: string;
  sourceContract: string;
  sourceEvent: string;
  versionNumber: number;
  debitAccountId: string;
  debitAccountCode: string;
  creditAccountId: string;
  creditAccountCode: string;
  costCenterRequired: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
  lifecycle: string;
}

export interface FinanceJournalLine {
  id: string;
  lineNumber: number;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  functionalDebit: number;
  functionalCredit: number;
  transactionAmount: number | null;
  transactionCurrencyCode: string | null;
  costCenterId: string | null;
  costCenterCode: string | null;
  description: string | null;
}

export interface FinanceJournal {
  id: string;
  companyId: string;
  journalNumber: string;
  postingDate: string;
  functionalCurrencyCode: string;
  sourceContract: string;
  sourceEvent: string;
  amountAuthority: string;
  approvalRequirement: string;
  description: string;
  status: string;
  lines: FinanceJournalLine[];
  version: string;
}

export interface FinanceGlLine {
  journalId: string;
  journalNumber: string;
  postingDate: string;
  functionalCurrencyCode: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  functionalDebit: number;
  functionalCredit: number;
  sourceContract: string;
  isReversal: boolean;
}

export interface FinanceHandoff {
  id: string;
  companyId: string;
  movementId: string;
  ledgerSequence: number;
  sourceType: string;
  signedBaseAmount: number;
  functionalCurrencyCode: string;
  status: string;
  contractVersion: string;
  valuationEvidenceId: string;
  valuationEvidenceVersion: number;
}

export interface FinanceAccountWriteRequest {
  companyId: string;
  code: string;
  englishName: string;
  arabicName: string | null;
  parentAccountId: string | null;
  accountType: string;
  isPostingAccount: boolean;
  currencyBehavior: string;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface FinanceJournalWriteRequest {
  companyId: string;
  journalDate: string;
  postingDate: string;
  transactionCurrencyCode: string | null;
  exchangeRate: number | null;
  description: string;
  lines: Array<{
    accountId: string;
    debit: number;
    credit: number;
    costCenterId: string | null;
    description: string | null;
  }>;
}

export interface FinancePostingRuleWriteRequest {
  companyId: string;
  sourceContract: string;
  sourceEvent: string;
  debitAccountId: string;
  creditAccountId: string;
  costCenterRequired: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface FinancePaymentMethod {
  id: string;
  companyId: string;
  code: string;
  englishName: string;
  arabicName: string | null;
  direction: string;
  lifecycle: string;
  isManual: boolean;
  requiresReference: boolean;
  version: string;
}

export interface FinanceCashAccount {
  id: string;
  companyId: string;
  code: string;
  englishName: string;
  arabicName: string | null;
  kind: string;
  currencyCode: string;
  linkedAccountId: string;
  linkedAccountCode: string;
  lifecycle: string;
  version: string;
}

export interface FinanceOpenItem {
  id: string;
  companyId: string;
  kind: string;
  supplierId: string | null;
  customerId: string | null;
  sourceContract: string;
  sourceEvidenceId: string;
  reference: string | null;
  documentDate: string;
  dueDate: string;
  currencyCode: string;
  originalAmount: number;
  allocatedAmount: number;
  outstandingAmount: number;
  status: string;
  recognitionState: string;
  recognitionJournalId: string | null;
  paymentTerm: { code: string; versionNumber: number; dueDate: string } | null;
  version: string;
}

export interface FinanceSettlementDocument {
  id: string;
  companyId: string;
  status: string;
  direction: string;
  supplierId: string | null;
  customerId: string | null;
  cashAccountId: string;
  paymentMethodId: string;
  documentDate: string;
  currencyCode: string;
  amount: number;
  functionalCurrencyCode: string;
  functionalAmount: number;
  externalReference: string | null;
  postedJournalId: string | null;
  unallocatedAmount: number;
  allocatedAmount: number;
  version: string;
  approvalRequirement?: string;
}

export interface FinanceMonetaryPolicy {
  id: string;
  companyId: string;
  functionalCurrencyCode: string;
  reportingCurrencyId: string | null;
  reportingCurrencyCode: string | null;
  roundingScale: number;
  roundingMode: string;
  revaluationEnabled: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
  versionNumber: number;
  version: string;
}

export interface FinanceExchangeRateEvidence {
  exchangeRateId: string;
  exchangeRateVersionId: string;
  versionNumber: number;
  sourceCurrencyCode: string;
  targetCurrencyCode: string;
  effectiveOn: string;
  rate: number;
  rateScale: number;
  provenance: string;
  sourceNotes: string | null;
  referenceValue: string;
}

export interface FinanceMonetaryEvidence {
  transactionCurrencyCode: string;
  transactionAmount: number;
  functionalCurrencyCode: string;
  functionalAmount: number;
  transactionToFunctionalRate: FinanceExchangeRateEvidence | null;
  reportingCurrencyCode: string | null;
  reportingAmount: number | null;
  functionalToReportingRate: FinanceExchangeRateEvidence | null;
  sourceUnroundedFunctionalAmount: number;
  sourceUnroundedReportingAmount: number | null;
  roundingScale: number;
  roundingMode: string;
  functionalRoundingDifference: number;
  reportingRoundingDifference: number | null;
  reportingEvidenceStatus: string;
}

export interface FinanceTaxEffect {
  id: string;
  companyId: string;
  openItemId: string;
  kind: string;
  taxId: string;
  taxCode: string;
  taxRateVersionId: string;
  taxRateVersionNumber: number;
  taxEffectiveOn: string;
  taxRatePercentage: number;
  taxableBase: number;
  taxAmount: number;
  transactionCurrencyCode: string;
  functionalAmount: number;
  functionalCurrencyCode: string;
  journalId: string;
  reversalJournalId: string | null;
  postingRuleId: string;
  postingRuleVersionNumber: number;
  monetaryEvidence: FinanceMonetaryEvidence;
  status: string;
  createdAt: string;
  version: string;
}

export interface FinanceRevaluationLine {
  id: string;
  batchId: string;
  companyId: string;
  sourceId: string;
  sourceType: string;
  asOfDate: string;
  transactionCurrencyCode: string;
  outstandingTransactionAmount: number;
  historicalFunctionalAmount: number;
  revaluedFunctionalAmount: number;
  difference: number;
  direction: string;
  exchangeRateEvidence: FinanceExchangeRateEvidence;
  journalId: string | null;
  reversalJournalId: string | null;
  status: string;
  version: string;
  monetaryEvidence: FinanceMonetaryEvidence | null;
  sourceSnapshotFingerprint: string | null;
  postingRuleId: string | null;
  postingRuleVersionNumber: number | null;
  reconciliationStatus: string;
}

export interface FinanceRevaluationBatch {
  id: string;
  companyId: string;
  asOfDate: string;
  scope: string;
  status: string;
  lines: FinanceRevaluationLine[];
  version: string;
}

export interface FinanceApSourceReady {
  sourceEvidenceId: string;
  companyId: string;
  supplierId: string;
  supplierCode: string | null;
  supplierName: string | null;
  supplierInvoiceReference: string | null;
  invoiceDate: string;
  currencyCode: string;
  amount: number;
  dueDate: string;
  paymentTerm: { code: string; englishName: string | null; arabicName: string | null; versionNumber: number; dueDate: string };
  matchResult: string;
  alreadyRecognized: boolean;
  sourceEvidenceVersion: number;
}

export interface FinanceManualReceivableRequest {
  companyId: string;
  customerId: string;
  documentDate: string;
  dueDate: string | null;
  paymentTermId: string;
  currencyCode: string;
  amount: number;
  exchangeRate: number | null;
  exchangeRateId: string | null;
  exchangeRateVersionId: string | null;
  exchangeRateVersionNumber: number | null;
  reference: string | null;
  description: string | null;
}

export interface FinanceSettlementWriteRequest {
  companyId: string;
  partyId: string;
  cashAccountId: string;
  paymentMethodId: string;
  documentDate: string;
  currencyCode: string;
  amount: number;
  exchangeRate: number | null;
  exchangeRateId: string | null;
  exchangeRateVersionId: string | null;
  exchangeRateVersionNumber: number | null;
  externalReference: string | null;
  description: string | null;
}

export interface FinanceAllocation {
  id: string;
  companyId: string;
  settlementDocumentId: string;
  openItemId: string;
  amount: number;
  currencyCode: string;
  functionalAmount: number;
  allocationDate: string;
  status: string;
  reversalOfAllocationId: string | null;
  journalId: string | null;
  version: string;
  historicalFunctionalAmount?: number;
  settlementFunctionalAmount?: number;
  realizedFxAmount?: number;
  realizedFxDirection?: string | null;
  realizedFxJournalId?: string | null;
  realizedFxRuleId?: string | null;
  realizedFxRuleVersionNumber?: number | null;
}

export interface FinanceFxReconciliation {
  allocationId: string;
  companyId: string;
  realizedDifference: number;
  postedDifference: number;
  direction: string;
  status: string;
  journalId: string | null;
  openItemId: string | null;
  settlementDocumentId: string | null;
  reversalJournalId: string | null;
  expectedAccountId: string | null;
  ruleId: string | null;
  ruleVersionNumber: number | null;
  statusReason: string | null;
}

export interface FinanceUnrealizedFxReconciliation {
  lineId: string;
  batchId: string;
  companyId: string;
  sourceId: string;
  sourceType: string;
  expectedAmount: number;
  postedAmount: number;
  direction: string;
  status: string;
  journalId: string | null;
  reversalJournalId: string | null;
  expectedAccountId: string | null;
  postingRuleId: string | null;
  postingRuleVersionNumber: number | null;
  statusReason: string | null;
}

export interface FinanceReportingCurrencyReconciliation {
  journalId: string;
  companyId: string;
  functionalCurrencyCode: string;
  functionalAmount: number;
  reportingCurrencyCode: string | null;
  reportingAmount: number | null;
  expectedReportingAmount: number | null;
  exchangeRateId: string | null;
  exchangeRateVersionId: string | null;
  exchangeRateVersionNumber: number | null;
  status: string;
  effectId: string | null;
  statusReason: string | null;
}

export interface FinanceAgingRow {
  openItemId: string;
  kind: string;
  supplierId: string | null;
  customerId: string | null;
  reference: string | null;
  dueDate: string;
  daysOverdue: number;
  currencyCode: string;
  outstandingAmount: number;
  status: string;
}

export interface FinanceExposure {
  companyId: string;
  customerId: string;
  currencyCode: string;
  openReceivables: number;
  overdueReceivables: number;
  unappliedCredits: number;
  netReceivableExposure: number;
  asOfDate: string;
  creditHold: boolean;
  holdReason: string | null;
}

export interface FinanceReconciliation {
  companyId: string;
  kind: string | null;
  scope: string;
  subledgerAmount: number;
  postedJournalAmount: number;
  difference: number;
  status: string;
  asOfDate: string;
}

export interface FinanceCloseCheck {
  code: string;
  status: string;
  message: string;
  expectedAmount: number | null;
  actualAmount: number | null;
}

export interface FinanceCloseReadiness {
  periodId: string;
  fiscalYearId: string;
  companyId: string;
  status: string;
  checks: FinanceCloseCheck[];
  snapshotFingerprint: string;
  evaluatedAt: string;
  periodVersion: string;
}

export interface FinancePeriodCloseRun {
  id: string;
  companyId: string;
  fiscalYearId: string;
  periodId: string;
  sequence: number;
  status: string;
  readinessStatus: string;
  snapshotFingerprint: string;
  checks: FinanceCloseCheck[];
  reason: string;
  createdAt: string;
  reopenedAt: string | null;
  version: string;
}

export interface FinancePeriodHistory {
  id: string;
  companyId: string;
  fiscalYearId: string;
  periodId: string;
  action: string;
  fromState: string;
  toState: string;
  reason: string;
  occurredAt: string;
}

export interface FinanceYearEndLine {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  debit: number;
  credit: number;
  netBalance: number;
  closingJournalLineId: string | null;
}

export interface FinanceYearEndRun {
  id: string;
  companyId: string;
  fiscalYearId: string;
  asOfDate: string;
  status: string;
  snapshotFingerprint: string;
  lines: FinanceYearEndLine[];
  closingJournalId: string | null;
  reversalJournalId: string | null;
  retainedEarningsAccountId: string | null;
  retainedEarningsAccountCode: string | null;
  postingRuleId: string | null;
  postingRuleVersionNumber: number | null;
  reason: string;
  createdAt: string;
  postedAt: string | null;
  reversedAt: string | null;
  version: string;
}

export interface FinanceTrialBalanceRow {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  openingBalance: number;
  periodDebit: number;
  periodCredit: number;
  closingBalance: number;
  functionalCurrencyCode: string;
  reportingCurrencyCode: string | null;
  reportingClosingBalance: number | null;
  reportingEvidenceStatus: string;
}

export interface FinanceTrialBalanceReport {
  companyId: string;
  asOfDate: string;
  fromDate: string | null;
  toDate: string | null;
  rows: FinanceTrialBalanceRow[];
  totalDebit: number;
  totalCredit: number;
  totalClosingBalance: number;
  functionalCurrencyCode: string;
  reportingCurrencyCode: string | null;
  reportingEvidenceStatus: string;
}

export interface FinanceGeneralLedgerLine {
  journalId: string;
  journalNumber: string;
  journalSequence: number;
  postingDate: string;
  accountCode: string;
  accountName: string;
  sourceContract: string;
  debit: number;
  credit: number;
  functionalDebit: number;
  functionalCredit: number;
  runningBalance: number;
  functionalCurrencyCode: string;
  reportingAmount: number | null;
  reportingEvidenceStatus: string;
  isReversal: boolean;
}

export interface FinanceAgingReportRow {
  openItemId: string;
  kind: string;
  supplierId: string | null;
  customerId: string | null;
  sourceReference: string | null;
  documentDate: string;
  dueDate: string;
  daysOverdue: number;
  agingBucket: string;
  currencyCode: string;
  originalAmount: number;
  allocatedAmount: number;
  outstandingAmount: number;
  functionalCurrencyCode: string;
  outstandingFunctionalAmount: number;
  status: string;
}

export interface FinanceReconciliationView {
  companyId: string;
  asOfDate: string;
  scope: string;
  status: string;
  expectedAmount: number | null;
  actualAmount: number | null;
  difference: number | null;
  sourceReference: string | null;
  detail: string | null;
  hasDurableEvidence: boolean;
}

export interface FinanceCloseReconciliation {
  companyId: string;
  periodId: string | null;
  asOfDate: string;
  overallStatus: string;
  items: FinanceReconciliationView[];
  closeHistory: FinancePeriodCloseRun[];
  yearEndRuns: FinanceYearEndRun[];
}

export interface FinanceStatementRow {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: string;
  openingBalance: number;
  debit: number;
  credit: number;
  closingBalance: number;
  functionalCurrencyCode: string;
}

export interface FinanceStatementReport {
  kind: string;
  companyId: string;
  fromDate: string;
  toDate: string;
  rows: FinanceStatementRow[];
  totalDebit: number;
  totalCredit: number;
  totalClosingBalance: number;
  functionalCurrencyCode: string;
  finding: string | null;
}
