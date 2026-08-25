import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
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
            apSourceReady: () => of([{ sourceEvidenceId: 'source-a', companyId: 'company-a', supplierId: 'supplier-a', supplierCode: 'SUP-1', supplierName: 'Supplier One', supplierInvoiceReference: 'PI-READY-1', invoiceDate: '2026-08-01', currencyCode: 'SAR', amount: 1250, dueDate: '2026-08-31', paymentTerm: { code: 'NET30', englishName: 'Net 30', arabicName: null, versionNumber: 1, dueDate: '2026-08-31' }, matchResult: 'ExactMatch', alreadyRecognized: false, sourceEvidenceVersion: 1 }]),
            recognizeAp: vi.fn().mockResolvedValue(openItem),
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

  it('renders an eligible AP source-ready candidate with a recognition action', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="ap-source-ready"]')).not.toBeNull();
    expect(element.textContent).toContain('PI-READY-1');
    expect(element.textContent).toContain('Recognize payable');
    expect(element.querySelectorAll('input[type="text"]').length).toBe(0);
  });

  it('renders manual AR with selector-based customer and Payment Term fields', async () => {
    const component = fixture.componentInstance;
    component.view.set('ar');
    component.load();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="ar-customer-select"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="ar-payment-term-select"]')).not.toBeNull();
    expect(element.querySelector('input[readonly]')).not.toBeNull();
  });
});
