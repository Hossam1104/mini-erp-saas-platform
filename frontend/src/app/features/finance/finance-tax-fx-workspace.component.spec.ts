import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceTaxFxWorkspaceComponent } from './finance-tax-fx-workspace.component';

describe('FinanceTaxFxWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceTaxFxWorkspaceComponent>;

  beforeEach(() => {
    const empty = () => of([]);
    TestBed.configureTestingModule({
      imports: [FinanceTaxFxWorkspaceComponent],
      providers: [provideRouter([]), {
        provide: FinanceService,
        useValue: {
          companies: () => of([{ tenantId: 'tenant-a', companyId: 'company-a', companyName: 'Alpha Company', functionalCurrencyCode: 'SAR', branchId: null, isActive: true }]),
          currencies: empty,
          taxes: () => of([{ id: 'tax-a', code: 'VAT15', englishName: 'VAT 15%', arabicName: null, lifecycleState: 'Active', version: 'AQ==' }]),
          monetaryPolicies: () => of([{ id: 'policy-a', tenantId: 'tenant-a', companyId: 'company-a', functionalCurrencyCode: 'SAR', reportingCurrencyId: 'currency-usd', reportingCurrencyCode: 'USD', roundingScale: 2, roundingMode: 'AwayFromZero', revaluationEnabled: true, effectiveFrom: '2026-01-01', effectiveTo: null, versionNumber: 1, version: 'AQ==' }]),
          taxEffects: empty,
          apOpenItems: empty,
          arOpenItems: empty,
          revaluationBatches: empty,
        },
      }],
    });
    fixture = TestBed.createComponent(FinanceTaxFxWorkspaceComponent);
    fixture.detectChanges();
  });

  it('renders the authorized Company, functional currency, and policy evidence', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('select')?.textContent).toContain('Alpha Company');
    expect(element.textContent).toContain('SAR');
    expect(element.textContent).toContain('USD');
    expect(element.textContent).toContain('Effective-dated reporting');
  });

  it('keeps the tax and FX workspace RTL-safe in Arabic', () => {
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('الضرائب');
    language.setLanguage('en');
  });
});
