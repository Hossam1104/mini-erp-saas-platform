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
  sourceContract: string;
  sourceEvent: string;
  description: string;
  lines: Array<{
    accountId: string;
    debit: number;
    credit: number;
    transactionAmount: number | null;
    transactionCurrencyCode: string | null;
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
