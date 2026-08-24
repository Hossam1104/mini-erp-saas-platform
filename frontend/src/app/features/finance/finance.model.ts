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
