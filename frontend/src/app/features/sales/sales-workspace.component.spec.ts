import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataService } from '../master-data/master-data.service';
import { PriceListService } from '../master-data/price-list.service';
import { PurchaseRequestService } from '../procurement/purchase-request.service';
import { InventoryService } from '../inventory/inventory.service';
import { SalesService } from './sales.service';
import { SalesWorkspaceComponent } from './sales-workspace.component';
import { SalesOrderResponse } from './sales.model';

describe('SalesWorkspaceComponent', () => {
  let fixture: ComponentFixture<SalesWorkspaceComponent>;
  let urls: BehaviorSubject<Array<{ path: string }>>;
  let params: BehaviorSubject<ParamMap>;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    urls = new BehaviorSubject([{ path: 'quotations' }]);
    params = new BehaviorSubject(convertToParamMap({}));
    const sales = {
      quotations: vi.fn(() => of([])), orders: vi.fn(() => of([])), quotation: vi.fn(), quotationRevisions: vi.fn(() => of([])), quotationHistory: vi.fn(() => of([])), quotationAudit: vi.fn(() => of([])),
      order: vi.fn(), orderHistory: vi.fn(() => of([])), orderAudit: vi.fn(() => of([])), orderCredit: vi.fn(() => of(null)), fulfillment: vi.fn(() => of({ lines: [], deliveries: [], invoiceRequests: [] })),
      reserveOrder: vi.fn(), postDelivery: vi.fn(), evaluateInvoiceEligibility: vi.fn(), requestInvoice: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [SalesWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: ActivatedRoute, useValue: { url: urls.asObservable(), paramMap: params.asObservable(), snapshot: { get url() { return urls.value; }, get paramMap() { return params.value; } } } },
        { provide: SalesService, useValue: sales },
        { provide: MasterDataService, useValue: { list: vi.fn(() => of([])) } },
        { provide: PriceListService, useValue: { list: vi.fn(() => of([])) } },
        { provide: PurchaseRequestService, useValue: { organizationScopes: vi.fn(() => of([])) } },
        { provide: InventoryService, useValue: { warehouses: vi.fn(() => of([])), reservations: vi.fn(() => of([])) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SalesWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('renders an empty quotation register with a distinct B2B commercial boundary', () => {
    const text = fixture.nativeElement.textContent as string;
    const language = TestBed.inject(LanguageService);
    expect(text).toContain(language.text('salesQuotationsNavLabel'));
    expect(text).toContain(language.text('noSalesQuotations'));
    expect(text).toContain(language.text('salesPricingEvidence'));
    expect(fixture.nativeElement.querySelector('[role="search"]')).toBeTruthy();
  });

  it('uses a server-provided organization scope selector instead of raw Company or Branch GUID inputs', async () => {
    params.next(convertToParamMap({}));
    urls.next([{ path: 'quotations' }, { path: 'new' }]);
    await settle();
    expect(fixture.nativeElement.querySelector('select[name="organizationScope"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[name="companyId"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="branchId"]')).toBeNull();
  });

  it('switches the workspace to Arabic without losing the accessible Sales surface', () => {
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.querySelector('h1')).toBeTruthy();
  });

  it('offers only the backend-supported returned-order correction actions and an edit route', () => {
    const order = { status: 'ReturnedForChange' } as SalesOrderResponse;
    const actions = fixture.componentInstance.orderActions(order);
    expect(actions.map(action => action.key)).toEqual(['submit']);
    expect(fixture.componentInstance.canEditOrder(order)).toBe(true);
    expect(fixture.componentInstance.orderActions({ status: 'Approved' } as SalesOrderResponse).map(action => action.key)).toContain('return');
  });
});
