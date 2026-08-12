import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyRecord, CustomerRecord, MasterDataRecord, PaymentTermRecord, SupplierRecord } from './master-data.models';
import { MasterDataService } from './master-data.service';
import { MasterDataWorkspaceComponent } from './master-data-workspace.component';

@Component({ standalone: true, template: '' })
class RouterTargetComponent {}

const category: MasterDataRecord = {
  id: 'category-1',
  tenantId: 'tenant-a',
  code: 'CAT-01',
  englishName: 'Materials',
  arabicName: 'مواد',
  parentCategoryId: null,
  lifecycleState: 'Active',
  version: 'AQ==',
  trackingDefaultEnabled: false,
};

const supplier: SupplierRecord = {
  id: 'supplier-1',
  tenantId: 'tenant-a',
  code: 'SUP-01',
  englishLegalName: 'Northwind Supply',
  arabicLegalName: null,
  englishTradingName: 'Northwind',
  arabicTradingName: null,
  registrationReference: null,
  lifecycleState: 'Active',
  version: 'Ag==',
  contacts: [],
};

const customer: CustomerRecord = {
  id: 'customer-1',
  tenantId: 'tenant-a',
  code: 'CUS-01',
  englishLegalName: 'Acme Buyer',
  arabicLegalName: null,
  englishTradingName: null,
  arabicTradingName: null,
  lifecycleState: 'Inactive',
  version: 'Aw==',
  contacts: [],
};

const currency: CurrencyRecord = {
  id: 'currency-1',
  tenantId: 'tenant-a',
  code: 'SAR',
  englishName: 'Saudi Riyal',
  arabicName: null,
  revision: 2,
  lifecycleState: 'Active',
  version: 'BA==',
};

const paymentTerm: PaymentTermRecord = {
  id: 'term-1',
  tenantId: 'tenant-a',
  lifecycleState: 'Active',
  version: 'BA==',
  code: 'NET30',
  englishName: 'Net 30',
  arabicName: null,
  currentVersionNumber: 1,
  versions: [{
    id: 'term-version-1',
    versionNumber: 1,
    effectiveFrom: '2026-01-01',
    effectiveTo: null,
    baseDateRule: 'InvoiceDate',
    scheduleMode: 'SingleDueDate',
    dueOffsetDays: 30,
    dueOffsetMonths: 0,
    installments: [],
    earlySettlementDiscountEnabled: false,
    earlySettlementDiscountPercentage: null,
    earlySettlementDiscountDays: 0,
    earlySettlementDiscountMonths: 0,
    code: 'NET30',
    englishName: 'Net 30',
    arabicName: null,
  }],
};

function routeMap(resource: string, id?: string): ParamMap {
  return convertToParamMap(id ? { resource, id } : { resource });
}

describe('MasterDataWorkspaceComponent', () => {
  let fixture: ComponentFixture<MasterDataWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let data: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    edit: ReturnType<typeof vi.fn>;
    lifecycle: ReturnType<typeof vi.fn>;
  };

  async function settleAsyncWork(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(async () => {
    routeParams = new BehaviorSubject(routeMap('categories'));
    data = {
      list: vi.fn((resource: string) => of(resource === 'categories' ? [category] : resource === 'suppliers' ? [supplier] : resource === 'customers' ? [customer] : resource === 'currencies' ? [currency] : resource === 'payment-terms' ? [paymentTerm] : [])),
      get: vi.fn((resource: string) => of(resource === 'suppliers' ? supplier : resource === 'customers' ? customer : resource === 'currencies' ? currency : resource === 'payment-terms' ? paymentTerm : category)),
      audit: vi.fn(() => of([])),
      create: vi.fn(),
      edit: vi.fn(),
      lifecycle: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [MasterDataWorkspaceComponent],
      providers: [
        provideRouter([
          { path: 'app/master-data/:resource', component: RouterTargetComponent },
          { path: 'app/master-data/:resource/:id', component: RouterTargetComponent },
        ]),
        LanguageService,
        { provide: MasterDataService, useValue: data },
        {
          provide: AuthService,
          useValue: {
            status: signal('authenticated'),
            session: signal({ selectedContextId: 'context-a' }),
          },
        },
        { provide: ActivatedRoute, useValue: { paramMap: routeParams.asObservable(), snapshot: { paramMap: routeMap('categories') } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MasterDataWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  });

  it('renders all eight bounded resource entries and a connected category list', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('.resource-link')).toHaveLength(8);
    expect(element.textContent).toContain('Categories');
    expect(element.textContent).toContain('CAT-01');
    expect(data.list).toHaveBeenCalledWith('categories');
  });

  it('filters the current list and exposes server-aware mutation affordances', () => {
    const input = fixture.nativeElement.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'does-not-exist';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No records match this view');
    expect((fixture.nativeElement.querySelector('.button--primary') as HTMLButtonElement).disabled).toBe(false);
  });

  it('keeps Supplier detail inside master-data lifecycle and states Procurement confirmation is downstream', async () => {
    routeParams.next(routeMap('suppliers', supplier.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(data.get).toHaveBeenCalledWith('suppliers', supplier.id);
    expect(fixture.nativeElement.textContent).toContain('Procurement confirmation is a downstream workflow.');
    expect(fixture.nativeElement.textContent).not.toContain('Confirm supplier');
  });

  it('renders a Customer lifecycle state without adding a separate approval or delete path', async () => {
    routeParams.next(routeMap('customers', customer.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Inactive');
    expect(fixture.nativeElement.textContent).toContain('No Draft or delete path is exposed.');
    expect(fixture.nativeElement.textContent).not.toContain('Approve');
  });

  it('renders Payment Term history and configuration boundaries without Finance effects', async () => {
    routeParams.next(routeMap('payment-terms', paymentTerm.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(data.get).toHaveBeenCalledWith('payment-terms', paymentTerm.id);
    expect(fixture.nativeElement.textContent).toContain('Configuration version');
    expect(fixture.nativeElement.textContent).toContain('AP/AR aging, settlement, posting, and discount accounting are Finance scope.');
    expect(fixture.nativeElement.textContent).not.toContain('Post payment');
  });
});
