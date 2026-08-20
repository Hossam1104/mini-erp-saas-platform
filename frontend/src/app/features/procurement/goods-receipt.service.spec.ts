import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { GoodsReceiptService } from './goods-receipt.service';

describe('GoodsReceiptService', () => {
  let service: GoodsReceiptService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(GoodsReceiptService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads eligible sources, warehouses, and Tenant-scoped Goods Receipts', () => {
    service.eligibleSources().subscribe((sources) => expect(sources).toEqual([]));
    const sources = httpMock.expectOne('/api/v1/procurement/goods-receipt-sources');
    expect(sources.request.method).toBe('GET');
    sources.flush([]);

    service.warehouses().subscribe((warehouses) => expect(warehouses).toEqual([]));
    const warehouses = httpMock.expectOne('/api/v1/procurement/warehouses');
    expect(warehouses.request.method).toBe('GET');
    warehouses.flush([]);

    service.list('Recorded', 'po-1').subscribe((records) => expect(records).toEqual([]));
    const list = httpMock.expectOne('/api/v1/procurement/goods-receipts?status=Recorded&purchaseOrderId=po-1');
    expect(list.request.method).toBe('GET');
    list.flush([]);
  });

  it('reads Goods Receipt detail, lifecycle history, and audit evidence', () => {
    service.get('gr-1').subscribe((record) => expect(record.id).toBe('gr-1'));
    httpMock.expectOne('/api/v1/procurement/goods-receipts/gr-1').flush({ id: 'gr-1' });

    service.history('gr-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/goods-receipts/gr-1/history').flush([]);

    service.audit('gr-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/goods-receipts/gr-1/audit').flush([]);
  });

  it('creates goods receipt with antiforgery and idempotency headers', async () => {
    const payload = {
      purchaseOrderId: 'po-1',
      warehouseId: 'wh-1',
      receivedDate: '2026-08-19',
      referenceNote: 'GRN-001',
      notes: null,
      lines: [{ purchaseOrderLineId: 'pol-1', receivedQuantity: 2, acceptedQuantity: 2, rejectedQuantity: 0 }],
    };

    const createPromise = service.create(payload);
    await Promise.resolve();
    const create = httpMock.expectOne('/api/v1/procurement/goods-receipts');
    expect(create.request.body).toEqual(payload);
    expect(create.request.headers.has('Idempotency-Key')).toBe(true);
    create.flush({ id: 'gr-1' });
    await createPromise;
  });

  it('cancels goods receipt with optimistic concurrency If-Match header', async () => {
    const cancelPromise = service.cancel('gr-1', 'V1', 'Entered in error');
    await Promise.resolve();
    const cancel = httpMock.expectOne('/api/v1/procurement/goods-receipts/gr-1/cancel');
    expect(cancel.request.headers.get('If-Match')).toBe('"V1"');
    expect(cancel.request.headers.has('Idempotency-Key')).toBe(true);
    expect(cancel.request.body).toEqual({ reason: 'Entered in error' });
    cancel.flush({ id: 'gr-1', status: 'Cancelled' });
    await cancelPromise;
  });

  it('fails closed when antiforgery bootstrap is unavailable', async () => {
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(false);
    await expect(service.cancel('gr-1', 'V1')).rejects.toMatchObject({ status: 403 });
    httpMock.expectNone('/api/v1/procurement/goods-receipts/gr-1/cancel');
  });
});
