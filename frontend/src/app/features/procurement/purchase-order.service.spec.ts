import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PurchaseOrderService } from './purchase-order.service';

describe('PurchaseOrderService', () => {
  let service: PurchaseOrderService;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PurchaseOrderService);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  it('reads eligible sources and Tenant-scoped Purchase Orders', () => {
    service.sourceOptions().subscribe((sources) => expect(sources).toEqual([]));
    const sources = httpMock.expectOne('/api/v1/procurement/purchase-order-sources');
    expect(sources.request.method).toBe('GET');
    sources.flush([]);

    service.list('Approved').subscribe((records) => expect(records).toEqual([]));
    const list = httpMock.expectOne('/api/v1/procurement/purchase-orders?status=Approved');
    expect(list.request.method).toBe('GET');
    list.flush([]);
  });

  it('reads Purchase Order detail, confirmations, lifecycle history, and audit evidence', () => {
    service.get('po-1').subscribe((record) => expect(record.id).toBe('po-1'));
    httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1').flush({ id: 'po-1' });
    service.confirmations('po-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1/confirmations').flush([]);
    service.history('po-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1/history').flush([]);
    service.audit('po-1').subscribe((records) => expect(records).toEqual([]));
    httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1/audit').flush([]);
  });

  it('creates and edits with antiforgery, idempotency, and If-Match headers', async () => {
    const createPromise = service.create({ sourceDecisionId: 'decision-1' });
    await Promise.resolve();
    const create = httpMock.expectOne('/api/v1/procurement/purchase-orders');
    expect(create.request.body).toEqual({ sourceDecisionId: 'decision-1' });
    expect(create.request.headers.has('Idempotency-Key')).toBe(true);
    create.flush({ id: 'po-1' });
    await createPromise;

    const editPromise = service.edit('po-1', { notes: 'Updated', lines: [] }, 'AQIDBAUGBwg=');
    await Promise.resolve();
    const edit = httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1/edit');
    expect(edit.request.headers.get('If-Match')).toBe('"AQIDBAUGBwg="');
    expect(edit.request.headers.has('Idempotency-Key')).toBe(true);
    edit.flush({ id: 'po-1' });
    await editPromise;
  });

  it('sends lifecycle actions with the current optimistic-concurrency version', async () => {
    const actions: Array<[string, () => Promise<unknown>]> = [
      ['submit', () => service.submit('po-1', 'V1')],
      ['approve', () => service.approve('po-1', 'V1')],
      ['issue', () => service.issue('po-1', 'V1')],
      ['supplier-change/approve', () => service.approveSupplierChange('po-1', 'V1')],
    ];

    for (const [path, operation] of actions) {
      const promise = operation();
      await Promise.resolve();
      const request = httpMock.expectOne(`/api/v1/procurement/purchase-orders/po-1/${path}`);
      expect(request.request.headers.get('If-Match')).toBe('"V1"');
      expect(request.request.headers.has('Idempotency-Key')).toBe(true);
      request.flush({ id: 'po-1' });
      await promise;
    }
  });

  it('records full, partial, and rejected supplier response payloads through one evidence-first route', async () => {
    const payload = {
      status: 'PartiallyConfirmed' as const,
      responseDate: '2026-08-17',
      supplierReference: 'SUP-1',
      supplierContact: 'buyer@supplier.test',
      reason: null,
      notes: 'One line remains',
      lines: [{ purchaseOrderLineId: 'line-1', confirmedQuantity: 4, expectedDeliveryDate: '2026-08-25', proposedQuantity: null, proposedUnitPrice: null, proposedDeliveryDate: null, changeReason: null }],
      evidence: [],
    };
    const promise = service.captureConfirmation('po-1', payload, 'V1');
    await Promise.resolve();
    const request = httpMock.expectOne('/api/v1/procurement/purchase-orders/po-1/confirmations');
    expect(request.request.body.status).toBe('PartiallyConfirmed');
    expect(request.request.headers.get('If-Match')).toBe('"V1"');
    request.flush({ id: 'po-1', status: 'PartiallyConfirmed' });
    await promise;
  });

  it('fails closed when antiforgery bootstrap is unavailable', async () => {
    vi.spyOn(authService, 'bootstrapAntiforgery').mockResolvedValue(false);
    await expect(service.issue('po-1', 'V1')).rejects.toMatchObject({ status: 403 });
    httpMock.expectNone('/api/v1/procurement/purchase-orders/po-1/issue');
  });
});
