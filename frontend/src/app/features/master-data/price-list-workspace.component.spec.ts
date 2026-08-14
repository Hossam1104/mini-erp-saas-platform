import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyRecord, CustomerRecord, MasterDataRecord, ProductRecord, UnitOfMeasureRecord } from './master-data.models';
import { MasterDataService } from './master-data.service';
import { PriceListPriceVersionRecord, PriceListRecord, PriceListReferenceRecord } from './price-list.model';
import { PriceListService } from './price-list.service';
import { PriceListWorkspaceComponent } from './price-list-workspace.component';

@Component({ standalone: true, template: '' })
class RouterTargetComponent {}

const product: ProductRecord = {
  id: 'product-1', tenantId: 'tenant-a', code: '', lifecycleState: 'Active', version: 'AQ==',
  sku: 'SKU-001', englishName: 'Widget', arabicName: null, description: null,
  categoryId: 'category-1', baseUnitOfMeasureId: 'unit-1', trackingDefaultEnabled: false,
  trackingEnabledOverride: null, trackingEnabled: false, isSellable: true, isPurchasable: true,
  isInventoryRelevant: true, barcodes: [],
} as unknown as ProductRecord;

const unit: UnitOfMeasureRecord = { id: 'unit-1', tenantId: 'tenant-a', code: 'PCS', englishName: 'Pieces', arabicName: null, lifecycleState: 'Active', version: 'AQ==' };

const currency: CurrencyRecord = { id: 'currency-1', tenantId: 'tenant-a', code: 'USD', englishName: 'US Dollar', arabicName: null, revision: 1, lifecycleState: 'Active', version: 'AQ==' };

const customer: CustomerRecord = {
  id: 'customer-1', tenantId: 'tenant-a', code: 'CUS-01', englishLegalName: 'Acme Buyer', arabicLegalName: null,
  englishTradingName: null, arabicTradingName: null, lifecycleState: 'Active', version: 'AQ==', contacts: [],
};

const priceVersion: PriceListPriceVersionRecord = {
  id: 'price-version-1', versionNumber: 1, productId: 'product-1', productSku: 'SKU-001', unitOfMeasureId: 'unit-1',
  unitOfMeasureCode: 'PCS', currencyId: 'currency-1', currencyCode: 'USD', customerId: null,
  organizationScopeKind: null, organizationScopeId: null, priority: 10, effectiveFrom: '2026-01-01', effectiveTo: null,
  price: 100, priceScale: 2, provenance: 'Manual', sourceReference: null, version: 'AQ==',
};

const priceList: PriceListRecord = {
  id: 'price-list-1', tenantId: 'tenant-a', code: 'PL-USD-STANDARD', englishName: 'Standard USD Price List',
  arabicName: null, currencyId: 'currency-1', currencyCode: 'USD', customerId: null, organizationScopeKind: null,
  organizationScopeId: null, priority: 10, lifecycleState: 'Active', currentVersionNumber: 1,
  prices: [priceVersion], version: 'AQIDBAUGBwg=',
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PriceListWorkspaceComponent', () => {
  let fixture: ComponentFixture<PriceListWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let priceLists: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    edit: ReturnType<typeof vi.fn>;
    appendPrice: ReturnType<typeof vi.fn>;
    deactivate: ReturnType<typeof vi.fn>;
    reactivate: ReturnType<typeof vi.fn>;
    resolveReference: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
  };
  let data: { list: ReturnType<typeof vi.fn> };
  let language: LanguageService;

  async function settleAsyncWork(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }

  async function navigateTo(id?: string): Promise<void> {
    routeParams.next(routeMap(id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeParams = new BehaviorSubject(routeMap());
    priceLists = {
      list: vi.fn(() => of([priceList])),
      get: vi.fn(() => of(priceList)),
      history: vi.fn(() => of([priceVersion])),
      create: vi.fn(() => Promise.resolve(priceList)),
      edit: vi.fn(() => Promise.resolve(priceList)),
      appendPrice: vi.fn(() => Promise.resolve(priceList)),
      deactivate: vi.fn(() => Promise.resolve({ ...priceList, lifecycleState: 'Inactive' })),
      reactivate: vi.fn(() => Promise.resolve({ ...priceList, lifecycleState: 'Active' })),
      resolveReference: vi.fn(),
      audit: vi.fn(() => of([])),
    };
    data = { list: vi.fn((resource: string) => of<MasterDataRecord[]>(resource === 'products' ? [product] : resource === 'units' ? [unit] : resource === 'currencies' ? [currency] : resource === 'customers' ? [customer] : [])) };

    await TestBed.configureTestingModule({
      imports: [PriceListWorkspaceComponent],
      providers: [
        provideRouter([
          { path: 'app/price-lists', component: RouterTargetComponent },
          { path: 'app/price-lists/:id', component: RouterTargetComponent },
        ]),
        LanguageService,
        { provide: PriceListService, useValue: priceLists },
        { provide: MasterDataService, useValue: data },
        { provide: AuthService, useValue: { status: signal('authenticated'), session: signal({ selectedContextId: 'context-a' }) } },
        { provide: ActivatedRoute, useValue: { paramMap: routeParams.asObservable(), snapshot: { paramMap: routeMap() } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PriceListWorkspaceComponent);
    language = TestBed.inject(LanguageService);
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  });

  it('loads the price list catalogue on init', () => {
    expect(priceLists.list).toHaveBeenCalledWith('');
    expect(fixture.nativeElement.textContent).toContain('PL-USD-STANDARD');
  });

  it('searches the catalogue via the server-side query', async () => {
    const input = fixture.nativeElement.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'standard';
    input.dispatchEvent(new Event('input'));
    fixture.componentInstance.onSearchSubmit();
    fixture.detectChanges();

    expect(priceLists.list).toHaveBeenCalledWith('standard');
  });

  it('opens a price list and shows overview fields without computing pricing itself', async () => {
    await navigateTo(priceList.id);

    expect(priceLists.get).toHaveBeenCalledWith(priceList.id);
    expect(fixture.nativeElement.textContent).toContain('PL-USD-STANDARD');
    expect(fixture.nativeElement.textContent).toContain('USD');
    expect(fixture.nativeElement.textContent).toContain('10');
  });

  it('creates a price list through the shared draft/save flow', async () => {
    await navigateTo('new');

    const component = fixture.componentInstance;
    component.setDraftField('code', 'PL-NEW');
    component.setDraftField('englishName', 'New Price List');
    component.setDraftField('currencyId', currency.id);
    component.setDraftField('priority', 20);
    await component.save();
    fixture.detectChanges();

    expect(priceLists.create).toHaveBeenCalledWith(expect.objectContaining({ code: 'PL-NEW', englishName: 'New Price List', currencyId: currency.id, priority: 20 }));
  });

  it('surfaces a concurrency conflict on edit without silently overwriting', async () => {
    await navigateTo(priceList.id);
    priceLists.edit.mockRejectedValueOnce(new HttpErrorResponse({ status: 409, error: { code: 'concurrency_conflict' } }));

    const component = fixture.componentInstance;
    component.startEdit();
    component.setDraftField('englishName', 'Renamed Price List');
    await component.save();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('conflictTitle'));
    expect(component.detailMode()).toBe('edit');
  });

  it('appends a price version using real product/UOM reference data', async () => {
    await navigateTo(priceList.id);

    const component = fixture.componentInstance;
    component.openAddPrice();
    component.setPriceField('productId', product.id);
    component.setPriceField('unitOfMeasureId', unit.id);
    component.setPriceField('price', 150);
    await component.submitPrice();
    fixture.detectChanges();

    expect(priceLists.appendPrice).toHaveBeenCalledWith(priceList.id, expect.objectContaining({ productId: product.id, unitOfMeasureId: unit.id, price: 150 }), priceList.version);
    expect(component.priceFormOpen()).toBe(false);
  });

  it('deactivates an active price list after confirmation with no destructive delete path', async () => {
    await navigateTo(priceList.id);
    expect(fixture.nativeElement.textContent).not.toContain('Delete');

    const component = fixture.componentInstance;
    component.openLifecycle('deactivate');
    await component.confirmLifecycle();
    fixture.detectChanges();

    expect(priceLists.deactivate).toHaveBeenCalledWith(priceList.id, priceList.version);
    expect(component.lifecycleAction()).toBeNull();
  });

  it('resolves the applicable price using only the server-authoritative result', async () => {
    await navigateTo(priceList.id);
    const reference: PriceListReferenceRecord = {
      priceListId: priceList.id, tenantId: 'tenant-a', priceListCode: priceList.code, priceVersionId: 'price-version-1',
      priceVersionNumber: 1, productId: product.id, productSku: product.sku, unitOfMeasureId: unit.id, unitOfMeasureCode: unit.code,
      currencyId: currency.id, currencyCode: currency.code, customerId: null, organizationScopeKind: null, organizationScopeId: null,
      priority: 10, effectiveOn: '2026-08-13', effectiveFrom: '2026-01-01', effectiveTo: null, price: 100, priceScale: 2,
      provenance: 'Manual', sourceReference: null, referenceValue: '100.00 USD', priceVersion: 'AQ==', masterVersion: 'AQ==',
    };
    priceLists.resolveReference.mockReturnValueOnce(of(reference));

    const component = fixture.componentInstance;
    component.setTab('resolve');
    component.setResolveField('productId', product.id);
    component.setResolveField('unitOfMeasureId', unit.id);
    component.setResolveField('currencyId', currency.id);
    await component.resolvePrice();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('100.00 USD');
  });

  it('shows a business-safe precedence conflict message and never picks a winner itself', async () => {
    await navigateTo(priceList.id);
    priceLists.resolveReference.mockImplementationOnce(() => {
      throw new HttpErrorResponse({ status: 409, error: { code: 'price_list_precedence_conflict' } });
    });

    const component = fixture.componentInstance;
    component.setTab('resolve');
    component.setResolveField('productId', product.id);
    component.setResolveField('unitOfMeasureId', unit.id);
    component.setResolveField('currencyId', currency.id);
    await component.resolvePrice();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('priceListPrecedenceConflictError'));
  });

  it('distinguishes "no applicable price" from "price list not found" in the resolver', async () => {
    await navigateTo(priceList.id);
    priceLists.resolveReference.mockImplementationOnce(() => {
      throw new HttpErrorResponse({ status: 404, error: { code: 'price_list_not_found' } });
    });

    const component = fixture.componentInstance;
    component.setTab('resolve');
    component.setResolveField('productId', product.id);
    component.setResolveField('unitOfMeasureId', unit.id);
    component.setResolveField('currencyId', currency.id);
    await component.resolvePrice();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('priceListResolutionNotFound'));
  });

  it('renders tab labels in Arabic and flips document direction to RTL after toggling language', async () => {
    await navigateTo(priceList.id);
    language.toggle();
    fixture.detectChanges();

    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.textContent).toContain(language.text('priceListOverview'));
    expect(fixture.nativeElement.textContent).not.toContain('Overview');
  });
});
