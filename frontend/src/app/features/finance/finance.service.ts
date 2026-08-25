import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';
import { MasterDataService } from '../master-data/master-data.service';
import { CurrencyRecord, TaxRecord } from '../master-data/master-data.models';
import {
  FinanceAccount,
  FinanceCompany,
  FinanceFiscalCalendar,
  FinanceFiscalPeriod,
  FinanceFiscalYear,
  FinanceGlLine,
  FinanceHandoff,
  FinanceJournal,
  FinanceJournalWriteRequest,
  FinancePostingRule,
  FinancePostingRuleWriteRequest,
  FinanceAccountWriteRequest,
  FinanceAllocation,
  FinanceAgingRow,
  FinanceCashAccount,
  FinanceExposure,
  FinanceOpenItem,
  FinancePaymentMethod,
  FinanceReconciliation,
  FinanceSettlementDocument,
  FinanceApSourceReady,
  FinanceManualReceivableRequest,
  FinanceSettlementWriteRequest,
  FinanceMonetaryPolicy,
  FinanceTaxEffect,
  FinanceRevaluationBatch,
} from './finance.model';

@Injectable({ providedIn: 'root' })
export class FinanceService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly masterData = inject(MasterDataService);

  companies(): Observable<FinanceCompany[]> { return this.http.get<FinanceCompany[]>('/api/v1/finance/companies'); }
  accounts(companyId: string): Observable<FinanceAccount[]> { return this.http.get<FinanceAccount[]>('/api/v1/finance/accounts', { params: new HttpParams().set('companyId', companyId) }); }
  calendars(companyId: string): Observable<FinanceFiscalCalendar[]> { return this.http.get<FinanceFiscalCalendar[]>('/api/v1/finance/calendars', { params: new HttpParams().set('companyId', companyId) }); }
  rules(companyId: string): Observable<FinancePostingRule[]> { return this.http.get<FinancePostingRule[]>('/api/v1/finance/posting-rules', { params: new HttpParams().set('companyId', companyId) }); }
  journals(companyId: string): Observable<FinanceJournal[]> { return this.http.get<FinanceJournal[]>('/api/v1/finance/journals', { params: new HttpParams().set('companyId', companyId) }); }
  gl(companyId: string): Observable<FinanceGlLine[]> { return this.http.get<FinanceGlLine[]>('/api/v1/finance/gl', { params: new HttpParams().set('companyId', companyId) }); }
  handoffs(companyId: string): Observable<FinanceHandoff[]> { return this.http.get<FinanceHandoff[]>('/api/v1/finance/inventory-handoffs', { params: new HttpParams().set('companyId', companyId) }); }
  periods(yearId: string): Observable<FinanceFiscalPeriod[]> { return this.http.get<FinanceFiscalPeriod[]>(`/api/v1/finance/years/${yearId}/periods`); }
  years(calendarId: string): Observable<FinanceFiscalYear[]> { return this.http.get<FinanceFiscalYear[]>(`/api/v1/finance/calendars/${calendarId}/years`); }

  createAccount(payload: FinanceAccountWriteRequest): Promise<FinanceAccount> { return this.mutate('/finance/accounts', payload); }
  createCalendar(payload: { companyId: string; name: string }): Promise<FinanceFiscalCalendar> { return this.mutate('/finance/calendars', payload); }
  createYear(payload: { calendarId: string; yearNumber: number; startDate: string; endDate: string }): Promise<FinanceFiscalYear> { return this.mutate('/finance/years', payload); }
  createPeriod(payload: { fiscalYearId: string; sequence: number; code: string; englishName: string | null; arabicName: string | null; startDate: string; endDate: string }): Promise<FinanceFiscalPeriod> { return this.mutate('/finance/periods', payload); }
  createJournal(payload: FinanceJournalWriteRequest): Promise<FinanceJournal> { return this.mutate('/finance/journals', payload); }
  createPostingRule(payload: FinancePostingRuleWriteRequest): Promise<FinancePostingRule> { return this.mutate('/finance/posting-rules', payload); }
  submitJournal(id: string, version: string): Promise<FinanceJournal> { return this.mutate(`/finance/journals/${id}/submit`, {}, version); }
  approveJournal(id: string, version: string): Promise<FinanceJournal> { return this.mutate(`/finance/journals/${id}/approve`, {}, version); }
  postJournal(id: string, version: string): Promise<FinanceJournal> { return this.mutate(`/finance/journals/${id}/post`, {}, version); }
  reverseJournal(id: string, postingDate: string, reason: string): Promise<FinanceJournal> { return this.mutate(`/finance/journals/${id}/reverse`, { postingDate, reason }); }
  processHandoff(id: string): Promise<FinanceJournal> { return this.mutate(`/finance/inventory-handoffs/${id}/process`, {}); }

  apOpenItems(companyId: string): Observable<FinanceOpenItem[]> { return this.http.get<FinanceOpenItem[]>('/api/v1/finance/ap/open-items', { params: new HttpParams().set('companyId', companyId) }); }
  apSourceReady(companyId: string): Observable<FinanceApSourceReady[]> { return this.http.get<FinanceApSourceReady[]>('/api/v1/finance/ap/source-ready', { params: new HttpParams().set('companyId', companyId) }); }
  arOpenItems(companyId: string): Observable<FinanceOpenItem[]> { return this.http.get<FinanceOpenItem[]>('/api/v1/finance/ar/open-items', { params: new HttpParams().set('companyId', companyId) }); }
  apAging(companyId: string): Observable<FinanceAgingRow[]> { return this.http.get<FinanceAgingRow[]>('/api/v1/finance/ap/aging', { params: new HttpParams().set('companyId', companyId) }); }
  arAging(companyId: string): Observable<FinanceAgingRow[]> { return this.http.get<FinanceAgingRow[]>('/api/v1/finance/ar/aging', { params: new HttpParams().set('companyId', companyId) }); }
  exposure(companyId: string, customerId: string): Observable<FinanceExposure | null> { return this.http.get<FinanceExposure | null>('/api/v1/finance/ar/exposure', { params: new HttpParams().set('companyId', companyId).set('customerId', customerId) }); }
  paymentMethods(companyId: string): Observable<FinancePaymentMethod[]> { return this.http.get<FinancePaymentMethod[]>('/api/v1/finance/payment-methods', { params: new HttpParams().set('companyId', companyId) }); }
  cashAccounts(companyId: string): Observable<FinanceCashAccount[]> { return this.http.get<FinanceCashAccount[]>('/api/v1/finance/cash-accounts', { params: new HttpParams().set('companyId', companyId) }); }
  payments(companyId: string): Observable<FinanceSettlementDocument[]> { return this.http.get<FinanceSettlementDocument[]>('/api/v1/finance/payments', { params: new HttpParams().set('companyId', companyId) }); }
  receipts(companyId: string): Observable<FinanceSettlementDocument[]> { return this.http.get<FinanceSettlementDocument[]>('/api/v1/finance/receipts', { params: new HttpParams().set('companyId', companyId) }); }
  allocations(companyId: string): Observable<FinanceAllocation[]> { return this.http.get<FinanceAllocation[]>('/api/v1/finance/allocations', { params: new HttpParams().set('companyId', companyId) }); }
  reconciliation(companyId: string): Observable<FinanceReconciliation[]> { return this.http.get<FinanceReconciliation[]>('/api/v1/finance/settlement/reconciliation', { params: new HttpParams().set('companyId', companyId) }); }
  customers(): Observable<unknown[]> { return this.http.get<unknown[]>('/api/v1/master-data/customers'); }
  suppliers(): Observable<unknown[]> { return this.http.get<unknown[]>('/api/v1/master-data/suppliers'); }
  paymentTerms(): Observable<unknown[]> { return this.http.get<unknown[]>('/api/v1/master-data/payment-terms'); }
  currencies(): Observable<CurrencyRecord[]> { return this.masterData.list('currencies') as Observable<CurrencyRecord[]>; }
  taxes(): Observable<TaxRecord[]> { return this.masterData.list('taxes') as Observable<TaxRecord[]>; }

  recognizeAp(sourceEvidenceId: string): Promise<FinanceOpenItem> { return this.mutate('/finance/ap/recognize', { sourceEvidenceId }); }
  createManualReceivable(payload: FinanceManualReceivableRequest): Promise<FinanceOpenItem> { return this.mutate('/finance/ar/manual', payload); }
  createPayment(payload: FinanceSettlementWriteRequest): Promise<FinanceSettlementDocument> { return this.mutate('/finance/payments', payload); }
  createReceipt(payload: FinanceSettlementWriteRequest): Promise<FinanceSettlementDocument> { return this.mutate('/finance/receipts', payload); }
  settlementAction(direction: 'Payment' | 'Receipt', id: string, action: 'submit' | 'approve' | 'reject', version: string, reason: string | null = null): Promise<FinanceSettlementDocument> { return this.mutate(`/finance/${direction === 'Payment' ? 'payments' : 'receipts'}/${id}/${action}`, { reason }, version); }
  postSettlement(direction: 'Payment' | 'Receipt', id: string, version: string): Promise<FinanceSettlementDocument> { return this.mutate(`/finance/${direction === 'Payment' ? 'payments' : 'receipts'}/${id}/post`, {}, version); }
  reverseSettlement(direction: 'Payment' | 'Receipt', id: string, reason: string): Promise<FinanceSettlementDocument> { return this.mutate(`/finance/${direction === 'Payment' ? 'payments' : 'receipts'}/${id}/reverse`, { postingDate: new Date().toISOString().slice(0, 10), reason }); }
  createAllocation(payload: { settlementDocumentId: string; openItemId: string; amount: number; allocationDate: string; reason: string | null }): Promise<FinanceAllocation> { return this.mutate('/finance/allocations', payload); }
  reverseAllocation(id: string, version: string, reason: string): Promise<FinanceAllocation> { return this.mutate(`/finance/allocations/${id}/reverse`, { reason }, version); }
  monetaryPolicies(companyId: string): Observable<FinanceMonetaryPolicy[]> { return this.http.get<FinanceMonetaryPolicy[]>('/api/v1/finance/monetary-policy', { params: new HttpParams().set('companyId', companyId) }); }
  taxEffects(companyId: string): Observable<FinanceTaxEffect[]> { return this.http.get<FinanceTaxEffect[]>('/api/v1/finance/tax-accounting', { params: new HttpParams().set('companyId', companyId) }); }
  previewTax(payload: { companyId: string; openItemId: string; taxId: string; taxableBase: number; sourceLineage: string | null }): Promise<FinanceTaxEffect> { return this.mutate('/finance/tax-accounting/preview', payload); }
  createMonetaryPolicy(payload: { companyId: string; reportingCurrencyId: string | null; roundingScale: number; roundingMode: string; revaluationEnabled: boolean; effectiveFrom: string; effectiveTo: string | null }): Promise<FinanceMonetaryPolicy> { return this.mutate('/finance/monetary-policy', payload); }
  postTax(payload: { companyId: string; openItemId: string; taxId: string; taxableBase: number; sourceLineage: string | null }): Promise<FinanceTaxEffect> { return this.mutate('/finance/tax-accounting', payload); }
  reverseTax(id: string, version: string, reason: string): Promise<FinanceTaxEffect> { return this.mutate(`/finance/tax-accounting/${id}/reverse`, { reason }, version); }
  revaluationBatches(companyId: string): Observable<FinanceRevaluationBatch[]> { return this.http.get<FinanceRevaluationBatch[]>('/api/v1/finance/revaluation', { params: new HttpParams().set('companyId', companyId) }); }
  createRevaluation(payload: { companyId: string; asOfDate: string; scope: string }): Promise<FinanceRevaluationBatch> { return this.mutate('/finance/revaluation', payload); }
  calculateRevaluation(id: string, version: string): Promise<FinanceRevaluationBatch> { return this.mutate(`/finance/revaluation/${id}/calculate`, {}, version); }
  postRevaluation(id: string, version: string): Promise<FinanceRevaluationBatch> { return this.mutate(`/finance/revaluation/${id}/post`, {}, version); }
  reverseRevaluation(id: string, version: string, reason: string): Promise<FinanceRevaluationBatch> { return this.mutate(`/finance/revaluation/${id}/reverse`, { reason }, version); }

  private async mutate<T>(path: string, payload: unknown, version?: string): Promise<T> {
    if (!await this.auth.bootstrapAntiforgery()) {
      throw new HttpErrorResponse({ status: 403, statusText: 'Antiforgery validation failed', error: { code: 'antiforgery_failed' } });
    }
    let headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
    if (version) headers = headers.set('If-Match', `"${version.replace(/^"|"$/g, '')}"`);
    return firstValueFrom(this.api.post<T>(path, payload, { headers }));
  }

  private idempotencyKey(): string { return globalThis.crypto?.randomUUID?.() ?? `finance-${Date.now().toString(36)}`; }
}
