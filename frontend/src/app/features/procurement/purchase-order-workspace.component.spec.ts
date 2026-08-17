import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { PurchaseOrderListItemResponse } from './purchase-order.model';
import { PurchaseOrderService } from './purchase-order.service';
import { PurchaseOrderWorkspaceComponent } from './purchase-order-workspace.component';

const purchaseOrderListItem: PurchaseOrderListItemResponse = {
  id: 'po-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  status: 'Draft',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  supplierQuotationReference: 'SUP-Q-2026-001',
  currencyCode: 'SAR',
  total: 575,
  lineCount: 1,
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  version: 'POVERSION',
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PurchaseOrderWorkspaceComponent', () => {
  let fixture: ComponentFixture<PurchaseOrderWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let orders: {
    list: ReturnType<typeof vi.fn>;
    sourceOptions: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    confirmations: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'purchase-orders' }]);
    routeParams = new BehaviorSubject(routeMap());
    orders = {
      list: vi.fn(() => of([purchaseOrderListItem])),
      sourceOptions: vi.fn(() => of([])),
      get: vi.fn(() => of(null)),
      confirmations: vi.fn(() => of([])),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
    };

    await TestBed.configureTestingModule({
      imports: [PurchaseOrderWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: ActivatedRoute, useValue: { url: routeUrls.asObservable(), paramMap: routeParams.asObservable(), snapshot: { get url() { return routeUrls.value; }, get paramMap() { return routeParams.value; } } } },
        { provide: PurchaseOrderService, useValue: orders },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseOrderWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('formats currency safely for both standard ISO and non-ISO MESP codes without throwing RangeError', () => {
    const comp = fixture.componentInstance;
    const lang = TestBed.inject(LanguageService);

    // Standard ISO currencies
    expect(comp.formatMoney(1234.56, 'SAR')).toContain('1,234.56');
    expect(comp.formatMoney(1234.56, 'SAR')).toContain('SAR');
    expect(comp.formatMoney(1234.56, 'USD')).toContain('1,234.56');

    // Non-ISO MESP configured currencies must not throw RangeError and must retain code
    expect(() => comp.formatMoney(1234.56, 'S2K')).not.toThrow();
    const s2kFormatted = comp.formatMoney(1234.56, 'S2K');
    expect(s2kFormatted).toContain('1,234.56');
    expect(s2kFormatted).toContain('S2K');

    expect(() => comp.formatMoney(500, 'CUSTOM')).not.toThrow();
    expect(comp.formatMoney(500, 'CUSTOM')).toContain('500.00');
    expect(comp.formatMoney(500, 'CUSTOM')).toContain('CUSTOM');

    // Arabic locale safe fallback
    lang.setLanguage('ar');
    expect(() => comp.formatMoney(1234.56, 'S2K')).not.toThrow();
    const arFormatted = comp.formatMoney(1234.56, 'S2K');
    expect(arFormatted).toContain('S2K');
    lang.setLanguage('en');
  });

  it('renders purchase order list with non-ISO currency code without breaking list rendering or subsequent rows', async () => {
    const s2kItem: PurchaseOrderListItemResponse = {
      ...purchaseOrderListItem,
      id: 's2k-po-id',
      currencyCode: 'S2K',
      total: 1250,
      supplierQuotationReference: 'SUP-Q-S2K',
    };
    const usdItem: PurchaseOrderListItemResponse = {
      ...purchaseOrderListItem,
      id: 'usd-po-id',
      currencyCode: 'USD',
      total: 2500,
      supplierQuotationReference: 'SUP-Q-USD',
    };

    orders.list.mockReturnValue(of([s2kItem, usdItem]));
    await fixture.componentInstance.loadList();
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('SUP-Q-S2K');
    expect(text).toContain('S2K');
    expect(text).toContain('1,250.00');
    // Subsequent row renders successfully
    expect(text).toContain('SUP-Q-USD');
    expect(text).toContain('2,500.00');
  });
});
