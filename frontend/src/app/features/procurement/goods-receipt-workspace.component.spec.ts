import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import {
  GoodsReceiptListItemResponse,
  GoodsReceiptResponse,
  GoodsReceiptEligibleSourceResponse,
  GoodsReceiptWarehouseOptionResponse,
} from './goods-receipt.model';
import { GoodsReceiptService } from './goods-receipt.service';
import { GoodsReceiptWorkspaceComponent } from './goods-receipt-workspace.component';

const goodsReceiptListItem: GoodsReceiptListItemResponse = {
  id: 'gr-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  purchaseOrderId: 'po-1',
  warehouseId: 'wh-1',
  status: 'Recorded',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  receivedDate: '2026-08-19',
  referenceNote: 'GRN-2026-001',
  totalReceivedQuantity: 5,
  totalAcceptedQuantity: 4,
  totalRejectedQuantity: 1,
  totalDamagedQuantity: 1,
  lineCount: 1,
  createdAt: '2026-08-19T10:00:00Z',
  updatedAt: '2026-08-19T10:00:00Z',
  version: 'GRVERSION',
};

const goodsReceiptDetail: GoodsReceiptResponse = {
  id: 'gr-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  purchaseOrderId: 'po-1',
  warehouseId: 'wh-1',
  receivedByActorId: 'actor-1',
  status: 'Recorded',
  supplierId: 'supplier-1',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  receivedDate: '2026-08-19',
  referenceNote: 'GRN-2026-001',
  notes: 'Dock notes',
  createdAt: '2026-08-19T10:00:00Z',
  updatedAt: '2026-08-19T10:00:00Z',
  cancelledAt: null,
  cancellationReason: null,
  lines: [
    {
      id: 'grl-1',
      purchaseOrderLineId: 'pol-1',
      productId: 'prod-1',
      productSku: 'SKU-001',
      productName: 'Test Product',
      unitOfMeasureCode: 'EA',
      orderedQuantityAtReceipt: 5,
      receivedQuantity: 5,
      acceptedQuantity: 4,
      rejectedQuantity: 1,
      damagedQuantity: 1,
      damageNotes: '1 box crushed',
      remainingReceivableQuantityAfter: 1,
      notes: null,
    },
  ],
  version: 'GRVERSION',
  canCancel: true,
};

const eligibleSource: GoodsReceiptEligibleSourceResponse = {
  purchaseOrderId: 'po-1',
  companyId: 'company-1',
  branchId: null,
  status: 'Confirmed',
  supplierId: 'supplier-1',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'SAR',
  lines: [
    {
      purchaseOrderLineId: 'pol-1',
      productId: 'prod-1',
      productSku: 'SKU-001',
      productName: 'Test Product',
      unitOfMeasureId: 'uom-1',
      unitOfMeasureCode: 'EA',
      unitPrice: 50,
      confirmedQuantity: 5,
      alreadyReceivedQuantity: 0,
      remainingReceivableQuantity: 5,
    },
  ],
};

const warehouseOption: GoodsReceiptWarehouseOptionResponse = {
  warehouseId: 'wh-1',
  code: 'WH-MAIN',
  name: 'Main Central Warehouse',
  isActive: true,
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('GoodsReceiptWorkspaceComponent', () => {
  let fixture: ComponentFixture<GoodsReceiptWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let serviceMock: {
    list: ReturnType<typeof vi.fn>;
    eligibleSources: ReturnType<typeof vi.fn>;
    warehouses: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    cancel: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'goods-receipts' }]);
    routeParams = new BehaviorSubject(routeMap());
    serviceMock = {
      list: vi.fn(() => of([goodsReceiptListItem])),
      eligibleSources: vi.fn(() => of([eligibleSource])),
      warehouses: vi.fn(() => of([warehouseOption])),
      get: vi.fn(() => of(goodsReceiptDetail)),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
      create: vi.fn(() => Promise.resolve(goodsReceiptDetail)),
      cancel: vi.fn(() => Promise.resolve({ ...goodsReceiptDetail, status: 'Cancelled' })),
    };

    await TestBed.configureTestingModule({
      imports: [GoodsReceiptWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        {
          provide: ActivatedRoute,
          useValue: {
            url: routeUrls.asObservable(),
            paramMap: routeParams.asObservable(),
            snapshot: {
              get url() {
                return routeUrls.value;
              },
              get paramMap() {
                return routeParams.value;
              },
            },
          },
        },
        { provide: GoodsReceiptService, useValue: serviceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(GoodsReceiptWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('renders goods receipt list with scope semantics and records', () => {
    const headers = Array.from(fixture.nativeElement.querySelectorAll('th')) as HTMLElement[];
    expect(headers.length).toBeGreaterThan(0);
    expect(headers.every((h) => h.getAttribute('scope') === 'col')).toBe(true);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('GRN-2026-001');
    expect(text).toContain('Supplier One');
  });

  it('renders detail view with tabs and cancellation option', async () => {
    routeUrls.next([{ path: 'goods-receipts' }, { path: ':id' }]);
    routeParams.next(routeMap('gr-1'));
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('GRN-2026-001');
    expect(text).toContain('Supplier One');

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLElement[];
    expect(tabs.length).toBe(4);
  });

  it('validates quantity integrity: received equals accepted plus rejected', async () => {
    const comp = fixture.componentInstance;
    comp.createLines = [
      {
        purchaseOrderLineId: 'pol-1',
        productSku: 'SKU-001',
        productName: 'Test Product',
        unitOfMeasureCode: 'EA',
        confirmedQuantity: 5,
        alreadyReceivedQuantity: 0,
        remainingReceivableQuantity: 5,
        unitPrice: 50,
        receivedQuantity: 5,
        acceptedQuantity: 3,
        rejectedQuantity: 1, // 3 + 1 != 5
        damagedQuantity: null,
        damageNotes: '',
        notes: '',
      },
    ];
    comp.createWarehouseId = 'wh-1';

    const isValid = comp.validateCreateForm();
    expect(isValid).toBe(false);
    expect(comp.validationError()).not.toBeNull();
  });
});
