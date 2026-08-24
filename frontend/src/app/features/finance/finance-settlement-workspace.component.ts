import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceAgingRow, FinanceCashAccount, FinanceCompany, FinanceOpenItem, FinancePaymentMethod, FinanceReconciliation, FinanceSettlementDocument, FinanceAllocation } from './finance.model';

type SettlementView = 'ap' | 'ar' | 'settlements';
type Bilingual = { en: string; ar: string };

const copy: Record<string, Bilingual> = {
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
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('apTitle') }}</p><h2>{{ text('apLead') }}</h2></div><span class="count">{{ openItems().length }}</span></div>
          @if (openItems().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('supplier') }}</th><th>{{ text('reference') }}</th><th>{{ text('invoiceDate') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th><th>{{ text('source') }}</th></tr></thead><tbody>@for (item of openItems(); track item.id) { <tr><td>{{ text('supplier') }}</td><td><strong>{{ item.reference || '—' }}</strong></td><td>{{ item.documentDate }}</td><td>{{ item.dueDate }}</td><td>{{ item.currencyCode }}</td><td class="numeric">{{ item.originalAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.outstandingAmount | number:'1.2-2' }}</td><td><span class="status" [class.active]="item.status === 'Open' || item.status === 'PartiallySettled'">{{ item.status }}</span></td><td>{{ item.sourceContract }}<small>{{ item.recognitionJournalId ? text('journal') : text('pending') }}</small></td></tr> }</tbody></table></div> }
        </section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('aging') }}</p><h2>{{ text('dueDate') }} / {{ text('daysOverdue') }}</h2></div></div>@if (aging().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('reference') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('daysOverdue') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of aging(); track row.openItemId) { <tr><td>{{ row.reference || '—' }}</td><td>{{ row.dueDate }}</td><td>{{ row.daysOverdue }}</td><td class="numeric">{{ row.outstandingAmount | number:'1.2-2' }} {{ row.currencyCode }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div> }</section>
      } @else if (view() === 'ar') {
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('arTitle') }}</p><h2>{{ text('arLead') }}</h2></div><span class="count">{{ openItems().length }}</span></div>
          @if (openItems().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('customer') }}</th><th>{{ text('reference') }}</th><th>{{ text('invoiceDate') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (item of openItems(); track item.id) { <tr><td>{{ text('customer') }}</td><td><strong>{{ item.reference || '—' }}</strong></td><td>{{ item.documentDate }}</td><td>{{ item.dueDate }}</td><td>{{ item.currencyCode }}</td><td class="numeric">{{ item.originalAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ item.outstandingAmount | number:'1.2-2' }}</td><td><span class="status" [class.active]="item.status === 'Open' || item.status === 'PartiallySettled'">{{ item.status }}</span></td></tr> }</tbody></table></div> }
        </section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('aging') }}</p><h2>{{ text('dueDate') }} / {{ text('daysOverdue') }}</h2></div></div>@if (aging().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <div class="table-wrap"><table><thead><tr><th>{{ text('reference') }}</th><th>{{ text('dueDate') }}</th><th>{{ text('daysOverdue') }}</th><th>{{ text('outstanding') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of aging(); track row.openItemId) { <tr><td>{{ row.reference || '—' }}</td><td>{{ row.dueDate }}</td><td>{{ row.daysOverdue }}</td><td class="numeric">{{ row.outstandingAmount | number:'1.2-2' }} {{ row.currencyCode }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div> }</section>
      } @else {
        <section class="settlement-cards"><article><span>{{ text('payment') }}</span><strong>{{ payments().length }}</strong><small>{{ text('unallocated') }} is retained explicitly.</small></article><article><span>{{ text('receipt') }}</span><strong>{{ receipts().length }}</strong><small>{{ text('unallocated') }} is retained explicitly.</small></article><article><span>{{ text('method') }}</span><strong>{{ methods().length }}</strong><small>{{ text('method') }} is Company-owned and configurable.</small></article><article><span>{{ text('cashAccount') }}</span><strong>{{ cashAccounts().length }}</strong><small>{{ text('cashAccount') }} links to configured GL accounts.</small></article></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('payment') }} / {{ text('receipt') }}</p><h2>{{ text('settlementLead') }}</h2></div></div><div class="table-wrap"><table><thead><tr><th>{{ text('status') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('allocated') }}</th><th>{{ text('unallocated') }}</th><th>{{ text('journal') }}</th></tr></thead><tbody>@for (document of allDocuments(); track document.id) { <tr><td><span class="status" [class.active]="document.status === 'Posted'">{{ document.direction === 'Payment' ? text('payment') : text('receipt') }} · {{ document.status }}</span></td><td>{{ document.currencyCode }}</td><td class="numeric">{{ document.amount | number:'1.2-2' }}</td><td class="numeric">{{ document.allocatedAmount | number:'1.2-2' }}</td><td class="numeric">{{ document.unallocatedAmount | number:'1.2-2' }}</td><td>{{ document.postedJournalId ? text('postedJournal') : text('pending') }}</td></tr> }</tbody></table></div></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('allocations') }}</p><h2>{{ text('reconciliation') }}</h2></div><span class="count">{{ allocations().length }}</span></div><div class="table-wrap"><table><thead><tr><th>{{ text('allocations') }}</th><th>{{ text('currency') }}</th><th>{{ text('original') }}</th><th>{{ text('status') }}</th><th>{{ text('journal') }}</th></tr></thead><tbody>@for (allocation of allocations(); track allocation.id) { <tr><td>{{ allocation.status === 'Reversed' ? text('pending') : text('allocations') }}</td><td>{{ allocation.currencyCode }}</td><td class="numeric">{{ allocation.amount | number:'1.2-2' }}</td><td>{{ allocation.status }}</td><td>{{ allocation.journalId ? text('postedJournal') : text('pending') }}</td></tr> }</tbody></table></div></section>
        <section class="settlement-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('reconciliation') }}</p><h2>{{ text('subledger') }} / {{ text('postedJournal') }}</h2></div></div><div class="table-wrap"><table><thead><tr><th>{{ text('reconciliation') }}</th><th>{{ text('subledger') }}</th><th>{{ text('postedJournal') }}</th><th>{{ text('difference') }}</th><th>{{ text('status') }}</th></tr></thead><tbody>@for (row of reconciliation(); track row.scope) { <tr><td>{{ row.scope }}</td><td class="numeric">{{ row.subledgerAmount | number:'1.2-2' }}</td><td class="numeric">{{ row.postedJournalAmount | number:'1.2-2' }}</td><td class="numeric">{{ row.difference | number:'1.2-2' }}</td><td>{{ row.status }}</td></tr> }</tbody></table></div></section>
      }
    </section>
  `,
  styles: [`
    :host { display:block; } .settlement-page { display:grid; gap:1rem; } .settlement-header { display:flex; align-items:end; justify-content:space-between; gap:1rem; } h1 { margin:.2rem 0 .55rem; } .lede { max-width:850px; color:var(--muted); line-height:1.6; } .settlement-nav { display:flex; gap:.35rem; overflow:auto; border-bottom:1px solid var(--line); } .settlement-nav a { padding:.75rem .85rem; color:var(--muted); font-weight:750; white-space:nowrap; text-decoration:none; border-bottom:3px solid transparent; } .settlement-nav a.is-active { color:var(--ink); border-bottom-color:var(--teal); } .settlement-controlbar { display:flex; align-items:end; justify-content:space-between; gap:1rem; padding:1rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } label { display:grid; gap:.4rem; min-width:min(100%,380px); } label span { color:var(--muted); font-size:.73rem; font-weight:750; letter-spacing:.08em; text-transform:uppercase; } select { min-height:2.65rem; padding:.55rem .7rem; border:1px solid var(--line-strong); border-radius:9px; background:var(--surface); color:var(--ink); font:inherit; } .currency-chip { padding:.7rem 1rem; border-radius:10px; background:var(--mint); color:var(--teal); font-weight:800; } .settlement-panel, .settlement-state { padding:1.35rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } .settlement-state { min-height:180px; display:grid; place-content:center; text-align:center; gap:.5rem; } .settlement-state--error { border-color:color-mix(in srgb,var(--danger) 45%,var(--line)); } .panel-heading { display:flex; justify-content:space-between; align-items:start; gap:1rem; margin-bottom:1rem; } .panel-heading h2 { margin:.25rem 0 0; max-width:920px; font-size:1.15rem; } .count { min-width:2rem; padding:.3rem .55rem; border-radius:999px; background:var(--mint); color:var(--teal); font-weight:800; text-align:center; } .settlement-cards { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:.9rem; } .settlement-cards article { display:grid; gap:.5rem; min-height:125px; padding:1.1rem; border:1px solid var(--line); border-radius:14px; background:var(--surface); box-shadow:var(--shadow-sm); } .settlement-cards span { color:var(--muted); font-size:.75rem; font-weight:800; text-transform:uppercase; } .settlement-cards strong { font-size:2rem; } .settlement-cards small, .empty-copy, td small { color:var(--muted); line-height:1.45; } .table-wrap { overflow:auto; } table { width:100%; border-collapse:collapse; } th,td { padding:.75rem .6rem; border-bottom:1px solid var(--line); text-align:start; vertical-align:top; white-space:nowrap; } th { color:var(--muted); font-size:.7rem; text-transform:uppercase; letter-spacing:.06em; } td small { display:block; margin-top:.2rem; } .numeric { text-align:end; font-variant-numeric:tabular-nums; } .status { display:inline-block; padding:.22rem .52rem; border-radius:999px; background:#f3eee5; color:var(--muted); font-size:.78rem; font-weight:750; } .status.active { background:var(--mint); color:var(--teal); } @media (max-width:900px) { .settlement-header,.settlement-controlbar { align-items:stretch; flex-direction:column; } .settlement-cards { grid-template-columns:repeat(2,minmax(0,1fr)); } } @media (max-width:560px) { .settlement-cards { grid-template-columns:1fr; } .settlement-panel { padding:1rem; } }
  `],
})
export class FinanceSettlementWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly finance = inject(FinanceService);
  private readonly route = inject(ActivatedRoute);
  readonly view = signal<SettlementView>((this.route.snapshot.url[0]?.path as SettlementView) || 'settlements');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly companies = signal<FinanceCompany[]>([]);
  readonly companyId = signal('');
  readonly openItems = signal<FinanceOpenItem[]>([]);
  readonly aging = signal<FinanceAgingRow[]>([]);
  readonly methods = signal<FinancePaymentMethod[]>([]);
  readonly cashAccounts = signal<FinanceCashAccount[]>([]);
  readonly payments = signal<FinanceSettlementDocument[]>([]);
  readonly receipts = signal<FinanceSettlementDocument[]>([]);
  readonly allocations = signal<FinanceAllocation[]>([]);
  readonly reconciliation = signal<FinanceReconciliation[]>([]);

  ngOnInit(): void { this.load(); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  title(): string { return this.text(this.view() === 'ap' ? 'apTitle' : this.view() === 'ar' ? 'arTitle' : 'settlementTitle'); }
  lead(): string { return this.text(this.view() === 'ap' ? 'apLead' : this.view() === 'ar' ? 'arLead' : 'settlementLead'); }
  selectedCurrency(): string { return this.companies().find(company => company.companyId === this.companyId())?.functionalCurrencyCode ?? '—'; }
  allDocuments(): FinanceSettlementDocument[] { return [...this.payments(), ...this.receipts()].sort((a, b) => b.documentDate.localeCompare(a.documentDate)); }
  selectCompany(id: string): void { this.companyId.set(id); this.loadView(); }

  load(): void { this.loading.set(true); this.error.set(null); this.finance.companies().subscribe({ next: companies => { this.companies.set(companies); if (!this.companyId() || !companies.some(company => company.companyId === this.companyId())) this.companyId.set(companies[0]?.companyId ?? ''); this.loadView(); }, error: () => { this.loading.set(false); this.error.set(this.text('unavailable')); } }); }
  private loadView(): void {
    const companyId = this.companyId(); if (!companyId) { this.loading.set(false); return; } this.loading.set(true);
    if (this.view() === 'ap') {
      forkJoin({ items: this.finance.apOpenItems(companyId), aging: this.finance.apAging(companyId) }).subscribe({ next: result => { this.openItems.set(result.items); this.aging.set(result.aging); this.loading.set(false); }, error: () => this.failed() }); return;
    }
    if (this.view() === 'ar') {
      forkJoin({ items: this.finance.arOpenItems(companyId), aging: this.finance.arAging(companyId) }).subscribe({ next: result => { this.openItems.set(result.items); this.aging.set(result.aging); this.loading.set(false); }, error: () => this.failed() }); return;
    }
    forkJoin({ methods: this.finance.paymentMethods(companyId), cashAccounts: this.finance.cashAccounts(companyId), payments: this.finance.payments(companyId), receipts: this.finance.receipts(companyId), allocations: this.finance.allocations(companyId), reconciliation: this.finance.reconciliation(companyId) }).subscribe({ next: result => { this.methods.set(result.methods); this.cashAccounts.set(result.cashAccounts); this.payments.set(result.payments); this.receipts.set(result.receipts); this.allocations.set(result.allocations); this.reconciliation.set(result.reconciliation); this.loading.set(false); }, error: () => this.failed() });
  }
  private failed(): void { this.loading.set(false); this.error.set(this.text('unavailable')); }
}
