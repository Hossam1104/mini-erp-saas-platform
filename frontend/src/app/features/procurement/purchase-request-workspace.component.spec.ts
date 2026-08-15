import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataRecord, ProductRecord, UnitOfMeasureRecord } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { PurchaseRequestListItemResponse, PurchaseRequestResponse } from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';
import { PurchaseRequestWorkspaceComponent } from './purchase-request-workspace.component';

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

const companyId = '22222222-2222-2222-2222-222222222222';

const listItem: PurchaseRequestListItemResponse = {
  id: 'pr-1', companyId, branchId: null, requesterId: 'requester-1', status: 'Draft',
  purpose: 'Office supplies', lineCount: 1, createdAt: '2026-08-13T10:00:00Z', updatedAt: '2026-08-13T10:00:00Z',
  version: 'AQ==',
};

const purchaseRequest: PurchaseRequestResponse = {
  id: 'pr-1', tenantId: 'tenant-a', companyId, branchId: null, requesterId: 'requester-1',
  status: 'Draft', purpose: 'Office supplies',
  lines: [{ id: 'line-1', productId: product.id, productSku: 'SKU-001', productName: 'Widget', unitOfMeasureId: unit.id, unitOfMeasureCode: 'PCS', quantity: 5, needByDate: '2026-09-01', purpose: 'Restocking', version: 'AQ==' }],
  approval: null, createdAt: '2026-08-13T10:00:00Z', updatedAt: '2026-08-13T10:00:00Z',
  submittedAt: null, approvedAt: null, cancelledAt: null, version: 'AQIDBAUGBwg=',
  canEdit: true, canSubmit: true, canApprove: false, canReject: true, canReturnForChange: false, canCancel: true,
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PurchaseRequestWorkspaceComponent', () => {
  let fixture: ComponentFixture<PurchaseRequestWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let currentRouteConfig: { path: string };
  let purchaseRequests: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    edit: ReturnType<typeof vi.fn>;
    submit: ReturnType<typeof vi.fn>;
    approve: ReturnType<typeof vi.fn>;
    reject: ReturnType<typeof vi.fn>;
    returnForChange: ReturnType<typeof vi.fn>;
    cancel: ReturnType<typeof vi.fn>;
  };
  let data: { list: ReturnType<typeof vi.fn> };
  let language: LanguageService;

  async function settleAsyncWork(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }

  async function navigateTo(path: string, id?: string): Promise<void> {
    currentRouteConfig.path = path;
    routeParams.next(routeMap(id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    currentRouteConfig = { path: 'procurement/purchase-requests' };
    routeParams = new BehaviorSubject(routeMap());
    purchaseRequests = {
      list: vi.fn(() => of([listItem])),
      get: vi.fn(() => of(purchaseRequest)),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
      create: vi.fn(() => Promise.resolve(purchaseRequest)),
      edit: vi.fn(() => Promise.resolve(purchaseRequest)),
      submit: vi.fn(() => Promise.resolve({ ...purchaseRequest, status: 'PendingApproval' })),
      approve: vi.fn(() => Promise.resolve({ ...purchaseRequest, status: 'Approved' })),
      reject: vi.fn(() => Promise.resolve({ ...purchaseRequest, status: 'Rejected' })),
      returnForChange: vi.fn(() => Promise.resolve({ ...purchaseRequest, status: 'ReturnedForChange' })),
      cancel: vi.fn(() => Promise.resolve({ ...purchaseRequest, status: 'Cancelled' })),
    };
    data = { list: vi.fn((resource: string) => of<MasterDataRecord[]>(resource === 'products' ? [product] : resource === 'units' ? [unit] : [])) };

    await TestBed.configureTestingModule({
      imports: [PurchaseRequestWorkspaceComponent],
      providers: [
        provideRouter([
          { path: 'app/procurement/purchase-requests', component: RouterTargetComponent },
          { path: 'app/procurement/purchase-requests/new', component: RouterTargetComponent },
          { path: 'app/procurement/purchase-requests/:id', component: RouterTargetComponent },
          { path: 'app/procurement/purchase-requests/:id/edit', component: RouterTargetComponent },
        ]),
        LanguageService,
        { provide: PurchaseRequestService, useValue: purchaseRequests },
        { provide: MasterDataService, useValue: data },
        { provide: AuthService, useValue: { status: signal('authenticated'), session: signal({ selectedContextId: 'context-a' }) } },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: routeParams.asObservable(),
            snapshot: {
              get routeConfig() { return currentRouteConfig; },
              get paramMap() { return routeParams.value; },
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseRequestWorkspaceComponent);
    language = TestBed.inject(LanguageService);
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  });

  it('loads the purchase request list on init using only the server status query', () => {
    expect(purchaseRequests.list).toHaveBeenCalledWith(undefined);
    expect(fixture.nativeElement.textContent).toContain(companyId);
  });

  it('re-queries the server when the status filter changes', () => {
    fixture.componentInstance.onStatusFilterChange('Draft');
    expect(purchaseRequests.list).toHaveBeenCalledWith('Draft');
  });

  it('filters the already-loaded list client-side without another server call', () => {
    const callsBefore = purchaseRequests.list.mock.calls.length;
    fixture.componentInstance.searchQuery.set('office');
    fixture.detectChanges();

    expect(fixture.componentInstance.filteredRecords().length).toBe(1);
    expect(purchaseRequests.list.mock.calls.length).toBe(callsBefore);

    fixture.componentInstance.searchQuery.set('no-match-here');
    fixture.detectChanges();
    expect(fixture.componentInstance.filteredRecords().length).toBe(0);
  });

  it('opens a purchase request and gates the action bar strictly by server-derived flags', async () => {
    await navigateTo('procurement/purchase-requests/:id', purchaseRequest.id);

    expect(purchaseRequests.get).toHaveBeenCalledWith(purchaseRequest.id);
    expect(fixture.nativeElement.textContent).toContain(language.text('submitForApproval'));
    expect(fixture.nativeElement.textContent).toContain(language.text('rejectRequest'));
    expect(fixture.nativeElement.textContent).toContain(language.text('cancelRequest'));
    expect(fixture.nativeElement.textContent).not.toContain(language.text('approveRequest'));
    expect(fixture.nativeElement.textContent).not.toContain(language.text('returnForChange'));
  });

  it('creates a purchase request through the shared draft/save flow using real Master Data references', async () => {
    await navigateTo('procurement/purchase-requests/new');

    const component = fixture.componentInstance;
    component.setDraftField('companyId', 'company-1');
    component.setLineField(0, 'productId', product.id);
    component.setLineField(0, 'unitOfMeasureId', unit.id);
    component.setLineField(0, 'quantity', 5);
    component.setLineField(0, 'needByDate', '2026-09-01');
    component.setLineField(0, 'purpose', 'Office restocking');
    await component.save();
    fixture.detectChanges();

    expect(purchaseRequests.create).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain(language.text('companyIdFormatError'));

    component.setDraftField('companyId', '11111111-1111-1111-1111-111111111111');
    await component.save();
    fixture.detectChanges();

    expect(purchaseRequests.create).toHaveBeenCalledWith(
      expect.objectContaining({
        companyId: '11111111-1111-1111-1111-111111111111',
        lines: [expect.objectContaining({ productId: product.id, unitOfMeasureId: unit.id, quantity: 5, needByDate: '2026-09-01', purpose: 'Office restocking' })],
      }),
    );
  });

  it('surfaces a concurrency conflict on edit without silently overwriting', async () => {
    await navigateTo('procurement/purchase-requests/:id/edit', purchaseRequest.id);
    purchaseRequests.edit.mockRejectedValueOnce(new HttpErrorResponse({ status: 409, error: { code: 'concurrency_conflict' } }));

    const component = fixture.componentInstance;
    component.setDraftField('purpose', 'Renamed purpose');
    await component.save();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('conflictTitle'));
    expect(fixture.nativeElement.textContent).toContain(language.text('prConcurrencyConflictError'));
    expect(component.mode()).toBe('edit');
  });

  it('requires a reason before confirming a reject decision', async () => {
    await navigateTo('procurement/purchase-requests/:id', purchaseRequest.id);

    const component = fixture.componentInstance;
    component.openLifecycle('reject');
    await component.confirmLifecycle();
    fixture.detectChanges();

    expect(purchaseRequests.reject).not.toHaveBeenCalled();
    expect(component.lifecycleReasonError()).toBe(true);

    component.lifecycleReason.set('Budget exceeded');
    await component.confirmLifecycle();

    expect(purchaseRequests.reject).toHaveBeenCalledWith(purchaseRequest.id, purchaseRequest.version, 'Budget exceeded');
  });

  it('renders Arabic status labels and flips document direction to RTL after toggling language', async () => {
    await navigateTo('procurement/purchase-requests/:id', purchaseRequest.id);
    language.toggle();
    fixture.detectChanges();

    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.textContent).toContain(language.text('prStatusDraft'));
    expect(fixture.nativeElement.textContent).not.toContain('Draft');
  });
});
