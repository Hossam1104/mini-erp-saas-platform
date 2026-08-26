import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceAgingReportRow, FinanceCloseReconciliation, FinanceCompany, FinanceGeneralLedgerLine, FinanceStatementReport, FinanceTrialBalanceReport } from './finance.model';

type ReportTab = 'trial' | 'ledger' | 'ap' | 'ar' | 'pnl' | 'bs' | 'reconciliation';
type Bilingual = { en: string; ar: string };
const copy: Record<string, Bilingual> = {
  kicker: { en: 'Finance evidence / reports', ar: 'أدلة المالية / التقارير' },
  title: { en: 'Core reports from posted facts', ar: 'التقارير الأساسية من الحقائق المرحلة' },
  lead: { en: 'Trial balance, GL, aging, statements, and close reconciliation share the same tenant-scoped posted-journal evidence.', ar: 'يتشارك ميزان المراجعة والأستاذ العام وأعمار الديون والقوائم وتسوية الإقفال أدلة القيود المرحلة ضمن نطاق المستأجر.' },
  company: { en: 'Authorized Company', ar: 'الشركة المصرح بها' },
  asOf: { en: 'As of', ar: 'كما في' },
  from: { en: 'From', ar: 'من' },
  to: { en: 'To', ar: 'إلى' },
  run: { en: 'Run report', ar: 'تشغيل التقرير' },
  export: { en: 'CSV export', ar: 'تصدير CSV' },
  trial: { en: 'Trial balance', ar: 'ميزان المراجعة' },
  ledger: { en: 'General ledger', ar: 'الأستاذ العام' },
  ap: { en: 'AP aging', ar: 'أعمار الدائنين' },
  ar: { en: 'AR aging', ar: 'أعمار العملاء' },
  pnl: { en: 'Profit & loss', ar: 'الأرباح والخسائر' },
  bs: { en: 'Balance sheet', ar: 'الميزانية العمومية' },
  reconciliation: { en: 'Close reconciliation', ar: 'تسوية الإقفال' },
  empty: { en: 'Choose a company and run a report.', ar: 'اختر الشركة وشغّل التقرير.' },
  unavailable: { en: 'The Finance report is temporarily unavailable.', ar: 'تقرير المالية غير متاح مؤقتاً.' },
};

@Component({
  selector: 'app-finance-reports-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="reports-page" [attr.dir]="language.language() === 'ar' ? 'rtl' : 'ltr'">
      <header class="hero"><div><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lead">{{ text('lead') }}</p></div><a class="button" href="/app/finance/close">{{ text('reconciliation') }}</a></header>
      <section class="toolbar"><label><span>{{ text('company') }}</span><select [ngModel]="companyId()" (ngModelChange)="companyId.set($event)"><option value="">—</option>@for (company of companies(); track company.companyId) { <option [value]="company.companyId">{{ company.companyName }} · {{ company.functionalCurrencyCode }}</option> }</select></label><label><span>{{ text('asOf') }}</span><input type="date" [(ngModel)]="asOfDate" /></label><label><span>{{ text('from') }}</span><input type="date" [(ngModel)]="fromDate" /></label><label><span>{{ text('to') }}</span><input type="date" [(ngModel)]="toDate" /></label><button class="button button--primary" type="button" (click)="run()" [disabled]="busy()">{{ text('run') }}</button></section>
      <nav class="tabs" aria-label="Finance reports">@for (tab of tabs; track tab) { <button type="button" [class.active]="active() === tab" (click)="active.set(tab); run()">{{ text(tab) }}</button> }</nav>
      @if (error()) { <p class="error">{{ error() }}</p> }
      @if (!companyId()) { <section class="empty">{{ text('empty') }}</section> }
      @if (active() === 'trial' && trial()) { <section class="panel"><div class="panel-head"><h2>{{ text('trial') }}</h2><a class="button button--small" [href]="exportUrl('trial-balance')">{{ text('export') }}</a></div><p class="summary">{{ trial()!.functionalCurrencyCode }} · Debit {{ trial()!.totalDebit | number:'1.2-2' }} · Credit {{ trial()!.totalCredit | number:'1.2-2' }}</p><div class="table-wrap"><table><thead><tr><th>Account</th><th>Type</th><th>Opening</th><th>Debit</th><th>Credit</th><th>Closing</th></tr></thead><tbody>@for (row of trial()!.rows; track row.accountId) { <tr><td><strong>{{ row.accountCode }}</strong><small>{{ row.accountName }}</small></td><td>{{ row.accountType }}</td><td class="num">{{ row.openingBalance | number:'1.2-2' }}</td><td class="num">{{ row.periodDebit | number:'1.2-2' }}</td><td class="num">{{ row.periodCredit | number:'1.2-2' }}</td><td class="num">{{ row.closingBalance | number:'1.2-2' }}</td></tr> }</tbody></table></div></section> }
      @if (active() === 'ledger' && ledger()) { <section class="panel"><div class="panel-head"><h2>{{ text('ledger') }}</h2><a class="button button--small" [href]="exportUrl('general-ledger')">{{ text('export') }}</a></div><div class="table-wrap"><table><thead><tr><th>Date</th><th>Journal</th><th>Account</th><th>Debit</th><th>Credit</th><th>Running</th></tr></thead><tbody>@for (row of ledger()!; track row.journalId + row.accountCode + row.journalSequence) { <tr><td>{{ row.postingDate }}</td><td>{{ row.journalNumber }}<small>{{ row.sourceContract }}</small></td><td><strong>{{ row.accountCode }}</strong><small>{{ row.accountName }}</small></td><td class="num">{{ row.functionalDebit | number:'1.2-2' }}</td><td class="num">{{ row.functionalCredit | number:'1.2-2' }}</td><td class="num">{{ row.runningBalance | number:'1.2-2' }}</td></tr> }</tbody></table></div></section> }
      @if ((active() === 'ap' || active() === 'ar') && aging()) { <section class="panel"><div class="panel-head"><h2>{{ text(active()) }}</h2><a class="button button--small" [href]="exportUrl(active() === 'ap' ? 'ap-aging' : 'ar-aging')">{{ text('export') }}</a></div><div class="table-wrap"><table><thead><tr><th>Reference</th><th>Due</th><th>Bucket</th><th>Currency</th><th>Outstanding</th></tr></thead><tbody>@for (row of aging()!; track row.openItemId) { <tr><td>{{ row.sourceReference || '—' }}</td><td>{{ row.dueDate }}</td><td>{{ row.agingBucket }}</td><td>{{ row.currencyCode }}</td><td class="num">{{ row.outstandingAmount | number:'1.2-2' }}</td></tr> }</tbody></table></div></section> }
      @if ((active() === 'pnl' || active() === 'bs') && statement()) { <section class="panel"><div class="panel-head"><h2>{{ text(active()) }}</h2></div><p class="summary">{{ statement()!.functionalCurrencyCode }} · Debit {{ statement()!.totalDebit | number:'1.2-2' }} · Credit {{ statement()!.totalCredit | number:'1.2-2' }}</p><div class="table-wrap"><table><thead><tr><th>Account</th><th>Type</th><th>Opening</th><th>Debit</th><th>Credit</th><th>Closing</th></tr></thead><tbody>@for (row of statement()!.rows; track row.accountId) { <tr><td><strong>{{ row.accountCode }}</strong><small>{{ row.accountName }}</small></td><td>{{ row.accountType }}</td><td class="num">{{ row.openingBalance | number:'1.2-2' }}</td><td class="num">{{ row.debit | number:'1.2-2' }}</td><td class="num">{{ row.credit | number:'1.2-2' }}</td><td class="num">{{ row.closingBalance | number:'1.2-2' }}</td></tr> }</tbody></table></div></section> }
      @if (active() === 'reconciliation' && reconciliation()) { <section class="panel"><div class="panel-head"><h2>{{ reconciliation()!.overallStatus }}</h2></div>@for (item of reconciliation()!.items; track item.scope + item.sourceReference) { <div class="recon-row"><strong>{{ item.scope }}</strong><span>{{ item.status }}</span><small>{{ item.detail }}</small></div> } @if (!reconciliation()!.items.length) { <p class="muted">{{ text('empty') }}</p> }</section> }
      @if (companyId() && !resultAvailable()) { <section class="empty">{{ text('empty') }}</section> }
    </section>
  `,
  styles: [`:host{display:block}.reports-page{display:grid;gap:1.1rem}.hero{display:flex;justify-content:space-between;align-items:end;gap:1rem}.eyebrow{margin:0;color:var(--teal);font-size:.72rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}.hero h1{margin:.35rem 0 .55rem;font-size:clamp(2rem,4vw,3.6rem)}.lead{max-width:760px;color:var(--muted);line-height:1.6}.toolbar,.tabs{display:flex;align-items:end;gap:.75rem;flex-wrap:wrap;padding:1rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}label{display:grid;gap:.35rem;min-width:170px;flex:1}label span{color:var(--muted);font-size:.72rem;font-weight:800;letter-spacing:.06em;text-transform:uppercase}select,input{min-height:2.6rem;padding:.5rem .7rem;border:1px solid var(--line-strong);border-radius:9px;background:var(--surface);color:var(--ink);font:inherit}.button{display:inline-flex;align-items:center;justify-content:center;min-height:2.6rem;padding:.5rem .8rem;border:1px solid var(--line-strong);border-radius:9px;background:var(--surface);color:var(--ink);font:inherit;font-weight:750;text-decoration:none;cursor:pointer}.button--primary{border-color:var(--teal);background:var(--ink);color:#fff}.button--small{min-height:2rem;padding:.25rem .6rem;font-size:.8rem}.button:disabled{opacity:.5}.tabs{overflow-x:auto;padding:.35rem}.tabs button{padding:.7rem .8rem;border:0;border-bottom:3px solid transparent;background:transparent;color:var(--muted);font:inherit;font-weight:750;white-space:nowrap;cursor:pointer}.tabs button.active{border-bottom-color:var(--teal);color:var(--ink)}.panel,.empty{padding:1.15rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}.empty{min-height:180px;display:grid;place-items:center;color:var(--muted)}.panel-head{display:flex;justify-content:space-between;align-items:center;gap:1rem;margin-bottom:.8rem}.panel h2{margin:0}.summary{color:var(--muted)}.table-wrap{overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{padding:.75rem .6rem;border-bottom:1px solid var(--line);text-align:start;vertical-align:top}th{color:var(--muted);font-size:.7rem;text-transform:uppercase;letter-spacing:.06em}td small{display:block;margin-top:.18rem;color:var(--muted)}.num{text-align:end;font-variant-numeric:tabular-nums}.recon-row{display:grid;grid-template-columns:1fr auto;gap:.25rem .8rem;padding:.75rem 0;border-bottom:1px solid var(--line)}.recon-row small{grid-column:1/-1;color:var(--muted)}.muted{color:var(--muted)}.error{padding:.75rem 1rem;border:1px solid #d98c8c;border-radius:9px;color:#9d3f3f;background:#fff4f4}@media(max-width:800px){.hero{align-items:stretch;flex-direction:column}.toolbar{align-items:stretch;flex-direction:column}label{width:100%}}`],
})
export class FinanceReportsWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(FinanceService);
  readonly tabs: ReportTab[] = ['trial', 'ledger', 'ap', 'ar', 'pnl', 'bs', 'reconciliation'];
  readonly companies = signal<FinanceCompany[]>([]);
  readonly companyId = signal('');
  readonly active = signal<ReportTab>('trial');
  readonly trial = signal<FinanceTrialBalanceReport | null>(null);
  readonly ledger = signal<FinanceGeneralLedgerLine[] | null>(null);
  readonly aging = signal<FinanceAgingReportRow[] | null>(null);
  readonly statement = signal<FinanceStatementReport | null>(null);
  readonly reconciliation = signal<FinanceCloseReconciliation | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  asOfDate = this.today();
  fromDate = `${new Date().getUTCFullYear()}-01-01`;
  toDate = this.today();

  ngOnInit(): void { this.service.companies().subscribe({ next: (items) => { this.companies.set(items); if (items[0]) { this.companyId.set(items[0].companyId); this.run(); } }, error: () => this.error.set(this.text('unavailable')) }); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  run(): void { const companyId = this.companyId(); if (!companyId) return; this.busy.set(true); this.error.set(null); const tab = this.active(); const done = () => this.busy.set(false); if (tab === 'trial') this.service.trialBalance(companyId, this.asOfDate).subscribe({ next: (item) => { this.trial.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'ledger') this.service.generalLedger(companyId, this.fromDate, this.toDate).subscribe({ next: (item) => { this.ledger.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'ap' || tab === 'ar') this.service.reportAging(companyId, this.asOfDate, tab === 'ap' ? 'Payable' : 'Receivable').subscribe({ next: (item) => { this.aging.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'pnl' || tab === 'bs') this.service.statement(companyId, this.fromDate, this.toDate, tab === 'pnl' ? 'profit-loss' : 'balance-sheet').subscribe({ next: (item) => { this.statement.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else this.service.closeReconciliation(companyId, this.asOfDate).subscribe({ next: (item) => { this.reconciliation.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); }
  exportUrl(report: string): string { const params = new URLSearchParams({ companyId: this.companyId() }); if (report === 'general-ledger') { params.set('fromDate', this.fromDate); params.set('toDate', this.toDate); } else { params.set('asOfDate', this.asOfDate); } return `/api/v1/finance/reports/${report}/export?${params.toString()}`; }
  resultAvailable(): boolean { return this.active() === 'trial' ? !!this.trial() : this.active() === 'ledger' ? !!this.ledger() : this.active() === 'ap' || this.active() === 'ar' ? !!this.aging() : this.active() === 'reconciliation' ? !!this.reconciliation() : !!this.statement(); }
  private today(): string { return new Date().toISOString().slice(0, 10); }
}
