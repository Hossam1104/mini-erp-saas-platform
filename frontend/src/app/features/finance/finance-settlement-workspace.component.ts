import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { catchError, firstValueFrom, forkJoin, map, Observable, of } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataService } from '../master-data/master-data.service';
import { CurrencyRecord, ExchangeRateRecord, ExchangeRateReferenceResponse } from '../master-data/master-data.models';
import { FinanceService } from './finance.service';
import { FinanceAgingRow, FinanceApSourceReady, FinanceCashAccount, FinanceCompany, FinanceOpenItem, FinancePaymentMethod, FinanceReconciliation, FinanceSettlementDocument, FinanceAllocation, FinanceManualReceivableRequest } from './finance.model';

type SettlementView = 'ap' | 'ar' | 'settlements';
type Bilingual = { en: string; ar: string };
type ReferenceOption = { id: string; code?: string; englishName?: string | null; arabicName?: string | null; lifecycleState?: string };
type PaymentTermVersionOption = { effectiveFrom: string; effectiveTo: string | null; baseDateRule: string; scheduleMode: string; dueOffsetDays: number; dueOffsetMonths: number; installments: Array<{ days: number; months: number }> };
type PaymentTermOption = ReferenceOption & { currentVersionNumber?: number; versions?: PaymentTermVersionOption[] };
type ArDraft = { customerId: string; paymentTermId: string; documentDate: string; currencyCode: string; amount: number; reference: string; description: string };
type SettlementDraft = { direction: 'Payment' | 'Receipt'; partyId: string; cashAccountId: string; paymentMethodId: string; documentDate: string; currencyCode: string; amount: number; externalReference: string; description: string };
type AllocationDraft = { documentId: string; itemId: string; amount: number; date: string; reason: string };
type FxDraftKind = 'ar' | 'settlement';
type ExchangeRateOption = ExchangeRateRecord & { code?: string };

const copy: Record<string, Bilingual> = {
  exchangeRate: { en: 'Exchange Rate', ar: '\u0633\u0639\u0631 \u0627\u0644\u0635\u0631\u0641' },
  rate: { en: 'Rate', ar: '\u0627\u0644\u0633\u0639\u0631' },
  rateVersion: { en: 'Rate version', ar: '\u0646\u0633\u062e\u0629 \u0627\u0644\u0633\u0639\u0631' },
  effectiveDate: { en: 'Effective date', ar: '\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0633\u0631\u064a\u0627\u0646' },
  noExactExchangeRate: { en: 'No exact MESP-120 Exchange Rate evidence is available for this currency and document date.', ar: '\u0644\u0627 \u062a\u062a\u0648\u0641\u0631 \u0623\u062f\u0644\u0629 \u0633\u0639\u0631 \u0635\u0631\u0641 MESP-120 \u062f\u0642\u064a\u0642\u0629 \u0644\u0647\u0630\u0647 \u0627\u0644\u0639\u0645\u0644\u0629 \u0648\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0645\u0633\u062a\u0646\u062f.' },
  fxEvidenceRequired: { en: 'Select an authorized Exchange Rate and resolve the exact document-date reference before creating this transaction.', ar: '\u0627\u062e\u062a\u0631 \u0633\u0639\u0631 \u0635\u0631\u0641 \u0645\u0635\u0631\u062d\u0627\u064b \u0648\u0627\u0633\u062a\u0639\u0644\u0645 \u0627\u0644\u0645\u0631\u062c\u0639 \u0627\u0644\u062f\u0642\u064a\u0642 \u0644\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0645\u0633\u062a\u0646\u062f \u0642\u0628\u0644 \u0625\u0646\u0634\u0627\u0621 \u0627\u0644\u0645\u0639\u0627\u0645\u0644\u0629.' },
  fxSettlementNotConfigured: { en: 'Allocation across different functional values is not configured in this release.', ar: '\u0627\u0644\u062a\u062e\u0635\u064a\u0635 \u0628\u064a\u0646 \u0642\u064a\u0645 \u0648\u0638\u064a\u0641\u064a\u0629 \u0645\u062e\u062a\u0644\u0641\u0629 \u063a\u064a\u0631 \u0645\u0647\u064a\u0623 \u0641\u064a \u0647\u0630\u0627 \u0627\u0644\u0625\u0635\u062f\u0627\u0631.' },
  functionalCurrencyRateExplicitOne: { en: 'Functional-currency transactions must not carry non-functional FX evidence.', ar: '\u064a\u062c\u0628 \u0623\u0644\u0627 \u062a\u062d\u0645\u0644 \u0627\u0644\u0645\u0639\u0627\u0645\u0644\u0627\u062a \u0628\u0627\u0644\u0639\u0645\u0644\u0629 \u0627\u0644\u0648\u0638\u064a\u0641\u064a\u0629 \u0623\u062f\u0644\u0629 \u0635\u0631\u0641 \u063a\u064a\u0631 \u0648\u0638\u064a\u0641\u064a\u0629.' },
  kicker: { en: 'Finance / subledgers and settlement', ar: 'المالية / الدفاتر الفرعية والتسوية' },
  apTitle: { en: 'Accounts Payable', ar: 'الحسابات الدائنة' },
  arTitle: { en: 'Accounts Receivable', ar: 'الحسابات المدينة' },
  settlementTitle: { en: 'Payments, receipts, and allocation', ar: 'المدفوعات والمقبوضات والتخصيص' },
  apLead: { en: 'Finance-ready supplier evidence becomes an immutable payable only after match, term, mapping, and journal checks.', ar: 'تتحول أدلة المورد الجاهزة للمالية إلى دائن غير قابل للتغيير بعد التحقق من المطابقة والشروط والربط والقيد.' },
  arLead: { en: 'A bounded Finance-owned receivable view before Sales. Manual AR remains distinct from a future Sales Invoice.', ar: 'عرض محدود لمدين تملكه المالية قبل المبيعات. يظل المدين اليدوي منفصلاً عن فاتورة مبيعات مستقبلية.' },
  settlementLead: { en: 'Cash posts first to an explicitly configured on-account effect. Allocation and reversal are separate audited truths.', ar: 'تُرحّل النقدية أولاً إلى أثر على الحساب مكوّن صراحة. يظل التخصيص والعكس حقيقتين منفصلتين ومدققتين.' },
  company: { en: 'Authorized Company', ar: 'الشركة المصرح بها' },
  chooseCompany: { en: 'Choose an authorized Company', ar: 'اختر شركة مصرحاً بها' },
  refresh: { en: 'Refresh evidence', ar: 'تحديث الأدلة' },
  supplier: { en: 'Supplier', ar: 'المورد' },
  customer: { en: 'Customer', ar: 'العميل' },
  reference: { en: 'Reference', ar: 'المرجع' },
  invoiceDate: { en: 'Document date', ar: 'تاريخ المستند' },
  dueDate: { en: 'Due date', ar: 'تاريخ الاستحقاق' },
  currency: { en: 'Currency', ar: 'العملة' },
  original: { en: 'Original', ar: 'الأصلي' },
  allocated: { en: 'Allocated', ar: 'المخصص' },
  outstanding: { en: 'Outstanding', ar: 'المتبقي' },
  status: { en: 'Status', ar: 'الحالة' },
  source: { en: 'Source / match evidence', ar: 'المصدر / دليل المطابقة' },
  journal: { en: 'Recognition journal', ar: 'قيد الاعتراف' },
  aging: { en: 'Aging', ar: 'أعمار الديون' },
  daysOverdue: { en: 'Days overdue', ar: 'أيام التأخر' },
  payment: { en: 'Supplier Payment', ar: 'دفعة المورد' },
  receipt: { en: 'Customer Receipt', ar: 'مقبوض العميل' },
  cashAccount: { en: 'Cash / Bank account', ar: 'حساب النقدية / البنك' },
  method: { en: 'Payment Method', ar: 'طريقة الدفع' },
  unallocated: { en: 'Unallocated / On Account', ar: 'غير مخصص / على الحساب' },
  allocations: { en: 'Allocations', ar: 'التخصيصات' },
  reconciliation: { en: 'Reconciliation', ar: 'المطابقة' },
  subledger: { en: 'Subledger', ar: 'الدفتر الفرعي' },
  postedJournal: { en: 'Posted journal', ar: 'القيد المرحل' },
  difference: { en: 'Difference', ar: 'الفرق' },
  loading: { en: 'Reading Finance evidence…', ar: 'جارٍ قراءة أدلة المالية…' },
  unavailable: { en: 'Finance evidence is unavailable right now. Try again shortly.', ar: 'أدلة المالية غير متاحة حالياً. حاول مرة أخرى بعد قليل.' },
  empty: { en: 'No records are configured for this Company yet.', ar: 'لا توجد سجلات مهيأة لهذه الشركة بعد.' },
  noCustomer: { en: 'Exposure is available when a valid Customer context is selected.', ar: 'يتوفر التعرض عند اختيار سياق عميل صالح.' },
  pending: { en: 'Pending / blocked', ar: 'معلق / محجوب' },
  sourceReady: { en: 'Finance-ready supplier invoices', ar: 'فواتير الموردين الجاهزة للمالية' },
  recognize: { en: 'Recognize payable', ar: 'إثبات الدائن' },
  match: { en: 'Match result', ar: 'نتيجة المطابقة' },
  paymentTerm: { en: 'Payment Term', ar: 'شرط الدفع' },
  createReceivable: { en: 'Create manual receivable', ar: 'إنشاء مدين يدوي' },
  createPayment: { en: 'Create payment', ar: 'إنشاء دفعة' },
  createReceipt: { en: 'Create receipt', ar: 'إنشاء مقبوض' },
  date: { en: 'Date', ar: 'التاريخ' },
  amount: { en: 'Amount', ar: 'المبلغ' },
  description: { en: 'Description', ar: 'الوصف' },
  externalReference: { en: 'Required reference', ar: 'المرجع المطلوب' },
  party: { en: 'Party', ar: 'الطرف' },
  submit: { en: 'Submit', ar: 'إرسال' },
  approve: { en: 'Approve', ar: 'اعتماد' },
  reject: { en: 'Reject', ar: 'رفض' },
  post: { en: 'Post', ar: 'ترحيل' },
  reverse: { en: 'Reverse', ar: 'عكس' },
  allocate: { en: 'Allocate', ar: 'تخصيص' },
  reverseAllocation: { en: 'Reverse allocation', ar: 'عكس التخصيص' },
  allocationDate: { en: 'Allocation date', ar: 'تاريخ التخصيص' },
  reason: { en: 'Reason', ar: 'السبب' },
  noOptions: { en: 'No authorized options are available.', ar: 'لا توجد خيارات مصرح بها.' },
};

@Component({
  selector: 'app-finance-settlement-workspace',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <section class="settlement-page" data-testid="finance-settlement-workspace">
      <header class="settlement-header">
        <div><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ title() }}</h1><p class="lede">{{ lead() }}</p></div>
        <button class="button button--primary" type="button" (click)="load()" [disabled]="loading()">{{ text('refresh') }}</button>
      </header>
      <nav class="settlement-nav" aria-label="Finance subledgers">
        <a routerLink="/app/finance" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }">Finance GL</a>
        <a routerLink="/app/finance/ap" routerLinkActive="is-active">{{ text('apTitle') }}</a>
        <a routerLink="/app/finance/ar" routerLinkActive="is-active">{{ text('arTitle') }}</a>
        <a routerLink="/app/finance/settlements" routerLinkActive="is-active">{{ text('settlementTitle') }}</a>
      </nav>
      <section class="settlement-controlbar"><label><span>{{ text('company') }}</span><select [value]="companyId()" (change)="selectCompany($any($event.target).value)" data-testid="settlement-company-select"><option value="">{{ text('chooseCompany') }}</option>@for (company of companies(); track company.companyId + (company.branchId ?? '')) { <option [value]="company.companyId">{{ company.companyName }} · {{ company.functionalCurrencyCode }}</option> }</select></label><span class="currency-chip">{{ selectedCurrency() }}</span></section>
      @if (loading()) { <section class="settlement-state" aria-live="polite"><h2>{{ text('loading') }}</h2></section> }
      @else if (error()) { <section class="settlement-state settlement-state--error" role="alert"><h2>{{ text('unavailable') }}</h2><p>{{ error() }}</p><button class="button button--secondary" type="button" (click)="load()">{{ text('refresh') }}</button></section> }
      @else if (!companyId()) { <section class="settlement-state"><h2>{{ text('chooseCompany') }}</h2></section> }
      @else if (view() === 'ap') {
        <section class="settlement-panel" data-testid="ap-source-ready"><div class="panel-heading"><div><p class="eyebrow">{{ text('sourceReady') }}</p><h2>{{ text('apLead') }}</h2></div><span class="count">{{ sourceReady().length }}</span></div>@if (sourceReady().length === 0) { <p class="empty-copy">{{ text('noOptions') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('supplier') }}</th><th>{{ text('reference') }}</th><th>{{ text('invoiceDate') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('amount') }}</th><th>{{ text('match') }}</th><th>{{ text('paymentTerm') }}</th><th></th></tr></thead><tbody>@for (candidate of sourceReady(); track candidate.sourceEvidenceId) { <tr><td>{{ candidate.supplierCode || candidate.supplierName || text('supplier') }}</td><td>{{ candidate.supplierInvoiceReference || '—' }}</td><td>{{ candidate.invoiceDate }}</td><td>{{ candidate.dueDate }}</td><td class="numeric">{{ candidate.amount | number:'1.2-2' }} {{ candidate.currencyCode }}</td><td>{{ candidate.matchResult }}</td><td>{{ candidate.paymentTerm.code }} · v{{ candidate.paymentTerm.versionNumber }}</td><td><button class="button button--secondary" type="button" (click)="recognize(candidate)" [disabled]="actionBusy()">{{ text('recognize') }}</button></td></tr> }</tbody></table></div> }</section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('apTitle') }}</p><h2>{{ text('apLead') }}</h2></div><span class="count">{{ openItems().length }}</span></div>
          @if (openItems().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('supplier') }}</th><th>{{ text('reference') }}</th><th>{{ text('invoiceDate') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th><th>{{ text('source') }}</th></tr></thead><tbody>@for (item of openItems(); track item.id) { <tr><td>{{ text('supplier') }}</td><td><strong>{{ item.reference || '—' }}</strong></td><td>{{ item.documentDate }}</td><td>{{ item.dueDate }}</td><td>{{ item.currencyCode }}</td><td class="numeric">{{ item.originalAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.outstandingAmount | number:'1.2-2' }}</td><td><span class="status" [class.active]="item.status === 'Open' || item.status === 'PartiallySettled'">{{ item.status }}</span></td><td>{{ item.sourceContract }}<small>{{ item.recognitionJournalId ? text('journal') : text('pending') }}</small></td></tr> }</tbody></table></div> }
        </section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('aging') }}</p><h2>{{ text('dueDate') }} / {{ text('daysOverdue') }}</h2></div></div>@if (aging().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('reference') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('daysOverdue') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of aging(); track row.openItemId) { <tr><td>{{ row.reference || '—' }}</td><td>{{ row.dueDate }}</td><td>{{ row.daysOverdue }}</td><td class="numeric">{{ row.outstandingAmount | number:'1.2-2' }} {{ row.currencyCode }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div> }</section>
      } @else if (view() === 'ar') {
        <section class="settlement-panel operational-form"><div class="panel-heading"><div><p class="eyebrow">{{ text('arTitle') }}</p><h2>{{ text('createReceivable') }}</h2></div></div><div class="form-grid"><label><span>{{ text('customer') }}</span><select [value]="arDraft.customerId" (change)="setAr('customerId', $any($event.target).value)" data-testid="ar-customer-select"><option value="">{{ text('customer') }}</option>@for (customer of customers(); track customer.id) { <option [value]="customer.id">{{ customer.code || customer.id }}</option> }</select></label><label><span>{{ text('paymentTerm') }}</span><select [value]="arDraft.paymentTermId" (change)="setAr('paymentTermId', $any($event.target).value)" data-testid="ar-payment-term-select"><option value="">{{ text('paymentTerm') }}</option>@for (term of paymentTerms(); track term.id) { <option [value]="term.id">{{ term.code || term.id }} · v{{ term.currentVersionNumber || '—' }}</option> }</select></label><label><span>{{ text('date') }}</span><input type="date" [value]="arDraft.documentDate" (input)="setAr('documentDate', $any($event.target).value)" /></label><label><span>{{ text('currency') }}</span><input [value]="arDraft.currencyCode" (input)="setAr('currencyCode', $any($event.target).value)" maxlength="16" /></label><label><span>{{ text('amount') }}</span><input type="number" min="0.01" step="0.01" [value]="arDraft.amount" (input)="setAr('amount', $any($event.target).valueAsNumber)" /></label><label><span>{{ text('dueDate') }}</span><input readonly [value]="derivedArDueDate()" placeholder="—" /></label><label><span>{{ text('reference') }}</span><input [value]="arDraft.reference" (input)="setAr('reference', $any($event.target).value)" /></label><label><span>{{ text('description') }}</span><input [value]="arDraft.description" (input)="setAr('description', $any($event.target).value)" /></label></div><div class="form-actions"><button class="button button--primary" type="button" (click)="createReceivable()" [disabled]="!canCreateAr() || actionBusy()">{{ text('createReceivable') }}</button>@if (actionError()) { <span class="form-error" role="alert">{{ actionError() }}</span> }</div></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('arTitle') }}</p><h2>{{ text('arLead') }}</h2></div><span class="count">{{ openItems().length }}</span></div>
          @if (openItems().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('customer') }}</th><th>{{ text('reference') }}</th><th>{{ text('invoiceDate') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (item of openItems(); track item.id) { <tr><td>{{ text('customer') }}</td><td><strong>{{ item.reference || '—' }}</strong></td><td>{{ item.documentDate }}</td><td>{{ item.dueDate }}</td><td>{{ item.currencyCode }}</td><td class="numeric">{{ item.originalAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.outstandingAmount | number:'1.2-2' }}</td><td><span class="status" [class.active]="item.status === 'Open' || item.status === 'PartiallySettled'">{{ item.status }}</span></td></tr> }</tbody></table></div> }
        </section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('aging') }}</p><h2>{{ text('dueDate') }} / {{ text('daysOverdue') }}</h2></div></div>@if (aging().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('reference') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('daysOverdue') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of aging(); track row.openItemId) { <tr><td>{{ row.reference || '—' }}</td><td>{{ row.dueDate }}</td><td>{{ row.daysOverdue }}</td><td class="numeric">{{ row.outstandingAmount | number:'1.2-2' }} {{ row.currencyCode }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div> }</section>
      } @else {
        <section class="settlement-panel operational-form"><div class="panel-heading"><div><p class="eyebrow">{{ text('settlementTitle') }}</p><h2>{{ text('createPayment') }} / {{ text('createReceipt') }}</h2></div></div><div class="form-grid"><label><span>{{ text('payment') }} / {{ text('receipt') }}</span><select [value]="settlementDraft.direction" (change)="setSettlement('direction', $any($event.target).value)"><option value="Payment">{{ text('payment') }}</option><option value="Receipt">{{ text('receipt') }}</option></select></label><label><span>{{ text('party') }}</span><select [value]="settlementDraft.partyId" (change)="setSettlement('partyId', $any($event.target).value)" data-testid="settlement-party-select"><option value="">{{ text('party') }}</option>@for (party of settlementParties(); track party.id) { <option [value]="party.id">{{ party.code || party.id }}</option> }</select></label><label><span>{{ text('method') }}</span><select [value]="settlementDraft.paymentMethodId" (change)="setSettlement('paymentMethodId', $any($event.target).value)"><option value="">{{ text('method') }}</option>@for (method of compatibleMethods(); track method.id) { <option [value]="method.id">{{ method.code }} · {{ method.direction }}</option> }</select></label><label><span>{{ text('cashAccount') }}</span><select [value]="settlementDraft.cashAccountId" (change)="setSettlement('cashAccountId', $any($event.target).value)"><option value="">{{ text('cashAccount') }}</option>@for (cash of activeCashAccounts(); track cash.id) { <option [value]="cash.id">{{ cash.code }} · {{ cash.currencyCode }}</option> }</select></label><label><span>{{ text('date') }}</span><input type="date" [value]="settlementDraft.documentDate" (input)="setSettlement('documentDate', $any($event.target).value)" /></label><label><span>{{ text('currency') }}</span><input [value]="settlementDraft.currencyCode" (input)="setSettlement('currencyCode', $any($event.target).value)" maxlength="16" /></label><label><span>{{ text('amount') }}</span><input type="number" min="0.01" step="0.01" [value]="settlementDraft.amount" (input)="setSettlement('amount', $any($event.target).valueAsNumber)" /></label><label><span>{{ text('externalReference') }}</span><input [value]="settlementDraft.externalReference" (input)="setSettlement('externalReference', $any($event.target).value)" /></label><label class="field-wide"><span>{{ text('description') }}</span><input [value]="settlementDraft.description" (input)="setSettlement('description', $any($event.target).value)" /></label></div><div class="form-actions"><button class="button button--primary" type="button" (click)="createSettlement()" [disabled]="!canCreateSettlement() || actionBusy()">{{ settlementDraft.direction === 'Payment' ? text('createPayment') : text('createReceipt') }}</button>@if (actionError()) { <span class="form-error" role="alert">{{ actionError() }}</span> }</div></section>
        <section class="settlement-cards"><article><span>{{ text('payment') }}</span><strong>{{ payments().length }}</strong><small>{{ text('unallocated') }} is retained explicitly.</small></article><article><span>{{ text('receipt') }}</span><strong>{{ receipts().length }}</strong><small>{{ text('unallocated') }} is retained explicitly.</small></article><article><span>{{ text('method') }}</span><strong>{{ methods().length }}</strong><small>{{ text('method') }} is Company-owned and configurable.</small></article><article><span>{{ text('cashAccount') }}</span><strong>{{ cashAccounts().length }}</strong><small>{{ text('cashAccount') }} links to configured GL accounts.</small></article></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('payment') }} / {{ text('receipt') }}</p><h2>{{ text('settlementLead') }}</h2></div></div><div class="table-wrap"><table><thead><tr><th>{{ text('status') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('unallocated') }}</th><th>{{ text('journal') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (document of allDocuments(); track document.id) { <tr><td><span class="status" [class.active]="document.status === 'Posted'">{{ document.direction === 'Payment' ? text('payment') : text('receipt') }} · {{ document.status }}</span></td><td>{{ document.currencyCode }}</td><td class="numeric">{{ document.amount | number:'1.2-2' }}</td><td class="numeric">{{ document.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ document.unallocatedAmount | number:'1.2-2' }}</td><td>{{ document.postedJournalId ? text('postedJournal') : text('pending') }}</td><td class="action-list">@if (document.status === 'Draft' || document.status === 'Rejected') { <button type="button" class="text-button" (click)="settlementAction(document, 'submit')">{{ text('submit') }}</button> } @if (document.status === 'Submitted' && document.approvalRequirement === 'Required') { <button type="button" class="text-button" (click)="settlementAction(document, 'approve')">{{ text('approve') }}</button><button type="button" class="text-button" (click)="settlementAction(document, 'reject')">{{ text('reject') }}</button> } @if (document.status === 'Submitted' && document.approvalRequirement === 'NotRequired') { <button type="button" class="text-button" (click)="settlementAction(document, 'post')">{{ text('post') }}</button> } @if (document.status === 'Approved') { <button type="button" class="text-button" (click)="settlementAction(document, 'post')">{{ text('post') }}</button> } @if (document.status === 'Posted') { <button type="button" class="text-button" (click)="settlementAction(document, 'reverse')">{{ text('reverse') }}</button> }</td></tr> }</tbody></table></div></section>
        <section class="settlement-panel operational-form"><div class="panel-heading"><div><p class="eyebrow">{{ text('allocations') }}</p><h2>{{ text('allocate') }}</h2></div></div><div class="form-grid"><label><span>{{ text('payment') }} / {{ text('receipt') }}</span><select [value]="allocationDraft.documentId" (change)="setAllocation('documentId', $any($event.target).value)"><option value="">{{ text('unallocated') }}</option>@for (document of postedDocuments(); track document.id) { <option [value]="document.id">{{ document.direction }} · {{ document.externalReference || document.id }} · {{ document.currencyCode }}</option> }</select></label><label><span>{{ text('reference') }}</span><select [value]="allocationDraft.itemId" (change)="setAllocation('itemId', $any($event.target).value)"><option value="">{{ text('outstanding') }}</option>@for (item of compatibleAllocationItems(); track item.id) { <option [value]="item.id">{{ item.reference || item.id }} · {{ item.outstandingAmount | number:'1.2-2' }} {{ item.currencyCode }}</option> }</select></label><label><span>{{ text('amount') }}</span><input type="number" min="0.01" step="0.01" [value]="allocationDraft.amount" (input)="setAllocation('amount', $any($event.target).valueAsNumber)" /></label><label><span>{{ text('allocationDate') }}</span><input type="date" [value]="allocationDraft.date" (input)="setAllocation('date', $any($event.target).value)" /></label><label class="field-wide"><span>{{ text('reason') }}</span><input [value]="allocationDraft.reason" (input)="setAllocation('reason', $any($event.target).value)" /></label></div><div class="form-actions"><button class="button button--secondary" type="button" (click)="createAllocation()" [disabled]="!canCreateAllocation() || actionBusy()">{{ text('allocate') }}</button></div>@if (allocations().length) { <div class="action-list">@for (allocation of allocations(); track allocation.id) { @if (allocation.status === 'Active' && !allocation.reversalOfAllocationId) { <button type="button" class="text-button" (click)="reverseAllocation(allocation)">{{ text('reverseAllocation') }} · {{ allocation.amount | number:'1.2-2' }}</button> } }</div> }</section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('allocations') }}</p><h2>{{ text('reconciliation') }}</h2></div><span class="count">{{ allocations().length }}</span></div><div class="table-wrap"><table><thead><tr><th>{{ text('allocations') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('status') }}</th><th>{{ text('journal') }}</th></tr></thead><tbody>@for (allocation of allocations(); track allocation.id) { <tr><td>{{ allocation.status === 'Reversed' ? text('pending') : text('allocations') }}</td><td>{{ allocation.currencyCode }}</td><td class="numeric">{{ allocation.amount | number:'1.2-2' }}</td><td>{{ allocation.status }}</td><td>{{ allocation.journalId ? text('postedJournal') : text('pending') }}</td></tr> }</tbody></table></div></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('reconciliation') }}</p><h2>{{ text('subledger') }} / {{ text('postedJournal') }}</h2></div></div><div class="table-wrap"><table><thead><tr><th>{{ text('reconciliation') }}</th><th>{{ text('subledger') }}</th><th>{{ text('postedJournal') }}</th><th>{{ text('difference') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of reconciliation(); track row.scope) { <tr><td>{{ row.scope }}</td><td class="numeric">{{ row.subledgerAmount | number:'1.2-2' }}</td><td class="numeric">{{ row.postedJournalAmount | number:'1.2-2' }}</td><td class="numeric">{{ row.difference | number:'1.2-2' }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div></section>
      }
      @if (view() === 'ar' && requiresArFx()) { <section class="settlement-panel fx-evidence-panel" data-testid="ar-fx-evidence"><div class="panel-heading"><div><p class="eyebrow">{{ text('exchangeRate') }}</p><h2>{{ text('fxEvidenceRequired') }}</h2></div></div><label><span>{{ text('exchangeRate') }}</span><select [value]="arExchangeRateId()" (change)="selectExchangeRate('ar', $any($event.target).value)" data-testid="ar-exchange-rate-select"><option value="">{{ text('exchangeRate') }}</option>@for (rate of exchangeRateOptions('ar'); track rate.id) { <option [value]="rate.id">{{ rate.sourceCurrencyCode }} → {{ rate.targetCurrencyCode }} · {{ rate.code || rate.id }}</option> }</select></label>@if (arExchangeRateReference(); as reference) { <p class="term-hint" data-testid="ar-fx-reference">{{ text('rate') }} {{ reference.rate }} · {{ text('rateVersion') }} v{{ reference.versionNumber }} · {{ text('effectiveDate') }} {{ reference.effectiveOn }}</p> } @else if (fxError()) { <p class="form-error" role="alert">{{ fxError() }}</p> }</section> }
      @if (view() === 'settlements' && requiresSettlementFx()) { <section class="settlement-panel fx-evidence-panel" data-testid="settlement-fx-evidence"><div class="panel-heading"><div><p class="eyebrow">{{ text('exchangeRate') }}</p><h2>{{ text('fxEvidenceRequired') }}</h2></div></div><label><span>{{ text('exchangeRate') }}</span><select [value]="settlementExchangeRateId()" (change)="selectExchangeRate('settlement', $any($event.target).value)" data-testid="settlement-exchange-rate-select"><option value="">{{ text('exchangeRate') }}</option>@for (rate of exchangeRateOptions('settlement'); track rate.id) { <option [value]="rate.id">{{ rate.sourceCurrencyCode }} → {{ rate.targetCurrencyCode }} · {{ rate.code || rate.id }}</option> }</select></label>@if (settlementExchangeRateReference(); as reference) { <p class="term-hint" data-testid="settlement-fx-reference">{{ text('rate') }} {{ reference.rate }} · {{ text('rateVersion') }} v{{ reference.versionNumber }} · {{ text('effectiveDate') }} {{ reference.effectiveOn }}</p> } @else if (fxError()) { <p class="form-error" role="alert">{{ fxError() }}</p> }</section> }
    </section>
  `,
  styles: [`
    :host { display:block; } .settlement-page { display:grid; gap:1rem; } .settlement-header { display:flex; align-items:end; justify-content:space-between; gap:1rem; } h1 { margin:.2rem 0 .55rem; } .lede { max-width:850px; color:var(--muted); line-height:1.6; } .settlement-nav { display:flex; gap:.35rem; overflow:auto; border-bottom:1px solid var(--line); } .settlement-nav a { padding:.75rem .85rem; color:var(--muted); font-weight:750; white-space:nowrap; text-decoration:none; border-bottom:3px solid transparent; } .settlement-nav a.is-active { color:var(--ink); border-bottom-color:var(--teal); } .settlement-controlbar { display:flex; align-items:end; justify-content:space-between; gap:1rem; padding:1rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } label { display:grid; gap:.4rem; min-width:min(100%,380px); } label span { color:var(--muted); font-size:.73rem; font-weight:750; letter-spacing:.08em; text-transform:uppercase; } select,input { min-height:2.65rem; padding:.55rem .7rem; border:1px solid var(--line-strong); border-radius:9px; background:var(--surface); color:var(--ink); font:inherit; } .currency-chip { padding:.7rem 1rem; border-radius:10px; background:var(--mint); color:var(--teal); font-weight:800; } .settlement-panel, .settlement-state { padding:1.35rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } .settlement-state { min-height:180px; display:grid; place-content:center; text-align:center; gap:.5rem; } .settlement-state--error { border-color:color-mix(in srgb,var(--danger) 45%,var(--line)); } .panel-heading { display:flex; justify-content:space-between; align-items:start; gap:1rem; margin-bottom:1rem; } .panel-heading h2 { margin:.25rem 0 0; max-width:920px; font-size:1.15rem; } .count { min-width:2rem; padding:.3rem .55rem; border-radius:999px; background:var(--mint); color:var(--teal); font-weight:800; text-align:center; } .form-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:.8rem; } .form-grid label { min-width:0; } .field-wide { grid-column:span 2; } .form-actions { display:flex; align-items:center; gap:.8rem; margin-top:1rem; flex-wrap:wrap; } .form-error { color:var(--danger); font-weight:700; } .action-list { display:flex; flex-wrap:wrap; gap:.4rem; min-width:190px; white-space:normal; } .settlement-cards { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:.9rem; } .settlement-cards article { display:grid; gap:.5rem; min-height:125px; padding:1.1rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } .settlement-cards span { color:var(--muted); font-size:.75rem; font-weight:800; text-transform:uppercase; } .settlement-cards strong { font-size:2rem; } .settlement-cards small, .empty-copy, td small { color:var(--muted); line-height:1.45; } .table-wrap { overflow:auto; } table { width:100%; border-collapse:collapse; } th,td { padding:.75rem .6rem; border-bottom:1px solid var(--line); text-align:start; vertical-align:top; white-space:nowrap; } th { color:var(--muted); font-size:.7rem; text-transform:uppercase; letter-spacing:.06em; } td small { display:block; margin-top:.2rem; } .numeric { text-align:end; font-variant-numeric:tabular-nums; } .status { display:inline-block; padding:.22rem .52rem; border-radius:999px; background:#f3eee5; color:var(--muted); font-size:.78rem; font-weight:750; } .status.active { background:var(--mint); color:var(--teal); } @media (max-width:900px) { .settlement-header,.settlement-controlbar { align-items:stretch; flex-direction:column; } .settlement-cards,.form-grid { grid-template-columns:repeat(2,minmax(0,1fr)); } } @media (max-width:560px) { .settlement-cards,.form-grid { grid-template-columns:1fr; } .settlement-panel { padding:1rem; } .field-wide { grid-column:auto; } }
  `],
})
export class FinanceSettlementWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly finance = inject(FinanceService);
  private readonly masterData = inject(MasterDataService, { optional: true });
  private readonly route = inject(ActivatedRoute);
  readonly view = signal<SettlementView>((this.route.snapshot.url[0]?.path as SettlementView) || 'settlements');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly companies = signal<FinanceCompany[]>([]);
  readonly companyId = signal('');
  readonly openItems = signal<FinanceOpenItem[]>([]);
  readonly apItems = signal<FinanceOpenItem[]>([]);
  readonly arItems = signal<FinanceOpenItem[]>([]);
  readonly sourceReady = signal<FinanceApSourceReady[]>([]);
  readonly aging = signal<FinanceAgingRow[]>([]);
  readonly methods = signal<FinancePaymentMethod[]>([]);
  readonly cashAccounts = signal<FinanceCashAccount[]>([]);
  readonly payments = signal<FinanceSettlementDocument[]>([]);
  readonly receipts = signal<FinanceSettlementDocument[]>([]);
  readonly allocations = signal<FinanceAllocation[]>([]);
  readonly reconciliation = signal<FinanceReconciliation[]>([]);
  readonly customers = signal<ReferenceOption[]>([]);
  readonly suppliers = signal<ReferenceOption[]>([]);
  readonly paymentTerms = signal<PaymentTermOption[]>([]);
  readonly currencies = signal<CurrencyRecord[]>([]);
  readonly exchangeRates = signal<ExchangeRateOption[]>([]);
  readonly arExchangeRateId = signal('');
  readonly settlementExchangeRateId = signal('');
  readonly arExchangeRateReference = signal<ExchangeRateReferenceResponse | null>(null);
  readonly settlementExchangeRateReference = signal<ExchangeRateReferenceResponse | null>(null);
  readonly fxLoading = signal<FxDraftKind | null>(null);
  readonly fxError = signal<string | null>(null);
  readonly actionBusy = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly arDraft: ArDraft = { customerId: '', paymentTermId: '', documentDate: new Date().toISOString().slice(0, 10), currencyCode: '', amount: 0, reference: '', description: '' };
  readonly settlementDraft: SettlementDraft = { direction: 'Payment', partyId: '', cashAccountId: '', paymentMethodId: '', documentDate: new Date().toISOString().slice(0, 10), currencyCode: '', amount: 0, externalReference: '', description: '' };
  readonly allocationDraft: AllocationDraft = { documentId: '', itemId: '', amount: 0, date: new Date().toISOString().slice(0, 10), reason: '' };

  ngOnInit(): void { this.load(); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  title(): string { return this.text(this.view() === 'ap' ? 'apTitle' : this.view() === 'ar' ? 'arTitle' : 'settlementTitle'); }
  lead(): string { return this.text(this.view() === 'ap' ? 'apLead' : this.view() === 'ar' ? 'arLead' : 'settlementLead'); }
  selectedCurrency(): string { return this.companies().find(company => company.companyId === this.companyId())?.functionalCurrencyCode ?? '—'; }
  allDocuments(): FinanceSettlementDocument[] { return [...this.payments(), ...this.receipts()].sort((a, b) => b.documentDate.localeCompare(a.documentDate)); }
  selectCompany(id: string): void { this.companyId.set(id); this.resetDraftCurrencies(); this.actionError.set(null); this.loadView(); }

  load(): void { this.loading.set(true); this.error.set(null); this.finance.companies().subscribe({ next: companies => { const currentCompany = this.companyId(); const nextCompany = currentCompany && companies.some(company => company.companyId === currentCompany) ? currentCompany : companies[0]?.companyId ?? ''; this.companies.set(companies); this.companyId.set(nextCompany); if (nextCompany !== currentCompany || !this.arDraft.currencyCode || !this.settlementDraft.currencyCode) this.resetDraftCurrencies(); this.loadView(); }, error: () => { this.loading.set(false); this.error.set(this.text('unavailable')); } }); }
  private loadView(): void {
    const companyId = this.companyId(); if (!companyId) { this.loading.set(false); return; } this.loading.set(true);
    if (this.view() === 'ap') {
      const sourceReady$ = this.finance.apSourceReady?.(companyId) ?? of([] as FinanceApSourceReady[]);
      forkJoin({ items: this.finance.apOpenItems(companyId), aging: this.finance.apAging(companyId), sourceReady: sourceReady$ }).subscribe({ next: result => { this.openItems.set(result.items); this.apItems.set(result.items); this.sourceReady.set(result.sourceReady); this.aging.set(result.aging); this.loading.set(false); }, error: () => this.failed() }); return;
    }
    if (this.view() === 'ar') {
      const customers$ = this.finance.customers?.().pipe(catchError(() => of([]))) ?? of([]);
      const terms$ = this.finance.paymentTerms?.().pipe(catchError(() => of([]))) ?? of([]);
      const { currencies$, exchangeRates$ } = this.masterDataReferences();
      forkJoin({ items: this.finance.arOpenItems(companyId), aging: this.finance.arAging(companyId), customers: customers$, paymentTerms: terms$, currencies: currencies$, exchangeRates: exchangeRates$ }).subscribe({ next: result => { this.openItems.set(result.items); this.arItems.set(result.items); this.customers.set(this.activeOptions(result.customers)); this.paymentTerms.set(this.activeOptions(result.paymentTerms) as PaymentTermOption[]); this.currencies.set(result.currencies); this.exchangeRates.set(result.exchangeRates); this.aging.set(result.aging); this.loading.set(false); }, error: () => this.failed() }); return;
    }
    const customers$ = this.finance.customers?.().pipe(catchError(() => of([]))) ?? of([]);
    const suppliers$ = this.finance.suppliers?.().pipe(catchError(() => of([]))) ?? of([]);
    const { currencies$, exchangeRates$ } = this.masterDataReferences();
    forkJoin({ methods: this.finance.paymentMethods(companyId), cashAccounts: this.finance.cashAccounts(companyId), payments: this.finance.payments(companyId), receipts: this.finance.receipts(companyId), allocations: this.finance.allocations(companyId), reconciliation: this.finance.reconciliation(companyId), apItems: this.finance.apOpenItems(companyId), arItems: this.finance.arOpenItems(companyId), customers: customers$, suppliers: suppliers$, currencies: currencies$, exchangeRates: exchangeRates$ }).subscribe({ next: result => { this.methods.set(result.methods); this.cashAccounts.set(result.cashAccounts); this.payments.set(result.payments); this.receipts.set(result.receipts); this.allocations.set(result.allocations); this.reconciliation.set(result.reconciliation); this.apItems.set(result.apItems); this.arItems.set(result.arItems); this.customers.set(this.activeOptions(result.customers)); this.suppliers.set(this.activeOptions(result.suppliers)); this.currencies.set(result.currencies); this.exchangeRates.set(result.exchangeRates); this.loading.set(false); }, error: () => this.failed() });
  }
  private masterDataReferences(): { currencies$: Observable<CurrencyRecord[]>; exchangeRates$: Observable<ExchangeRateOption[]> } {
    const currencies$: Observable<CurrencyRecord[]> = this.masterData ? this.masterData.list('currencies').pipe(catchError(() => of([])), map(items => items as CurrencyRecord[])) : of([]);
    const exchangeRates$: Observable<ExchangeRateOption[]> = this.masterData ? this.masterData.list('exchange-rates').pipe(catchError(() => of([])), map(items => items as ExchangeRateOption[])) : of([]);
    return { currencies$, exchangeRates$ };
  }
  private failed(): void { this.loading.set(false); this.error.set(this.text('unavailable')); }

  private resetDraftCurrencies(): void {
    const currency = this.selectedCurrency();
    if (!currency) return;
    this.arDraft.currencyCode = currency;
    this.settlementDraft.currencyCode = currency;
    this.clearExchangeRate('ar');
    this.clearExchangeRate('settlement');
  }

  transactionCurrencyCodes(): string[] {
    const configured = this.currencies().filter(item => !item.lifecycleState || item.lifecycleState === 'Active').map(item => item.code.trim().toUpperCase()).filter(Boolean);
    const functional = this.selectedCurrency();
    return [...new Set([functional, ...configured].filter(Boolean))];
  }

  exchangeRateOptions(kind: FxDraftKind): ExchangeRateOption[] {
    const currency = kind === 'ar' ? this.arDraft.currencyCode : this.settlementDraft.currencyCode;
    const functional = this.selectedCurrency();
    if (!currency || !functional || currency.trim().toUpperCase() === functional) return [];
    return this.exchangeRates().filter(rate => rate.lifecycleState === 'Active'
      && rate.sourceCurrencyCode.trim().toUpperCase() === currency.trim().toUpperCase()
      && rate.targetCurrencyCode.trim().toUpperCase() === functional);
  }

  requiresArFx(): boolean { return this.requiresFx('ar'); }
  requiresSettlementFx(): boolean { return this.requiresFx('settlement'); }
  private requiresFx(kind: FxDraftKind): boolean {
    const currency = kind === 'ar' ? this.arDraft.currencyCode : this.settlementDraft.currencyCode;
    const functional = this.selectedCurrency();
    return !!currency && !!functional && currency.trim().toUpperCase() !== functional;
  }

  private fxReference(kind: FxDraftKind): ExchangeRateReferenceResponse | null {
    return kind === 'ar' ? this.arExchangeRateReference() : this.settlementExchangeRateReference();
  }

  private fxId(kind: FxDraftKind): string {
    return kind === 'ar' ? this.arExchangeRateId() : this.settlementExchangeRateId();
  }

  private setFxId(kind: FxDraftKind, id: string): void {
    if (kind === 'ar') this.arExchangeRateId.set(id); else this.settlementExchangeRateId.set(id);
  }

  private setFxReference(kind: FxDraftKind, reference: ExchangeRateReferenceResponse | null): void {
    if (kind === 'ar') this.arExchangeRateReference.set(reference); else this.settlementExchangeRateReference.set(reference);
  }

  private clearExchangeRate(kind: FxDraftKind): void {
    this.setFxId(kind, '');
    this.setFxReference(kind, null);
    this.fxError.set(null);
  }

  selectExchangeRate(kind: FxDraftKind, id: string): void {
    this.setFxId(kind, id);
    this.setFxReference(kind, null);
    this.fxError.set(null);
    void this.resolveExchangeRate(kind, id);
  }

  private async resolveSelectedExchangeRate(kind: FxDraftKind): Promise<void> {
    const id = this.fxId(kind);
    if (id) await this.resolveExchangeRate(kind, id);
  }

  private async resolveExchangeRate(kind: FxDraftKind, id: string): Promise<void> {
    if (!id || !this.requiresFx(kind)) {
      this.setFxReference(kind, null);
      return;
    }
    if (!this.exchangeRateOptions(kind).some(rate => rate.id === id) || !this.masterData) {
      this.setFxReference(kind, null);
      this.fxError.set(this.text('noExactExchangeRate'));
      return;
    }
    const date = kind === 'ar' ? this.arDraft.documentDate : this.settlementDraft.documentDate;
    this.fxLoading.set(kind);
    this.fxError.set(null);
    try {
      const reference = await firstValueFrom(this.masterData.referenceExchangeRate(id, date));
      const currency = kind === 'ar' ? this.arDraft.currencyCode.trim().toUpperCase() : this.settlementDraft.currencyCode.trim().toUpperCase();
      if (reference.id !== id || reference.lifecycleState !== 'Active' || reference.effectiveOn !== date || reference.sourceCurrencyCode.trim().toUpperCase() !== currency || reference.targetCurrencyCode.trim().toUpperCase() !== this.selectedCurrency() || reference.rate <= 0) {
        throw new Error('exchange-rate-reference-invalid');
      }
      this.setFxReference(kind, reference);
    } catch {
      this.setFxReference(kind, null);
      this.fxError.set(this.text('noExactExchangeRate'));
    } finally {
      this.fxLoading.set(null);
    }
  }

  private exchangeRateReady(kind: FxDraftKind): boolean {
    if (!this.requiresFx(kind)) return true;
    const reference = this.fxReference(kind);
    const date = kind === 'ar' ? this.arDraft.documentDate : this.settlementDraft.documentDate;
    return !!reference && reference.id === this.fxId(kind) && reference.effectiveOn === date;
  }

  private fxFields(kind: FxDraftKind): Pick<FinanceManualReceivableRequest, 'exchangeRate' | 'exchangeRateId' | 'exchangeRateVersionId' | 'exchangeRateVersionNumber'> {
    const reference = this.fxReference(kind);
    return {
      exchangeRate: this.requiresFx(kind) ? reference?.rate ?? null : null,
      exchangeRateId: this.requiresFx(kind) ? reference?.id ?? null : null,
      exchangeRateVersionId: this.requiresFx(kind) ? reference?.versionId ?? null : null,
      exchangeRateVersionNumber: this.requiresFx(kind) ? reference?.versionNumber ?? null : null,
    };
  }

  setAr(field: keyof ArDraft, value: string | number): void {
    const normalized = field === 'currencyCode' ? String(value).trim().toUpperCase() : value;
    (this.arDraft as unknown as Record<string, string | number>)[field] = normalized;
    if (field === 'currencyCode') this.clearExchangeRate('ar');
    if (field === 'documentDate') void this.resolveSelectedExchangeRate('ar');
    this.actionError.set(null);
  }

  setSettlement(field: keyof SettlementDraft, value: string | number): void {
    const normalized = field === 'currencyCode' ? String(value).trim().toUpperCase() : value;
    (this.settlementDraft as unknown as Record<string, string | number>)[field] = normalized;
    if (field === 'direction') this.settlementDraft.partyId = '';
    if (field === 'currencyCode') this.clearExchangeRate('settlement');
    if (field === 'documentDate') void this.resolveSelectedExchangeRate('settlement');
    this.actionError.set(null);
  }
  setAllocation(field: keyof AllocationDraft, value: string | number): void { (this.allocationDraft as unknown as Record<string, string | number>)[field] = value; if (field === 'documentId') this.allocationDraft.itemId = ''; this.actionError.set(null); }

  derivedArDueDate(): string { const term = this.paymentTerms().find(item => item.id === this.arDraft.paymentTermId); const version = this.termVersion(term, this.arDraft.documentDate); if (!version || version.baseDateRule !== 'DocumentDate') return ''; const installment = version.scheduleMode === 'Installments' ? version.installments.at(-1) : undefined; const months = installment?.months ?? version.dueOffsetMonths; const days = installment?.days ?? version.dueOffsetDays; const date = new Date(`${this.arDraft.documentDate}T00:00:00Z`); date.setUTCMonth(date.getUTCMonth() + (months || 0)); date.setUTCDate(date.getUTCDate() + (days || 0)); return date.toISOString().slice(0, 10); }
  canCreateAr(): boolean { return !!this.companyId() && !!this.arDraft.customerId && !!this.arDraft.paymentTermId && !!this.derivedArDueDate() && !!this.arDraft.currencyCode.trim() && this.arDraft.amount > 0 && this.exchangeRateReady('ar'); }
  canCreateSettlement(): boolean { return !!this.companyId() && !!this.settlementDraft.partyId && !!this.settlementDraft.cashAccountId && !!this.settlementDraft.paymentMethodId && !!this.settlementDraft.externalReference.trim() && !!this.settlementDraft.currencyCode.trim() && this.settlementDraft.amount > 0 && this.exchangeRateReady('settlement'); }
  activeOptions(value: unknown[]): ReferenceOption[] { return (value as ReferenceOption[]).filter(item => !item.lifecycleState || item.lifecycleState === 'Active'); }
  private termVersion(term: PaymentTermOption | undefined, date: string): PaymentTermVersionOption | undefined { return term?.versions?.filter(version => version.effectiveFrom <= date && (!version.effectiveTo || version.effectiveTo >= date)).sort((left, right) => right.effectiveFrom.localeCompare(left.effectiveFrom))[0]; }

  async recognize(candidate: FinanceApSourceReady): Promise<void> { await this.runAction(() => this.finance.recognizeAp(candidate.sourceEvidenceId)); }
  async createReceivable(): Promise<void> { if (!this.canCreateAr()) return; const payload: FinanceManualReceivableRequest = { companyId: this.companyId(), customerId: this.arDraft.customerId, documentDate: this.arDraft.documentDate, dueDate: this.derivedArDueDate(), paymentTermId: this.arDraft.paymentTermId, currencyCode: this.arDraft.currencyCode.trim().toUpperCase(), amount: Number(this.arDraft.amount), ...this.fxFields('ar'), reference: this.arDraft.reference.trim() || null, description: this.arDraft.description.trim() || null }; await this.runAction(() => this.finance.createManualReceivable(payload)); }
  async createSettlement(): Promise<void> { if (!this.canCreateSettlement()) return; const payload = { companyId: this.companyId(), partyId: this.settlementDraft.partyId, cashAccountId: this.settlementDraft.cashAccountId, paymentMethodId: this.settlementDraft.paymentMethodId, documentDate: this.settlementDraft.documentDate, currencyCode: this.settlementDraft.currencyCode.trim().toUpperCase(), amount: Number(this.settlementDraft.amount), ...this.fxFields('settlement'), externalReference: this.settlementDraft.externalReference.trim(), description: this.settlementDraft.description.trim() || null }; await this.runAction(() => this.settlementDraft.direction === 'Payment' ? this.finance.createPayment(payload) : this.finance.createReceipt(payload)); }
  async settlementAction(document: FinanceSettlementDocument, action: 'submit' | 'approve' | 'reject' | 'post' | 'reverse'): Promise<void> { const direction = document.direction as 'Payment' | 'Receipt'; const promise = action === 'post' ? this.finance.postSettlement(direction, document.id, document.version) : action === 'reverse' ? this.finance.reverseSettlement(direction, document.id, 'Operator requested settlement reversal') : this.finance.settlementAction(direction, document.id, action, document.version, action === 'reject' ? 'Operator rejected settlement' : null); await this.runAction(() => promise); }
  postedDocuments(): FinanceSettlementDocument[] { return this.allDocuments().filter(document => document.status === 'Posted'); }
  settlementParties(): ReferenceOption[] { return this.settlementDraft.direction === 'Payment' ? this.suppliers() : this.customers(); }
  compatibleMethods(): FinancePaymentMethod[] { return this.methods().filter(method => method.lifecycle === 'Active' && method.isManual && (method.direction === 'Both' || method.direction === this.settlementDraft.direction)); }
  activeCashAccounts(): FinanceCashAccount[] { return this.cashAccounts().filter(account => account.lifecycle === 'Active' && account.currencyCode === this.settlementDraft.currencyCode.trim().toUpperCase()); }
  compatibleAllocationItems(): FinanceOpenItem[] { const document = this.allDocuments().find(item => item.id === this.allocationDraft.documentId); if (!document) return []; const items = document.direction === 'Payment' ? this.apItems() : this.arItems(); return items.filter(item => item.companyId === document.companyId && item.currencyCode === document.currencyCode && item.outstandingAmount > 0 && (document.direction === 'Payment' ? item.supplierId === document.supplierId : item.customerId === document.customerId)); }
  canCreateAllocation(): boolean { return !!this.allocationDraft.documentId && !!this.allocationDraft.itemId && this.allocationDraft.amount > 0 && !!this.allocationDraft.date; }
  async createAllocation(): Promise<void> { if (!this.canCreateAllocation()) return; await this.runAction(() => this.finance.createAllocation({ settlementDocumentId: this.allocationDraft.documentId, openItemId: this.allocationDraft.itemId, amount: Number(this.allocationDraft.amount), allocationDate: this.allocationDraft.date, reason: this.allocationDraft.reason.trim() || null })); }
  async reverseAllocation(allocation: FinanceAllocation): Promise<void> { await this.runAction(() => this.finance.reverseAllocation(allocation.id, allocation.version, this.allocationDraft.reason.trim() || 'Operator requested allocation reversal')); }
  private async runAction<T>(action: () => Promise<T>): Promise<void> { this.actionBusy.set(true); this.actionError.set(null); try { await action(); this.loadView(); } catch (error) { this.actionError.set(this.fxErrorMessage(error) ?? this.errorMessage(error)); } finally { this.actionBusy.set(false); } }
  private fxErrorMessage(error: unknown): string | null { const code = error instanceof HttpErrorResponse ? error.error?.code : null; const messages: Record<string, string> = { exact_exchange_rate_evidence_required: this.text('fxEvidenceRequired'), exchange_rate_evidence_mismatch: this.text('noExactExchangeRate'), fx_settlement_not_configured: this.text('fxSettlementNotConfigured'), functional_currency_rate_must_be_explicit_one: this.text('functionalCurrencyRateExplicitOne') }; return code && messages[code] ? messages[code] : null; }
  private errorMessage(error: unknown): string { const code = error instanceof HttpErrorResponse ? error.error?.code : null; const messages: Record<string, string> = { payment_method_not_supported: 'Only configured manual payment methods can be used.', payment_method_not_configured: 'No compatible active manual payment method is configured.', cash_account_not_configured: 'No compatible active Cash / Bank account is configured.', posting_rule_cash_account_mismatch: 'The selected Cash / Bank account is not the configured posting account.', posting_rule_control_account_mismatch: 'The allocation rule does not clear this item’s historical control account.', approval_policy_not_configured: 'Approval policy is not configured for this settlement.', approval_required: 'An authorized approval is required before posting.', self_approval_forbidden: 'The creator cannot approve this settlement.', pending_mapping: 'Finance posting mapping is pending.', ambiguous_mapping: 'Finance posting mapping is ambiguous.', payment_term_not_configured: 'A valid Payment Term is required and must be effective on the document date.', payment_term_snapshot_mismatch: 'The supplied due date does not match the server-derived Payment Term snapshot.', allocation_exceeds_outstanding: 'The allocation exceeds the open item balance.', allocation_exceeds_unallocated: 'The allocation exceeds the settlement balance.', active_allocations_require_reversal: 'Reverse active allocations before reversing this settlement.', concurrency_conflict: 'The record changed. Refresh the evidence and try again.', settlement_not_posted: 'Only a Posted settlement can be allocated.' }; return code && messages[code] ? messages[code] : 'The Finance operation was not completed. Refresh the evidence and try again.'; }
}
