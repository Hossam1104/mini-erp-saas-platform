import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceSettlementWorkspaceComponent } from './finance-settlement-workspace.component';

describe('FinanceSettlementWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceSettlementWorkspaceComponent>;

  const companies = [{
    tenantId: 'tenant-a',
    companyId: 'company-a',
    companyName: 'Alpha Company',
    functionalCurrencyCode: 'SAR',
    branchId: null,
    isActive: true,
  }];

  const openItem = {
    id: 'open-item-a',
    companyId: 'company-a',
    kind: 'Payable',
    partyId: 'supplier-a',
    reference: 'PI-1001',
    documentDate: '2026-08-01',
    dueDate: '2026-08-31',
    currencyCode: 'SAR',
    originalAmount: 1250,
    allocatedAmount: 0,
    outstandingAmount: 1250,
    sourceContract: 'procurement-supplier-invoice.v1',
    sourceIdentity: 'match-a',
    recognitionState: 'Recognized',
    status: 'Open',
    recognitionJournalId: 'journal-a',
    version: 'AQ==',
  };

  beforeEach(() => {
    const empty = () => of([]);
    TestBed.configureTestingModule({
      imports: [FinanceSettlementWorkspaceComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { url: [{ path: 'ap' }] } } },
        {
          provide: FinanceService,
          useValue: {
            companies: () => of(companies),
            apOpenItems: () => of([openItem]),
            apAging: () => of([{ openItemId: 'open-item-a', reference: 'PI-1001', dueDate: '2026-08-31', daysOverdue: 0, outstandingAmount: 1250, currencyCode: 'SAR', status: 'Open' }]),
            arOpenItems: empty,
            arAging: empty,
            paymentMethods: empty,
            cashAccounts: empty,
            payments: empty,
            receipts: empty,
            allocations: empty,
            reconciliation: empty,
          },
        },
      ],
    });
    fixture = TestBed.createComponent(FinanceSettlementWorkspaceComponent);
    fixture.detectChanges();
  });

  it('renders the Company-scoped AP open item and aging evidence', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="finance-settlement-workspace"]')).not.toBeNull();
    expect(element.querySelector('select')?.textContent).toContain('Alpha Company');
    expect(element.querySelector('h1')?.textContent).toContain('Accounts Payable');
    expect(element.textContent).toContain('PI-1001');
    expect(element.textContent).toContain('procurement-supplier-invoice.v1');
    expect(element.textContent).toContain('Aging');
  });

  it('keeps settlement navigation and copy RTL-safe for Arabic', () => {
    const englishTitle = fixture.nativeElement.querySelector('h1')?.textContent;
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(language.language()).toBe('ar');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).not.toBe(englishTitle);
    language.setLanguage('en');
  });
});
