import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceTaxFxWorkspaceComponent } from './finance-tax-fx-workspace.component';

const policy = {
  id: 'policy-a', tenantId: 'tenant-a', companyId: 'company-a', functionalCurrencyCode: 'SAR',
  reportingCurrencyId: 'currency-usd', reportingCurrencyCode: 'USD', roundingScale: 2,
  roundingMode: 'AwayFromZero', revaluationEnabled: true, effectiveFrom: '2026-01-01',
  effectiveTo: null, versionNumber: 1, version: 'AQ==',
};

const taxEffect = {
  id: 'effect-a', companyId: 'company-a', openItemId: 'open-item-a', kind: 'Receivable', taxId: 'tax-a',
  taxCode: 'VAT15', taxRateVersionId: 'tax-rate-a', taxRateVersionNumber: 1, taxEffectiveOn: '2026-01-01',
  taxRatePercentage: 15, taxableBase: 100, taxAmount: 15, transactionCurrencyCode: 'SAR', functionalAmount: 15,
  functionalCurrencyCode: 'SAR', journalId: 'journal-tax-a', reversalJournalId: null, postingRuleId: 'rule-a',
  postingRuleVersionNumber: 1, monetaryEvidence: {
    transactionCurrencyCode: 'SAR', transactionAmount: 15, functionalCurrencyCode: 'SAR', functionalAmount: 15,
    reportingCurrencyCode: 'USD', reportingAmount: 4, transactionToFunctionalRate: null,
    functionalToReportingRate: { id: 'rate-a', versionId: 'rate-version-a', versionNumber: 1, rate: 3.75 },
    sourceUnroundedFunctionalAmount: 15, sourceUnroundedReportingAmount: 4, roundingScale: 2,
    roundingMode: 'AwayFromZero', functionalRoundingDifference: 0, reportingRoundingDifference: 0,
    reportingEvidenceStatus: 'Captured',
  }, status: 'Posted', createdAt: '2026-08-26T00:00:00Z', version: 'AQ==',
};

const revaluation = {
  id: 'batch-a', companyId: 'company-a', asOfDate: '2026-08-25', scope: 'AP_AR_AND_UNALLOCATED_SETTLEMENTS',
  status: 'Draft', lines: [], version: 'AQ==',
};

describe('FinanceTaxFxWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceTaxFxWorkspaceComponent>;
  let service: any;

  beforeEach(() => {
    service = {
      companies: vi.fn(() => of([{ tenantId: 'tenant-a', companyId: 'company-a', companyName: 'Alpha Company', functionalCurrencyCode: 'SAR', branchId: null, isActive: true }])),
      currencies: vi.fn(() => of([{ id: 'currency-usd', code: 'USD', englishName: 'US Dollar', arabicName: null, lifecycleState: 'Active', version: 'AQ==' }])),
      taxes: vi.fn(() => of([{ id: 'tax-a', code: 'VAT15', englishName: 'VAT 15%', arabicName: null, lifecycleState: 'Active', version: 'AQ==' }])),
      monetaryPolicies: vi.fn(() => of([policy])),
      taxEffects: vi.fn(() => of([taxEffect])),
      apOpenItems: vi.fn(() => of([{ id: 'open-item-a', companyId: 'company-a', kind: 'Receivable', partyId: 'customer-a', reference: 'AR-1001', documentDate: '2026-08-01', dueDate: '2026-08-31', currencyCode: 'SAR', originalAmount: 100, allocatedAmount: 0, outstandingAmount: 100, sourceContract: 'manual-ar.v1', recognitionState: 'Recognized', status: 'Open', recognitionJournalId: 'journal-ar-a', version: 'AQ==' }])),
      arOpenItems: vi.fn(() => of([])),
      revaluationBatches: vi.fn(() => of([revaluation])),
      fxReconciliation: vi.fn(() => of([{ allocationId: 'allocation-a', companyId: 'company-a', realizedDifference: 2, postedDifference: 2, direction: 'Gain', status: 'Reconciled', journalId: 'journal-fx-a', openItemId: 'open-item-a', settlementDocumentId: null, reversalJournalId: null, expectedAccountId: 'account-fx', ruleId: 'rule-fx', ruleVersionNumber: 1, statusReason: null }])),
      unrealizedFxReconciliation: vi.fn(() => of([{ lineId: 'line-a', batchId: 'batch-a', companyId: 'company-a', sourceId: 'open-item-a', sourceType: 'AR', expectedAmount: 3, postedAmount: 3, direction: 'Gain', status: 'Reconciled', journalId: 'journal-unrealized-a', reversalJournalId: null, expectedAccountId: 'account-fx', postingRuleId: 'rule-fx', postingRuleVersionNumber: 1, statusReason: null }])),
      reportingCurrencyReconciliation: vi.fn(() => of([{ journalId: 'journal-tax-a', companyId: 'company-a', functionalCurrencyCode: 'SAR', functionalAmount: 15, reportingCurrencyCode: 'USD', reportingAmount: 4, expectedReportingAmount: 4, exchangeRateId: 'rate-a', exchangeRateVersionId: 'rate-version-a', exchangeRateVersionNumber: 1, status: 'Reconciled', effectId: 'effect-a', statusReason: null }])),
      createMonetaryPolicy: vi.fn().mockResolvedValue(policy),
      previewTax: vi.fn().mockResolvedValue(taxEffect),
      postTax: vi.fn().mockResolvedValue(taxEffect),
      reverseTax: vi.fn().mockResolvedValue(taxEffect),
      createRevaluation: vi.fn().mockResolvedValue(revaluation),
      calculateRevaluation: vi.fn().mockResolvedValue({ ...revaluation, status: 'Calculated' }),
      postRevaluation: vi.fn().mockResolvedValue({ ...revaluation, status: 'Posted' }),
      reverseRevaluation: vi.fn().mockResolvedValue({ ...revaluation, status: 'Reversed' }),
    };

    TestBed.configureTestingModule({
      imports: [FinanceTaxFxWorkspaceComponent],
      providers: [provideRouter([]), { provide: FinanceService, useValue: service }],
    });
    fixture = TestBed.createComponent(FinanceTaxFxWorkspaceComponent);
    TestBed.inject(LanguageService).setLanguage('en');
    fixture.detectChanges();
  });

  async function settle(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  it('renders the authorized Company, functional currency, and policy evidence', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('select')?.textContent).toContain('Alpha Company');
    expect(element.textContent).toContain('SAR');
    expect(element.textContent).toContain('USD');
    expect(element.textContent).toContain('Effective-dated reporting');
  });

  it('loads all durable reconciliation feeds for the selected Company', () => {
    expect(service.fxReconciliation).toHaveBeenCalledWith('company-a');
    expect(service.unrealizedFxReconciliation).toHaveBeenCalledWith('company-a');
    expect(service.reportingCurrencyReconciliation).toHaveBeenCalledWith('company-a');
    const component = fixture.componentInstance;
    component.tab.set('revaluation');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="realized-fx-reconciliation"]')?.textContent).toContain('journal-fx-a');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="unrealized-fx-reconciliation"]')?.textContent).toContain('AR · open-item-a');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="reporting-currency-reconciliation"]')?.textContent).toContain('journal-tax-a');
  });

  it('saves the effective-dated monetary policy with the selected reporting currency', async () => {
    const component = fixture.componentInstance;
    component.policyDraft = { reportingCurrencyId: 'currency-usd', roundingScale: 3, roundingMode: 'ToEven', revaluationEnabled: false, effectiveFrom: '2026-09-01', effectiveTo: null };
    component.savePolicy();
    await settle();
    expect(service.createMonetaryPolicy).toHaveBeenCalledWith({ companyId: 'company-a', reportingCurrencyId: 'currency-usd', roundingScale: 3, roundingMode: 'ToEven', revaluationEnabled: false, effectiveFrom: '2026-09-01', effectiveTo: null });
  });

  it('previews and posts tax through the same selected recognized source', async () => {
    const component = fixture.componentInstance;
    component.taxDraft = { openItemId: 'open-item-a', taxId: 'tax-a', taxableBase: 100 };
    component.previewTax();
    await settle();
    expect(service.previewTax).toHaveBeenCalledWith({ companyId: 'company-a', openItemId: 'open-item-a', taxId: 'tax-a', taxableBase: 100, sourceLineage: 'finance-tax-workspace' });
    expect(component.preview()?.monetaryEvidence.reportingAmount).toBe(4);
    component.postTax();
    await settle();
    expect(service.postTax).toHaveBeenCalledWith({ companyId: 'company-a', openItemId: 'open-item-a', taxId: 'tax-a', taxableBase: 100, sourceLineage: 'finance-tax-workspace' });
  });

  it('blocks tax reversal without a reason and sends the reason with immutable version evidence', async () => {
    const component = fixture.componentInstance;
    component.reverseTax(taxEffect as any);
    expect(service.reverseTax).not.toHaveBeenCalled();
    expect(component.error()).toContain('reason');
    component.taxReverseReason = 'Corrected source evidence';
    component.reverseTax(taxEffect as any);
    await settle();
    expect(service.reverseTax).toHaveBeenCalledWith('effect-a', 'AQ==', 'Corrected source evidence');
    expect(component.taxReverseReason).toBe('');
  });

  it('creates only the approved explicit revaluation scope and protects every action by version', async () => {
    const component = fixture.componentInstance;
    component.revaluationDraft = { asOfDate: '2026-08-25', scope: component.revaluationScope };
    component.createRevaluation();
    await settle();
    expect(service.createRevaluation).toHaveBeenCalledWith({ companyId: 'company-a', asOfDate: '2026-08-25', scope: 'AP_AR_AND_UNALLOCATED_SETTLEMENTS' });
    component.calculate(revaluation as any);
    component.post(revaluation as any);
    await settle();
    expect(service.calculateRevaluation).toHaveBeenCalledWith('batch-a', 'AQ==');
    expect(service.postRevaluation).toHaveBeenCalledWith('batch-a', 'AQ==');
    component.reverse(revaluation as any);
    expect(service.reverseRevaluation).not.toHaveBeenCalled();
    component.revaluationReverseReason = 'Re-run after source correction';
    component.reverse(revaluation as any);
    await settle();
    expect(service.reverseRevaluation).toHaveBeenCalledWith('batch-a', 'AQ==', 'Re-run after source correction');
  });

  it('maps server evidence failures to actionable bilingual-safe messages', () => {
    const component = fixture.componentInstance as any;
    expect(component.errorCode({ error: { code: 'reporting_exchange_rate_required' } })).toContain('exact historical');
    expect(component.errorCode({ error: { code: 'tax_evidence_ambiguous' } })).toContain('ambiguous');
    expect(component.errorCode({ error: { code: 'revaluation_source_changed' } })).toContain('calculate again');
    expect(component.errorCode({ error: { code: 'reporting_evidence_invalid' } })).toContain('invalid');
  });

  it('keeps the tax and FX workspace RTL-safe in Arabic', () => {
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('الضرائب');
    expect(fixture.nativeElement.querySelector('.tax-fx-page')?.getAttribute('dir')).toBe('rtl');
    language.setLanguage('en');
  });

  it('renders the no-authorized-company state without inventing a workspace identifier', () => {
    const component = fixture.componentInstance;
    component.companies.set([]);
    component.companyId.set('');
    component.loading.set(false);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No authorized Company context');
    expect((fixture.nativeElement as HTMLElement).querySelector('input[name="workspace"]')).toBeNull();
  });
});
